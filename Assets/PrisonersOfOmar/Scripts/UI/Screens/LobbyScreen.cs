using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>
    /// Lobby: up to 4 prisoners + Omar. Pick a character (◀ ▶), ready up; the host sets the night length and the
    /// difficulty and starts (the AI plays Omar when nobody picks him). Shows a 3D preview facing the camera.
    /// The CODE field unlocks a secret prisoner (<see cref="GameInfo.SecretSkinFor"/>): it joins this player's ◀ ▶ list
    /// and is picked at once; the code goes to the host with every lobby request, the host checks it.
    /// </summary>
    public sealed class LobbyScreen : UIScreen
    {
        int _sel;
        int _choice; // index into _choices (the last one is Omar)
        bool _ready;
        float _readySentAt = -10f;
        RenderTexture _rt;
        Transform _previewRoot;
        HumanoidRig _preview;
        CharacterSkin? _previewSkin;   // what the 3D preview shows (by skin: list indices shift when a secret one is added)
        float _yaw;
        string _hostAddresses;
        // CODE field: the text being typed, whether it has the keyboard, the feedback line under the menu
        string _code = "";
        string _codeBefore = "";   // the field's text when typing started (Esc / a click outside puts it back)
        bool _editCode;
        string _codeMsg = "";
        bool _codeOk;
        float _codeMsgAt = -10f;
        /// <summary>The last code that unlocked a secret prisoner (kept for the whole run, so the pick survives the next lobby).</summary>
        static string s_unlockedCode = "";
        /// <summary>This player's ◀ ▶ list: <see cref="Choices"/> plus the secret prisoners they unlocked (before Omar).</summary>
        readonly System.Collections.Generic.List<CharacterSkin> _choices = new System.Collections.Generic.List<CharacterSkin>(Choices);

        static string Describe(CharacterSkin skin)
        {
            switch (skin)
            {
                case CharacterSkin.Prisoner1: return "NUMBER 19. TRACK STAR. BIG MOUTH, BIGGER LUNGS. HE THOUGHT IT WAS A PRANK.";
                case CharacterSkin.Prisoner2: return "SHE CAME LOOKING FOR HER BROTHER. SHE FOUND HIS JACKET IN THE PENS.";
                case CharacterSkin.Prisoner3: return "QUIET. OBSERVANT. SHE COUNTS THE STEPS BETWEEN THE CAGES EVERY NIGHT.";
                case CharacterSkin.Prisoner4: return "HE READ ABOUT THE BASE ON A FORUM. HE WANTED PROOF. NOW HE IS THE PROOF.";
                case CharacterSkin.Prisoner5: return "SHE CAME TO FILM AN ABANDONED FARM. THE TAPE IS STILL RUNNING.";
                case CharacterSkin.Prisoner6: return "HE SNUCK OUT TO FOLLOW HIS SISTER. NOBODY KNOWS HE IS HERE.";
                case CharacterSkin.Prisoner7: return "HE CAME TO BRING THEM HOME. THE GATE CLOSED BEHIND HIS CAR.";
                case CharacterSkin.Prisoner8: return "HE WALKED IN THROUGH THE FRONT GATE. HE PLANS TO WALK OUT THE SAME WAY.";
                default: return "OMAR ALIBUTAEV. MASTER OF THE BASE OF THE SECOND CLASS. NOBODY LEAVES.";
            }
        }

        static readonly string[] DifficultyNames = { "EASY", "NORMAL", "HARD", "NIGHTMARE" };
        static readonly string[] DifficultyLines =
        {
            "MORE LIGHT, SLOWER OMAR", "THE NIGHT AS INTENDED", "DARKER. HE HEARS MORE.", "PITCH BLACK. NO SECOND CHANCES.",
        };

        /// <summary>Selectable appearances: every available prisoner skin, then Omar.</summary>
        static readonly System.Collections.Generic.List<CharacterSkin> Choices = BuildChoices();

        static System.Collections.Generic.List<CharacterSkin> BuildChoices()
        {
            var list = new System.Collections.Generic.List<CharacterSkin>();
            foreach (var s in GameInfo.PrisonerSkins) if (HumanoidFactory.HasSkin(s)) list.Add(s);
            list.Add(CharacterSkin.Omar);
            return list;
        }

        int OmarChoice => _choices.Count - 1;
        CharacterSkin ChosenSkin => _choices[Mathf.Clamp(_choice, 0, _choices.Count - 1)];

        int ChoiceOf(PlayerInfo p)
        {
            if (p.Role == PlayerRole.Omar) return OmarChoice;
            int i = _choices.IndexOf(p.Skin);
            return i >= 0 ? i : 0;
        }

        /// <summary>Puts an unlocked secret prisoner into this player's list (just before Omar); returns its index.</summary>
        int AddChoice(CharacterSkin skin)
        {
            int i = _choices.IndexOf(skin);
            if (i >= 0) return i;
            _choices.Insert(_choices.Count - 1, skin);
            return _choices.Count - 2;
        }

        /// <summary>Roster tag of a prisoner: its place in the lobby list, the secret ones after it (#8 ...).</summary>
        static string RosterTag(CharacterSkin skin)
        {
            int secret = System.Array.IndexOf(GameInfo.SecretSkins, skin);
            if (secret >= 0) return "#" + (GameInfo.PrisonerSkins.Length + 1 + secret);
            return "#" + (Choices.IndexOf(skin) + 1);
        }

        public override void OnOpen()
        {
            VhsEffect.Mode = VhsMode.Menu;
            AudioManager.PlayMusic(Snd.MenuTheme, 2.5f);
            GameInput.SetCursorLocked(false);
            var me = NetSession.Instance.LocalPlayer;
            // a secret prisoner unlocked earlier in this run (or still held from the last match) stays in the list
            var unlocked = GameInfo.SecretSkinFor(s_unlockedCode);
            if (unlocked.HasValue && HumanoidFactory.HasSkin(unlocked.Value)) AddChoice(unlocked.Value);
            if (me != null && GameInfo.IsSecretSkin(me.Skin)) AddChoice(me.Skin);
            _code = s_unlockedCode;
            if (me != null) _choice = ChoiceOf(me);
            // after a match the host resets everybody to "not ready": don't trust the stale roster
            _ready = me != null && me.Ready && NetSession.Instance.IsHost;
            _hostAddresses = NetSession.LocalAddresses();
            var go = new GameObject("LobbyPreview");
            Object.DontDestroyOnLoad(go);
            go.transform.position = new Vector3(0, -800, 0);
            _previewRoot = go.transform;
            _rt = new RenderTexture(96, 128, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "LobbyPreviewRT" };
        }

        public override void OnClose()
        {
            if (_previewRoot != null) Object.Destroy(_previewRoot.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
        }

        void UpdatePreview()
        {
            if (_previewSkin != ChosenSkin)
            {
                _previewSkin = ChosenSkin;
                if (_preview != null) Object.Destroy(_preview.gameObject);
                try
                {
                    _preview = HumanoidFactory.Build(ChosenSkin, _previewRoot, Layers.Preview);
                }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            _yaw += Time.unscaledDeltaTime * 35f;
            if (_preview != null)
            {
                // yaw 0 = camera in front of the character (characters face +Z): sway around the front view
                try { PreviewRenderer.Render(_preview.transform, _rt, Mathf.Sin(_yaw * Mathf.Deg2Rad) * 40f, 8f, 1f); } catch { }
            }
        }

        void SendChoice()
        {
            var skin = ChosenSkin;
            var role = skin == CharacterSkin.Omar ? PlayerRole.Omar : PlayerRole.Prisoner;
            NetSession.Instance.RequestLobby(role, skin, _ready, s_unlockedCode);
        }

        /// <summary>Enter in the CODE field: a right code unlocks its secret prisoner and picks it.</summary>
        void ApplyCode(NetSession s)
        {
            _editCode = false;
            string code = _code.Trim().ToUpperInvariant();
            _code = code;
            if (code.Length == 0) return;
            var skin = GameInfo.SecretSkinFor(code);
            _codeMsgAt = Time.unscaledTime;
            if (!skin.HasValue || !HumanoidFactory.HasSkin(skin.Value))
            {
                _codeOk = false;
                _codeMsg = "WRONG CODE";
                AudioManager.Play2D(Snd.UiError, 0.8f, 1f, AudioCategory.Ui);
                return;
            }
            s_unlockedCode = code;
            _choice = AddChoice(skin.Value);
            _codeOk = true;
            string name = HumanoidFactory.DisplayName(skin.Value);
            bool taken = s.Players.Exists(o => o.Id != s.LocalId && o.IsPrisoner && o.Skin == skin.Value);
            var me = s.LocalPlayer;
            int prisoners = 0;
            foreach (var o in s.Players) if (o.IsPrisoner && o.Id != s.LocalId) prisoners++;
            bool full = (me == null || !me.IsPrisoner) && prisoners >= GameInfo.MaxPrisoners;
            _codeMsg = taken ? "CODE ACCEPTED - SOMEBODY ALREADY PLAYS " + name
                : full ? "CODE ACCEPTED - NO FREE PRISONER SLOT FOR " + name
                : "CODE ACCEPTED: " + name;
            AudioManager.Play2D(Snd.UiSelect, 0.9f, 0.8f, AudioCategory.Ui);
            SendChoice();
        }

        const string RussianKeys = "ЙЦУКЕНГШЩЗФЫВАПРОЛДЯЧСМИТЬ", LatinKeys = "QWERTYUIOPASDFGHJKLZXCVBNM";

        /// <summary>Typing into the CODE field (letters / digits, backspace; Enter applies, Esc stops typing).</summary>
        void EditCode(NetSession s)
        {
            foreach (char ch in Input.inputString)
            {
                if (ch == '\b') { if (_code.Length > 0) { _code = _code.Substring(0, _code.Length - 1); AudioManager.Play2D(Snd.UiType, 0.5f, 0.9f, AudioCategory.Ui); } }
                else if (ch == '\n' || ch == '\r') { ApplyCode(s); return; }
                else if (_code.Length < 8 && char.IsLetterOrDigit(ch))
                {
                    char up = char.ToUpperInvariant(ch);
                    int ru = RussianKeys.IndexOf(up);
                    if (ru >= 0) up = LatinKeys[ru];   // a Russian keyboard layout: take the letter printed on the same key
                    if (up >= 127) continue;
                    _code += up;
                    AudioManager.Play2D(Snd.UiType, 0.5f, Random.Range(0.95f, 1.08f), AudioCategory.Ui);
                }
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { _editCode = false; _code = _codeBefore; AudioManager.Play2D(Snd.UiBack, 0.6f, 1f, AudioCategory.Ui); }
        }

        public override void Draw(VhsUI ui, bool input)
        {
            var s = NetSession.Instance;
            if (s == null) return;
            UIStyle.Dim(ui, 0.62f);
            ui.FontOverride = ui.TinyFont;
            UpdatePreview();
            bool host = s.IsHost;
            // readiness comes from the host's roster (except right after we toggled it)
            var me0 = s.LocalPlayer;
            if (!host && me0 != null && Time.unscaledTime - _readySentAt > 1.5f) _ready = me0.Ready;
            // the header uses the big UI font (18 px lines): the address goes under it, the panels under that
            UIStyle.Header(ui, s.Practice ? "PLAY ALONE" : "LOBBY", 6);
            if (host && !s.Practice) ui.Text("YOUR ADDRESS: " + _hostAddresses + "  PORT " + Settings.Port, ui.Width * 0.5f, 6 + VhsFont.Small.LineHeight + 1, VhsUI.Dim, 1, Align.Center);

            // ---- player list (left column)
            float colW = ui.Width * 0.5f - 16;
            float lx = 12, ly = 44;
            int lh = ui.LineHeight() + 2;
            var panel = UIStyle.Panel(ui, new Rect(lx - 4, ly - 4, colW, lh * 5 + 8), 0.55f);
            for (int i = 0; i < s.Players.Count && i < 5; i++)
            {
                var p = s.Players[i];
                bool mine = p.Id == s.LocalId;
                Color c = p.Role == PlayerRole.Omar ? VhsUI.Red : (mine ? VhsUI.White : VhsUI.Dim);
                string ready = p.IsBot ? "" : (p.Ready ? " ●" : " ○");
                string ping = (!host || p.Id == s.LocalId || p.IsBot) ? "" : " " + Mathf.RoundToInt(s.GetRtt(p.Id) * 1000f) + "MS";
                string name = p.Name.Length > 10 ? p.Name.Substring(0, 10) : p.Name;
                ui.Text((mine ? "▶" : " ") + name, lx, ly + i * lh, c);
                string role = p.Role == PlayerRole.Omar ? (p.IsBot ? "OMAR AI" : "OMAR") : p.Role == PlayerRole.Spectator ? "WATCH" : RosterTag(p.Skin);
                ui.Text(role + ready + ping, panel.xMax - 5, ly + i * lh, c, 1, Align.Right);
            }
            float infoY = panel.yMax + 4;
            if (s.FindOmar() == null) { ui.Text("OMAR: THE AI", lx, infoY, new Color(0.65f, 0.2f, 0.2f)); infoY += ui.LineHeight() + 1; }
            int diff = Mathf.Clamp((int)s.Settings.Difficulty, 0, 3);
            if (!host) { ui.Text(DifficultyNames[diff] + " - " + DifficultyLines[diff], lx, infoY, VhsUI.Dim); infoY += ui.LineHeight() + 1; }

            // ---- character (right column)
            float rx = ui.Width * 0.5f + 6, rw = ui.Width * 0.5f - 16;
            float ph = Mathf.Min(110f, ui.Height * 0.42f), pw = ph * 0.75f;
            var pr = new Rect(Mathf.Round(rx + rw * 0.5f - pw * 0.5f), 40, Mathf.Round(pw), Mathf.Round(ph));
            ui.Rect(pr, new Color(0, 0, 0, 0.5f));
            if (_rt != null) ui.Image(_rt, pr, Color.white);
            ui.Frame(pr, new Color(0.5f, 0.5f, 0.5f, 0.6f));
            bool omarChosen = ChosenSkin == CharacterSkin.Omar;
            string cname = omarChosen ? "OMAR" : HumanoidFactory.DisplayName(ChosenSkin);
            ui.Text("◀ " + cname + " ▶", rx + rw * 0.5f, pr.yMax + 3, omarChosen ? VhsUI.Red : VhsUI.White, 1, Align.Center);
            ui.TextWrapped(Describe(ChosenSkin), rx + rw * 0.5f, pr.yMax + 4 + ui.LineHeight(), (int)rw, VhsUI.Dim, 1, Align.Center);
            bool clickLeft = input && ui.Click && ui.Hover(new Rect(pr.x - 30, pr.y, 30, pr.height));
            bool clickRight = input && ui.Click && ui.Hover(new Rect(pr.xMax, pr.y, 30, pr.height));

            // ---- actions (left column, under the list)
            if (!input && _editCode) { _editCode = false; _code = _codeBefore; }
            bool editing = _editCode;
            if (editing) EditCode(s);   // before the menu: the keys typed into the field must not drive it
            var items = new System.Collections.Generic.List<string>();
            var actions = new System.Collections.Generic.List<int>();
            if (!host) { items.Add(_ready ? "READY ●" : "NOT READY ○"); actions.Add(0); }
            if (host)
            {
                items.Add("NIGHT: " + s.Settings.NightMinutes + " MIN"); actions.Add(1);
                items.Add("DIFFICULTY: " + DifficultyNames[diff]); actions.Add(2);
            }
            bool caret = _editCode && (Time.unscaledTime * 2f % 2f) < 1f;
            string codeText = _editCode ? _code + (caret ? "_" : " ") : (_code.Length > 0 ? _code : "____");
            items.Add("CODE: " + codeText); actions.Add(5);
            if (host) { items.Add("START"); actions.Add(3); }
            items.Add("LEAVE"); actions.Add(4);
            float my = Mathf.Max(infoY + 6, panel.yMax + 10);
            var arr = items.ToArray();
            var disabled = new bool[arr.Length];
            string why = "";
            if (host) { bool ok = s.CanStart(out why); disabled[actions.IndexOf(3)] = !ok; }
            int codeRow = actions.IndexOf(5);
            if (_editCode) _sel = codeRow;
            float mcx = lx - 4 + colW * 0.5f;
            int a = ui.Menu(arr, ref _sel, mcx, my, 1, 4, input && !editing && !_editCode, disabled, 8); // LEAVE stands apart
            // the CODE row is drawn as a field: a box round it, bright while typing
            int mlh = ui.LineHeight() + 4;
            int cw = ui.TextWidth("CODE: " + (_code.Length > 4 ? _code : "____") + " ");
            var codeRect = new Rect(Mathf.Round(mcx - cw * 0.5f - 3), my + codeRow * mlh - 2, cw + 6, mlh - 1);
            ui.Frame(codeRect, _editCode ? VhsUI.White : new Color(0.5f, 0.5f, 0.5f, 0.55f));
            if (_editCode && input && ui.Click && !ui.Hover(codeRect)) { _editCode = false; _code = _codeBefore; }
            bool msg = Time.unscaledTime - _codeMsgAt < 3f;
            if (_editCode) UIStyle.Footer(ui, "TYPE THE CODE   ENTER OK   ESC CANCEL");
            else if (msg) ui.Text(_codeMsg, ui.Width * 0.5f, ui.Height - 13, _codeOk ? new Color(0.55f, 0.8f, 0.55f) : new Color(0.8f, 0.35f, 0.3f), 1, Align.Center);
            else if (host && !string.IsNullOrEmpty(why)) ui.Text(why, ui.Width * 0.5f, ui.Height - 13, new Color(0.7f, 0.3f, 0.3f), 1, Align.Center);
            else if (host && _sel < actions.Count && actions[_sel] == 2) UIStyle.Footer(ui, DifficultyLines[diff]);
            else UIStyle.Footer(ui, "A / D  CHANGE CHARACTER");

            if (!input || editing) return;
            int dir = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || clickLeft) dir = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || clickRight) dir = 1;
            int act = a >= 0 ? actions[a] : -1;
            if (dir != 0)
            {
                if (_sel < actions.Count && (actions[_sel] == 1 || actions[_sel] == 2))
                {
                    if (actions[_sel] == 1) s.Settings.NightMinutes = Mathf.Clamp(s.Settings.NightMinutes + dir * 5, 10, 40);
                    else s.Settings.Difficulty = (Difficulty)Mathf.Clamp((int)s.Settings.Difficulty + dir, 0, 3);
                    s.HostSettingsChanged();
                }
                else
                {
                    _choice = (_choice + dir + _choices.Count) % _choices.Count;
                    SendChoice();
                }
                AudioManager.Play2D(Snd.UiMove, 0.6f, 1f, AudioCategory.Ui);
            }
            switch (act)
            {
                case 0: _ready = !_ready; _readySentAt = Time.unscaledTime; SendChoice(); break;
                case 1: s.Settings.NightMinutes = s.Settings.NightMinutes >= 40 ? 10 : s.Settings.NightMinutes + 5; s.HostSettingsChanged(); break;
                case 2: s.Settings.Difficulty = (Difficulty)(((int)s.Settings.Difficulty + 1) % 4); s.HostSettingsChanged(); break;
                case 3:
                    if (!s.HostStartMatch()) UIManager.Instance.Push(new MessageScreen("CAN'T START", s.LastError));
                    break;
                case 4: GameRoot.Instance.LeaveSession(); break;
                case 5: _editCode = true; _codeBefore = _code; break;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) GameRoot.Instance.LeaveSession();

            // keep our displayed choice in sync with what the host accepted
            var me = s.LocalPlayer;
            if (me != null && dir == 0)
            {
                int accepted = ChoiceOf(me);
                if (accepted != _choice && Time.frameCount % 30 == 0) _choice = accepted;
            }
        }
    }
}
