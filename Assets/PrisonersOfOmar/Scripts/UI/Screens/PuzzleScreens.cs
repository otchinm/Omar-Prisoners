using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Map;
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
}
