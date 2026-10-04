using System;
using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Gameplay;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>
    /// (iteration 2) F10: the owner's admin panel. Password prompt first (masked, REMEMBER toggle), then category tabs
    /// with every command of <see cref="AdminCmds.All"/> (lobby / match filtered), argument pickers per <see cref="AdminArg"/>
    /// and the feedback log. Keyboard: Q / E or TAB tabs, W / S rows, A / D value (SHIFT + A / D second value),
    /// ENTER runs. Mouse: click a tab / row, click the selected row to run it.
    /// </summary>
    public sealed class AdminScreen : UIScreen
    {
        public override bool Modal => true;

        string _pw = "";
        bool _remember = true;
        int _tab, _row, _scroll;
        readonly Dictionary<AdminCmd, int> _argA = new Dictionary<AdminCmd, int>();
        readonly Dictionary<AdminCmd, int> _argB = new Dictionary<AdminCmd, int>();
        readonly Dictionary<AdminCmd, float> _num = new Dictionary<AdminCmd, float>();
        static readonly AdminCategory[] Cats = (AdminCategory[])Enum.GetValues(typeof(AdminCategory));
        static readonly float[] SpeedSteps = { 1f, 2f, 4f, 0.5f };

        public static void Toggle()
        {
            var ui = UIManager.Instance;
            if (ui == null) return;
            var open = ui.Find<AdminScreen>();
            if (open != null) ui.Remove(open);
            else ui.Push(new AdminScreen());
        }

        public override void Draw(VhsUI ui, bool input)
        {
            UIStyle.Dim(ui, 0.72f);
            if (input && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.F10))) { UIManager.Instance.Remove(this); return; }
            if (!Admin.LoggedIn) { DrawLogin(ui, input); return; }
            if (!Admin.IsAdmin)
            {
                UIStyle.Header(ui, "ADMIN", 30);
                ui.Text(Admin.Pending ? "WAITING FOR THE HOST TO ACCEPT YOUR KEY..." : Admin.LastError, ui.Width * 0.5f, ui.Height * 0.45f, VhsUI.Dim, 1, Align.Center);
                if (input && Input.GetKeyDown(KeyCode.Delete)) Admin.Logout();
                UIStyle.Footer(ui, "DEL  LOG OUT     F10  CLOSE");
                return;
            }
            DrawPanel(ui, input);
        }

        // ------------------------------------------------------------------ login

        void DrawLogin(VhsUI ui, bool input)
        {
            UIStyle.Header(ui, "ADMIN ACCESS", ui.Height * 0.22f);
            float x = ui.Width * 0.5f - 90, y = ui.Height * 0.42f;
            ui.Text("PASSWORD", x, y - ui.LineHeight() - 4, VhsUI.Dim);
            var r = new Rect(x, y - 2, 180, ui.LineHeight() + 3);
            ui.Rect(r, new Color(0, 0, 0, 0.6f));
            ui.Frame(r, VhsUI.White);
            bool enter = false;
            if (input)
            {
                foreach (char ch in Input.inputString)
                {
                    if (ch == '\b') { if (_pw.Length > 0) _pw = _pw.Substring(0, _pw.Length - 1); }
                    else if (ch == '\n' || ch == '\r') enter = true;
                    else if (_pw.Length < 40 && ch >= 32 && ch < 127) { _pw += ch; AudioManager.Play2D(Snd.UiType, 0.5f, UnityEngine.Random.Range(0.95f, 1.08f), AudioCategory.Ui); }
                }
                if (Input.GetKeyDown(KeyCode.Tab)) _remember = !_remember;
            }
            bool caret = (Time.unscaledTime * 2f % 2f) < 1f;
            ui.Text(new string('*', Mathf.Min(_pw.Length, 26)) + (caret ? "_" : ""), x + 3, y, VhsUI.White);
            var rem = new Rect(x, y + ui.LineHeight() + 8, 180, ui.LineHeight() + 2);
            ui.Text((_remember ? "[X]" : "[ ]") + " REMEMBER ON THIS PC (TAB)", x, rem.y, VhsUI.Dim);
            if (input && ui.Click && ui.Hover(rem)) _remember = !_remember;
            if (!string.IsNullOrEmpty(Admin.LastError)) ui.Text(Admin.LastError, ui.Width * 0.5f, rem.y + ui.LineHeight() + 10, VhsUI.Red, 1, Align.Center);
            if (enter && _pw.Length > 0)
            {
                bool ok = Admin.TryLogin(_pw, _remember);
                _pw = "";
                AudioManager.Play2D(ok ? Snd.UiSelect : Snd.UiBack, 0.8f, 1f, AudioCategory.Ui);
            }
            UIStyle.Footer(ui, "ENTER  LOG IN     F10  CLOSE");
        }

        // ------------------------------------------------------------------ panel

        List<AdminCmdInfo> RowsFor(AdminCategory cat, bool lobby)
        {
            var list = new List<AdminCmdInfo>();
            foreach (var c in AdminCmds.All)
            {
                if (c.Category != cat) continue;
                if (c.Lobby != lobby && !(c.Local && c.Category == AdminCategory.Debug)) continue;
                list.Add(c);
            }
            return list;
        }

        void DrawPanel(VhsUI ui, bool input)
        {
            var s = NetSession.Instance;
            bool lobby = s != null && s.State == SessionState.Lobby;
            bool match = MatchWorld.Instance != null;
            // tabs that have something to show right now
            var tabs = new List<AdminCategory>();
            foreach (var c in Cats)
            {
                if (lobby ? c != AdminCategory.Lobby && c != AdminCategory.Debug : c == AdminCategory.Lobby) continue;
                if (!lobby && !match && c != AdminCategory.Debug) continue;
                tabs.Add(c);
            }
            if (tabs.Count == 0) tabs.Add(AdminCategory.Debug);
            _tab = Mathf.Clamp(_tab, 0, tabs.Count - 1);
            if (input)
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (Input.GetKeyDown(KeyCode.E) || (Input.GetKeyDown(KeyCode.Tab) && !shift)) { _tab = (_tab + 1) % tabs.Count; _row = 0; _scroll = 0; Blip(); }
                if (Input.GetKeyDown(KeyCode.Q) || (Input.GetKeyDown(KeyCode.Tab) && shift)) { _tab = (_tab + tabs.Count - 1) % tabs.Count; _row = 0; _scroll = 0; Blip(); }
            }

            // header: tabs
            ui.Text("ADMIN", 8, 6, VhsUI.Red);
            float tx = 8 + ui.TextWidth("ADMIN") + 10, ty = 6;
            for (int i = 0; i < tabs.Count; i++)
            {
                string name = tabs[i].ToString().ToUpperInvariant();
                int tw = ui.TextWidth(name, 1, ui.TinyFont);
                if (tx + tw > ui.Width - 8) { tx = 8; ty += ui.LineHeight(1, ui.TinyFont) + 3; }
                var r = new Rect(tx - 2, ty - 1, tw + 4, ui.LineHeight(1, ui.TinyFont) + 2);
                bool sel = i == _tab;
                if (sel) ui.Rect(r, new Color(1, 1, 1, 0.15f));
                ui.Text(name, tx, ty, sel ? VhsUI.White : VhsUI.Dim, 1, Align.Left, ui.TinyFont);
                if (input && ui.Click && ui.Hover(r) && !sel) { _tab = i; _row = 0; _scroll = 0; Blip(); }
                tx += tw + 8;
            }
            float top = ty + ui.LineHeight(1, ui.TinyFont) + 6;
            ui.Rect(8, top - 3, ui.Width - 16, 1, new Color(1, 1, 1, 0.2f));

            var rows = RowsFor(tabs[_tab], lobby);
            int lh = ui.LineHeight() + 2;
            int visible = Mathf.Max(3, (int)((ui.Height - top - 52) / lh));
            if (rows.Count == 0) ui.Text("NOTHING HERE RIGHT NOW", ui.Width * 0.5f, top + 20, VhsUI.Dim, 1, Align.Center);
            else
            {
                if (input)
                {
                    if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { _row = (_row + 1) % rows.Count; Blip(); }
                    if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { _row = (_row + rows.Count - 1) % rows.Count; Blip(); }
                }
                _row = Mathf.Clamp(_row, 0, rows.Count - 1);
                if (_row < _scroll) _scroll = _row;
                if (_row >= _scroll + visible) _scroll = _row - visible + 1;
                for (int i = _scroll; i < Mathf.Min(rows.Count, _scroll + visible); i++)
                {
                    var c = rows[i];
                    float y = top + (i - _scroll) * lh;
                    var r = new Rect(8, y - 1, ui.Width - 16, lh);
                    bool sel = i == _row;
                    if (input && ui.Hover(r) && ui.MouseMoved && !sel) { _row = i; sel = true; }
                    if (sel) ui.Rect(r, new Color(1, 1, 1, 0.1f));
                    ui.Text((sel ? "▶ " : "  ") + c.Label, 12, y, sel ? VhsUI.White : VhsUI.Dim);
                    string val = ValueText(c, s);
                    if (!string.IsNullOrEmpty(val)) ui.Text(val, ui.Width - 12, y, sel ? VhsUI.Yellow : VhsUI.Dim, 1, Align.Right);
                    if (sel && input)
                    {
                        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                        int d = 0;
                        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) d = 1;
                        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) d = -1;
                        if (d != 0) { Step(c, d, shift, s); Blip(); }
                        bool run = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || (ui.Click && ui.Hover(r));
                        if (run) Execute(c, s);
                    }
                }
                if (rows.Count > visible) ui.Text((_scroll + 1) + "-" + Mathf.Min(rows.Count, _scroll + visible) + " / " + rows.Count, ui.Width - 12, top + visible * lh, VhsUI.Dim, 1, Align.Right, ui.TinyFont);
                var cur = rows[_row];
                if (!string.IsNullOrEmpty(cur.Hint)) ui.Text(cur.Hint, 12, top + visible * lh, VhsUI.Dim, 1, Align.Left, ui.TinyFont);
            }

            // log
            float ly = ui.Height - 40;
            ui.Rect(8, ly - 3, ui.Width - 16, 1, new Color(1, 1, 1, 0.2f));
            int n = Admin.Log.Count;
            for (int i = Mathf.Max(0, n - 3); i < n; i++)
            {
                ui.Text(Admin.Log[i], 10, ly, VhsUI.Yellow, 1, Align.Left, ui.TinyFont);
                ly += ui.LineHeight(1, ui.TinyFont) + 1;
            }
            ui.Text("Q/E TABS  W/S SELECT  A/D VALUE (SHIFT: 2ND)  ENTER RUN  F10 CLOSE", ui.Width * 0.5f, ui.Height - 9, VhsUI.Dim, 1, Align.Center, ui.TinyFont);
        }

        static void Blip() => AudioManager.Play2D(Snd.UiMove, 0.5f, 1f, AudioCategory.Ui);

        // ------------------------------------------------------------------ arguments

        static List<PlayerInfo> PlayerList(NetSession s)
        {
            var l = new List<PlayerInfo>();
            if (s != null) foreach (var p in s.Players) l.Add(p);
            return l;
        }

        int A(AdminCmd c) => _argA.TryGetValue(c, out var v) ? v : 0;
        int B(AdminCmd c) => _argB.TryGetValue(c, out var v) ? v : 0;

        float Num(AdminCmdInfo c)
        {
            if (_num.TryGetValue(c.Cmd, out var v)) return v;
            return c.Default > 0f ? c.Default : 1f;
        }

        static readonly ItemType[] Items = BuildItems();
        static ItemType[] BuildItems()
        {
            var l = new List<ItemType>();
            foreach (ItemType t in Enum.GetValues(typeof(ItemType))) if (t != ItemType.None) l.Add(t);
            return l.ToArray();
        }
        static readonly WorldEventKind[] Events = (WorldEventKind[])Enum.GetValues(typeof(WorldEventKind));
        static readonly EndingId[] Endings = BuildEndings();
        static EndingId[] BuildEndings()
        {
            var l = new List<EndingId>();
            foreach (EndingId e in Enum.GetValues(typeof(EndingId))) if (e != EndingId.None) l.Add(e);
            return l.ToArray();
        }
        static readonly PlayerRole[] Roles = { PlayerRole.Prisoner, PlayerRole.Omar, PlayerRole.Spectator };
        static readonly string[] Diffs = { "EASY", "NORMAL", "HARD", "NIGHTMARE" };

        static int Wrap(int v, int n) => n <= 0 ? 0 : ((v % n) + n) % n;

        void Step(AdminCmdInfo c, int d, bool second, NetSession s)
        {
            switch (c.Arg)
            {
                case AdminArg.Number:
                    {
                        float v = Num(c);
                        if (c.Cmd == AdminCmd.Speed)
                        {
                            int i = Array.IndexOf(SpeedSteps, v);
                            v = SpeedSteps[Wrap((i < 0 ? 0 : i) + d, SpeedSteps.Length)];
                        }
                        else
                        {
                            float step = v >= 60f ? 30f : v >= 10f ? 5f : 1f;
                            v = Mathf.Clamp(v + d * step, 1f, 3600f);
                        }
                        _num[c.Cmd] = v;
                        break;
                    }
                case AdminArg.PlayerItem:
                case AdminArg.PlayerRole:
                    if (second) _argB[c.Cmd] = B(c.Cmd) + d;
                    else _argA[c.Cmd] = A(c.Cmd) + d;
                    break;
                case AdminArg.Toggle:
                case AdminArg.None:
                    break;
                default:
                    _argA[c.Cmd] = A(c.Cmd) + d;
                    break;
            }
        }

        string ValueText(AdminCmdInfo c, NetSession s)
        {
            var players = PlayerList(s);
            switch (c.Arg)
            {
                case AdminArg.Toggle: return Admin.IsOn(c.Cmd) ? "[ON]" : "[OFF]";
                case AdminArg.Player: return players.Count > 0 ? "◀ " + players[Wrap(A(c.Cmd), players.Count)].Name + " ▶" : "-";
                case AdminArg.Item: return "◀ " + ItemDefs.Get(Items[Wrap(A(c.Cmd), Items.Length)]).Name + " ▶";
                case AdminArg.PlayerItem:
                    return (players.Count > 0 ? players[Wrap(A(c.Cmd), players.Count)].Name : "-") + " / " + ItemDefs.Get(Items[Wrap(B(c.Cmd), Items.Length)]).Name;
                case AdminArg.Event: return "◀ " + Events[Wrap(A(c.Cmd), Events.Length)].ToString().ToUpperInvariant() + " ▶";
                case AdminArg.Ending: return "◀ " + Endings[Wrap(A(c.Cmd), Endings.Length)].ToString().ToUpperInvariant() + " ▶";
                case AdminArg.Location:
                    {
                        var locs = Admin.Locations();
                        return locs.Count > 0 ? "◀ " + Short(locs[Wrap(A(c.Cmd), locs.Count)]) + " ▶" : "-";
                    }
                case AdminArg.Number: return "◀ " + Num(c).ToString(c.Cmd == AdminCmd.Speed ? "0.#" : "0") + (c.Cmd == AdminCmd.Speed ? "X" : "") + " ▶";
                case AdminArg.PlayerRole:
                    return (players.Count > 0 ? players[Wrap(A(c.Cmd), players.Count)].Name : "-") + " / " + Roles[Wrap(B(c.Cmd), Roles.Length)].ToString().ToUpperInvariant();
                case AdminArg.Difficulty: return "◀ " + Diffs[Wrap(A(c.Cmd), Diffs.Length)] + " ▶";
                default: return "";
            }
        }

        static string Short(string s)
        {
            s = s.Replace("House.", "").Replace("Basement.", "BSMT ").ToUpperInvariant();
            return s.Length > 18 ? s.Substring(0, 18) : s;
        }

        void Execute(AdminCmdInfo c, NetSession s)
        {
            var players = PlayerList(s);
            int pid = players.Count > 0 ? players[Wrap(A(c.Cmd), players.Count)].Id : -1;
            AudioManager.Play2D(Snd.UiSelect, 0.8f, 1f, AudioCategory.Ui);
            switch (c.Arg)
            {
                case AdminArg.Player: if (pid >= 0) Admin.Run(c.Cmd, pid); break;
                case AdminArg.Item: Admin.Run(c.Cmd, (int)Items[Wrap(A(c.Cmd), Items.Length)]); break;
                case AdminArg.PlayerItem: if (pid >= 0) Admin.Run(c.Cmd, pid, (int)Items[Wrap(B(c.Cmd), Items.Length)]); break;
                case AdminArg.Event: Admin.Run(c.Cmd, (int)Events[Wrap(A(c.Cmd), Events.Length)]); break;
                case AdminArg.Ending: Admin.Run(c.Cmd, (int)Endings[Wrap(A(c.Cmd), Endings.Length)]); break;
                case AdminArg.Location:
                    {
                        var locs = Admin.Locations();
                        if (locs.Count > 0) Admin.Run(c.Cmd, 0, 0, 0f, locs[Wrap(A(c.Cmd), locs.Count)]);
                        break;
                    }
                case AdminArg.Number: Admin.Run(c.Cmd, 0, 0, Num(c)); break;
                case AdminArg.PlayerRole: if (pid >= 0) Admin.Run(c.Cmd, pid, (int)Roles[Wrap(B(c.Cmd), Roles.Length)]); break;
                case AdminArg.Difficulty: Admin.Run(c.Cmd, Wrap(A(c.Cmd), Diffs.Length)); break;
                default: Admin.Run(c.Cmd); break;
            }
            if (!string.IsNullOrEmpty(Admin.LastError) && !Admin.IsAdmin) Admin.AddLogPublic(Admin.LastError);
        }
    }
}
