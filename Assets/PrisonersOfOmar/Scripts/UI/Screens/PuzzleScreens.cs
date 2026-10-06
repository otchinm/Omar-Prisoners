using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>(iteration 3) Entering a code into a code lock: a keypad (digits), clock hands (time) or a row of coloured
    /// buttons (sequence). The host answers with right / wrong (<see cref="Result"/>).</summary>
    public sealed class CodeLockScreen : UIScreen
    {
        readonly MatchWorld _w;
        public readonly CodeLockEntity Lock;
        string _code = "";
        int _hour = 12, _minute;
        float _resultTimer = -1f, _t;
        bool _ok, _waiting;
        static readonly string[] Keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "OK" };
        static readonly Color[] SymbolColors =
        {
            new Color(0.8f, 0.12f, 0.1f), new Color(0.2f, 0.65f, 0.25f), new Color(0.2f, 0.35f, 0.85f), new Color(0.9f, 0.8f, 0.2f),
            new Color(0.88f, 0.88f, 0.84f), new Color(0.12f, 0.12f, 0.12f), new Color(0.92f, 0.5f, 0.12f), new Color(0.55f, 0.25f, 0.7f),
            new Color(0.5f, 0.5f, 0.5f),
        };

        public CodeLockScreen(MatchWorld w, CodeLockEntity lk) { _w = w; Lock = lk; }

        int Length => Mathf.Clamp(Lock.Info.Length, 1, 8);
        int Symbols => Mathf.Clamp(Lock.Info.Symbols, 2, SymbolColors.Length);

        public void Result(bool ok)
        {
            _ok = ok;
            _waiting = false;
            _resultTimer = ok ? 1.2f : 1f;
            AudioManager.Play2D(ok ? Snd.KeypadOk : Snd.KeypadWrong, 0.9f);
            if (!ok) _code = "";
        }

        void Submit(string code)
        {
            if (_waiting || _resultTimer > 0f) return;
            _waiting = true;
            _w.SendCode(Lock, code);
        }

        void PressKey(string k)
        {
            if (_resultTimer > 0f || _waiting) return;
            AudioManager.Play2D(Snd.KeypadBeep, 0.7f, 0.9f + _code.Length * 0.05f);
            if (k == "CLR") { _code = ""; return; }
            if (k == "OK") { if (_code.Length == Length) Submit(_code); return; }
            if (_code.Length < Length) _code += k;
            if (_code.Length == Length) Submit(_code);
        }

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            UIStyle.Dim(ui, 0.55f);
            switch (Lock.Info.Kind)
            {
                case CodeKind.Time: DrawClock(ui, input); break;
                case CodeKind.Sequence: DrawSequence(ui, input); break;
                default: DrawKeypad(ui, input); break;
            }

            if (_resultTimer > 0f)
            {
                _resultTimer -= Time.unscaledDeltaTime;
                if (_resultTimer <= 0f && _ok) { UIManager.Instance.Remove(this); return; }
            }
            if (!input) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) && _code.Length == 0 && _t > 0.25f)
                UIManager.Instance.Remove(this);
        }

        Color DisplayColor => _resultTimer > 0f ? (_ok ? VhsUI.Green : VhsUI.Red) : VhsUI.Green;
        string ResultText => _ok ? "OPEN" : "WRONG";

        // ------------------------------------------------------------------ digits

        void DrawKeypad(VhsUI ui, bool input)
        {
            float pw = Mathf.Max(120, 26 + Length * 14);
            var panel = UIStyle.Panel(ui, new Rect(ui.Width * 0.5f - pw * 0.5f, ui.Height * 0.5f - 90, pw, 180), 0.92f);
            ui.Text(Lock.Info.Label ?? "KEYPAD", panel.center.x, panel.y + 6, VhsUI.Yellow, 1, Align.Center);
            string disp = "";
            for (int i = 0; i < Length; i++) disp += (i < _code.Length ? _code[i].ToString() : "_") + (i < Length - 1 ? " " : "");
            var dr = new Rect(panel.x + 12, panel.y + 22, panel.width - 24, 18);
            ui.Rect(dr, new Color(0.05f, 0.1f, 0.05f, 1f));
            ui.Text(_resultTimer > 0f ? ResultText : disp, dr.center.x, dr.y + 3, DisplayColor, 1, Align.Center);
            float gx = panel.center.x - 48;
            for (int i = 0; i < Keys.Length; i++)
            {
                int cx = i % 3, cy = i / 3;
                var r = new Rect(gx + cx * 33, panel.y + 48 + cy * 30, 30, 26);
                bool hover = ui.Hover(r);
                ui.Rect(r, hover ? new Color(0.35f, 0.35f, 0.33f) : new Color(0.2f, 0.2f, 0.19f));
                ui.Frame(r, new Color(0, 0, 0, 0.8f));
                ui.Text(Keys[i], r.center.x, r.y + 7, VhsUI.White, 1, Align.Center);
                if (input && ui.Click && hover) PressKey(Keys[i]);
            }
            UIStyle.Footer(ui, "TYPE THE CODE   ESC CLOSE");
            if (!input) return;
            foreach (char ch in Input.inputString)
            {
                if (ch >= '0' && ch <= '9') PressKey(ch.ToString());
                else if (ch == '\b') { if (_code.Length > 0 && _resultTimer <= 0f && !_waiting) _code = _code.Substring(0, _code.Length - 1); }
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) PressKey("OK");
        }

        // ------------------------------------------------------------------ time (clock hands)

        void DrawClock(VhsUI ui, bool input)
        {
            var panel = UIStyle.Panel(ui, new Rect(ui.Width * 0.5f - 80, ui.Height * 0.5f - 100, 160, 200), 0.92f);
            ui.Text(Lock.Info.Label ?? "THE CLOCK", panel.center.x, panel.y + 6, VhsUI.Yellow, 1, Align.Center);
            Vector2 c = new Vector2(panel.center.x, panel.y + 80);
            const float R = 50f;
            // the dial: a ring of dots, heavier marks at the hours
            ui.Rect(c.x - R - 4, c.y - R - 4, 2 * R + 8, 2 * R + 8, new Color(0.78f, 0.72f, 0.58f, 0.18f));
            for (int i = 0; i < 60; i++)
            {
                float a = i / 60f * Mathf.PI * 2f;
                Vector2 p = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * R;
                bool hourMark = i % 5 == 0;
                ui.Rect(p.x - (hourMark ? 1 : 0), p.y - (hourMark ? 1 : 0), hourMark ? 3 : 1, hourMark ? 3 : 1, hourMark ? VhsUI.White : VhsUI.Dim);
            }
            for (int h = 1; h <= 12; h++)
            {
                float a = h / 12f * Mathf.PI * 2f;
                Vector2 p = c + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * (R - 11);
                ui.Text(h.ToString(), p.x, p.y - 4, VhsUI.Dim, 1, Align.Center, ui.TinyFont);
            }
            // hands (the hour hand creeps with the minutes, like a real clock)
            float hourA = ((_hour % 12) + _minute / 60f) / 12f * Mathf.PI * 2f, minA = _minute / 60f * Mathf.PI * 2f;
            Hand(ui, c, hourA, R * 0.5f, VhsUI.White, 2);
            Hand(ui, c, minA, R * 0.82f, VhsUI.Yellow, 1);
            ui.Rect(c.x - 1, c.y - 1, 3, 3, VhsUI.Red);

            float by = panel.y + 140;
            string readout = _resultTimer > 0f ? ResultText : _hour + ":" + _minute.ToString("00");
            ui.Text(readout, panel.center.x, by, DisplayColor, 1, Align.Center);
            var hm = new Rect(panel.x + 10, by + 16, 34, 16); var hp = new Rect(panel.x + 46, by + 16, 34, 16);
            var mm = new Rect(panel.x + 82, by + 16, 34, 16); var mp = new Rect(panel.x + 118, by + 16, 32, 16);
            if (Button(ui, hm, "H-", input)) StepHour(-1);
            if (Button(ui, hp, "H+", input)) StepHour(1);
            if (Button(ui, mm, "M-", input)) StepMinute(-1);
            if (Button(ui, mp, "M+", input)) StepMinute(1);
            var ok = new Rect(panel.center.x - 22, by + 36, 44, 14);
            if (Button(ui, ok, "SET", input)) Submit(TimeCode);
            UIStyle.Footer(ui, "A/D HOURS   W/S OR WHEEL MINUTES   ENTER SET   ESC CLOSE");
            if (!input || _waiting || _resultTimer > 0f) return;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) StepHour(-1);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) StepHour(1);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.mouseScrollDelta.y < -0.1f) StepMinute(-1);
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.mouseScrollDelta.y > 0.1f) StepMinute(1);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Submit(TimeCode);
        }

        string TimeCode => _hour.ToString("00") + _minute.ToString("00");

        void StepHour(int d)
        {
            if (_waiting || _resultTimer > 0f) return;
            _hour = (_hour - 1 + d + 12) % 12 + 1;
            AudioManager.Play2D(Snd.InventoryScroll, 0.6f, 0.8f);
        }

        void StepMinute(int d)
        {
            if (_waiting || _resultTimer > 0f) return;
            _minute = (_minute + d * 5 + 60) % 60;
            AudioManager.Play2D(Snd.InventoryScroll, 0.5f, 1.15f);
        }

        static void Hand(VhsUI ui, Vector2 c, float a, float len, Color col, int w)
        {
            Vector2 d = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
            for (int i = 0; i < len; i++)
            {
                Vector2 p = c + d * i;
                ui.Rect(p.x - (w - 1) * 0.5f, p.y - (w - 1) * 0.5f, w, w, col);
            }
        }

        // ------------------------------------------------------------------ sequence (coloured buttons)

        void DrawSequence(VhsUI ui, bool input)
        {
            int n = Symbols;
            float bw = 24, gap = 6, pw = Mathf.Max(140, n * (bw + gap) + 20);
            var panel = UIStyle.Panel(ui, new Rect(ui.Width * 0.5f - pw * 0.5f, ui.Height * 0.5f - 60, pw, 120), 0.92f);
            ui.Text(Lock.Info.Label ?? "PANEL", panel.center.x, panel.y + 6, VhsUI.Yellow, 1, Align.Center);
            // what has been pressed so far: little lamps
            var dr = new Rect(panel.x + 12, panel.y + 22, panel.width - 24, 18);
            ui.Rect(dr, new Color(0.05f, 0.06f, 0.05f, 1f));
            if (_resultTimer > 0f) ui.Text(ResultText, dr.center.x, dr.y + 3, DisplayColor, 1, Align.Center);
            else
            {
                float lx = dr.center.x - Length * 7f;
                for (int i = 0; i < Length; i++)
                {
                    var lr = new Rect(lx + i * 14 + 2, dr.y + 5, 10, 8);
                    if (i < _code.Length) ui.Rect(lr, SymbolColors[_code[i] - '0']);
                    else ui.Frame(lr, VhsUI.Dim);
                }
            }
            float x0 = panel.center.x - (n * (bw + gap) - gap) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var r = new Rect(x0 + i * (bw + gap), panel.y + 52, bw, 30);
                bool hover = ui.Hover(r);
                var col = SymbolColors[i];
                ui.Rect(r, hover ? Color.Lerp(col, Color.white, 0.25f) : col * 0.85f);
                ui.Frame(r, new Color(0, 0, 0, 0.9f));
                ui.Text((i + 1).ToString(), r.center.x, r.yMax + 3, VhsUI.Dim, 1, Align.Center, ui.TinyFont);
                if (input && ui.Click && hover) PressSymbol(i);
            }
            var clr = new Rect(panel.center.x - 20, panel.yMax - 18, 40, 13);
            if (Button(ui, clr, "CLR", input) && !_waiting && _resultTimer <= 0f) _code = "";
            UIStyle.Footer(ui, "PRESS THE BUTTONS IN ORDER (1-" + n + ")   ESC CLOSE");
            if (!input) return;
            foreach (char ch in Input.inputString)
                if (ch >= '1' && ch <= '9' && ch - '1' < n) PressSymbol(ch - '1');
        }

        void PressSymbol(int i)
        {
            if (_resultTimer > 0f || _waiting) return;
            AudioManager.Play2D(Snd.KeypadBeep, 0.7f, 0.8f + i * 0.08f);
            _code += (char)('0' + i);
            if (_code.Length >= Length) Submit(_code);
        }

        // ------------------------------------------------------------------ helpers

        static bool Button(VhsUI ui, Rect r, string label, bool input)
        {
            bool hover = ui.Hover(r);
            ui.Rect(r, hover ? new Color(0.35f, 0.35f, 0.33f) : new Color(0.2f, 0.2f, 0.19f));
            ui.Frame(r, new Color(0, 0, 0, 0.8f));
            ui.Text(label, r.center.x, r.y + Mathf.Floor((r.height - ui.LineHeight(1, ui.TinyFont)) * 0.5f), VhsUI.White, 1, Align.Center, ui.TinyFont);
            return input && ui.Click && hover;
        }
    }

    /// <summary>(iteration 3) Watching the home video: black screen, PLAY OSD, one captioned shot after another with static
    /// between them. ENTER skips to the next shot, ESC stops.</summary>
    public sealed class TapeScreen : UIScreen
    {
        readonly string[] _shots;
        int _shot = -1;
        float _t, _shotT;
        const float ShotSeconds = 5.5f, TearSeconds = 0.45f;

        public TapeScreen(string[] shots) { _shots = shots ?? new string[0]; }
        public override bool Opaque => true;
        public override bool ShowCursor => false;

        public override void OnOpen()
        {
            AudioManager.Play2D(Snd.TapePlay, 0.8f, 1f, AudioCategory.Ui);
            VhsEffect.TriggerGlitch(0.6f, 0.4f);
        }

        public override void OnClose()
        {
            VhsEffect.StaticOverride = 0f;
            AudioManager.Play2D(Snd.TapeStop, 0.7f, 1f, AudioCategory.Ui);
        }

        void Next()
        {
            _shot++;
            _shotT = 0f;
            if (_shot >= _shots.Length) { UIManager.Instance.Remove(this); return; }
            AudioManager.Play2D(Snd.StaticBurst, 0.35f, Random.Range(0.9f, 1.1f), AudioCategory.Ui);
            VhsEffect.TriggerGlitch(0.35f, 0.25f);
        }

        public override void Draw(VhsUI ui, bool input)
        {
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            _shotT += dt;
            ui.Rect(0, 0, ui.Width, ui.Height, new Color(0.02f, 0.03f, 0.08f, 1f));
            if (_shot < 0) { if (_t > 0.8f) Next(); }
            else if (_shotT > ShotSeconds) Next();
            if (_shot >= _shots.Length) return;
            bool tear = _shot < 0 || _shotT < TearSeconds;
            VhsEffect.StaticOverride = tear ? 0.8f : 0.08f;
            UIStyle.Osd(ui, "PLAY ▶", UIStyle.TapeCounter());
            if (!tear)
            {
                string text = _shots[_shot];
                int shown = Mathf.Clamp(Mathf.FloorToInt((_shotT - TearSeconds) * 40f), 0, text.Length);
                int w = Mathf.Min(ui.Width - 40, 380);
                var f = ui.Wrap(text, w, 1, ui.Font).Count * ui.LineHeight() > ui.Height * 0.6f ? ui.TinyFont : ui.Font;
                ui.TextWrapped(text.Substring(0, shown), ui.Width * 0.5f, ui.Height * 0.32f, w, VhsUI.White, 1, Align.Center, f);
            }
            UIStyle.Footer(ui, "ENTER NEXT   ESC STOP");
            if (!input) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) && _t > 0.5f) Next();
            else if (Input.GetKeyDown(KeyCode.Escape)) UIManager.Instance.Remove(this);
        }
    }

    /// <summary>(iteration 3) Everything this prisoner has read or watched tonight (notes, the tape), newest last.</summary>
    public sealed class JournalScreen : UIScreen
    {
        readonly MatchWorld _w;
        int _sel;
        float _t;

        public JournalScreen(MatchWorld w) { _w = w; _sel = Mathf.Max(0, w.Journal.Count - 1); }
        public override bool ShowCursor => true;

        public override void OnOpen() => AudioManager.Play2D(Snd.InventoryOpen, 0.6f, 0.95f, AudioCategory.Ui);
        public override void OnClose() => AudioManager.Play2D(Snd.InventoryClose, 0.6f, 0.95f, AudioCategory.Ui);

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            UIStyle.Dim(ui, 0.86f);
            UIStyle.Header(ui, "JOURNAL", 14);
            var list = _w.Journal;
            if (list.Count == 0)
            {
                ui.TextWrapped("NOTHING YET. NOTES, WRITING ON THE WALLS AND TAPES I FIND END UP HERE.", ui.Width * 0.5f, ui.Height * 0.4f, ui.Width - 60, VhsUI.Dim, 1, Align.Center);
            }
            else
            {
                _sel = Mathf.Clamp(_sel, 0, list.Count - 1);
                float lx = 16, ly = 40, lw = Mathf.Min(150, ui.Width * 0.36f);
                int rows = Mathf.Max(1, Mathf.FloorToInt((ui.Height - ly - 30) / (ui.LineHeight(1, ui.TinyFont) + 3)));
                int first = Mathf.Clamp(_sel - rows / 2, 0, Mathf.Max(0, list.Count - rows));
                for (int i = first; i < list.Count && i < first + rows; i++)
                {
                    var r = new Rect(lx, ly + (i - first) * (ui.LineHeight(1, ui.TinyFont) + 3), lw, ui.LineHeight(1, ui.TinyFont) + 2);
                    bool hover = ui.Hover(r);
                    if (i == _sel) ui.Rect(r, new Color(1, 1, 1, 0.12f));
                    ui.Text(UIStyle.NightClock(_w.NightLength > 0f ? list[i].At / _w.NightLength : 0f) + " " + list[i].Title, r.x + 2, r.y + 1,
                        i == _sel ? VhsUI.Yellow : VhsUI.White, 1, Align.Left, ui.TinyFont);
                    if (input && ui.Click && hover) { _sel = i; AudioManager.Play2D(Snd.InventoryScroll, 0.5f, 1f, AudioCategory.Ui); }
                }
                var e = list[_sel];
                float tx = lx + lw + 14, tw = ui.Width - tx - 16;
                var paper = new Rect(tx - 6, ly - 4, tw + 12, ui.Height - ly - 26);
                ui.Rect(paper, new Color(0.78f, 0.72f, 0.58f, 0.9f));
                ui.Text(e.Title, tx, ly, new Color(0.35f, 0.05f, 0.05f), 1, Align.Left, null, false);
                var nf = ui.Wrap(e.Text, (int)tw, 1, ui.Font).Count * ui.LineHeight() > paper.height - 30 ? ui.TinyFont : ui.Font;
                float y = ly + ui.LineHeight() + 6;
                foreach (var l in ui.Wrap(e.Text, (int)tw, 1, nf)) { ui.Text(l, tx, y, new Color(0.12f, 0.1f, 0.08f), 1, Align.Left, nf, false); y += nf.LineHeight; }
            }
            UIStyle.Footer(ui, "W/S SELECT   J / ESC CLOSE");
            if (!input) return;
            if (list.Count > 0)
            {
                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { _sel = Mathf.Max(0, _sel - 1); AudioManager.Play2D(Snd.InventoryScroll, 0.5f, 1f, AudioCategory.Ui); }
                if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { _sel = Mathf.Min(list.Count - 1, _sel + 1); AudioManager.Play2D(Snd.InventoryScroll, 0.5f, 1f, AudioCategory.Ui); }
            }
            if (_t > 0.2f && (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)))
                UIManager.Instance.Remove(this);
        }
    }
}
