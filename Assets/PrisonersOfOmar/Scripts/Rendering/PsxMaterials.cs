using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>Shader / blend variants available for world geometry.</summary>
    public enum PsxSurface
    {
        Lit = 0,        // opaque, vertex lit (PS1 Gouraud), fog, vertex snapping, affine UVs, anomalies
        LitCutout,      // alpha tested (foliage, chain-link, cage bars, decals with holes), double sided
        LitDoubleSided, // opaque, no backface culling (thin sheets: cloth, paper, single-plane props)
        Unlit,          // opaque, full bright (TV screens, sky cards) but still fogged + snapped
        UnlitCutout,    // full bright alpha tested
        Emissive,       // full bright glowing (lit windows, bulbs, lamp lenses); not darkened by fog as much
        Transparent,    // alpha blended, vertex lit (dirty glass, puddles, blood pools)
        Additive,       // additive glow (flames, light halos, sparks), no depth write, double sided
        Decal,          // alpha blended / tested overlay with depth offset to avoid z-fighting (blood, graffiti, stains)
    }

    /// <summary>
    /// Central texture + material cache. All world materials MUST come from here so the PSX shaders,
    /// point filtering and global effects (fog, anomalies) apply everywhere.
    /// Texture paths are relative to a Resources folder, without extension, e.g. "Textures/Env/wall_wallpaper_blue".
    /// </summary>
    public static class PsxMaterials
    {
        public const string ShaderLit = "PrisonersOfOmar/PSX Lit";
        public const string ShaderLitCutout = "PrisonersOfOmar/PSX Lit Cutout";
        public const string ShaderLitDoubleSided = "PrisonersOfOmar/PSX Lit Double Sided";
        public const string ShaderUnlit = "PrisonersOfOmar/PSX Unlit";
        public const string ShaderUnlitCutout = "PrisonersOfOmar/PSX Unlit Cutout";
        public const string ShaderEmissive = "PrisonersOfOmar/PSX Emissive";
        public const string ShaderTransparent = "PrisonersOfOmar/PSX Transparent";
        public const string ShaderAdditive = "PrisonersOfOmar/PSX Additive";
        public const string ShaderDecal = "PrisonersOfOmar/PSX Decal";

        static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>();
        static Texture2D _fallback;

        /// <summary>Loads a texture from Resources (cached), forcing point filtering. Never returns null (gray noise fallback).</summary>
        public static Texture2D Texture(string path)
        {
            if (string.IsNullOrEmpty(path)) return WhiteTexture;
            if (_textures.TryGetValue(path, out var t) && t != null) return t;
            t = Resources.Load<Texture2D>(path);
            if (t == null)
            {
                Debug.LogWarning("[PsxMaterials] missing texture: " + path);
                t = Fallback;
            }
            else
            {
                t.filterMode = FilterMode.Point;
                t.anisoLevel = 0;
            }
            _textures[path] = t;
            return t;
        }

        public static Texture2D WhiteTexture => Texture2D.whiteTexture;

        static Texture2D Fallback
        {
            get
            {
                if (_fallback != null) return _fallback;
                _fallback = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "fallback" };
                var rng = new DeterministicRandom(1234);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float v = 0.35f + rng.NextFloat() * 0.2f;
                        _fallback.SetPixel(x, y, new Color(v, v, v, 1));
                    }
                _fallback.Apply();
                return _fallback;
            }
        }

        static readonly Dictionary<PsxSurface, Shader> _shaders = new Dictionary<PsxSurface, Shader>();

        public static Shader ShaderFor(PsxSurface surface)
        {
            if (_shaders.TryGetValue(surface, out var cached) && cached != null) return cached;
            string name;
            switch (surface)
            {
                case PsxSurface.LitCutout: name = ShaderLitCutout; break;
                case PsxSurface.LitDoubleSided: name = ShaderLitDoubleSided; break;
                case PsxSurface.Unlit: name = ShaderUnlit; break;
                case PsxSurface.UnlitCutout: name = ShaderUnlitCutout; break;
                case PsxSurface.Emissive: name = ShaderEmissive; break;
                case PsxSurface.Transparent: name = ShaderTransparent; break;
                case PsxSurface.Additive: name = ShaderAdditive; break;
                case PsxSurface.Decal: name = ShaderDecal; break;
                default: name = ShaderLit; break;
            }
            var s = Shader.Find(name);
            if (s == null)
            {
                Debug.LogWarning("[PsxMaterials] missing shader: " + name);
                s = Shader.Find("Unlit/Texture");
            }
            else _shaders[surface] = s;
            return s;
        }

        /// <summary>Shared (cached) material for a texture + surface + tint. Do not modify the returned material.</summary>
        public static Material Get(string texturePath, PsxSurface surface = PsxSurface.Lit, Color? tint = null)
        {
            Color c = tint ?? Color.white;
            string key = texturePath + "|" + (int)surface + "|" + ColorUtility.ToHtmlStringRGBA(c);
            if (_materials.TryGetValue(key, out var m) && m != null) return m;
            m = Create(texturePath, surface, c);
            _materials[key] = m;
            return m;
        }

        /// <summary>Shared untextured material of a flat color.</summary>
        public static Material GetColor(Color color, PsxSurface surface = PsxSurface.Lit) => Get(null, surface, color);

        /// <summary>New (uncached) material instance, safe to modify (e.g. animated emission).</summary>
        public static Material Create(string texturePath, PsxSurface surface = PsxSurface.Lit, Color? tint = null)
        {
            var m = new Material(ShaderFor(surface));
            m.name = (texturePath ?? "color") + "_" + surface;
            m.mainTexture = string.IsNullOrEmpty(texturePath) ? WhiteTexture : Texture(texturePath);
            m.color = tint ?? Color.white;
            if (m.HasProperty(PsxShaderIds.Cutoff)) m.SetFloat(PsxShaderIds.Cutoff, 0.5f);
            return m;
        }
    }
}
