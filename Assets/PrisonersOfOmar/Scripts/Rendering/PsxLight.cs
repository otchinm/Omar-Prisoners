using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    public enum PsxLightType { Point = 0, Spot = 1 }

    public enum PsxFlicker
    {
        None = 0,
        Candle,     // lighter / candle: fast small random wobble
        FaultyBulb, // mostly on, random short dropouts and buzz dips
        Strobe,     // on/off at FlickerSpeed Hz
        Pulse,      // slow sine breathing (FlickerSpeed * 0.1 Hz)
        Fire,       // big fire: strong low-frequency wobble
    }

    /// <summary>
    /// Light source for the custom PS1 vertex lighting (Unity Light components are NOT used).
    /// The PsxLightManager picks the most relevant lights each frame and uploads them as global shader arrays.
    /// Spot lights point along transform.forward.
    /// </summary>
    public sealed class PsxLight : MonoBehaviour
    {
        static readonly List<PsxLight> _all = new List<PsxLight>();
        public static IReadOnlyList<PsxLight> All => _all;

        public PsxLightType Type = PsxLightType.Point;
        public Color Color = new Color(1f, 0.85f, 0.6f);
        public float Intensity = 1f;
        public float Range = 6f;
        [Range(1f, 179f)] public float SpotAngle = 50f;
        public PsxFlicker Flicker = PsxFlicker.None;
        [Range(0f, 1f)] public float FlickerAmount = 0.3f;
        public float FlickerSpeed = 8f;
        /// <summary>Switch (power outages, lighter on/off). Off lights are not uploaded.</summary>
        public bool On = true;
        /// <summary>Higher priority lights are kept first when more lights exist than shader slots (players' lighters use 10).</summary>
        public int Priority = 0;

        /// <summary>Intensity after flicker and On state; updated by the manager every frame.</summary>
        public float CurrentIntensity { get; internal set; }

        /// <summary>Per-light phase of the flicker noise (so identical lamps do not flicker in sync).</summary>
        internal float FlickerSeed;

        void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
            unchecked
            {
                uint h = (uint)GetInstanceID() * 2654435761u;
                FlickerSeed = (h % 9973u) * 0.1f;
            }
            CurrentIntensity = On ? Intensity : 0f;
            PsxRenderDriver.Ensure();
        }

        void OnDisable() { _all.Remove(this); }

        public static PsxLight Create(Transform parent, Vector3 localPosition, Color color, float intensity, float range,
            PsxFlicker flicker = PsxFlicker.None, string name = "PsxLight")
        {
            var t = GeoUtil.CreateChild(parent, name, localPosition, Quaternion.identity, Layers.Default);
            var l = t.gameObject.AddComponent<PsxLight>();
            l.Color = color; l.Intensity = intensity; l.Range = range; l.Flicker = flicker;
            l.CurrentIntensity = l.On ? intensity : 0f;
            return l;
        }

        public static PsxLight CreateSpot(Transform parent, Vector3 localPosition, Quaternion localRotation, Color color, float intensity,
            float range, float spotAngle, PsxFlicker flicker = PsxFlicker.None, string name = "PsxSpot")
        {
            var t = GeoUtil.CreateChild(parent, name, localPosition, localRotation, Layers.Default);
            var l = t.gameObject.AddComponent<PsxLight>();
            l.Type = PsxLightType.Spot; l.Color = color; l.Intensity = intensity; l.Range = range; l.SpotAngle = spotAngle; l.Flicker = flicker;
            l.CurrentIntensity = l.On ? intensity : 0f;
            return l;
        }
    }

    /// <summary>
    /// Uploads lights + environment to global shader properties each frame and answers light queries
    /// for gameplay (stealth). Created automatically on first use (see PsxRenderDriver): every frame, right before
    /// the world camera culls, flicker is updated, up to 16 lights are selected (priority first, then
    /// distance-to-camera minus range; lights entirely outside the view frustum / beyond the fog are skipped)
    /// and uploaded as _PsxLightPos / _PsxLightColor / _PsxLightDir / _PsxLightCount.
    /// </summary>
    public static class PsxLightManager
    {
        /// <summary>Number of light slots in the shaders.</summary>
        public const int MaxShaderLights = 16;

        /// <summary>Wrap / spot-edge constants shared with PsxCore.cginc.</summary>
        const float SpotSoftness = 0.35f;
        const float LightClamp = 2f;

        static readonly Vector4[] _pos = new Vector4[MaxShaderLights];
        static readonly Vector4[] _col = new Vector4[MaxShaderLights];
        static readonly Vector4[] _dir = new Vector4[MaxShaderLights];
        static readonly Vector4[] _pvPos = new Vector4[MaxShaderLights];
        static readonly Vector4[] _pvCol = new Vector4[MaxShaderLights];
        static readonly Vector4[] _pvDir = new Vector4[MaxShaderLights];
        static readonly PsxLight[] _sel = new PsxLight[MaxShaderLights];
        static readonly float[] _selScore = new float[MaxShaderLights];
        static readonly int[] _selPrio = new int[MaxShaderLights];
        static readonly Plane[] _planes = new Plane[6];
        static int _count;

        /// <summary>How many lights were uploaded for the last rendered frame (debug / HUD).</summary>
        public static int UploadedLightCount => _count;

        /// <summary>Approximate brightness (0 = pitch black, ~1 = well lit) at a world position,
        /// using the same falloff as the shader. Used by stealth/visibility.</summary>
        public static float SampleIllumination(Vector3 worldPos, bool includeAmbient = true)
        {
            float sum = includeAmbient ? PsxEnvironment.Ambient.grayscale : 0f;
            var all = PsxLight.All;
            for (int i = 0; i < all.Count; i++)
            {
                var l = all[i];
                if (l == null || !l.On) continue;
                float intensity = l.CurrentIntensity;
                if (intensity <= 0f || l.Range <= 0f) continue;
                Vector3 toLight = l.transform.position - worldPos;
                float d2 = toLight.sqrMagnitude;
                float r2 = l.Range * l.Range;
                if (d2 >= r2) continue;
                float att = 1f - d2 / r2;
                att *= att;
                float spot = 1f;
                if (l.Type == PsxLightType.Spot)
                {
                    float d = Mathf.Sqrt(d2);
                    if (d > 1e-4f)
                    {
                        float cosA = Vector3.Dot(-toLight / d, l.transform.forward);
                        float cosOuter = SpotCosOuter(l.SpotAngle);
                        spot = SmoothStep(cosOuter, cosOuter + (1f - cosOuter) * SpotSoftness, cosA);
                    }
                }
                sum += att * spot * intensity * l.Color.grayscale;
            }
            return Mathf.Min(sum, LightClamp);
        }

        // ------------------------------------------------------------------ helpers shared with the shader

        internal static float SpotCosOuter(float spotAngle)
        {
            return Mathf.Min(Mathf.Cos(Mathf.Clamp(spotAngle, 1f, 179f) * 0.5f * Mathf.Deg2Rad), 0.999f);
        }

        /// <summary>GLSL/HLSL smoothstep (Mathf.SmoothStep is a different function).</summary>
        internal static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / Mathf.Max(edge1 - edge0, 1e-6f));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ flicker

        /// <summary>Updates CurrentIntensity of every enabled light (flicker + On switch).</summary>
        internal static void UpdateFlicker(float time)
        {
            var all = PsxLight.All;
            for (int i = 0; i < all.Count; i++)
            {
                var l = all[i];
                if (l == null) continue;
                l.CurrentIntensity = l.On ? Mathf.Max(0f, l.Intensity) * FlickerFactor(l, time) : 0f;
            }
        }

        static float FlickerFactor(PsxLight l, float t)
        {
            float s = l.FlickerSeed;
            float amt = Mathf.Clamp01(l.FlickerAmount);
            float spd = Mathf.Max(0.01f, l.FlickerSpeed);
            switch (l.Flicker)
            {
                case PsxFlicker.Candle:
                {
                    float n = (Noise1(t * spd * 1.3f + s) - 0.5f) * 0.9f + (Noise1(t * spd * 3.7f + s * 1.7f + 11f) - 0.5f) * 0.5f;
                    return Mathf.Max(0f, 1f + amt * n);
                }
                case PsxFlicker.FaultyBulb:
                {
                    // dropouts: ~7 slots per second, a few of them go (nearly) dark with a fast stutter
                    float slot = Mathf.Floor(t * 7f);
                    if (Hash(slot, s) < 0.02f + 0.12f * amt)
                        return Hash(Mathf.Floor(t * 40f), s + 3f) < 0.55f ? 0.03f : 0.45f;
                    // buzz: small fast wobble + occasional dips
                    float buzz = 1f - amt * 0.18f * Noise1(t * spd * 5f + s);
                    if (Hash(Mathf.Floor(t * 14f), s + 7f) < 0.06f) buzz -= amt * 0.4f;
                    return Mathf.Max(0f, buzz);
                }
                case PsxFlicker.Strobe:
                    return Frac(t * spd + s * 0.137f) < 0.5f ? 1f : 0f;
                case PsxFlicker.Pulse:
                    return 1f - amt * (0.5f + 0.5f * Mathf.Sin((t * spd * 0.1f + s) * 2f * Mathf.PI));
                case PsxFlicker.Fire:
                {
                    float n = (Noise1(t * spd * 0.55f + s) - 0.5f) * 1.3f + (Noise1(t * spd * 1.7f + s * 2.3f + 5f) - 0.5f) * 0.6f;
                    return Mathf.Max(0f, 1f + amt * n);
                }
                default:
                    return 1f;
            }
        }

        static float Frac(float x) => x - Mathf.Floor(x);

        /// <summary>Integer hash of (floor(x), seed) to [0,1).</summary>
        static float Hash(float x, float seed)
        {
            unchecked
            {
                uint h = (uint)(int)x * 374761393u + (uint)(int)(seed * 1000f) * 668265263u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        /// <summary>Smooth 1D value noise in [0,1].</summary>
        static float Noise1(float x)
        {
            float i = Mathf.Floor(x);
            float f = x - i;
            float u = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i, 0.5f), Hash(i + 1f, 0.5f), u);
        }

        // ------------------------------------------------------------------ selection + upload

        static bool Better(int prioA, float scoreA, int prioB, float scoreB)
            => prioA != prioB ? prioA > prioB : scoreA < scoreB;

        static bool SphereVisible(Vector3 c, float r)
        {
            for (int i = 0; i < 6; i++)
                if (_planes[i].GetDistanceToPoint(c) < -r) return false;
            return true;
        }

        /// <summary>Picks up to 16 lights for <paramref name="cam"/> (null = no camera: no culling, sorted around the origin) and uploads them.</summary>
        internal static void SelectAndUpload(Camera cam)
        {
            bool hasCam = cam != null;
            Vector3 camPos = hasCam ? cam.transform.position : Vector3.zero;
            float reach = float.MaxValue;
            if (hasCam)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, _planes);
                float fogLimit = PsxEnvironment.FogEnd > PsxEnvironment.FogStart + 0.001f ? PsxEnvironment.FogEnd : float.MaxValue;
                reach = Mathf.Min(cam.farClipPlane, fogLimit) + 2f;
            }

            int count = 0;
            var all = PsxLight.All;
            for (int i = 0; i < all.Count; i++)
            {
                var l = all[i];
                if (l == null || !l.On || !l.isActiveAndEnabled) continue;
                float range = l.Range;
                if (l.CurrentIntensity <= 0.001f || range <= 0.01f) continue;
                Vector3 p = l.transform.position;
                float score = Vector3.Distance(p, camPos) - range;
                if (hasCam)
                {
                    if (score > reach) continue;                         // whole sphere beyond the visible distance
                    if (!SphereVisible(p, range * 1.1f + 1f)) continue;  // whole sphere outside the frustum (margin for anomaly warps)
                }

                int prio = l.Priority;
                int n = count;
                if (n == MaxShaderLights)
                {
                    if (!Better(prio, score, _selPrio[n - 1], _selScore[n - 1])) continue;
                    n = MaxShaderLights - 1; // drop the worst
                }
                int j = n;
                while (j > 0 && Better(prio, score, _selPrio[j - 1], _selScore[j - 1]))
                {
                    _sel[j] = _sel[j - 1]; _selPrio[j] = _selPrio[j - 1]; _selScore[j] = _selScore[j - 1];
                    j--;
                }
                _sel[j] = l; _selPrio[j] = prio; _selScore[j] = score;
                count = n + 1;
            }

            for (int i = 0; i < MaxShaderLights; i++)
            {
                if (i < count)
                {
                    var l = _sel[i];
                    Vector3 p = l.transform.position;
                    float k = l.CurrentIntensity;
                    bool spot = l.Type == PsxLightType.Spot;
                    _pos[i] = new Vector4(p.x, p.y, p.z, l.Range);
                    _col[i] = new Vector4(l.Color.r * k, l.Color.g * k, l.Color.b * k, spot ? 1f : 0f);
                    if (spot)
                    {
                        Vector3 f = l.transform.forward;
                        _dir[i] = new Vector4(f.x, f.y, f.z, SpotCosOuter(l.SpotAngle));
                    }
                    else _dir[i] = new Vector4(0f, 0f, 1f, -1f);
                }
                else
                {
                    _pos[i] = Vector4.zero;
                    _col[i] = Vector4.zero;
                    _dir[i] = new Vector4(0f, 0f, 1f, -1f);
                }
                _sel[i] = null; // do not keep references to destroyed lights
            }
            _count = count;
            UploadArrays(_pos, _col, _dir, _count);
        }

        /// <summary>Always full 16-length arrays: Unity locks a global array's size on its first upload.</summary>
        static void UploadArrays(Vector4[] pos, Vector4[] col, Vector4[] dir, int count)
        {
            Shader.SetGlobalVectorArray(PsxShaderIds.LightPos, pos);
            Shader.SetGlobalVectorArray(PsxShaderIds.LightColor, col);
            Shader.SetGlobalVectorArray(PsxShaderIds.LightDir, dir);
            Shader.SetGlobalFloat(PsxShaderIds.LightCount, count);
        }

        /// <summary>Re-uploads the last selection (after a preview render temporarily replaced the globals).</summary>
        internal static void ReuploadCached() => UploadArrays(_pos, _col, _dir, _count);

        /// <summary>Temporary two-light rig for PreviewRenderer.</summary>
        internal static void UploadPreviewLights(Vector3 keyPos, Color key, float keyRange, Vector3 fillPos, Color fill, float fillRange)
        {
            for (int i = 0; i < MaxShaderLights; i++)
            {
                _pvPos[i] = Vector4.zero;
                _pvCol[i] = Vector4.zero;
                _pvDir[i] = new Vector4(0f, 0f, 1f, -1f);
            }
            _pvPos[0] = new Vector4(keyPos.x, keyPos.y, keyPos.z, keyRange);
            _pvCol[0] = new Vector4(key.r, key.g, key.b, 0f);
            _pvPos[1] = new Vector4(fillPos.x, fillPos.y, fillPos.z, fillRange);
            _pvCol[1] = new Vector4(fill.r, fill.g, fill.b, 0f);
            UploadArrays(_pvPos, _pvCol, _pvDir, 2);
        }
    }

    /// <summary>Global environment (ambient + fog + PS1 rasterizer settings) used by the PSX shaders.</summary>
    public static class PsxEnvironment
    {
        public static Color Ambient = new Color(0.06f, 0.07f, 0.09f);
        public static Color FogColor = new Color(0.02f, 0.025f, 0.035f);
        public static float FogStart = 6f;
        public static float FogEnd = 38f;

        /// <summary>Vertical size of the virtual pixel grid vertices snap to (PS1 jitter; width follows the aspect).
        /// Lower = more wobble. 0 disables snapping. Default 120 (2 low-res pixels at 240 lines).</summary>
        public static float VertexSnapHeight = 120f;

        /// <summary>Affine texture warping: 0 = perspective correct, 1 = full PS1 affine. Default 0.7.</summary>
        public static float AffineAmount = 0.7f;

        /// <summary>(iteration 2) Overall scene brightness multiplier set per difficulty by the gameplay
        /// (Nightmare = 1, the original very dark look; Easy brightest). Scales ambient and light contribution.</summary>
        public static float Brightness = 1f;

        /// <summary>Set ambient + fog; applied to shaders on the next frame.</summary>
        public static void Set(Color ambient, Color fogColor, float fogStart, float fogEnd)
        {
            Ambient = ambient; FogColor = fogColor; FogStart = fogStart; FogEnd = fogEnd;
        }

        /// <summary>Restores the default (menu-like) environment and rasterizer settings.</summary>
        public static void ResetDefaults()
        {
            Ambient = new Color(0.06f, 0.07f, 0.09f);
            FogColor = new Color(0.02f, 0.025f, 0.035f);
            FogStart = 6f; FogEnd = 38f;
            VertexSnapHeight = 120f;
            AffineAmount = 0.7f;
        }

        static Vector4 _snapCached;

        internal static void Upload(Camera cam)
        {
            float aspect = cam != null ? cam.aspect : (Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f);
            _snapCached = VertexSnapHeight > 0f ? new Vector4(VertexSnapHeight * aspect, VertexSnapHeight, 0f, 0f) : Vector4.zero;
            UploadValues(Ambient, FogColor, FogStart, FogEnd, _snapCached, AffineAmount);
        }

        internal static void ReuploadCached() => UploadValues(Ambient, FogColor, FogStart, FogEnd, _snapCached, AffineAmount);

        /// <summary>Uploads explicit values (fogEnd &lt;= fogStart disables fog).</summary>
        internal static void UploadValues(Color ambient, Color fogColor, float fogStart, float fogEnd, Vector4 snapRes, float affine)
        {
            Shader.SetGlobalVector(PsxShaderIds.Ambient, new Vector4(ambient.r, ambient.g, ambient.b, 1f));
            Shader.SetGlobalVector(PsxShaderIds.FogColor, new Vector4(fogColor.r, fogColor.g, fogColor.b, 1f));
            float inv = fogEnd > fogStart + 0.001f ? 1f / (fogEnd - fogStart) : 0f;
            Shader.SetGlobalVector(PsxShaderIds.FogParams, new Vector4(fogStart, inv, inv > 0f ? 1f : 0f, 0f));
            Shader.SetGlobalVector(PsxShaderIds.SnapRes, snapRes);
            Shader.SetGlobalFloat(PsxShaderIds.Affine, Mathf.Clamp01(affine));
        }
    }
}
