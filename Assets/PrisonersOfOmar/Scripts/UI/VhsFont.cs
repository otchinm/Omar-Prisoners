using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>Monospace bitmap font: atlas texture + JSON metrics (see Docs/ASSETS.md, font_vhs.json).</summary>
    public sealed class VhsFont
    {
        [Serializable]
        sealed class Meta
        {
            public int cellW = 8, cellH = 13, cols = 16, first = 32, advance = 8, lineHeight = 14, atlasW, atlasH, padding;
            public string glyphs;
        }

        public Texture2D Texture { get; private set; }
        public int CellW { get; private set; }
        public int CellH { get; private set; }
        public int Advance { get; private set; }
        public int LineHeight { get; private set; }
        /// <summary>Transparent border around each glyph inside its cell (ink starts this many pixels in).</summary>
        public int Padding { get; private set; }

        readonly Dictionary<char, Rect> _uv = new Dictionary<char, Rect>();
        Rect _unknown;

        static VhsFont _small, _big, _tiny;

        /// <summary>Default UI font (VT323 based).</summary>
        public static VhsFont Small => _small ?? (_small = Load("Textures/UI/font_vhs") ?? Load("UIFallback/font_fallback") ?? Builtin());
        /// <summary>Headings.</summary>
        public static VhsFont Big => _big ?? (_big = Load("Textures/UI/font_vhs_big") ?? Small);
        /// <summary>Compact font for dense screens (settings, lobby, long texts).</summary>
        public static VhsFont Tiny => _tiny ?? (_tiny = Load("Textures/UI/font_vhs_small") ?? Load("UIFallback/font_fallback") ?? Small);

        public static VhsFont Load(string path)
        {
            try
            {
                var tex = Resources.Load<Texture2D>(path);
                var json = Resources.Load<TextAsset>(path);
                if (tex == null || json == null) return null;
                var meta = JsonUtility.FromJson<Meta>(json.text);
                if (meta == null || meta.cellW <= 0 || meta.cellH <= 0 || meta.cols <= 0) return null;
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                var f = new VhsFont { Texture = tex, CellW = meta.cellW, CellH = meta.cellH };
                f.Advance = meta.advance > 0 ? meta.advance : meta.cellW;
                f.LineHeight = meta.lineHeight > 0 ? meta.lineHeight : meta.cellH + 1;
                f.Padding = Mathf.Max(0, meta.padding);
                float aw = meta.atlasW > 0 ? meta.atlasW : tex.width;
                float ah = meta.atlasH > 0 ? meta.atlasH : tex.height;
                string glyphs = meta.glyphs;
                if (string.IsNullOrEmpty(glyphs))
                {
                    var sb = new System.Text.StringBuilder();
                    for (int c = meta.first; c < 127; c++) sb.Append((char)c);
                    glyphs = sb.ToString();
                }
                for (int i = 0; i < glyphs.Length; i++)
                {
                    int col = i % meta.cols, row = i / meta.cols;
                    float x = col * meta.cellW, yTop = row * meta.cellH;
                    var r = Rect.MinMaxRect(x / aw, 1f - (yTop + meta.cellH) / ah, (x + meta.cellW) / aw, 1f - yTop / ah);
                    f._uv[glyphs[i]] = r;
                }
                f._unknown = f._uv.TryGetValue('?', out var q) ? q : default;
                return f;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VhsFont] failed to load " + path + ": " + e.Message);
                return null;
            }
        }

        static VhsFont Builtin()
        {
            // Last resort: solid blocks (should never happen; fallback atlas ships with the project).
            return new VhsFont { Texture = Texture2D.whiteTexture, CellW = 6, CellH = 9, Advance = 7, LineHeight = 11 };
        }

        public bool TryGetUV(char c, out Rect uv)
        {
            if (_uv.TryGetValue(c, out uv)) return true;
            char up = char.ToUpperInvariant(c);
            if (_uv.TryGetValue(up, out uv)) return true;
            uv = _unknown;
            return _unknown.width > 0;
        }
    }
}
