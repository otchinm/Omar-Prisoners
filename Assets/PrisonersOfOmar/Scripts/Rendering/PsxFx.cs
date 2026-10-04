using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Low-fi visual effects built from PSX materials (billboards / small particle systems).
    /// All created objects live under <paramref name="parent"/> (or the scene root when null)
    /// and destroy themselves when finished unless stated otherwise.
    /// One-shot effects (blood, sparks, dust, explosions...) are camera independent and use pooled sprites.
    /// Textures: Resources/Textures/FX/* when present, procedural fallbacks otherwise (see PsxFxAssets).
    /// </summary>
    public static class PsxFx
    {
        static readonly DeterministicRandom _rng = new DeterministicRandom(0x5EED, 7);

        /// <summary>Maximum number of blood drop decals alive at once (oldest are recycled).</summary>
        public const int MaxBloodDecals = 200;
        /// <summary>Seconds a blood drop stays before fading.</summary>
        public static float BloodDecalLifetime = 60f;

        static readonly PsxDecalFade[] _blood = new PsxDecalFade[MaxBloodDecals];
        static int _bloodNext;

        /// <summary>(iteration 2) Hides every blood drop decal (called when a match is torn down).</summary>
        public static void ClearBloodDecals()
        {
            for (int i = 0; i < _blood.Length; i++) if (_blood[i] != null) _blood[i].gameObject.SetActive(false);
            _bloodNext = 0;
        }

        /// <summary>(iteration 2) Revolver muzzle flash at <paramref name="position"/> pointing along <paramref name="direction"/>.</summary>
        public static void MuzzleFlash(Vector3 position, Vector3 direction, int layer = -1)
        {
            if (layer < 0) layer = Layers.Default;
            LightFlash(position, new Color(1f, 0.85f, 0.6f), 4f, 9f, 0.08f);
            direction = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.forward;
            // star-shaped flash + a hot core, gone in a few frames
            var flash = Particle(position + direction * 0.06f, PsxFxAssets.Texture("glow"), PsxSurface.Additive, layer, false);
            if (flash != null)
            {
                flash.Lifetime = 0.07f;
                flash.StartSize = new Vector2(0.45f, 0.45f); flash.EndSize = new Vector2(0.2f, 0.2f);
                flash.StartColor = new Color(1f, 0.9f, 0.6f, 1f); flash.EndColor = new Color(1f, 0.5f, 0.15f, 0f);
                flash.Play();
            }
            var core = Particle(position + direction * 0.03f, PsxFxAssets.Texture("spark"), PsxSurface.Additive, layer, false);
            if (core != null)
            {
                core.Lifetime = 0.05f;
                core.StartSize = new Vector2(0.18f, 0.18f); core.EndSize = new Vector2(0.08f, 0.08f);
                core.StartColor = Color.white; core.EndColor = new Color(1f, 0.8f, 0.4f, 0f);
                core.Play();
            }
            Sparks(position, direction);
            for (int i = 0; i < 3; i++)
            {
                var smoke = Particle(position + direction * (0.05f + 0.04f * i), PsxFxAssets.Texture("smoke"), PsxSurface.Transparent, layer, false);
                if (smoke == null) continue;
                smoke.Lifetime = 0.9f + 0.3f * i;
                smoke.StartSize = new Vector2(0.08f, 0.08f); smoke.EndSize = new Vector2(0.35f, 0.35f);
                smoke.StartColor = new Color(0.7f, 0.7f, 0.68f, 0.45f); smoke.EndColor = new Color(0.6f, 0.6f, 0.6f, 0f);
                smoke.Velocity = direction * 0.6f + Vector3.up * 0.25f + UnityEngine.Random.insideUnitSphere * 0.1f;
                smoke.Drag = 2f;
                smoke.Play();
            }
        }

        // ------------------------------------------------------------------ persistent effects

        /// <summary>
        /// Lighter flame (persistent; destroy or SetActive(false) to hide): one tall tongue with a white-yellow core
        /// that sways and trails behind motion, plus a soft warm glow. ~5 cm tall at scale 1.
        /// </summary>
        public static GameObject CreateFlame(Transform parent, float scale = 1f)
        {
            int layer = parent != null ? parent.gameObject.layer : Layers.Default;
            var go = new GameObject("Flame");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;

            var frames = PsxFxAssets.Frames("flame_", 4);
            var mat = PsxFxAssets.Material(frames[0], PsxSurface.Additive);
            var flame = go.AddComponent<PsxFlame>();
            var t = PersistentSprite(go.transform, "Tongue", Vector3.zero, layer, PsxFxAssets.QuadBottom, mat, true);
            t.Frames = frames;
            t.Fps = 13f;
            t.StartSize = t.EndSize = new Vector2(flame.Width, flame.Height);
            t.StartColor = t.EndColor = new Color(1f, 0.97f, 0.9f, 1f);
            t.Flicker = 0.12f;
            t.SizeJitter = 0.15f;
            t.Play();
            flame.Tongue = t;
            flame.Bill = t.GetComponent<PsxBillboard>();

            var glow = PersistentSprite(go.transform, "FlameGlow", new Vector3(0f, 0.02f, 0f), layer, PsxFxAssets.QuadCenter,
                PsxFxAssets.Material("glow", PsxSurface.Additive), false);
            glow.StartSize = glow.EndSize = new Vector2(0.13f, 0.13f);
            glow.StartColor = glow.EndColor = new Color(1f, 0.6f, 0.25f, 0.3f);
            glow.Flicker = 0.3f;
            glow.SizeJitter = 0.12f;
            glow.Play();
            flame.Glow = glow;
            return go;
        }

        /// <summary>Persistent large fire (barrels, explosion aftermath). Includes its own PsxLight.</summary>
        public static GameObject CreateFire(Transform parent, Vector3 localPosition, float size)
        {
            size = Mathf.Max(0.05f, size);
            int layer = parent != null ? parent.gameObject.layer : Layers.Default;
            var go = new GameObject("Fire");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var frames = PsxFxAssets.Frames("fire_", 4);
            var mat = PsxFxAssets.Material(frames[0], PsxSurface.Additive);
            for (int i = 0; i < 3; i++)
            {
                float s = size * (1f - i * 0.22f);
                var off = new Vector3((i - 1) * 0.22f * size, 0f, ((i * 2) % 3 - 1) * 0.12f * size);
                var t = PersistentSprite(go.transform, "Tongue" + i, off, layer, PsxFxAssets.QuadBottom, mat, true);
                t.Frames = frames;
                t.Fps = 9f + i * 2.5f;
                t.StartSize = t.EndSize = new Vector2(0.95f * s, 1.55f * s);
                t.StartColor = t.EndColor = new Color(1f, 0.85f, 0.7f, 1f);
                t.Flicker = 0.2f;
                t.SizeJitter = 0.15f;
                t.Play();
            }

            var glow = PersistentSprite(go.transform, "FireGlow", new Vector3(0f, 0.55f * size, 0f), layer, PsxFxAssets.QuadCenter,
                PsxFxAssets.Material("glow", PsxSurface.Additive), false);
            glow.StartSize = glow.EndSize = new Vector2(3.2f * size, 3.2f * size);
            glow.StartColor = glow.EndColor = new Color(1f, 0.42f, 0.12f, 0.38f);
            glow.Flicker = 0.3f;
            glow.Play();

            var light = PsxLight.Create(go.transform, new Vector3(0f, 0.9f * size, 0f), new Color(1f, 0.52f, 0.2f), 1.6f,
                Mathf.Clamp(10f * size, 3f, 20f), PsxFlicker.Fire, "FireLight");
            light.FlickerAmount = 0.35f;
            light.FlickerSpeed = 6f;
            light.Priority = 3;

            AddEmitter(go.transform, "Smoke", new Vector3(0f, 1.4f * size, 0f), layer, PsxEmitter.Kind.Smoke, 2.5f, size, Color.white);
            AddEmitter(go.transform, "Embers", new Vector3(0f, 0.6f * size, 0f), layer, PsxEmitter.Kind.Embers, 3f, size, Color.white);
            return go;
        }

        /// <summary>Road flare / signal flare (persistent, red flickering light + sprite).</summary>
        public static GameObject CreateFlare(Transform parent, Vector3 localPosition)
        {
            int layer = parent != null ? parent.gameObject.layer : Layers.Default;
            var go = new GameObject("Flare");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var frames = PsxFxAssets.Frames("flame_", 4);
            var core = PersistentSprite(go.transform, "FlareFlame", Vector3.zero, layer, PsxFxAssets.QuadBottom,
                PsxFxAssets.Material(frames[0], PsxSurface.Additive), true);
            core.Frames = frames;
            core.Fps = 18f;
            core.StartSize = core.EndSize = new Vector2(0.07f, 0.13f);
            core.StartColor = core.EndColor = new Color(1f, 0.38f, 0.3f, 1f);
            core.Flicker = 0.3f;
            core.SizeJitter = 0.25f;
            core.Play();

            var glow = PersistentSprite(go.transform, "FlareGlow", new Vector3(0f, 0.05f, 0f), layer, PsxFxAssets.QuadCenter,
                PsxFxAssets.Material("glow", PsxSurface.Additive), false);
            glow.StartSize = glow.EndSize = new Vector2(0.9f, 0.9f);
            glow.StartColor = glow.EndColor = new Color(1f, 0.12f, 0.08f, 0.45f);
            glow.Flicker = 0.35f;
            glow.Play();

            var light = PsxLight.Create(go.transform, new Vector3(0f, 0.25f, 0f), new Color(1f, 0.16f, 0.1f), 1.7f, 9f, PsxFlicker.Candle, "FlareLight");
            light.FlickerAmount = 0.45f;
            light.FlickerSpeed = 11f;
            light.Priority = 2;

            AddEmitter(go.transform, "Smoke", new Vector3(0f, 0.15f, 0f), layer, PsxEmitter.Kind.Smoke, 2f, 0.4f, new Color(1f, 0.75f, 0.75f));
            AddEmitter(go.transform, "Embers", new Vector3(0f, 0.05f, 0f), layer, PsxEmitter.Kind.Embers, 4f, 0.5f, new Color(1f, 0.45f, 0.4f));
            return go;
        }

        // ------------------------------------------------------------------ one-shot effects

        /// <summary>Blood spray burst at a hit point.</summary>
        public static void BloodBurst(Vector3 position, Vector3 direction, float amount = 1f)
        {
            amount = Mathf.Clamp(amount, 0.1f, 4f);
            Vector3 dir = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.up;
            float floor = GroundY(position, 3f) + 0.01f;
            var tex = PsxFxAssets.Texture("blood_spray");
            int n = Mathf.Clamp(Mathf.RoundToInt(10f * amount), 3, 40);
            float speed = Mathf.Sqrt(amount);
            for (int i = 0; i < n; i++)
            {
                var s = Particle(position, tex, PsxSurface.Transparent, Layers.Default, false);
                if (s == null) break;
                s.Velocity = (dir * R(1.5f, 4f) + RandomInSphere() * 1.3f) * speed;
                s.Gravity = 9.8f;
                s.Drag = 0.6f;
                s.FloorY = floor;
                s.Lifetime = R(0.45f, 0.9f);
                float size = R(0.05f, 0.13f);
                s.StartSize = new Vector2(size, size);
                s.EndSize = s.StartSize * 1.4f;
                s.StartColor = new Color(0.75f, 0.05f, 0.05f, 0.95f);
                s.EndColor = new Color(0.6f, 0.03f, 0.03f, 0f);
                s.FadeStart = 0.5f;
                SetRoll(s, R(0f, 360f));
                s.Play();
            }
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 1e-6f) flat.Normalize();
            int drops = Mathf.Clamp(Mathf.RoundToInt(2f * amount), 1, 6);
            for (int i = 0; i < drops; i++)
            {
                Vector2 c = _rng.InsideUnitCircle() * 0.25f;
                BloodDrop(position + flat * R(0.1f, 0.9f) + new Vector3(c.x, 0f, c.y));
            }
        }

        /// <summary>Blood drop decal on the ground below a point (injured prisoners leave a trail). Fades after a while.</summary>
        public static void BloodDrop(Vector3 position)
        {
            if (!Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 4f, Layers.Solid, QueryTriggerInteraction.Ignore))
                return;
            var d = NextBloodDecal();
            float size = R(0.06f, 0.2f);
            Quaternion rot = Quaternion.AngleAxis(R(0f, 360f), hit.normal) * Quaternion.FromToRotation(Vector3.back, hit.normal);
            d.transform.SetPositionAndRotation(hit.point + hit.normal * 0.004f, rot);
            d.transform.localScale = new Vector3(size, size * R(0.8f, 1.2f), 1f);
            d.Restart(new Color(0.34f, 0.02f, 0.02f, 0.92f), BloodDecalLifetime, 6f);
        }

        /// <summary>Big explosion: flash light, fireball billboards, smoke, debris.</summary>
        public static void Explosion(Vector3 position, float size = 1f)
        {
            size = Mathf.Max(0.1f, size);
            LightFlash(position + Vector3.up * (0.5f * size), new Color(1f, 0.62f, 0.28f), 4f, 16f * size, 0.9f);

            var frames = PsxFxAssets.Frames("explosion_", 4);
            for (int i = 0; i < 8; i++)
            {
                var s = Particle(position + RandomInSphere() * (0.4f * size), frames[0], PsxSurface.Additive, Layers.Default, false);
                if (s == null) break;
                s.Frames = frames;
                s.LoopFrames = false;
                s.Lifetime = R(0.45f, 0.8f);
                s.Velocity = RandomInSphere() * (R(0.5f, 2f) * size) + Vector3.up * (R(0.5f, 1.5f) * size);
                s.Drag = 2f;
                float sz = R(1f, 1.8f) * size;
                s.StartSize = new Vector2(sz, sz);
                s.EndSize = s.StartSize * 1.6f;
                s.StartColor = new Color(1f, 0.92f, 0.8f, 1f);
                s.EndColor = new Color(0.7f, 0.25f, 0.08f, 0f);
                SetRoll(s, R(0f, 360f));
                s.Play();
            }

            var smoke = PsxFxAssets.Texture("smoke");
            for (int i = 0; i < 10; i++)
            {
                var s = Particle(position + RandomInSphere() * (0.6f * size) + Vector3.up * (0.3f * size), smoke, PsxSurface.Transparent, Layers.Default, false);
                if (s == null) break;
                s.Velocity = RandomInSphere() * (0.8f * size) + Vector3.up * (R(1f, 2.5f) * size);
                s.Drag = 0.8f;
                s.Lifetime = R(3f, 5f);
                float sz = R(1.5f, 3f) * size;
                s.StartSize = new Vector2(sz, sz);
                s.EndSize = s.StartSize * 2f;
                s.StartColor = new Color(0.2f, 0.18f, 0.16f, 0.8f);
                s.EndColor = new Color(0.12f, 0.11f, 0.1f, 0f);
                SetRoll(s, R(0f, 360f));
                s.Play();
            }

            SparkBurst(position, Vector3.up, 18, 5f, 12f, 1f);
            for (int i = 0; i < 12; i++) SpawnDebris(position + Vector3.up * 0.2f, size);
            Dust(position, size * 1.5f);
        }

        /// <summary>Small spark burst (bolt cutters, cleaver hitting metal).</summary>
        public static void Sparks(Vector3 position, Vector3 normal)
        {
            Vector3 n = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
            SparkBurst(position + n * 0.02f, n, 14, 2f, 5.5f, 0.75f);
            LightFlash(position + n * 0.1f, new Color(1f, 0.75f, 0.45f), 1.2f, 2.5f, 0.09f);
        }

        /// <summary>Dust puff (doors, falling objects).</summary>
        public static void Dust(Vector3 position, float size = 1f)
        {
            size = Mathf.Max(0.05f, size);
            var tex = PsxFxAssets.Texture("dust");
            for (int i = 0; i < 7; i++)
            {
                Vector2 c = _rng.InsideUnitCircle();
                var s = Particle(position + new Vector3(c.x, 0f, c.y) * (0.15f * size), tex, PsxSurface.Transparent, Layers.Default, false);
                if (s == null) break;
                Vector2 h = _rng.InsideUnitCircle().normalized * R(0.3f, 0.9f);
                s.Velocity = new Vector3(h.x, R(0.05f, 0.35f), h.y) * size;
                s.Drag = 1.8f;
                s.Lifetime = R(1.2f, 2.4f);
                float sz = R(0.25f, 0.45f) * size;
                s.StartSize = new Vector2(sz, sz);
                s.EndSize = s.StartSize * 2.2f;
                s.StartColor = new Color(0.55f, 0.5f, 0.45f, 0.42f);
                s.EndColor = new Color(0.55f, 0.5f, 0.45f, 0f);
                SetRoll(s, R(0f, 360f));
                s.Play();
            }
        }

        /// <summary>Glass shards (bottle break).</summary>
        public static void GlassShatter(Vector3 position)
        {
            float floor = GroundY(position, 3f) + 0.005f;
            var tex = PsxFxAssets.Texture("glass_shard");
            for (int i = 0; i < 14; i++)
            {
                var s = Particle(position, tex, PsxSurface.Transparent, Layers.Default, false);
                if (s == null) break;
                s.Velocity = RandomInSphere() * R(1f, 3f) + Vector3.up * R(0.8f, 2.2f);
                s.Gravity = 9.8f;
                s.Drag = 0.3f;
                s.FloorY = floor;
                s.Lifetime = R(2.5f, 4f);
                float sz = R(0.025f, 0.06f);
                s.StartSize = s.EndSize = new Vector2(sz, sz);
                s.StartColor = new Color(0.8f, 0.9f, 0.95f, 0.9f);
                s.EndColor = new Color(0.8f, 0.9f, 0.95f, 0f);
                s.FadeStart = 0.7f;
                SetRoll(s, R(0f, 360f));
                s.Play();
            }
            // a few glints
            var glint = PsxFxAssets.Texture("spark");
            for (int i = 0; i < 5; i++)
            {
                var s = Particle(position, glint, PsxSurface.Additive, Layers.Default, false);
                if (s == null) break;
                s.Velocity = RandomInSphere() * R(1f, 2.5f) + Vector3.up;
                s.Gravity = 9.8f;
                s.FloorY = floor;
                s.Lifetime = R(0.2f, 0.45f);
                s.StartSize = s.EndSize = new Vector2(0.03f, 0.03f);
                s.StartColor = new Color(0.8f, 0.9f, 1f, 1f);
                s.EndColor = new Color(0.8f, 0.9f, 1f, 0f);
                s.Play();
            }
            Dust(position, 0.4f);
        }

        /// <summary>Brief flare/light flash with a PsxLight (gunshot-like flash, explosion).</summary>
        public static void LightFlash(Vector3 position, Color color, float intensity, float range, float duration)
        {
            var t = GeoUtil.CreateChild(null, "LightFlash", position, Quaternion.identity, Layers.Default);
            var l = t.gameObject.AddComponent<PsxLight>();
            l.Color = color;
            l.Intensity = intensity;
            l.Range = Mathf.Max(0.1f, range);
            l.Priority = 8;
            l.CurrentIntensity = intensity;
            var tl = t.gameObject.AddComponent<PsxTimedLight>();
            tl.Light = l;
            tl.Duration = Mathf.Max(0.02f, duration);
            tl.StartIntensity = intensity;
        }

        // ------------------------------------------------------------------ internals

        /// <summary>Spawns one particle for an emitter (fire smoke, embers).</summary>
        internal static void Emit(PsxEmitter e)
        {
            Vector3 p = e.transform.position;
            float size = Mathf.Max(0.05f, e.Size);
            // particles live in world space: never on the view model layer (they would draw over the world)
            int layer = e.gameObject.layer == Layers.ViewModel ? Layers.Default : e.gameObject.layer;
            if (e.Type == PsxEmitter.Kind.Smoke)
            {
                Vector2 c = _rng.InsideUnitCircle() * (0.15f * size);
                var s = Particle(p + new Vector3(c.x, 0f, c.y), PsxFxAssets.Texture("smoke"), PsxSurface.Transparent, layer, false);
                if (s == null) return;
                float k = Mathf.Sqrt(size);
                s.Velocity = new Vector3(R(-0.25f, 0.25f), R(0.7f, 1.3f), R(-0.25f, 0.25f)) * k;
                s.Drag = 0.3f;
                s.Lifetime = R(2.5f, 4f);
                float sz = 0.5f * size;
                s.StartSize = new Vector2(sz, sz);
                s.EndSize = s.StartSize * 3.6f;
                s.StartColor = new Color(0.35f * e.Tint.r, 0.33f * e.Tint.g, 0.3f * e.Tint.b, 0.55f);
                s.EndColor = new Color(0.25f * e.Tint.r, 0.24f * e.Tint.g, 0.22f * e.Tint.b, 0f);
                SetRoll(s, R(0f, 360f));
                s.Play();
            }
            else
            {
                Vector2 c = _rng.InsideUnitCircle() * (0.3f * size);
                var s = Particle(p + new Vector3(c.x, 0f, c.y), PsxFxAssets.Texture("spark"), PsxSurface.Additive, layer, false);
                if (s == null) return;
                s.Velocity = new Vector3(R(-0.4f, 0.4f), R(1.2f, 2.5f), R(-0.4f, 0.4f)) * Mathf.Sqrt(size);
                s.Gravity = -0.3f;
                s.Drag = 0.8f;
                s.Lifetime = R(0.6f, 1.4f);
                float sz = 0.03f * Mathf.Max(0.5f, size);
                s.StartSize = new Vector2(sz, sz);
                s.EndSize = s.StartSize * 0.5f;
                s.StartColor = new Color(1f * e.Tint.r, 0.55f * e.Tint.g, 0.2f * e.Tint.b, 1f);
                s.EndColor = new Color(0.8f * e.Tint.r, 0.2f * e.Tint.g, 0.05f * e.Tint.b, 0f);
                s.Flicker = 0.5f;
                s.Play();
            }
        }

        static void SparkBurst(Vector3 position, Vector3 n, int count, float minSpeed, float maxSpeed, float spread)
        {
            float floor = GroundY(position, 4f) + 0.01f;
            var tex = PsxFxAssets.Texture("spark");
            for (int i = 0; i < count; i++)
            {
                var s = Particle(position, tex, PsxSurface.Additive, Layers.Default, false);
                if (s == null) break;
                s.Velocity = (n + RandomInSphere() * spread).normalized * R(minSpeed, maxSpeed);
                s.Gravity = 9.8f;
                s.Drag = 1.2f;
                s.FloorY = floor;
                s.Lifetime = R(0.2f, 0.55f);
                s.StartSize = new Vector2(0.035f, 0.035f);
                s.EndSize = new Vector2(0.015f, 0.015f);
                s.StartColor = new Color(1f, 0.85f, 0.5f, 1f);
                s.EndColor = new Color(1f, 0.4f, 0.1f, 0f);
                s.Flicker = 0.4f;
                s.Play();
            }
        }

        static void SpawnDebris(Vector3 position, float size)
        {
            var go = new GameObject("Debris");
            go.layer = Layers.Default;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f)));
            PsxFxAssets.AddRenderer(go, PsxFxAssets.Cube, PsxFxAssets.DebrisMaterial);
            var d = go.AddComponent<PsxDebris>();
            Vector3 v = RandomInSphere();
            v.y = Mathf.Abs(v.y) + 0.4f;
            d.Velocity = v.normalized * (R(4f, 9f) * Mathf.Sqrt(size));
            d.AngularVelocity = RandomInSphere() * R(200f, 700f);
            d.Size = R(0.04f, 0.14f) * size;
            d.Lifetime = R(4f, 7f);
            go.transform.localScale = Vector3.one * d.Size;
        }

        static PsxSprite PersistentSprite(Transform parent, string name, Vector3 localPos, int layer, Mesh mesh, Material material, bool cylindrical)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            PsxFxAssets.AddRenderer(go, mesh, material);
            var bb = go.AddComponent<PsxBillboard>();
            bb.Cylindrical = cylindrical;
            var s = go.AddComponent<PsxSprite>();
            s.Lifetime = -1f;
            return s;
        }

        static void AddEmitter(Transform parent, string name, Vector3 localPos, int layer, PsxEmitter.Kind kind, float rate, float size, Color tint)
        {
            var t = GeoUtil.CreateChild(parent, name, localPos, Quaternion.identity, layer);
            var e = t.gameObject.AddComponent<PsxEmitter>();
            e.Type = kind;
            e.Rate = rate;
            e.Size = size;
            e.Tint = tint;
        }

        static PsxSprite Particle(Vector3 position, Texture2D texture, PsxSurface surface, int layer, bool cylindrical)
        {
            return PsxSpritePool.Spawn(position, PsxFxAssets.Material(texture, surface), PsxFxAssets.QuadCenter, layer, true, cylindrical);
        }

        static void SetRoll(PsxSprite s, float degrees)
        {
            var bb = s.GetComponent<PsxBillboard>();
            if (bb != null) bb.Roll = degrees;
        }

        static PsxDecalFade NextBloodDecal()
        {
            int i = _bloodNext;
            _bloodNext = (_bloodNext + 1) % MaxBloodDecals;
            var d = _blood[i];
            if (d == null)
            {
                var go = new GameObject("BloodDrop");
                go.layer = Layers.Default;
                go.transform.SetParent(PsxSpritePool.Root, false);
                PsxFxAssets.AddRenderer(go, PsxFxAssets.QuadCenter, PsxFxAssets.Material("blood_drop", PsxSurface.Decal));
                d = go.AddComponent<PsxDecalFade>();
                _blood[i] = d;
            }
            return d;
        }

        static float GroundY(Vector3 p, float maxDistance)
        {
            if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, maxDistance + 0.1f, Layers.Solid, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return p.y - maxDistance;
        }

        static float R(float min, float max) => _rng.Range(min, max);

        static Vector3 RandomInSphere() => _rng.OnUnitSphere() * Mathf.Pow(_rng.NextFloat(), 1f / 3f);
    }

    /// <summary>Dark soft blob under characters / props (no real shadows in this style).</summary>
    public static class BlobShadow
    {
        /// <summary>Adds a ground blob (radius in meters) that follows <paramref name="target"/> (child of it, same layer).</summary>
        public static GameObject Attach(Transform target, float radius)
        {
            var go = new GameObject("BlobShadow");
            go.layer = target != null ? target.gameObject.layer : Layers.Default;
            go.transform.SetParent(target, false);
            PsxFxAssets.AddRenderer(go, PsxFxAssets.QuadCenter, PsxFxAssets.Material("blob_shadow", PsxSurface.Decal));
            var b = go.AddComponent<PsxBlobShadow>();
            b.Target = target;
            b.Radius = Mathf.Max(0.05f, radius);
            return go;
        }
    }

    /// <summary>Night sky dome that follows the world camera.</summary>
    public static class PsxSky
    {
        public const string ShaderName = "PrisonersOfOmar/PSX Sky";

        static GameObject _current;

        /// <summary>The current sky (only one exists; creating a new one destroys the previous).</summary>
        public static GameObject Current => _current;

        /// <summary>
        /// Builds the sky dome from a cylindrical panorama (Resources path, e.g. "Textures/Sky/sky_night"; a procedural
        /// overcast sky is used when missing) multiplied by <paramref name="tint"/>. Drawn behind everything, horizon fades to
        /// the fog color, always centered on the world camera.
        /// </summary>
        public static GameObject Create(string texturePath, Color tint)
        {
            if (_current != null) Object.Destroy(_current);
            var go = new GameObject("PsxSky");
            go.layer = Layers.Default;

            var shader = Shader.Find(ShaderName);
            if (shader == null) shader = PsxMaterials.ShaderFor(PsxSurface.Unlit);
            var mat = new Material(shader) { name = "PsxSky" };
            Texture2D tex = string.IsNullOrEmpty(texturePath) ? null : Resources.Load<Texture2D>(texturePath);
            if (tex == null) tex = PsxFxAssets.ProceduralSky();
            tex.filterMode = FilterMode.Point;
            tex.wrapModeU = TextureWrapMode.Repeat;
            tex.wrapModeV = TextureWrapMode.Clamp;
            mat.mainTexture = tex;
            mat.color = tint;

            PsxFxAssets.AddRenderer(go, PsxFxAssets.SkyDome, mat);
            var cam = PsxRenderDriver.WorldCamera();
            if (cam != null) go.transform.position = cam.transform.position;
            _current = go;
            return go;
        }

        internal static void Follow(Vector3 cameraPosition)
        {
            if (_current != null) _current.transform.position = cameraPosition;
        }
    }

    /// <summary>
    /// Renders a model (put on Layers.Preview, placed anywhere) into a RenderTexture for the inventory / lobby.
    /// Uses its own temporary lighting so the model is readable regardless of world lights.
    /// Call it from Update / LateUpdate (or inside a DrawOverlay handler) and draw the texture afterwards.
    /// </summary>
    public static class PreviewRenderer
    {
        static Camera _cam;
        static readonly List<Renderer> _renderers = new List<Renderer>(32);

        /// <summary>True while a preview camera is rendering (the world light globals are temporarily replaced).</summary>
        public static bool IsRendering { get; private set; }

        public static Color KeyLightColor = new Color(1.1f, 1.02f, 0.9f);
        public static Color FillLightColor = new Color(0.32f, 0.38f, 0.5f);
        public static Color Ambient = new Color(0.22f, 0.22f, 0.25f);
        public static float FieldOfView = 30f;

        /// <summary>
        /// Renders <paramref name="model"/> framed to fit, rotated by <paramref name="yawDegrees"/> around Y.
        /// <paramref name="target"/> is cleared to transparent black first.
        /// </summary>
        public static void Render(Transform model, RenderTexture target, float yawDegrees, float pitchDegrees = 15f, float zoom = 1f)
        {
            if (model == null || target == null || IsRendering || !Application.isPlaying) return;
            var cam = GetCamera();

            // ---- framing (bounds of every enabled renderer under the model)
            _renderers.Clear();
            model.GetComponentsInChildren(false, _renderers);
            bool has = false;
            Bounds b = default;
            for (int i = 0; i < _renderers.Count; i++)
            {
                var r = _renderers[i];
                if (r == null || !r.enabled) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
            _renderers.Clear();
            if (!has) b = new Bounds(model.position, Vector3.one * 0.5f);

            float radius = Mathf.Max(b.extents.magnitude, 0.03f);
            float fov = Mathf.Clamp(FieldOfView, 5f, 90f);
            float dist = radius / Mathf.Sin(fov * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.05f, zoom);
            // model faces +Z: yaw 0 looks at its front; turning the model by +yaw == orbiting the camera by -yaw
            Quaternion rot = Quaternion.Euler(pitchDegrees, 180f - yawDegrees, 0f);
            Vector3 camPos = b.center - rot * Vector3.forward * dist;
            cam.transform.SetPositionAndRotation(camPos, rot);
            cam.fieldOfView = fov;
            cam.nearClipPlane = Mathf.Max(0.01f, dist - radius * 2f);
            cam.farClipPlane = dist + radius * 2f + 1f;

            // ---- target (render through a temporary depth buffer if the target has none)
            if (!target.IsCreated()) target.Create();
            RenderTexture temp = null;
            RenderTexture rt = target;
            if (target.depth == 0)
            {
                temp = RenderTexture.GetTemporary(target.width, target.height, 24, RenderTextureFormat.ARGB32);
                temp.filterMode = FilterMode.Point;
                rt = temp;
            }
            cam.targetTexture = rt;
            cam.aspect = target.width / (float)Mathf.Max(1, target.height);

            // ---- temporary lighting: warm key from the upper left, cool rim from the back right, no fog / anomalies
            Vector3 right = rot * Vector3.right, up = rot * Vector3.up, fwd = rot * Vector3.forward;
            Vector3 keyPos = b.center + (-right * 0.9f + up * 1f - fwd * 0.9f).normalized * (dist * 1.2f);
            Vector3 fillPos = b.center + (right * 1f - up * 0.2f + fwd * 0.6f).normalized * (dist * 1.2f);
            var prevActive = RenderTexture.active;
            IsRendering = true;
            PsxLightManager.UploadPreviewLights(keyPos, KeyLightColor, dist * 3f, fillPos, FillLightColor, dist * 3f);
            PsxEnvironment.UploadValues(Ambient, Color.black, 0f, 0f,
                new Vector4(target.width * 0.5f, target.height * 0.5f, 0f, 0f), PsxEnvironment.AffineAmount);
            AnomalySystem.UploadNeutral();
            PsxBillboard.FaceAll(cam.transform, 1 << Layers.Preview); // e.g. a lighter flame on a preview model

            GL.PushMatrix();
            try
            {
                cam.Render();
                if (temp != null) Graphics.Blit(temp, target);
            }
            catch (Exception e) { Debug.LogException(e); }
            finally
            {
                GL.PopMatrix();
                cam.targetTexture = null;
                if (temp != null) RenderTexture.ReleaseTemporary(temp);
                RenderTexture.active = prevActive;
                IsRendering = false;
                PsxRenderDriver.ReapplyCached();
            }
        }

        static Camera GetCamera()
        {
            if (_cam != null) return _cam;
            var go = new GameObject("PsxPreviewCamera");
            go.hideFlags = HideFlags.HideInHierarchy;
            Object.DontDestroyOnLoad(go);
            _cam = go.AddComponent<Camera>();
            _cam.enabled = false;
            _cam.cullingMask = 1 << Layers.Preview;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.renderingPath = RenderingPath.Forward;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.useOcclusionCulling = false;
            _cam.depth = -100f;
            return _cam;
        }
    }
}
