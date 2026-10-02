using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>
    /// Lobby: up to 4 prisoners + Omar. Pick a character (◀ ▶), ready up; the host sets the night length,
    /// AI Omar and starts. Shows a rotating 3D preview of the chosen character.
    /// </summary>
    public sealed class LobbyScreen : UIScreen
    {
        int _sel;
        int _choice; // 0..3 prisoners, 4 omar
        bool _ready;
        RenderTexture _rt;
        Transform _previewRoot;
        HumanoidRig _preview;
        int _previewChoice = -1;
        float _yaw;
        string _hostAddresses;

        static readonly string[] Descriptions =
        {
            "NUMBER 19. TRACK STAR. BIG MOUTH, BIGGER LUNGS. HE THOUGHT IT WAS A PRANK.",
            "SHE CAME LOOKING FOR HER BROTHER. SHE FOUND HIS JACKET IN THE PENS.",
            "QUIET. OBSERVANT. SHE COUNTS THE STEPS BETWEEN THE CAGES EVERY NIGHT.",
            "HE READ ABOUT THE BASE ON A FORUM. HE WANTED PROOF. NOW HE IS THE PROOF.",
            "OMAR ALIBUTAEV. MASTER OF THE BASE OF THE SECOND CLASS. NOBODY LEAVES.",
        };

        public override void OnOpen()
        {
            VhsEffect.Mode = VhsMode.Menu;
            AudioManager.PlayMusic(Snd.MenuTheme, 2.5f);
            GameInput.SetCursorLocked(false);
            var me = NetSession.Instance.LocalPlayer;
            if (me != null) _choice = me.Role == PlayerRole.Omar ? 4 : Mathf.Clamp((int)me.Skin, 0, 3);
            _ready = me != null && me.Ready;
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
            if (_previewChoice != _choice)
            {
                _previewChoice = _choice;
                if (_preview != null) Object.Destroy(_preview.gameObject);
                try
                {
                    _preview = HumanoidFactory.Build(_choice == 4 ? CharacterSkin.Omar : (CharacterSkin)_choice, _previewRoot, Layers.Preview);
                }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            _yaw += Time.unscaledDeltaTime * 35f;
            if (_preview != null)
            {
                try { PreviewRenderer.Render(_preview.transform, _rt, 180f + Mathf.Sin(_yaw * Mathf.Deg2Rad) * 40f, 8f, 1f); } catch { }
            }
        }

        void SendChoice()
        {
            var role = _choice == 4 ? PlayerRole.Omar : PlayerRole.Prisoner;
            var skin = _choice == 4 ? CharacterSkin.Omar : (CharacterSkin)_choice;
            NetSession.Instance.RequestLobby(role, skin, _ready);
        }

        public override void Draw(VhsUI ui, bool input)
        {
            var s = NetSession.Instance;
            if (s == null) return;
            UIStyle.Dim(ui, 0.62f);
            ui.FontOverride = ui.TinyFont;
            UpdatePreview();
            bool host = s.IsHost;
            UIStyle.Header(ui, s.Practice ? "PLAY ALONE" : "LOBBY", 8);
            if (host && !s.Practice) ui.Text("YOUR ADDRESS: " + _hostAddresses + "  PORT " + Settings.Port, ui.Width * 0.5f, 8 + ui.LineHeight(), VhsUI.Dim, 1, Align.Center);

            // ---- player list (left column)
            float colW = ui.Width * 0.5f - 16;
            float lx = 12, ly = 36;
            int lh = ui.LineHeight() + 1;
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
                string role = p.Role == PlayerRole.Omar ? (p.IsBot ? "OMAR AI" : "OMAR") : p.Role == PlayerRole.Spectator ? "WATCH" : "#" + ((int)p.Skin + 1);
                ui.Text(role + ready + ping, panel.xMax - 5, ly + i * lh, c, 1, Align.Right);
            }
            if (s.FindOmar() == null)
                ui.Text(s.Settings.AiOmar ? "OMAR: THE AI" : "OMAR: NOBODY", lx, panel.yMax + 3, new Color(0.65f, 0.2f, 0.2f));

            // ---- character (right column)
            float rx = ui.Width * 0.5f + 6, rw = ui.Width * 0.5f - 16;
            float ph = Mathf.Min(110f, ui.Height * 0.42f), pw = ph * 0.75f;
            var pr = new Rect(Mathf.Round(rx + rw * 0.5f - pw * 0.5f), 34, Mathf.Round(pw), Mathf.Round(ph));
            ui.Rect(pr, new Color(0, 0, 0, 0.5f));
            if (_rt != null) ui.Image(_rt, pr, Color.white);
            ui.Frame(pr, new Color(0.5f, 0.5f, 0.5f, 0.6f));
            string cname = _choice == 4 ? "OMAR" : HumanoidFactory.DisplayName((CharacterSkin)_choice);
            ui.Text("◀ " + cname + " ▶", rx + rw * 0.5f, pr.yMax + 3, _choice == 4 ? VhsUI.Red : VhsUI.White, 1, Align.Center);
            ui.TextWrapped(Descriptions[_choice], rx + rw * 0.5f, pr.yMax + 4 + ui.LineHeight(), (int)rw, VhsUI.Dim, 1, Align.Center);
            bool clickLeft = input && ui.Click && ui.Hover(new Rect(pr.x - 30, pr.y, 30, pr.height));
            bool clickRight = input && ui.Click && ui.Hover(new Rect(pr.xMax, pr.y, 30, pr.height));

            // ---- actions (left column, under the list)
            var items = new System.Collections.Generic.List<string>();
            var actions = new System.Collections.Generic.List<int>();
            if (!host) { items.Add(_ready ? "READY ●" : "NOT READY ○"); actions.Add(0); }
            if (host)
            {
                items.Add("NIGHT: " + s.Settings.NightMinutes + " MIN"); actions.Add(1);
                items.Add("AI OMAR: " + (s.Settings.AiOmar ? "ON" : "OFF")); actions.Add(2);
                items.Add("START"); actions.Add(3);
            }
            items.Add("LEAVE"); actions.Add(4);
            float my = panel.yMax + ui.LineHeight() + 8;
            var arr = items.ToArray();
            var disabled = new bool[arr.Length];
            string why = "";
            if (host) { bool ok = s.CanStart(out why); disabled[actions.IndexOf(3)] = !ok; }
            int a = ui.Menu(arr, ref _sel, lx - 4 + colW * 0.5f, my, 1, 1, input, disabled);
            if (host && !string.IsNullOrEmpty(why)) ui.Text(why, ui.Width * 0.5f, ui.Height - 13, new Color(0.7f, 0.3f, 0.3f), 1, Align.Center);
            else UIStyle.Footer(ui, "A / D  CHANGE CHARACTER");

            if (!input) return;
            int dir = 0;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || clickLeft) dir = -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || clickRight) dir = 1;
            int act = a >= 0 ? actions[a] : -1;
            if (dir != 0)
            {
                if (_sel < actions.Count && (actions[_sel] == 1 || actions[_sel] == 2))
                {
                    if (actions[_sel] == 1) s.Settings.NightMinutes = Mathf.Clamp(s.Settings.NightMinutes + dir * 5, 10, 40);
                    else s.Settings.AiOmar = !s.Settings.AiOmar;
                    s.HostSettingsChanged();
                }
                else
                {
                    _choice = (_choice + dir + 5) % 5;
                    SendChoice();
                }
                AudioManager.Play2D(Snd.UiMove, 0.6f, 1f, AudioCategory.Ui);
            }
            switch (act)
            {
                case 0: _ready = !_ready; SendChoice(); break;
                case 1: s.Settings.NightMinutes = s.Settings.NightMinutes >= 40 ? 10 : s.Settings.NightMinutes + 5; s.HostSettingsChanged(); break;
                case 2: s.Settings.AiOmar = !s.Settings.AiOmar; s.HostSettingsChanged(); break;
                case 3:
                    if (!s.HostStartMatch()) UIManager.Instance.Push(new MessageScreen("CAN'T START", s.LastError));
                    break;
                case 4: GameRoot.Instance.LeaveSession(); break;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) GameRoot.Instance.LeaveSession();

            // keep our displayed choice in sync with what the host accepted
            var me = s.LocalPlayer;
            if (me != null && dir == 0)
            {
                int accepted = me.Role == PlayerRole.Omar ? 4 : (int)me.Skin;
                if (accepted != _choice && Time.frameCount % 30 == 0) _choice = accepted;
            }
        }
    }
}
