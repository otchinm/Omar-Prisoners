using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// PS1-style rendering "anomalies": the whole environment warps / swims / tears as a rendering artifact
    /// (vertex precision collapse, affine texture swimming, screen-space geometry waves, polygon spikes,
    /// texture page corruption). This never edits meshes - it drives global shader parameters.
    /// Zones raise the effect locally; pulses raise it globally for a while.
    /// Use a shared time source (match time) so all players see the same pulses.
    ///
    /// Shader side (PsxCore.cginc): per-vertex intensity = baseline + pulse + sum over the (up to 8, nearest to the
    /// camera) zones of strength * saturate(1 - distance / radius), clamped to 1.5, times <see cref="VisualScale"/>.
    /// ~0.05 = occasional subtle wobble, 1 = nauseating (grid collapse, waves, breathing, spikes, texture corruption).
    /// </summary>
    public static class AnomalySystem
    {
        public struct Zone { public Vector3 Center; public float Radius; public float Strength; }

        /// <summary>Number of zones the shaders evaluate (the nearest ones to the camera are uploaded).</summary>
        public const int MaxShaderZones = 8;

        static readonly List<Zone> _zones = new List<Zone>();
        static float _baseline = 0.05f;
        static float _pulseStart, _pulseEnd, _pulseStrength;

        public static IReadOnlyList<Zone> Zones => _zones;

        /// <summary>Time source (seconds). Defaults to Time.time; gameplay sets it to synchronized match time.</summary>
        public static Func<double> TimeSource = () => Time.time;

        public static int Seed;

        /// <summary>Accessibility multiplier of the visual distortion (0 = off, 1 = full). Does not change gameplay intensity.</summary>
        public static float VisualScale = 1f;

        /// <summary>Low constant global level (0..1).</summary>
        public static void SetBaseline(float value) => _baseline = Mathf.Clamp01(value);
        public static float Baseline => _baseline;

        public static int RegisterZone(Vector3 center, float radius, float strength)
        {
            _zones.Add(new Zone { Center = center, Radius = radius, Strength = strength });
            return _zones.Count - 1;
        }

        /// <summary>Moves / re-tunes a registered zone (e.g. a zone that follows Omar, or fading a zone out with strength 0).</summary>
        public static void SetZone(int index, Vector3 center, float radius, float strength)
        {
            if (index < 0 || index >= _zones.Count) return;
            _zones[index] = new Zone { Center = center, Radius = radius, Strength = strength };
        }

        public static void ClearZones() => _zones.Clear();

        /// <summary>Global surge: ramps up quickly, holds, fades out over <paramref name="duration"/> seconds.</summary>
        public static void Pulse(float strength, float duration)
        {
            float now = (float)Now();
            _pulseStart = now;
            _pulseEnd = now + Mathf.Max(0.1f, duration);
            _pulseStrength = Mathf.Max(strength, CurrentPulse);
        }

        public static float CurrentPulse
        {
            get
            {
                float now = (float)Now();
                if (now >= _pulseEnd || _pulseEnd <= _pulseStart) return 0f;
                float t = (now - _pulseStart) / (_pulseEnd - _pulseStart);
                float env = t < 0.1f ? t / 0.1f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.1f) / 0.9f);
                return _pulseStrength * env;
            }
        }

        /// <summary>Total anomaly intensity at a position (baseline + pulse + zones), 0..1+.
        /// Same formula as the shaders (which additionally clamp to 1.5 and only see the 8 zones nearest the camera).</summary>
        public static float GetIntensityAt(Vector3 pos)
        {
            float v = _baseline + CurrentPulse;
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                float d = Vector3.Distance(pos, z.Center);
                if (d < z.Radius) v += z.Strength * (1f - d / z.Radius);
            }
            return v;
        }

        public static void Reset()
        {
            _zones.Clear();
            _pulseEnd = _pulseStart = 0;
            _pulseStrength = 0;
            _baseline = 0.05f;
            TimeSource = () => Time.time;
        }

        // ------------------------------------------------------------------ shader upload (called by PsxRenderDriver)

        static readonly Vector4[] _zoneVec = new Vector4[MaxShaderZones];
        static readonly Vector4[] _zoneParams = new Vector4[MaxShaderZones];
        static readonly int[] _zoneSel = new int[MaxShaderZones];
        static readonly float[] _zoneScore = new float[MaxShaderZones];
        static Vector4 _anomalyVec;
        static float _timeCached;
        static int _zoneCount;

        static double Now()
        {
            var src = TimeSource;
            if (src == null) return Time.timeAsDouble;
            try { return src(); }
            catch (Exception) { return Time.timeAsDouble; }
        }

        /// <summary>Uploads _PsxTime, _PsxAnomaly and the zone arrays (zones sorted by distance to the camera).</summary>
        internal static void UploadGlobals(Vector3 cameraPos)
        {
            double now = Now();
            _timeCached = (float)(now - Math.Floor(now / 3600.0) * 3600.0);

            int count = 0;
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (z.Radius <= 0f || z.Strength == 0f) continue;
                float score = Vector3.Distance(cameraPos, z.Center) - z.Radius;
                int n = count;
                if (n == MaxShaderZones)
                {
                    if (score >= _zoneScore[n - 1]) continue;
                    n = MaxShaderZones - 1;
                }
                int j = n;
                while (j > 0 && score < _zoneScore[j - 1])
                {
                    _zoneSel[j] = _zoneSel[j - 1]; _zoneScore[j] = _zoneScore[j - 1];
                    j--;
                }
                _zoneSel[j] = i; _zoneScore[j] = score;
                count = n + 1;
            }
            for (int i = 0; i < MaxShaderZones; i++)
            {
                if (i < count)
                {
                    var z = _zones[_zoneSel[i]];
                    _zoneVec[i] = new Vector4(z.Center.x, z.Center.y, z.Center.z, z.Radius);
                    _zoneParams[i] = new Vector4(z.Strength, 0f, 0f, 0f);
                }
                else
                {
                    _zoneVec[i] = Vector4.zero;
                    _zoneParams[i] = Vector4.zero;
                }
            }
            _zoneCount = count;

            int seed = ((Seed % 997) + 997) % 997;
            _anomalyVec = new Vector4(Mathf.Max(0f, _baseline + CurrentPulse), seed * 0.1f, Mathf.Max(0f, VisualScale), 0f);
            ReuploadCached();
        }

        internal static void ReuploadCached()
        {
            Shader.SetGlobalFloat(PsxShaderIds.Time, _timeCached);
            Shader.SetGlobalVector(PsxShaderIds.Anomaly, _anomalyVec);
            Shader.SetGlobalVectorArray(PsxShaderIds.AnomalyZones, _zoneVec);       // always full 8-length arrays
            Shader.SetGlobalVectorArray(PsxShaderIds.AnomalyZoneParams, _zoneParams);
            Shader.SetGlobalFloat(PsxShaderIds.AnomalyZoneCount, _zoneCount);
        }

        /// <summary>No anomaly at all (preview renders). Restore with ReuploadCached.</summary>
        internal static void UploadNeutral()
        {
            Shader.SetGlobalVector(PsxShaderIds.Anomaly, new Vector4(0f, _anomalyVec.y, 0f, 0f));
            Shader.SetGlobalFloat(PsxShaderIds.AnomalyZoneCount, 0f);
        }
    }
}
