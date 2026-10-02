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
        Pulse,      // slow sine breathing
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

        void OnEnable() { if (!_all.Contains(this)) _all.Add(this); CurrentIntensity = On ? Intensity : 0f; }
        void OnDisable() { _all.Remove(this); }

        public static PsxLight Create(Transform parent, Vector3 localPosition, Color color, float intensity, float range,
            PsxFlicker flicker = PsxFlicker.None, string name = "PsxLight")
        {
            var t = GeoUtil.CreateChild(parent, name, localPosition, Quaternion.identity, Layers.Default);
            var l = t.gameObject.AddComponent<PsxLight>();
            l.Color = color; l.Intensity = intensity; l.Range = range; l.Flicker = flicker;
            return l;
        }

        public static PsxLight CreateSpot(Transform parent, Vector3 localPosition, Quaternion localRotation, Color color, float intensity,
            float range, float spotAngle, PsxFlicker flicker = PsxFlicker.None, string name = "PsxSpot")
        {
            var t = GeoUtil.CreateChild(parent, name, localPosition, localRotation, Layers.Default);
            var l = t.gameObject.AddComponent<PsxLight>();
            l.Type = PsxLightType.Spot; l.Color = color; l.Intensity = intensity; l.Range = range; l.SpotAngle = spotAngle; l.Flicker = flicker;
            return l;
        }
    }

    /// <summary>
    /// Uploads lights + environment to global shader properties each frame and answers light queries
    /// for gameplay (stealth). Created automatically on first use.
    /// </summary>
    public static class PsxLightManager
    {
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
                Vector3 d = worldPos - l.transform.position;
                float dist = d.magnitude;
                if (dist >= l.Range) continue;
                float att = 1f - dist / l.Range; att *= att;
                if (l.Type == PsxLightType.Spot && dist > 0.001f)
                {
                    float cosA = Vector3.Dot(d / dist, l.transform.forward);
                    float cosOuter = Mathf.Cos(l.SpotAngle * 0.5f * Mathf.Deg2Rad);
                    if (cosA < cosOuter) continue;
                }
                sum += att * l.CurrentIntensity * l.Color.grayscale;
            }
            return sum;
        }
    }

    /// <summary>Global environment (ambient + fog) used by the PSX shaders.</summary>
    public static class PsxEnvironment
    {
        public static Color Ambient = new Color(0.06f, 0.07f, 0.09f);
        public static Color FogColor = new Color(0.02f, 0.025f, 0.035f);
        public static float FogStart = 6f;
        public static float FogEnd = 38f;

        /// <summary>Set ambient + fog; applied to shaders on the next frame.</summary>
        public static void Set(Color ambient, Color fogColor, float fogStart, float fogEnd)
        {
            Ambient = ambient; FogColor = fogColor; FogStart = fogStart; FogEnd = fogEnd;
        }
    }
}
