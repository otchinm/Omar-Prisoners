using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    public enum Align { Left, Center, Right }

    /// <summary>
    /// Immediate-mode UI in low-res pixel space (origin top-left), recorded during Update and rendered
    /// into the low resolution frame (PsxCameraRig.DrawOverlay) BEFORE the VHS pass, so text bleeds and
    /// wobbles with the tape like in Puppet Combo games.
    /// </summary>
    public sealed class VhsUI
    {
        struct Cmd
        {
            public Texture Tex;
            public Rect Dst;
            public Rect Uv;
            public Color Color;
        }

        readonly List<Cmd> _cmds = new List<Cmd>(2048);
        readonly List<Cmd> _render = new List<Cmd>(2048);
        Material _mat;

        public int Width { get; private set; } = 426;
        public int Height { get; private set; } = 240;
        public Vector2 Mouse { get; private set; }
        public bool MouseMoved { get; private set; }
        public bool Click { get; private set; }
        public bool CursorVisible;
        Vector2 _lastMouse;

        public static readonly Color White = new Color(0.92f, 0.92f, 0.88f);
        public static readonly Color Dim = new Color(0.55f, 0.55f, 0.52f);
        public static readonly Color Red = new Color(0.86f, 0.08f, 0.06f);
        public static readonly Color DarkRed = new Color(0.45f, 0.03f, 0.03f);
        public static readonly Color Yellow = new Color(0.95f, 0.85f, 0.35f);
        public static readonly Color Green = new Color(0.4f, 0.9f, 0.45f);
        public static readonly Color Blue = new Color(0.45f, 0.65f, 1f);

        /// <summary>Font used by widgets / text when no font is given (screens may switch to VhsFont.Tiny). Reset per screen.</summary>
        public VhsFont FontOverride;
        public VhsFont Font => FontOverride ?? VhsFont.Small;
        public VhsFont TinyFont => VhsFont.Tiny;
        public VhsFont BigFont => VhsFont.Big;

        Material Mat
        {
            get
            {
                if (_mat != null) return _mat;
                var sh = Shader.Find("PrisonersOfOmar/UI Sprite");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                return _mat;
            }
        }

        // ------------------------------------------------------------------ frame

        public void BeginFrame(int width, int height, Vector2 mouseLowRes, bool click)
        {
            _cmds.Clear();
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            Mouse = mouseLowRes;
            MouseMoved = (Mouse - _lastMouse).sqrMagnitude > 0.25f;
            _lastMouse = Mouse;
            Click = click;
        }

        /// <summary>Called at the end of Update: freezes this frame's commands for rendering.</summary>
        public void EndFrame()
        {
            if (CursorVisible) DrawCursor();
            _render.Clear();
            _render.AddRange(_cmds);
        }

        /// <summary>Renders the frozen command list. Called from the camera rig overlay (pixel matrix set up).</summary>
        public void Render()
        {
            if (_render.Count == 0) return;
            var mat = Mat;
            int i = 0;
            while (i < _render.Count)
            {
                Texture tex = _render[i].Tex;
                mat.mainTexture = tex;
                mat.SetPass(0);
                GL.Begin(GL.QUADS);
                while (i < _render.Count && _render[i].Tex == tex)
                {
                    var c = _render[i];
                    GL.Color(c.Color);
                    GL.TexCoord2(c.Uv.xMin, c.Uv.yMax); GL.Vertex3(c.Dst.xMin, c.Dst.yMin, 0);
                    GL.TexCoord2(c.Uv.xMax, c.Uv.yMax); GL.Vertex3(c.Dst.xMax, c.Dst.yMin, 0);
                    GL.TexCoord2(c.Uv.xMax, c.Uv.yMin); GL.Vertex3(c.Dst.xMax, c.Dst.yMax, 0);
                    GL.TexCoord2(c.Uv.xMin, c.Uv.yMin); GL.Vertex3(c.Dst.xMin, c.Dst.yMax, 0);
                    i++;
                }
                GL.End();
            }
        }

        // ------------------------------------------------------------------ primitives

        public void Rect(float x, float y, float w, float h, Color color)
        {
            if (w <= 0 || h <= 0 || color.a <= 0f) return;
            _cmds.Add(new Cmd { Tex = Texture2D.whiteTexture, Dst = new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(w), Mathf.Round(h)), Uv = new Rect(0, 0, 1, 1), Color = color });
        }

        public void Rect(Rect r, Color color) => Rect(r.x, r.y, r.width, r.height, color);

        public void Frame(Rect r, Color color, int thickness = 1)
        {
            Rect(r.x, r.y, r.width, thickness, color);
            Rect(r.x, r.yMax - thickness, r.width, thickness, color);
            Rect(r.x, r.y, thickness, r.height, color);
            Rect(r.xMax - thickness, r.y, thickness, r.height, color);
        }

        public void Image(Texture tex, Rect dst, Color color) => Image(tex, dst, color, new Rect(0, 0, 1, 1));

        public void Image(Texture tex, Rect dst, Color color, Rect uv)
        {
            if (tex == null || color.a <= 0f) return;
            _cmds.Add(new Cmd { Tex = tex, Dst = dst, Uv = uv, Color = color });
        }

        /// <summary>Full-screen image stretched to cover (keeps aspect, crops).</summary>
        public void ImageCover(Texture tex, Color color)
        {
            if (tex == null) return;
            float ta = (float)tex.width / Mathf.Max(1, tex.height), sa = (float)Width / Height;
            Rect uv = new Rect(0, 0, 1, 1);
            if (ta > sa) { float w = sa / ta; uv = new Rect((1 - w) * 0.5f, 0, w, 1); }
            else { float h = ta / sa; uv = new Rect(0, (1 - h) * 0.5f, 1, h); }
            Image(tex, new Rect(0, 0, Width, Height), color, uv);
        }

        /// <summary>Image fitted inside a box (keeps aspect, letterboxed), centered.</summary>
        public Rect ImageFit(Texture tex, Rect box, Color color)
        {
            if (tex == null) return box;
            float ta = (float)tex.width / Mathf.Max(1, tex.height), ba = box.width / Mathf.Max(1, box.height);
            Rect r = box;
            if (ta > ba) { r.height = box.width / ta; r.y = box.y + (box.height - r.height) * 0.5f; }
            else { r.width = box.height * ta; r.x = box.x + (box.width - r.width) * 0.5f; }
            r.x = Mathf.Round(r.x); r.y = Mathf.Round(r.y); r.width = Mathf.Round(r.width); r.height = Mathf.Round(r.height);
            Image(tex, r, color);
            return r;
        }

        // ------------------------------------------------------------------ text

        /// <summary>Largest scale (≤ maxScale) at which <paramref name="s"/> fits in <paramref name="maxWidth"/> pixels (at least 1).</summary>
        public int FitScale(string s, int maxScale, float maxWidth, VhsFont font = null)
        {
            for (int sc = maxScale; sc > 1; sc--) if (TextWidth(s, sc, font) <= maxWidth) return sc;
            return 1;
        }

        public int TextWidth(string s, int scale = 1, VhsFont font = null)
        {
            font = font ?? Font;
            if (string.IsNullOrEmpty(s)) return 0;
            int max = 0, cur = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\n') { max = Mathf.Max(max, cur); cur = 0; continue; }
                cur++;
            }
            max = Mathf.Max(max, cur);
            return max * font.Advance * scale;
        }

        public int LineHeight(int scale = 1, VhsFont font = null) => (font ?? Font).LineHeight * scale;

        /// <summary>Draws text (\n supported). Returns the height used.</summary>
        public int Text(string s, float x, float y, Color color, int scale = 1, Align align = Align.Left, VhsFont font = null, bool shadow = true)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            font = font ?? Font;
            int lines = 0;
            int start = 0;
            float ly = Mathf.Round(y);
            for (int i = 0; i <= s.Length; i++)
            {
                if (i == s.Length || s[i] == '\n')
                {
                    string line = s.Substring(start, i - start);
                    float lx = x;
                    int w = line.Length * font.Advance * scale;
                    if (align == Align.Center) lx = x - w * 0.5f;
                    else if (align == Align.Right) lx = x - w;
                    lx = Mathf.Round(lx);
                    if (shadow) DrawLine(line, lx + scale, ly + scale, new Color(0, 0, 0, color.a * 0.8f), scale, font);
                    DrawLine(line, lx, ly, color, scale, font);
                    ly += font.LineHeight * scale;
                    lines++;
                    start = i + 1;
                }
            }
            return lines * font.LineHeight * scale;
        }

        void DrawLine(string line, float x, float y, Color color, int scale, VhsFont font)
        {
            float cw = font.CellW * scale, ch = font.CellH * scale;
            x -= font.Padding * scale;
            y -= font.Padding * scale;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c != ' ' && font.TryGetUV(c, out var uv))
                    _cmds.Add(new Cmd { Tex = font.Texture, Dst = new Rect(x, y, cw, ch), Uv = uv, Color = color });
                x += font.Advance * scale;
            }
        }

        /// <summary>Word-wraps text to a pixel width.</summary>
        public List<string> Wrap(string s, int maxWidth, int scale = 1, VhsFont font = null)
        {
            font = font ?? Font;
            var result = new List<string>();
            if (string.IsNullOrEmpty(s)) return result;
            int maxChars = Mathf.Max(1, maxWidth / (font.Advance * scale));
            foreach (var para in s.Split('\n'))
            {
                var words = para.Split(' ');
                var line = new System.Text.StringBuilder();
                foreach (var w in words)
                {
                    if (line.Length > 0 && line.Length + 1 + w.Length > maxChars)
                    {
                        result.Add(line.ToString());
                        line.Length = 0;
                    }
                    string word = w;
                    while (word.Length > maxChars) { result.Add(word.Substring(0, maxChars)); word = word.Substring(maxChars); }
                    if (line.Length > 0) line.Append(' ');
                    line.Append(word);
                }
                result.Add(line.ToString());
            }
            return result;
        }

        public int TextWrapped(string s, float x, float y, int maxWidth, Color color, int scale = 1, Align align = Align.Left, VhsFont font = null)
        {
            font = font ?? Font;
            var lines = Wrap(s, maxWidth, scale, font);
            float ly = y;
            foreach (var l in lines) { Text(l, x, ly, color, scale, align, font); ly += LineHeight(scale, font); }
            return Mathf.RoundToInt(ly - y);
        }

        /// <summary>Wrapped text in the default font, or the tiny font when it would not fit in <paramref name="maxHeight"/>.</summary>
        public int TextWrappedFit(string s, float x, float y, int maxWidth, float maxHeight, Color color, Align align = Align.Left)
        {
            var font = Font;
            if (Wrap(s, maxWidth, 1, font).Count * LineHeight(1, font) > maxHeight) font = TinyFont;
            return TextWrapped(s, x, y, maxWidth, color, 1, align, font);
        }

        // ------------------------------------------------------------------ widgets

        public bool Hover(Rect r) => CursorVisible && r.Contains(Mouse);

        /// <summary>
        /// Vertical text menu. Keyboard (W/S/arrows, Enter/Space/E) + mouse (hover selects, click activates).
        /// Returns the activated index or -1. <paramref name="lastGap"/> pushes the last item (BACK / LEAVE) further down,
        /// with a faint rule in the gap, so it is not mistaken for one more option of the list.
        /// </summary>
        public int Menu(string[] items, ref int selected, float cx, float y, int scale = 1, int spacing = 4, bool input = true, bool[] disabled = null, int lastGap = 0)
        {
            int lh = LineHeight(scale) + spacing;
            int activated = -1;
            if (items.Length == 0) return -1;
            selected = Mathf.Clamp(selected, 0, items.Length - 1);
            if (input)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) { selected = Next(selected, 1, items.Length, disabled); AudioManager.Play2D(Snd.UiMove, 0.6f, 1f, AudioCategory.Ui); }
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) { selected = Next(selected, -1, items.Length, disabled); AudioManager.Play2D(Snd.UiMove, 0.6f, 1f, AudioCategory.Ui); }
            }
            for (int i = 0; i < items.Length; i++)
            {
                bool off = disabled != null && i < disabled.Length && disabled[i];
                int w = TextWidth(items[i], scale);
                float iy = y + i * lh + (lastGap > 0 && items.Length > 1 && i == items.Length - 1 ? lastGap : 0);
                if (lastGap > 0 && items.Length > 1 && i == items.Length - 1)
                    Rect(cx - 30, iy - spacing - lastGap * 0.5f - 1, 60, 1, new Color(0.55f, 0.55f, 0.52f, 0.35f));
                var r = new Rect(cx - w * 0.5f - 6, iy - 2, w + 12, lh);
                if (input && !off && Hover(r) && MouseMoved && selected != i) { selected = i; AudioManager.Play2D(Snd.UiMove, 0.5f, 1f, AudioCategory.Ui); }
                bool sel = i == selected;
                Color c = off ? new Color(0.35f, 0.35f, 0.35f) : sel ? White : Dim;
                string label = sel && !off ? "▶ " + items[i] + " ◀" : items[i];
                Text(label, cx, iy, c, scale, Align.Center);
                if (input && !off && Click && Hover(r)) activated = i;
            }
            if (input && activated < 0 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
                if (disabled == null || selected >= disabled.Length || !disabled[selected]) activated = selected;
            if (activated >= 0) AudioManager.Play2D(Snd.UiSelect, 0.8f, 1f, AudioCategory.Ui);
            return activated;
        }

        static int Next(int cur, int dir, int count, bool[] disabled)
        {
            for (int k = 0; k < count; k++)
            {
                cur = (cur + dir + count) % count;
                if (disabled == null || cur >= disabled.Length || !disabled[cur]) return cur;
            }
            return cur;
        }

        /// <summary>Left/right adjustable row. Returns -1/+1 when changed by keys or clicks, else 0.</summary>
        public int Stepper(string label, string value, float x, float y, float width, bool selected, bool input = true)
        {
            Color c = selected ? White : Dim;
            Text((selected ? "▶ " : "  ") + label, x, y, c);
            string v = "◀ " + value + " ▶";
            Text(v, x + width, y, c, 1, Align.Right);
            int delta = 0;
            if (input && selected)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) delta = -1;
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) delta = 1;
            }
            if (input && Click)
            {
                int vw = TextWidth(v);
                var left = new Rect(x + width - vw - 2, y - 2, 14, LineHeight() + 2);
                var right = new Rect(x + width - 12, y - 2, 14, LineHeight() + 2);
                if (Hover(left)) delta = -1;
                if (Hover(right)) delta = 1;
            }
            if (delta != 0) AudioManager.Play2D(Snd.UiMove, 0.6f, 1f, AudioCategory.Ui);
            return delta;
        }

        /// <summary>Single line text input; edits while <paramref name="active"/>. Returns true when Enter is pressed.</summary>
        public bool TextField(ref string value, float x, float y, int width, bool active, int maxLen = 24, string allowed = null)
        {
            Rect r = new Rect(x, y - 2, width, LineHeight() + 3);
            Rect(r, new Color(0, 0, 0, 0.55f));
            Frame(r, active ? White : Dim);
            bool enter = false;
            if (active)
            {
                foreach (char ch in Input.inputString)
                {
                    if (ch == '\b') { if (value.Length > 0) { value = value.Substring(0, value.Length - 1); AudioManager.Play2D(Snd.UiType, 0.5f, 0.9f, AudioCategory.Ui); } }
                    else if (ch == '\n' || ch == '\r') enter = true;
                    else if (value.Length < maxLen && ch >= 32 && ch < 127)
                    {
                        char up = char.ToUpperInvariant(ch);
                        if (allowed == null || allowed.IndexOf(up) >= 0)
                        {
                            value += up;
                            AudioManager.Play2D(Snd.UiType, 0.5f, Random.Range(0.95f, 1.08f), AudioCategory.Ui);
                        }
                    }
                }
            }
            string shown = value;
            int maxChars = Mathf.Max(1, (width - 6) / Font.Advance);
            if (shown.Length > maxChars) shown = shown.Substring(shown.Length - maxChars);
            bool caret = active && (Time.unscaledTime * 2f % 2f) < 1f;
            Text(shown + (caret ? "_" : ""), x + 3, y, active ? White : Dim);
            return enter;
        }

        /// <summary>0..1 bar.</summary>
        public void Bar(Rect r, float value, Color fill, Color back)
        {
            Rect(r, back);
            Rect(r.x + 1, r.y + 1, Mathf.Max(0, (r.width - 2) * Mathf.Clamp01(value)), r.height - 2, fill);
        }

        void DrawCursor()
        {
            var tex = UITex.Get("Textures/UI/cursor");
            float x = Mathf.Round(Mouse.x), y = Mathf.Round(Mouse.y);
            if (tex != null)
            {
                Image(tex, new Rect(x, y, tex.width, tex.height), Color.white);
            }
            else
            {
                // pixel arrow
                for (int i = 0; i < 9; i++)
                {
                    Rect(x, y + i, Mathf.Max(1, (i + 1) * 0.6f), 1, Color.black);
                    Rect(x, y + i, Mathf.Max(1, (i + 1) * 0.6f) - 1, 1, White);
                }
            }
        }
    }
}
