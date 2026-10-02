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
    /// </summary>
    public static class AnomalySystem
    {
        public struct Zone { public Vector3 Center; public float Radius; public float Strength; }

        static readonly List<Zone> _zones = new List<Zone>();
        static float _baseline = 0.05f;
        static float _pulseStart, _pulseEnd, _pulseStrength;

        public static IReadOnlyList<Zone> Zones => _zones;

        /// <summary>Time source (seconds). Defaults to Time.time; gameplay sets it to synchronized match time.</summary>
        public static Func<double> TimeSource = () => Time.time;

        public static int Seed;

        /// <summary>Low constant global level (0..1).</summary>
        public static void SetBaseline(float value) => _baseline = Mathf.Clamp01(value);
        public static float Baseline => _baseline;

        public static int RegisterZone(Vector3 center, float radius, float strength)
        {
            _zones.Add(new Zone { Center = center, Radius = radius, Strength = strength });
            return _zones.Count - 1;
        }

        public static void ClearZones() => _zones.Clear();

        /// <summary>Global surge: ramps up quickly, holds, fades out over <paramref name="duration"/> seconds.</summary>
        public static void Pulse(float strength, float duration)
        {
            float now = (float)TimeSource();
            _pulseStart = now;
            _pulseEnd = now + Mathf.Max(0.1f, duration);
            _pulseStrength = Mathf.Max(strength, CurrentPulse);
        }

        public static float CurrentPulse
        {
            get
            {
                float now = (float)TimeSource();
                if (now >= _pulseEnd || _pulseEnd <= _pulseStart) return 0f;
                float t = (now - _pulseStart) / (_pulseEnd - _pulseStart);
                float env = t < 0.1f ? t / 0.1f : 1f - Mathf.SmoothStep(0f, 1f, (t - 0.1f) / 0.9f);
                return _pulseStrength * env;
            }
        }

        /// <summary>Total anomaly intensity at a position (baseline + pulse + zones), 0..1+.</summary>
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
    }
}
