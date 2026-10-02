using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Low-fi visual effects built from PSX materials (billboards / small particle systems).
    /// All created objects live under <paramref name="parent"/> (or the scene root when null)
    /// and destroy themselves when finished unless stated otherwise.
    /// </summary>
    public static class PsxFx
    {
        /// <summary>Animated lighter flame billboard (persistent; destroy or SetActive(false) to hide). ~3cm tall at scale 1.</summary>
        public static GameObject CreateFlame(Transform parent, float scale = 1f)
        {
            var go = new GameObject("Flame");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>Persistent large fire (barrels, explosion aftermath). Includes its own PsxLight.</summary>
        public static GameObject CreateFire(Transform parent, Vector3 localPosition, float size)
        {
            var go = new GameObject("Fire");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        /// <summary>Blood spray burst at a hit point.</summary>
        public static void BloodBurst(Vector3 position, Vector3 direction, float amount = 1f) { }

        /// <summary>Blood drop decal on the ground below a point (injured prisoners leave a trail). Fades after a while.</summary>
        public static void BloodDrop(Vector3 position) { }

        /// <summary>Big explosion: flash light, fireball billboards, smoke, debris.</summary>
        public static void Explosion(Vector3 position, float size = 1f) { }

        /// <summary>Small spark burst (bolt cutters, cleaver hitting metal).</summary>
        public static void Sparks(Vector3 position, Vector3 normal) { }

        /// <summary>Dust puff (doors, falling objects).</summary>
        public static void Dust(Vector3 position, float size = 1f) { }

        /// <summary>Glass shards (bottle break).</summary>
        public static void GlassShatter(Vector3 position) { }

        /// <summary>Brief flare/light flash with a PsxLight (gunshot-like flash, explosion).</summary>
        public static void LightFlash(Vector3 position, Color color, float intensity, float range, float duration) { }

        /// <summary>Road flare / signal flare (persistent, red flickering light + sprite).</summary>
        public static GameObject CreateFlare(Transform parent, Vector3 localPosition)
        {
            var go = new GameObject("Flare");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }
    }

    /// <summary>Dark soft blob under characters / props (no real shadows in this style).</summary>
    public static class BlobShadow
    {
        public static GameObject Attach(Transform target, float radius)
        {
            var go = new GameObject("BlobShadow");
            go.transform.SetParent(target, false);
            return go;
        }
    }

    /// <summary>Night sky dome that follows the world camera.</summary>
    public static class PsxSky
    {
        public static GameObject Create(string texturePath, Color tint)
        {
            var go = new GameObject("PsxSky");
            return go;
        }
    }

    /// <summary>
    /// Renders a model (put on Layers.Preview, placed anywhere) into a RenderTexture for the inventory / lobby.
    /// Uses its own temporary lighting so the model is readable regardless of world lights.
    /// </summary>
    public static class PreviewRenderer
    {
        /// <summary>
        /// Renders <paramref name="model"/> framed to fit, rotated by <paramref name="yawDegrees"/> around Y.
        /// <paramref name="target"/> is cleared to transparent black first.
        /// </summary>
        public static void Render(Transform model, RenderTexture target, float yawDegrees, float pitchDegrees = 15f, float zoom = 1f) { }
    }
}
