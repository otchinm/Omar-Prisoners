using System.Collections.Generic;
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
        float _resultTimer = -1f, _t, _waitT, _resendAt = -1f;
        bool _ok, _waiting;
        /// <summary>A padlock on furniture: a mechanical dial (clicks and rattles), not an electronic keypad.</summary>
        bool Mechanical => Lock.Embedded;
        static readonly string[] Keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "OK" };
        static readonly Color[] SymbolColors =
        {
            new Color(0.8f, 0.12f, 0.1f), new Color(0.2f, 0.65f, 0.25f), new Color(0.2f, 0.35f, 0.85f), new Color(0.9f, 0.8f, 0.2f),
            new Color(0.88f, 0.88f, 0.84f), new Color(0.12f, 0.12f, 0.12f), new Color(0.92f, 0.5f, 0.12f), new Color(0.55f, 0.25f, 0.7f),
            new Color(0.5f, 0.5f, 0.5f),
        };

        public CodeLockScreen(MatchWorld w, CodeLockEntity lk)
        {
            _w = w; Lock = lk;
            // the hands stay where the last person left them
            if (lk.DraftHour > 0) { _hour = lk.DraftHour; _minute = lk.DraftMinute; }
        }

        int Length => Mathf.Clamp(Lock.Info.Length, 1, 8);
        int Symbols => Mathf.Clamp(Lock.Info.Symbols, 2, SymbolColors.Length);

        /// <summary>Host answer: 0 wrong, 1 right, 2 already open, 3 too fast (try again), 4 refused (too far / can't now).</summary>
        public void Result(byte status)
        {
            _waiting = false;
            switch (status)
            {
                case 1: case 2:
                    _ok = true; _resultTimer = 1.2f;
                    AudioManager.Play2D(Mechanical ? Snd.KeyUnlock : Snd.KeypadOk, 0.9f);
                    break;
                case 3: _waiting = true; _waitT = 0f; _resendAt = Time.unscaledTime + 1.05f; break;   // too fast: send it again in a moment
                case 4: UIManager.Instance.Remove(this); break;
                default:
                    _ok = false; _resultTimer = 1f; _code = "";
                    AudioManager.Play2D(Mechanical ? Snd.LockedRattle : Snd.KeypadWrong, 0.9f);
                    break;
            }
        }

        void Submit(string code)
        {
            if (_waiting || _resultTimer > 0f) return;
            _waiting = true;
            _waitT = 0f;
            _w.SendCode(Lock, code);
        }

        void PressKey(string k)
        {
            if (_resultTimer > 0f || _waiting) return;
            if (Mechanical) AudioManager.Play2D(Snd.InventoryScroll, 0.7f, 0.8f + _code.Length * 0.08f);
            else AudioManager.Play2D(Snd.KeypadBeep, 0.7f, 0.9f + _code.Length * 0.05f);
            if (k == "CLR") { _code = ""; return; }
            if (k == "OK") { if (_code.Length == Length) Submit(_code); return; }
            if (_code.Length < Length) _code += k;
            if (_code.Length == Length) Submit(_code);
        }

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            // gone when you can't use it any more: caught / hidden / walked away, or the drawer was opened another way
            var st = _w.LocalStatus;
            var av = _w.LocalAvatar;
            var owner = Lock.Embedded ? _w.DrawerWithCodeLock(Lock.Index) : null;
            if (st == null || st.Life != LifeState.Free || st.Hidden || st.Trapped || st.InCar
                || (av != null && Vector3.Distance(av.Position, Lock.InteractPoint) > 4.5f) || (owner != null && !owner.Locked && !(_ok && _resultTimer > 0f)))
            {
                UIManager.Instance.Remove(this);
                return;
            }
            // somebody else opened it
            if (Lock.Open && !(_ok && _resultTimer > 0f)) { _ok = true; _waiting = false; _resultTimer = 0.8f; }
            // the host said "too fast": resend the same code once the second is up
            if (_resendAt > 0f && Time.unscaledTime >= _resendAt) { _resendAt = -1f; _waitT = 0f; _w.SendCode(Lock, Lock.Info.Kind == CodeKind.Time ? TimeCode : _code); }
            // the host always answers; if nothing came back (lost / ignored), let the player try again
            else if (_resendAt < 0f && _waiting && (_waitT += Time.unscaledDeltaTime) > 2f) { _waiting = false; _code = ""; AudioManager.Play2D(Snd.UiError, 0.6f); }
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
            if (Input.GetKeyDown(KeyCode.J)) { UIManager.Instance.Push(new JournalScreen(_w)); return; }   // look the code up
            // E leaves only an untouched keypad / dial (on the clock W/A/S/D move the hands right around E: only ESC there)
            bool eCloses = Lock.Info.Kind != CodeKind.Time && _code.Length == 0 && _t > 0.25f;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) && eCloses)
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
            ui.Text(_resultTimer > 0f ? ResultText : _waiting ? (Mathf.Repeat(_t, 0.5f) < 0.25f ? "..." : "") : disp, dr.center.x, dr.y + 3, DisplayColor, 1, Align.Center);
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
            UIStyle.Footer(ui, "TYPE THE CODE   J JOURNAL   ESC CLOSE");
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
            UIStyle.Footer(ui, "A/D HOURS   W/S MINUTES   ENTER SET   J JOURNAL   ESC");
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
            Lock.DraftHour = _hour; Lock.DraftMinute = _minute;
            AudioManager.Play2D(Snd.InventoryScroll, 0.6f, 0.8f);
        }

        void StepMinute(int d)
        {
            if (_waiting || _resultTimer > 0f) return;
            _minute = (_minute + d * 5 + 60) % 60;
            Lock.DraftHour = _hour; Lock.DraftMinute = _minute;
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
            UIStyle.Footer(ui, "PRESS THE BUTTONS IN ORDER (1-" + n + ")   J JOURNAL   ESC");
            if (!input) return;
            foreach (char ch in Input.inputString)
                if (ch >= '1' && ch <= '9' && ch - '1' < n) PressSymbol(ch - '1');
        }

        void PressSymbol(int i)
        {
            if (_resultTimer > 0f || _waiting || _code.Length >= Length) return;
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

    /// <summary>(iteration 3) Watching the home video: PLAY OSD, one captioned shot after another with static between them.
    /// Each shot stays as long as its caption needs; the shot that gives the code away waits for ENTER. ENTER first finishes
    /// the caption, then goes on; ESC stops. The tape cuts out when Omar is after you or you are caught.</summary>
    public sealed class TapeScreen : UIScreen
    {
        readonly string[] _shots;
        readonly int _holdShot;
        int _shot = -1;
        float _t, _shotT;
        List<string> _lines;
        VhsFont _font;
        int _chars;
        const float TearSeconds = 0.45f, TypeRate = 40f;

        public TapeScreen(string[] shots, int holdShot = -1) { _shots = shots ?? new string[0]; _holdShot = holdShot; }
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
            MatchWorld.Instance?.ShowJournalHint();
        }

        void Next()
        {
            _shot++;
            _shotT = 0f;
            _lines = null;
            if (_shot >= _shots.Length) { UIManager.Instance.Remove(this); return; }
            AudioManager.Play2D(Snd.StaticBurst, 0.35f, Random.Range(0.9f, 1.1f), AudioCategory.Ui);
            VhsEffect.TriggerGlitch(0.35f, 0.25f);
        }

        float TypedAt => TearSeconds + _chars / TypeRate;
        float ShotLength => TypedAt + Mathf.Max(3f, _chars / 15f);

        public override void Draw(VhsUI ui, bool input)
        {
            // the tape cuts out when Omar comes for you
            var w = MatchWorld.Instance;
            var st = w != null ? w.LocalStatus : null;
            if (w != null && ((st != null && st.Life != LifeState.Free) || (w.Chase != null && w.Chase.IsTarget(w.LocalId))))
            {
                w.AddMessage("THE TAPE CUTS OUT", 2f);
                UIManager.Instance.Remove(this);
                return;
            }
            float dt = Time.unscaledDeltaTime;
            _t += dt;
            _shotT += dt;
            ui.Rect(0, 0, ui.Width, ui.Height, new Color(0.02f, 0.03f, 0.08f, 0.95f));
            if (_shot < 0) { if (_t > 0.8f) Next(); }
            else if (_lines != null && _shot != _holdShot && _shotT > ShotLength) Next();
            if (_shot >= _shots.Length) return;
            bool tear = _shot < 0 || _shotT < TearSeconds;
            VhsEffect.StaticOverride = tear ? 0.8f : 0.08f;
            UIStyle.Osd(ui, "PLAY ▶", UIStyle.TapeCounter());
            int wrapW = Mathf.Min(ui.Width - 40, 380);
            if (_shot >= 0 && _lines == null)
            {
                // wrap the whole caption once, so the words don't jump between lines while it types out
                string text = _shots[_shot];
                _font = ui.Wrap(text, wrapW, 1, ui.Font).Count * ui.LineHeight() > ui.Height * 0.6f ? ui.TinyFont : ui.Font;
                _lines = ui.Wrap(text, wrapW, 1, _font);
                _chars = 0;
                foreach (var l in _lines) _chars += l.Length;
            }
            bool typed = _shotT >= TypedAt;
            if (!tear && _lines != null)
            {
                int left = Mathf.Clamp(Mathf.FloorToInt((_shotT - TearSeconds) * TypeRate), 0, _chars);
                float y = ui.Height * 0.32f;
                foreach (var l in _lines)
                {
                    if (left <= 0) break;
                    ui.Text(l.Length <= left ? l : l.Substring(0, left), ui.Width * 0.5f - ui.TextWidth(l, 1, _font) * 0.5f, y, VhsUI.White, 1, Align.Left, _font);
                    left -= l.Length;
                    y += _font.LineHeight;
                }
            }
            UIStyle.Footer(ui, typed && _shot == _holdShot ? "ENTER NEXT   ESC STOP   (IT IS IN THE JOURNAL)" : "ENTER NEXT   ESC STOP");
            if (!input) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) && _t > 0.5f)
            {
                if (_shot >= 0 && !tear && !typed) _shotT = TypedAt;   // first press finishes the caption
                else Next();
            }
            else if (Input.GetKeyDown(KeyCode.Escape)) UIManager.Instance.Remove(this);
        }
    }

    /// <summary>(iteration 3) Everything this prisoner has read or watched tonight (notes, the tape), newest last. Clues
    /// (code digits, combinations) are marked with '*'; the shelter digits found so far are collected on top.</summary>
    public sealed class JournalScreen : UIScreen
    {
        readonly MatchWorld _w;
        int _sel, _scroll, _scrollFor = -1;
        float _t;

        public JournalScreen(MatchWorld w) { _w = w; _sel = Mathf.Max(0, w.Journal.Count - 1); }
        public override bool ShowCursor => true;

        public override void OnOpen() => AudioManager.Play2D(Snd.InventoryOpen, 0.6f, 0.95f, AudioCategory.Ui);
        public override void OnClose() => AudioManager.Play2D(Snd.InventoryClose, 0.6f, 0.95f, AudioCategory.Ui);

        static string ShortClock(float at, float night)
        {
            // 01:00 .. 06:00 like the OSD, without seconds
            float h = 1f + 5f * Mathf.Clamp01(night > 0f ? at / night : 0f);
            int hh = Mathf.FloorToInt(h), mm = Mathf.FloorToInt((h - hh) * 60f);
            return hh + ":" + mm.ToString("00");
        }

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            UIStyle.Dim(ui, 0.86f);
            UIStyle.Header(ui, "JOURNAL", 14);
            var list = _w.Journal;
            var tiny = ui.TinyFont;
            int scrollMove = 0;
            if (list.Count == 0)
            {
                ui.TextWrapped("NOTHING YET. NOTES, WRITING ON THE WALLS AND TAPES I FIND END UP HERE.", ui.Width * 0.5f, ui.Height * 0.4f, ui.Width - 60, VhsUI.Dim, 1, Align.Center);
            }
            else
            {
                _sel = Mathf.Clamp(_sel, 0, list.Count - 1);
                float lx = 16, ly = 40, lw = Mathf.Min(150, ui.Width * 0.36f);
                // the shelter digits collected so far
                string digits = _w.ShelterDigitsFound(out bool complete);
                if (digits != "_ _ _ _") { ui.Text("SHELTER: " + digits, lx, ly, complete ? VhsUI.Yellow : VhsUI.Dim, 1, Align.Left, tiny); ly += ui.LineHeight(1, tiny) + 4; }
                int rowH = ui.LineHeight(1, tiny) + 3;
                int rows = Mathf.Max(1, Mathf.FloorToInt((ui.Height - ly - 30) / rowH));
                int first = Mathf.Clamp(_sel - rows / 2, 0, Mathf.Max(0, list.Count - rows));
                int maxChars = Mathf.Max(6, Mathf.FloorToInt((lw - 4) / Mathf.Max(1, ui.TextWidth("M", 1, tiny))));
                for (int i = first; i < list.Count && i < first + rows; i++)
                {
                    var r = new Rect(lx, ly + (i - first) * rowH, lw, rowH - 1);
                    bool hover = ui.Hover(r);
                    if (i == _sel) ui.Rect(r, new Color(1, 1, 1, 0.12f));
                    string row = (list[i].Clue ? "* " : "  ") + ShortClock(list[i].At, _w.NightLength) + " " + list[i].Title;
                    if (row.Length > maxChars) row = row.Substring(0, maxChars - 1) + ".";
                    ui.Text(row, r.x + 2, r.y + 1, i == _sel ? VhsUI.Yellow : list[i].Clue ? VhsUI.White : VhsUI.Dim, 1, Align.Left, tiny);
                    if (input && ui.Click && hover && _sel != i) { _sel = i; AudioManager.Play2D(Snd.InventoryScroll, 0.5f, 1f, AudioCategory.Ui); }
                }
                // the page: wrapped once, scrolled when it is longer than the sheet
                var e = list[_sel];
                if (_scrollFor != _sel) { _scrollFor = _sel; _scroll = 0; }
                float tx = lx + lw + 14, tw = ui.Width - tx - 16, py = 40;
                var paper = new Rect(tx - 6, py - 4, tw + 12, ui.Height - py - 26);
                ui.Rect(paper, new Color(0.78f, 0.72f, 0.58f, 0.95f));
                var ink = new Color(0.12f, 0.1f, 0.08f);
                ui.Text(e.Title, tx, py, new Color(0.35f, 0.05f, 0.05f), 1, Align.Left, tiny, false);
                float y = py + ui.LineHeight(1, tiny) + 5;
                var nf = ui.Wrap(e.Text, (int)tw, 1, ui.Font).Count * ui.LineHeight() > paper.yMax - y - 4 ? tiny : ui.Font;
                var lines = ui.Wrap(e.Text, (int)tw, 1, nf);
                int fit = Mathf.Max(1, Mathf.FloorToInt((paper.yMax - 6 - y) / nf.LineHeight));
                int maxScroll = Mathf.Max(0, lines.Count - fit);
                _scroll = Mathf.Clamp(_scroll, 0, maxScroll);
                for (int i = _scroll; i < lines.Count && i < _scroll + fit; i++) { ui.Text(lines[i], tx, y, ink, 1, Align.Left, nf, false); y += nf.LineHeight; }
                if (_scroll > 0) ui.Text("▲", paper.xMax - 8, paper.y + 2, ink, 1, Align.Center, tiny, false);
                if (_scroll < maxScroll) ui.Text("▼", paper.xMax - 8, paper.yMax - ui.LineHeight(1, tiny) - 1, ink, 1, Align.Center, tiny, false);
                if (input)
                {
                    float wheel = Input.mouseScrollDelta.y;
                    if (wheel < -0.1f || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.PageDown)) scrollMove = 1;
                    if (wheel > 0.1f || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.PageUp)) scrollMove = -1;
                    _scroll = Mathf.Clamp(_scroll + scrollMove * Mathf.Max(1, fit - 2), 0, maxScroll);
                }
            }
            UIStyle.Footer(ui, "W/S SELECT   A/D OR WHEEL SCROLL   J / ESC CLOSE");
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
