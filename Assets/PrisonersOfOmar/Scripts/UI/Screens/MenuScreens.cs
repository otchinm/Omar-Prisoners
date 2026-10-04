using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>VCR power-on: tape insert, warning card, the cover, then the menu.</summary>
    public sealed class BootScreen : UIScreen
    {
        float _t;
        bool _soundDone;
        public override bool Opaque => true;
        public override bool ShowCursor => false;

        public override void OnOpen()
        {
            AudioManager.Play2D(Snd.TapeInsert, 0.9f, 1f, AudioCategory.Ui);
            VhsEffect.Mode = VhsMode.Menu;
        }

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            bool skip = input && _t > 0.4f && (Input.anyKeyDown || ui.Click);
            UIStyle.Dim(ui, 1f);
            if (_t < 1.4f)
            {
                VhsEffect.StaticOverride = 1f - _t / 1.4f;
                UIStyle.Osd(ui, "PLAY ▶", UIStyle.TapeCounter());
            }
            else if (_t < 6.5f)
            {
                VhsEffect.StaticOverride = 0f;
                if (!_soundDone) { _soundDone = true; AudioManager.Play2D(Snd.TapePlay, 0.8f, 1f, AudioCategory.Ui); }
                float y = ui.Height * 0.2f;
                ui.Text("WARNING", ui.Width * 0.5f, y, VhsUI.Red, 2, Align.Center, ui.BigFont);
                y += ui.BigFont.LineHeight * 2 + 10;
                ui.TextWrapped("THIS TAPE CONTAINS SCENES OF EXTREME VIOLENCE, STROBING LIGHTS, LOUD NOISES AND DEPICTIONS OF CAPTIVITY. " +
                               "IT WAS RECOVERED FROM THE BASE OF THE SECOND CLASS. VIEWER DISCRETION IS ADVISED.",
                               ui.Width * 0.5f, y, Mathf.Min(ui.Width - 60, 340), VhsUI.White, 1, Align.Center);
                if (_t > 3.8f)
                {
                    var cover = UITex.Get("Textures/UI/vhs_cover");
                    if (cover != null)
                    {
                        float a = Mathf.Clamp01((_t - 3.8f) / 0.6f);
                        UIStyle.Dim(ui, a);
                        ui.ImageFit(cover, new Rect(20, 14, ui.Width - 40, ui.Height - 28), new Color(1, 1, 1, a));
                    }
                }
                UIStyle.Osd(ui, "PLAY ▶", UIStyle.TapeCounter());
            }
            else skip = true;

            if (skip)
            {
                VhsEffect.StaticOverride = 0f;
                VhsEffect.TriggerGlitch(0.8f, 0.4f);
                UIManager.Instance.Replace(new MainMenuScreen());
            }
        }
    }

    public sealed class MainMenuScreen : UIScreen
    {
        int _sel;
        static readonly string[] Items = { "HOST GAME", "JOIN GAME", "PLAY ALONE (VS AI OMAR)", "HOW TO PLAY", "SETTINGS", "CREDITS", "QUIT" };

        public override void OnOpen()
        {
            VhsEffect.Mode = VhsMode.Menu;
            AudioManager.PlayMusic(Snd.MenuTheme, 2.5f);
            GameInput.SetCursorLocked(false);
        }

        public override void Draw(VhsUI ui, bool input)
        {
            UIStyle.Dim(ui, 0.25f);
            float bottom = UIStyle.Title(ui, 6, Mathf.Min(ui.Width - 40, 300), ui.Height * 0.32f);
            float menuY = Mathf.Max(bottom + 4, ui.Height * 0.36f);
            float avail = ui.Height - VhsFont.Tiny.LineHeight - 8 - menuY;
            if (avail < Items.Length * ui.LineHeight()) ui.FontOverride = ui.TinyFont;
            int spacing = Mathf.Clamp(Mathf.FloorToInt(avail / Items.Length) - ui.LineHeight(), 0, 4);
            int a = ui.Menu(Items, ref _sel, ui.Width * 0.5f, menuY, 1, spacing, input);
            UIStyle.Footer(ui, "MMXXVI  THE BASE OF THE SECOND CLASS  -  v" + GameInfo.Version);
            UIStyle.Osd(ui, "PLAY ▶", UIStyle.TapeCounter());
            switch (a)
            {
                case 0: GameRoot.Instance.HostGame(false); break;
                case 1: UIManager.Instance.Push(new JoinScreen()); break;
                case 2: GameRoot.Instance.HostGame(true); break;
                case 3: UIManager.Instance.Push(new HowToPlayScreen()); break;
                case 4: UIManager.Instance.Push(new SettingsScreen()); break;
                case 5: UIManager.Instance.Push(new CreditsScreen()); break;
                case 6: GameRoot.Instance.QuitGame(); break;
            }
        }
    }

    public sealed class JoinScreen : UIScreen
    {
        int _field; // 0 name, 1 address, 2 port, 3 connect, 4.. lan, last back
        string _name, _address, _port;
        float _dots;

        public override bool Opaque => false;

        public override void OnOpen()
        {
            _name = Settings.PlayerName;
            _address = Settings.LastAddress;
            _port = Settings.Port.ToString();
            try { NetSession.Instance.Discovery.StartListening(); } catch { }
        }

        public override void OnClose()
        {
            try { NetSession.Instance.Discovery.StopListening(); } catch { }
        }

        public override void Draw(VhsUI ui, bool input)
        {
            ui.FontOverride = ui.TinyFont;
            UIStyle.Dim(ui, 0.7f);
            var s = NetSession.Instance;
            UIStyle.Header(ui, "JOIN GAME", 14);
            float x = ui.Width * 0.5f - 150, w = 300, y = 40;
            int lh = ui.LineHeight() + 6;
            var lan = s.Discovery.Sessions;
            int count = 4 + lan.Count + 1;
            if (input)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.Tab)) { _field = (_field + 1) % count; AudioManager.Play2D(Snd.UiMove, 0.5f, 1f, AudioCategory.Ui); }
                if (Input.GetKeyDown(KeyCode.UpArrow)) { _field = (_field + count - 1) % count; AudioManager.Play2D(Snd.UiMove, 0.5f, 1f, AudioCategory.Ui); }
            }
            bool connecting = s.State == SessionState.Connecting;

            ui.Text("NAME", x, y, _field == 0 ? VhsUI.White : VhsUI.Dim);
            if (input && ui.Click && ui.Hover(new Rect(x + 80, y - 2, w - 80, lh))) _field = 0;
            ui.TextField(ref _name, x + 80, y, (int)w - 80, input && _field == 0 && !connecting, 14, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_- .");
            y += lh;
            ui.Text("ADDRESS", x, y, _field == 1 ? VhsUI.White : VhsUI.Dim);
            if (input && ui.Click && ui.Hover(new Rect(x + 80, y - 2, w - 80, lh))) _field = 1;
            bool enter = ui.TextField(ref _address, x + 80, y, (int)w - 80, input && _field == 1 && !connecting, 64, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.-:_");
            y += lh;
            ui.Text("PORT", x, y, _field == 2 ? VhsUI.White : VhsUI.Dim);
            if (input && ui.Click && ui.Hover(new Rect(x + 80, y - 2, 80, lh))) _field = 2;
            enter |= ui.TextField(ref _port, x + 80, y, 80, input && _field == 2 && !connecting, 5, "0123456789");
            y += lh + 4;

            string connectLabel = connecting ? "CONNECTING" + new string('.', 1 + (int)(_dots += Time.unscaledDeltaTime * 2f) % 3) : (_field == 3 ? "▶ CONNECT ◀" : "CONNECT");
            ui.Text(connectLabel, ui.Width * 0.5f, y, _field == 3 ? VhsUI.White : VhsUI.Dim, 1, Align.Center);
            var cr = new Rect(ui.Width * 0.5f - 60, y - 2, 120, lh);
            bool doConnect = input && !connecting && ((ui.Click && ui.Hover(cr)) || (_field == 3 && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))) || (enter && _field < 3));
            y += lh + 6;

            ui.Text("GAMES ON YOUR NETWORK:", x, y, VhsUI.Dim);
            y += lh;
            if (lan.Count == 0) { ui.Text("(SEARCHING...)", x + 10, y, new Color(0.4f, 0.4f, 0.4f)); y += lh; }
            for (int i = 0; i < lan.Count; i++)
            {
                var g = lan[i];
                bool sel = _field == 4 + i;
                string label = (sel ? "▶ " : "  ") + g.HostName + "  " + g.Players + "/" + g.MaxPlayers + (g.InLobby ? "" : "  (IN MATCH)") + "  " + g.Address;
                ui.Text(label, x + 4, y, sel ? VhsUI.White : VhsUI.Dim);
                var r = new Rect(x, y - 2, w, lh);
                if (input && ui.Hover(r) && ui.MouseMoved) _field = 4 + i;
                if (input && !connecting && g.InLobby && ((ui.Click && ui.Hover(r)) || (sel && Input.GetKeyDown(KeyCode.Return))))
                {
                    _address = g.Address; _port = g.Port.ToString();
                    doConnect = true;
                }
                y += lh;
            }
            y += 4;
            bool backSel = _field == count - 1;
            ui.Text(backSel ? "▶ BACK ◀" : "BACK", ui.Width * 0.5f, y, backSel ? VhsUI.White : VhsUI.Dim, 1, Align.Center);
            var br = new Rect(ui.Width * 0.5f - 40, y - 2, 80, lh);
            bool back = input && ((ui.Click && ui.Hover(br)) || (backSel && Input.GetKeyDown(KeyCode.Return)) || Input.GetKeyDown(KeyCode.Escape));

            if (!string.IsNullOrEmpty(s.LastError) && !connecting)
                ui.Text(s.LastError, ui.Width * 0.5f, ui.Height - 26, VhsUI.Red, 1, Align.Center);

            if (doConnect)
            {
                int port;
                if (!int.TryParse(_port, out port) || port < 1 || port > 65535) port = GameInfo.DefaultPort;
                Settings.PlayerName = Settings.CleanName(_name);
                Settings.LastAddress = _address.Trim().ToLowerInvariant();
                Settings.Port = port;
                Settings.Save();
                s.LastError = "";
                AudioManager.Play2D(Snd.UiSelect, 0.8f, 1f, AudioCategory.Ui);
                GameRoot.Instance.JoinGame(Settings.LastAddress, port);
            }
            if (back)
            {
                AudioManager.Play2D(Snd.UiBack, 0.7f, 1f, AudioCategory.Ui);
                // leaving while connecting ends the session, which already rebuilds the stack as [main menu]
                if (connecting) s.Leave();
                if (UIManager.Instance.Top == this) UIManager.Instance.Pop();
            }
        }
    }

    public sealed class SettingsScreen : UIScreen
    {
        int _sel;
        string _name;
        const int Rows = 13;
        const int NameRow = 11;
        static readonly string[] PresetNames = { "DEFAULT", "CLEAN", "WORN TAPE", "CAMCORDER", "BLACK & WHITE", "SEPIA", "OFF" };

        public override void OnOpen() { _name = Settings.PlayerName; }
        public override void OnClose()
        {
            Settings.PlayerName = Settings.CleanName(_name);
            Settings.Save();
            Settings.ApplyDisplay();
        }

        public override void Draw(VhsUI ui, bool input)
        {
            ui.FontOverride = ui.TinyFont;
            UIStyle.Dim(ui, 0.78f);
            UIStyle.Header(ui, "SETTINGS", 6);
            float x = ui.Width * 0.5f - 150, w = 300, y = 28;
            int lh = Mathf.Max(ui.LineHeight(), Mathf.Min(ui.LineHeight() + 3, Mathf.FloorToInt((ui.Height - 28 - 18) / 14f)));
            if (input)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow) || (Input.GetKeyDown(KeyCode.S) && _sel != NameRow)) { _sel = (_sel + 1) % Rows; AudioManager.Play2D(Snd.UiMove, 0.5f, 1f, AudioCategory.Ui); }
                if (Input.GetKeyDown(KeyCode.UpArrow) || (Input.GetKeyDown(KeyCode.W) && _sel != NameRow)) { _sel = (_sel + Rows - 1) % Rows; AudioManager.Play2D(Snd.UiMove, 0.5f, 1f, AudioCategory.Ui); }
            }
            for (int i = 0; i < Rows; i++)
            {
                var r = new Rect(x, y + i * lh - 2, w, lh);
                if (input && ui.Hover(r) && ui.MouseMoved) _sel = i;
            }
            int d;
            d = ui.Stepper("MASTER VOLUME", Pct(Settings.MasterVolume), x, y, w, _sel == 0, input); Settings.MasterVolume = Mathf.Clamp01(Settings.MasterVolume + d * 0.1f); y += lh;
            d = ui.Stepper("MUSIC VOLUME", Pct(Settings.MusicVolume), x, y, w, _sel == 1, input); Settings.MusicVolume = Mathf.Clamp01(Settings.MusicVolume + d * 0.1f); y += lh;
            d = ui.Stepper("EFFECTS VOLUME", Pct(Settings.SfxVolume), x, y, w, _sel == 2, input); Settings.SfxVolume = Mathf.Clamp01(Settings.SfxVolume + d * 0.1f); y += lh;
            d = ui.Stepper("MOUSE SENSITIVITY", Settings.MouseSensitivity.ToString("0.0"), x, y, w, _sel == 3, input); Settings.MouseSensitivity = Mathf.Clamp(Settings.MouseSensitivity + d * 0.2f, 0.2f, 8f); y += lh;
            d = ui.Stepper("INVERT MOUSE Y", Settings.InvertY ? "ON" : "OFF", x, y, w, _sel == 4, input); if (d != 0) Settings.InvertY = !Settings.InvertY; y += lh;
            d = ui.Stepper("FIELD OF VIEW", Mathf.RoundToInt(Settings.FieldOfView).ToString(), x, y, w, _sel == 5, input); Settings.FieldOfView = Mathf.Clamp(Settings.FieldOfView + d * 5f, 55f, 95f); y += lh;
            int ri = System.Array.IndexOf(Settings.InternalHeights, Settings.InternalHeight);
            d = ui.Stepper("RESOLUTION", Settings.InternalHeight + "P", x, y, w, _sel == 6, input);
            if (d != 0) { ri = Mathf.Clamp(ri + d, 0, Settings.InternalHeights.Length - 1); Settings.InternalHeight = Settings.InternalHeights[ri]; Settings.ApplyDisplay(); }
            y += lh;
            int pi = Mathf.Clamp((int)Settings.VhsPreset, 0, PresetNames.Length - 1);
            d = ui.Stepper("VHS FILTER", PresetNames[pi], x, y, w, _sel == 7, input);
            if (d != 0) { Settings.VhsPreset = (VhsPreset)((pi + d + PresetNames.Length) % PresetNames.Length); VhsEffect.Preset = Settings.VhsPreset; }
            y += lh;
            d = ui.Stepper("VHS DISTORTION", Pct(Settings.VhsIntensity), x, y, w, _sel == 8, input);
            if (d != 0) { Settings.VhsIntensity = Mathf.Clamp(Settings.VhsIntensity + d * 0.25f, 0.25f, 1.5f); VhsEffect.UserIntensity = Settings.VhsIntensity; }
            y += lh;
            d = ui.Stepper("FULLSCREEN", Settings.Fullscreen ? "ON" : "OFF", x, y, w, _sel == 9, input); if (d != 0) { Settings.Fullscreen = !Settings.Fullscreen; Settings.ApplyDisplay(); } y += lh;
            d = ui.Stepper("VSYNC", Settings.VSync ? "ON" : "OFF", x, y, w, _sel == 10, input); if (d != 0) { Settings.VSync = !Settings.VSync; Settings.ApplyDisplay(); } y += lh;
            ui.Text((_sel == NameRow ? "▶ " : "  ") + "NAME", x, y, _sel == NameRow ? VhsUI.White : VhsUI.Dim);
            ui.TextField(ref _name, x + 120, y, (int)w - 120, input && _sel == NameRow, 14, "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_- .");
            y += lh;
            bool backSel = _sel == 12;
            ui.Text(backSel ? "▶ BACK ◀" : "BACK", ui.Width * 0.5f, y, backSel ? VhsUI.White : VhsUI.Dim, 1, Align.Center);
            bool back = input && (Input.GetKeyDown(KeyCode.Escape) || (backSel && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
                || (ui.Click && ui.Hover(new Rect(ui.Width * 0.5f - 40, y - 2, 80, lh))));
            UIStyle.Footer(ui, "◀ ▶ CHANGE   ESC BACK");
            if (back)
            {
                AudioManager.Play2D(Snd.UiBack, 0.7f, 1f, AudioCategory.Ui);
                UIManager.Instance.Remove(this);
            }
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }

    public sealed class HowToPlayScreen : UIScreen
    {
        int _page;
        static readonly string[] Titles = { "PRISONERS", "OMAR" };
        static readonly string[] Pages =
        {
            "GET OUT BEFORE 6 AM. THE GATE, THE CAR, THE SHELTER, THE RADIO OR THE FUEL DRUMS.\n\n" +
            "WASD  MOVE        SHIFT  RUN        C  CROUCH\n" +
            "E  INTERACT / HIDE        HOLD LMB  DRAG A DOOR\n" +
            "F  USE ITEM        1-3  SELECT        G  DROP        TAB  ITEMS",
            "NOBODY LEAVES BEFORE DAWN.\n\n" +
            "WASD  MOVE        SHIFT  RUN        LMB  CLEAVER        RMB  SCREAM\n" +
            "E  UNLOCK / SEARCH / SMASH        Q  SENSE\n" +
            "T  TRIPWIRE        G  BEAR TRAP",
        };

        public override void Draw(VhsUI ui, bool input)
        {
            ui.FontOverride = ui.TinyFont;
            UIStyle.Dim(ui, 0.85f);
            UIStyle.Header(ui, "HOW TO PLAY - " + Titles[_page], 14);
            ui.TextWrapped(Pages[_page], ui.Width * 0.5f, 40, Mathf.Min(ui.Width - 30, 400), VhsUI.White, 1, Align.Center);
            UIStyle.Footer(ui, "◀ " + (_page + 1) + "/" + Pages.Length + " ▶      ESC BACK");
            if (!input) return;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || ui.Click) { _page = (_page + 1) % Pages.Length; AudioManager.Play2D(Snd.UiMove, 0.6f, 1f, AudioCategory.Ui); }
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { _page = (_page + Pages.Length - 1) % Pages.Length; AudioManager.Play2D(Snd.UiMove, 0.6f, 1f, AudioCategory.Ui); }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return)) { AudioManager.Play2D(Snd.UiBack, 0.7f, 1f, AudioCategory.Ui); UIManager.Instance.Remove(this); }
        }
    }

    public sealed class CreditsScreen : UIScreen
    {
        public override void Draw(VhsUI ui, bool input)
        {
            ui.FontOverride = ui.TinyFont;
            UIStyle.Dim(ui, 0.82f);
            UIStyle.Header(ui, "CREDITS", 14);
            string text =
                "THE PRISONERS OF OMAR\n\n" +
                "A LOW-POLY VHS HORROR GAME, INSPIRED BY THE WORK OF PUPPET COMBO.\n\n" +
                "MENU MUSIC AND OMAR'S VOICE: SUPPLIED BY THE PROJECT OWNER.\n" +
                "PHOTO SOURCES: SCIKIT-IMAGE SAMPLE DATA (CC0), POLY HAVEN HDRIS (CC0).\n" +
                "FONTS: VT323, ANTON (SIL OPEN FONT LICENSE).\n" +
                "EVERYTHING ELSE GENERATED FOR THIS GAME.\n\n" +
                "MADE WITH UNITY.";
            ui.TextWrapped(text, ui.Width * 0.5f, 40, Mathf.Min(ui.Width - 40, 380), VhsUI.White, 1, Align.Center);
            UIStyle.Footer(ui, "PRESS ANY KEY");
            if (input && (Input.anyKeyDown || ui.Click)) { AudioManager.Play2D(Snd.UiBack, 0.7f, 1f, AudioCategory.Ui); UIManager.Instance.Remove(this); }
        }
    }

    public sealed class MessageScreen : UIScreen
    {
        readonly string _title, _text;
        float _t;
        public MessageScreen(string title, string text) { _title = title; _text = text; }

        public override void OnOpen() => AudioManager.Play2D(Snd.UiError, 0.7f, 1f, AudioCategory.Ui);

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            UIStyle.Dim(ui, 0.6f);
            var r = UIStyle.Panel(ui, new Rect(ui.Width * 0.5f - 150, ui.Height * 0.5f - 45, 300, 90), 0.9f);
            ui.Text(_title, r.center.x, r.y + 8, VhsUI.Red, 1, Align.Center);
            ui.TextWrapped(_text, r.center.x, r.y + 28, (int)r.width - 20, VhsUI.White, 1, Align.Center);
            ui.Text("OK", r.center.x, r.yMax - 16, VhsUI.White, 1, Align.Center);
            if (input && _t > 0.3f && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || ui.Click))
                UIManager.Instance.Remove(this);
        }
    }

    public sealed class LoadingScreen : UIScreen
    {
        public override bool Opaque => true;
        public override bool ShowCursor => false;

        public override void Draw(VhsUI ui, bool input)
        {
            UIStyle.Dim(ui, 1f);
            VhsEffect.StaticOverride = 0.55f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f);
            var s = NetSession.Instance;
            string dots = new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3);
            ui.Text("TRACKING" + dots, ui.Width * 0.5f, ui.Height * 0.42f, VhsUI.White, ui.FitScale("TRACKING...", 2, ui.Width - 16, ui.BigFont), Align.Center, ui.BigFont);
            ui.Text(s != null && s.State == SessionState.Loading ? "WAITING FOR THE OTHER TAPES" : "LOADING THE BASE OF THE SECOND CLASS", ui.Width * 0.5f, ui.Height * 0.62f, VhsUI.Dim, 1, Align.Center);
            UIStyle.Osd(ui, "▶▶ FF", UIStyle.TapeCounter());
        }

        public override void OnClose() { VhsEffect.StaticOverride = 0f; }
    }
}
