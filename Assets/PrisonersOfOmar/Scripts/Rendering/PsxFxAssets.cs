using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Textures, meshes and materials of the FX module. Textures come from Resources/Textures/FX/&lt;name&gt; when present,
    /// otherwise a small procedural replacement is generated so effects are never invisible or magenta.
    /// </summary>
    public static class PsxFxAssets
    {
        public const string FxFolder = "Textures/FX/";

        static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Texture2D[]> _frames = new Dictionary<string, Texture2D[]>();
        static readonly Dictionary<(Texture, PsxSurface), Material> _materials = new Dictionary<(Texture, PsxSurface), Material>();
        static Mesh _quadCenter, _quadBottom, _cube, _dome;
        static Material _debris;
        static Texture2D _sky;

        // ------------------------------------------------------------------ textures

        /// <summary>FX texture by name ("glow", "smoke", "spark", "blood_drop", "flame_0", ...). Never null.</summary>
        public static Texture2D Texture(string name)
        {
            if (_textures.TryGetValue(name, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(FxFolder + name);
            if (t == null) t = Generate(name);
            t.filterMode = FilterMode.Point;
            t.wrapMode = TextureWrapMode.Clamp;
            _textures[name] = t;
            return t;
        }

        /// <summary>Flipbook frames prefix0..prefix(count-1) (e.g. "flame_", 4). Never null / never contains null.</summary>
        public static Texture2D[] Frames(string prefix, int count)
        {
            if (_frames.TryGetValue(prefix, out var f) && f != null && f.Length == count && f[0] != null) return f;
            f = new Texture2D[count];
            bool fromResources = Resources.Load<Texture2D>(FxFolder + prefix + "0") != null;
            for (int i = 0; i < count; i++)
            {
                string name = prefix + i;
                Texture2D t = null;
                if (fromResources)
                {
                    t = Resources.Load<Texture2D>(FxFolder + name);
                    if (t != null) { t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Clamp; }
                    else if (i > 0) t = f[i - 1];
                }
                if (t == null) t = Texture(name);
                f[i] = t;
            }
            _frames[prefix] = f;
            return f;
        }

        // ------------------------------------------------------------------ materials

        /// <summary>Shared material of a PSX surface with this texture (swap frames / colors with MaterialPropertyBlocks).</summary>
        public static Material Material(Texture texture, PsxSurface surface)
        {
            var key = (texture, surface);
            if (_materials.TryGetValue(key, out var m) && m != null) return m;
            m = PsxMaterials.Create(null, surface, Color.white);
            m.mainTexture = texture;
            m.name = "Fx_" + (texture != null ? texture.name : "none") + "_" + surface;
            _materials[key] = m;
            return m;
        }

        public static Material Material(string textureName, PsxSurface surface) => Material(Texture(textureName), surface);

        internal static Material DebrisMaterial
        {
            get
            {
                if (_debris == null) _debris = PsxMaterials.Create(null, PsxSurface.Lit, new Color(0.16f, 0.14f, 0.12f));
                return _debris;
            }
        }

        // ------------------------------------------------------------------ meshes

        /// <summary>1x1 quad in the XY plane centered on the origin, front face towards -Z (billboard / decal).</summary>
        public static Mesh QuadCenter => _quadCenter != null ? _quadCenter : (_quadCenter = BuildQuad(-0.5f, 0.5f, "PsxFxQuad"));

        /// <summary>1x1 quad in the XY plane with its pivot at the bottom center (flames), front face towards -Z.</summary>
        public static Mesh QuadBottom => _quadBottom != null ? _quadBottom : (_quadBottom = BuildQuad(0f, 1f, "PsxFxQuadBottom"));

        /// <summary>Unit cube (debris).</summary>
        public static Mesh Cube
        {
            get
            {
                if (_cube != null) return _cube;
                var mb = new MeshBuilder();
                mb.SetMaterial(DebrisMaterial);
                mb.AddBox(Vector3.zero, Vector3.one, BoxUV.PerFace);
                _cube = mb.ToMesh("PsxFxCube");
                return _cube;
            }
        }

        static Mesh BuildQuad(float y0, float y1, string name)
        {
            var m = new Mesh { name = name };
            m.vertices = new[] { new Vector3(-0.5f, y0, 0f), new Vector3(-0.5f, y1, 0f), new Vector3(0.5f, y1, 0f), new Vector3(0.5f, y0, 0f) };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.colors32 = new[] { new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255), new Color32(255, 255, 255, 255) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 }; // clockwise seen from -Z (Unity front face)
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Inverted sky dome (radius 150) with cylindrical UVs: u wraps once around, v = 0 at 10 deg below the horizon .. 1 at 80 deg.</summary>
        internal static Mesh SkyDome
        {
            get
            {
                if (_dome != null) return _dome;
                const int sides = 32;
                const float radius = 150f;
                float[] elev = { -90f, -45f, -15f, -4f, 0f, 4f, 9f, 15f, 23f, 33f, 45f, 60f, 75f, 90f };
                int ringVerts = sides + 1;
                var verts = new Vector3[elev.Length * ringVerts];
                var normals = new Vector3[verts.Length];
                var uvs = new Vector2[verts.Length];
                var colors = new Color32[verts.Length];
                for (int r = 0; r < elev.Length; r++)
                {
                    float e = elev[r] * Mathf.Deg2Rad;
                    float y = Mathf.Sin(e), c = Mathf.Cos(e);
                    float v = Mathf.Clamp01((elev[r] + 10f) / 90f);
                    for (int i = 0; i <= sides; i++)
                    {
                        float a = (float)i / sides * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Sin(a) * c, y, Mathf.Cos(a) * c);
                        int k = r * ringVerts + i;
                        verts[k] = dir * radius;
                        normals[k] = -dir;
                        uvs[k] = new Vector2((float)i / sides, v);
                        colors[k] = new Color32(255, 255, 255, 255);
                    }
                }
                var tris = new int[(elev.Length - 1) * sides * 6];
                int ti = 0;
                for (int r = 0; r < elev.Length - 1; r++)
                    for (int i = 0; i < sides; i++)
                    {
                        int a = r * ringVerts + i, b = a + ringVerts, c = b + 1, d = a + 1;
                        // clockwise when seen from the center (camera inside the dome); the sky shader is Cull Off anyway
                        tris[ti++] = a; tris[ti++] = b; tris[ti++] = c;
                        tris[ti++] = a; tris[ti++] = c; tris[ti++] = d;
                    }
                _dome = new Mesh { name = "PsxSkyDome" };
                _dome.vertices = verts;
                _dome.normals = normals;
                _dome.uv = uvs;
                _dome.colors32 = colors;
                _dome.triangles = tris;
                _dome.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f); // never frustum culled
                return _dome;
            }
        }

        // ------------------------------------------------------------------ procedural fallbacks

        static Texture2D Make(int w, int h, string name, Func<float, float, Color> f)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name + "_procedural",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    // u, v in [-1, 1] (pixel centers), v up
                    float u = ((x + 0.5f) / w) * 2f - 1f;
                    float v = ((y + 0.5f) / h) * 2f - 1f;
                    Color c = f(u, v);
                    px[y * w + x] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), Mathf.Clamp01(c.a));
                }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        static uint HashU(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float Hash01(int x, int y, int seed) => (HashU(x, y, seed) & 0xFFFFFFu) / 16777216f;

        /// <summary>2D value noise in [0,1]; <paramref name="periodX"/> &gt; 0 makes it tile horizontally.</summary>
        static float ValueNoise(float x, float y, int seed, int periodX = 0)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x0 = ix, x1 = ix + 1;
            if (periodX > 0) { x0 = ((x0 % periodX) + periodX) % periodX; x1 = ((x1 % periodX) + periodX) % periodX; }
            float a = Hash01(x0, iy, seed), b = Hash01(x1, iy, seed);
            float c = Hash01(x0, iy + 1, seed), d = Hash01(x1, iy + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int seed, int periodX = 0)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < 4; o++)
            {
                int p = periodX > 0 ? periodX << o : 0;
                sum += ValueNoise(x, y, seed + o * 31, p) * amp;
                norm += amp;
                x *= 2f; y *= 2f; amp *= 0.5f;
            }
            return sum / norm;
        }

        static Texture2D Generate(string name)
        {
            int frame = 0;
            int us = name.LastIndexOf('_');
            if (us >= 0 && us < name.Length - 1 && char.IsDigit(name[us + 1])) int.TryParse(name.Substring(us + 1), out frame);
            string baseName = name.StartsWith("flame_") ? "flame" : name.StartsWith("fire_") ? "fire" : name.StartsWith("explosion_") ? "explosion" : name;

            switch (baseName)
            {
                case "glow":
                    return Make(32, 32, name, (u, v) =>
                    {
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                        return new Color(1f, 1f, 1f, a * a);
                    });
                case "flame":
                    return Make(16, 32, name, (u, v) => Flame(u, (v + 1f) * 0.5f, frame));
                case "fire":
                    return Make(32, 64, name, (u, v) => Fire(u, (v + 1f) * 0.5f, frame));
                case "explosion":
                    return Make(32, 32, name, (u, v) => ExplosionTex(u, v, frame));
                case "smoke":
                    return Make(32, 32, name, (u, v) =>
                    {
                        float r = Mathf.Sqrt(u * u + v * v);
                        float n = Fbm(u * 2.5f + 4f, v * 2.5f + 4f, 11);
                        float a = Mathf.Pow(Mathf.Clamp01((1f - r) * 1.4f), 1.5f) * (0.45f + 0.55f * n);
                        float g = 0.45f + 0.2f * n;
                        return new Color(g, g, g * 0.97f, a);
                    });
                case "spark":
                case "glint":
                    return Make(8, 8, name, (u, v) =>
                    {
                        float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v)), 2f) * 1.3f;
                        return new Color(1f, 0.92f, 0.7f, a);
                    });
                case "blood_drop":
                    return Make(16, 16, name, (u, v) =>
                    {
                        float ang = Mathf.Atan2(v, u);
                        float r = Mathf.Sqrt(u * u + v * v);
                        float edge = 0.6f + 0.12f * Mathf.Sin(ang * 3f + 1.3f) + 0.08f * Mathf.Sin(ang * 7f);
                        bool dot = (u - 0.72f) * (u - 0.72f) + (v + 0.55f) * (v + 0.55f) < 0.02f
                                || (u + 0.65f) * (u + 0.65f) + (v - 0.7f) * (v - 0.7f) < 0.015f;
                        float a = (r < edge || dot) ? 0.95f : 0f;
                        float dark = Mathf.Clamp01(1f - r / Mathf.Max(edge, 0.01f));
                        return new Color(0.42f - 0.12f * dark, 0.03f, 0.03f, a);
                    });
                case "blood_spray":
                    return Make(32, 32, name, (u, v) =>
                    {
                        float a = 0f;
                        for (int i = 0; i < 14; i++)
                        {
                            float cx = Hash01(i, 1, 77) * 1.5f - 0.75f, cy = Hash01(i, 2, 77) * 1.5f - 0.75f;
                            float rad = 0.06f + Hash01(i, 3, 77) * 0.16f;
                            float dx = u - cx, dy = (v - cy) * 1.3f;
                            if (dx * dx + dy * dy < rad * rad) a = 0.95f;
                        }
                        return new Color(0.45f, 0.03f, 0.03f, a);
                    });
                case "dust":
                    return Make(16, 16, name, (u, v) =>
                    {
                        float r = Mathf.Sqrt(u * u + v * v);
                        float n = ValueNoise(u * 3f + 7f, v * 3f + 7f, 5);
                        float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f) * (0.6f + 0.4f * n);
                        return new Color(0.6f, 0.56f, 0.5f, a);
                    });
                case "glass_shard":
                    return Make(8, 8, name, (u, v) =>
                    {
                        // sliver triangle (-0.8,-0.9) (0.9,-0.3) (-0.2,0.95)
                        bool inside = Edge(u, v, -0.8f, -0.9f, 0.9f, -0.3f) >= 0f && Edge(u, v, 0.9f, -0.3f, -0.2f, 0.95f) >= 0f
                                   && Edge(u, v, -0.2f, 0.95f, -0.8f, -0.9f) >= 0f;
                        return new Color(0.82f, 0.9f, 1f, inside ? 0.85f : 0f);
                    });
                case "blob_shadow":
                    return Make(32, 32, name, (u, v) =>
                    {
                        float r = Mathf.Sqrt(u * u + v * v);
                        float a = 1f - Mathf.Clamp01((r - 0.25f) / 0.75f);
                        return new Color(0f, 0f, 0f, a * a * (3f - 2f * a) * 0.9f);
                    });
                default:
                    return Make(16, 16, name, (u, v) =>
                    {
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v));
                        return new Color(1f, 1f, 1f, a);
                    });
            }
        }

        static float Edge(float px, float py, float ax, float ay, float bx, float by)
            => (bx - ax) * (py - ay) - (by - ay) * (px - ax);

        /// <summary>Lighter flame: teardrop, white-yellow core, orange body, red tip, blue base. v = 0 bottom .. 1 top.</summary>
        static Color Flame(float u, float v, int frame)
        {
            float tip = 0.82f + 0.18f * Hash01(frame, 0, 5);
            float vv = v / tip;
            float sway = Mathf.Sin(vv * 3.2f + frame * 1.6f) * 0.13f * vv;
            float width = vv < 0.3f ? 0.58f * Mathf.Sqrt(Mathf.Max(vv, 0f) / 0.3f) : 0.58f * Mathf.Pow(Mathf.Clamp01(1f - (vv - 0.3f) / 0.7f), 1.3f);
            float d = Mathf.Abs(u - sway) / Mathf.Max(width, 0.001f);
            float a = vv > 1f ? 0f : Mathf.Clamp01((1f - d) * 2.2f);
            float core = Mathf.Clamp01(1f - d * 1.7f) * Mathf.Clamp01(1f - vv * 1.2f);
            Color c = Color.Lerp(new Color(1f, 0.45f, 0.08f), new Color(1f, 0.96f, 0.78f), core);
            c = Color.Lerp(c, new Color(0.9f, 0.2f, 0.05f), Mathf.Clamp01((vv - 0.6f) / 0.4f) * 0.6f);
            if (vv < 0.16f)
            {
                float b = (1f - vv / 0.16f) * 0.65f;
                c = Color.Lerp(c, new Color(0.3f, 0.45f, 1f), b);
                a *= 1f - b * 0.5f;
            }
            c.a = a;
            return c;
        }

        /// <summary>Big fire: noisy tongues scrolling upwards. v = 0 bottom .. 1 top.</summary>
        static Color Fire(float u, float v, int frame)
        {
            float n = Fbm(u * 2.2f + 3f, v * 3.5f - frame * 0.85f, 21);
            float width = 0.95f * Mathf.Pow(Mathf.Clamp01(1f - v), 0.7f);
            float env = Mathf.Clamp01(1f - Mathf.Abs(u) / Mathf.Max(0.05f, width));
            float flame = env * (0.55f + 0.7f * n) - v * 0.55f;
            float a = Mathf.Clamp01(flame * 3f);
            float heat = Mathf.Clamp01(flame * 2.2f);
            Color c = Color.Lerp(new Color(0.8f, 0.15f, 0.02f), new Color(1f, 0.55f, 0.1f), heat);
            c = Color.Lerp(c, new Color(1f, 0.95f, 0.7f), Mathf.Clamp01(heat * 1.5f - 0.8f));
            c.a = a;
            return c;
        }

        /// <summary>Fireball frames: grows, cools and thins out from frame 0 to 3.</summary>
        static Color ExplosionTex(float u, float v, int frame)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            float n = Fbm(u * 2.5f + 9f, v * 2.5f + frame * 1.7f, 41);
            float radius = 0.55f + 0.13f * frame;
            float edge = radius * (0.75f + 0.4f * n);
            float a = Mathf.Clamp01((edge - r) * 6f) * (1f - frame * 0.15f);
            float heat = Mathf.Clamp01((edge - r) / Mathf.Max(edge, 0.01f) * 1.6f) * (1f - frame * 0.22f);
            Color c = Color.Lerp(new Color(0.35f, 0.08f, 0.02f), new Color(1f, 0.6f, 0.15f), heat);
            c = Color.Lerp(c, new Color(1f, 0.95f, 0.8f), Mathf.Clamp01(heat * 2f - 1.2f));
            c.a = a;
            return c;
        }

        /// <summary>Procedural night sky panorama (256x64, tiles horizontally): overcast clouds + faint moon glow.</summary>
        internal static Texture2D ProceduralSky()
        {
            if (_sky != null) return _sky;
            const int w = 256, h = 64;
            _sky = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "sky_procedural", filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float v = (y + 0.5f) / h;
                    float n = Fbm(x / 16f, y / 10f, 3, w / 16);
                    Color baseCol = Color.Lerp(new Color(0.11f, 0.125f, 0.15f), new Color(0.03f, 0.035f, 0.05f), v);
                    float cloud = Mathf.Clamp01((n - 0.42f) * 2.2f);
                    Color c = Color.Lerp(baseCol, baseCol * 1.7f + new Color(0.01f, 0.01f, 0.012f), cloud);
                    float du = Mathf.Min(Mathf.Abs(x - w * 0.3f), w - Mathf.Abs(x - w * 0.3f)) / (w * 0.06f);
                    float dv = (v - 0.62f) / 0.18f;
                    float moon = Mathf.Exp(-(du * du + dv * dv)) * (0.6f + 0.4f * cloud);
                    c += new Color(0.07f, 0.075f, 0.08f) * moon;
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            _sky.SetPixels32(px);
            _sky.Apply(false, false);
            return _sky;
        }

        /// <summary>Common renderer setup for FX objects (no shadows / probes / motion vectors).</summary>
        internal static MeshRenderer AddRenderer(GameObject go, Mesh mesh, Material material)
        {
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            return mr;
        }
    }
}
