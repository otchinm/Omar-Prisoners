using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>Inventory (TAB) in the style of the reference: name + index, description, rotating 3D item.</summary>
    public sealed class InventoryScreen : UIScreen
    {
        readonly MatchWorld _w;
        int _slot;
        RenderTexture _rt;
        Transform _root;
        GameObject _model;
        ItemType _modelType = (ItemType)255;
        float _yaw;

        public InventoryScreen(MatchWorld w) { _w = w; _slot = w.Inventory.Selected; }

        public override bool ShowCursor => false;

        public override void OnOpen()
        {
            AudioManager.Play2D(Snd.InventoryOpen, 0.7f, 1f, AudioCategory.Ui);
            _rt = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, name = "InventoryRT" };
            var go = new GameObject("InventoryPreview");
            go.transform.position = new Vector3(0, -900, 0);
            _root = go.transform;
        }

        public override void OnClose()
        {
            AudioManager.Play2D(Snd.InventoryClose, 0.7f, 1f, AudioCategory.Ui);
            if (_root != null) Object.Destroy(_root.gameObject);
            if (_rt != null) { _rt.Release(); Object.Destroy(_rt); }
        }

        public override void Draw(VhsUI ui, bool input)
        {
            var inv = _w.Inventory;
            UIStyle.Dim(ui, 0.86f);
            UIStyle.Header(ui, "INVENTORY", 14);
            float cy = 34;
            ui.Text("◀", ui.Width * 0.5f - 60, cy, VhsUI.White, 1, Align.Center);
            ui.Text("▶", ui.Width * 0.5f + 60, cy, VhsUI.White, 1, Align.Center);

            var it = inv.At(_slot);
            ItemType type = it != null ? it.Type : ItemType.None;
            if (type != _modelType)
            {
                _modelType = type;
                if (_model != null) Object.Destroy(_model);
                _model = null;
                if (type != ItemType.None)
                {
                    try
                    {
                        _model = ItemMeshFactory.Build(type);
                        _model.transform.SetParent(_root, false);
                        GeoUtil.SetLayerRecursive(_model, Layers.Preview);
                    }
                    catch (System.Exception e) { Debug.LogException(e); }
                }
            }
            _yaw += Time.unscaledDeltaTime * 50f;
            if (_model != null) { try { PreviewRenderer.Render(_model.transform, _rt, _yaw, 20f, 1f); } catch { } }

            float textX = Mathf.Max(16, ui.Width * 0.5f - 200);
            float y = 62;
            string title = it != null ? it.Def.Name + " " + (_slot + 1) + "/3" : "EMPTY SLOT " + (_slot + 1) + "/3";
            ui.Text(title, textX, y, VhsUI.White);
            y += ui.LineHeight() + 6;
            if (it != null)
            {
                y += ui.TextWrapped(it.Def.Description, textX, y, 150, VhsUI.White, 1, Align.Left, ui.TinyFont);
                if (it.Def.HasCharge) ui.Text((it.Type == ItemType.Lighter ? "FUEL " : "BATTERY ") + Mathf.RoundToInt(it.Charge * 100f) + "%", textX, y + 6, it.Charge < 0.2f ? VhsUI.Red : VhsUI.Yellow);
            }
            else ui.TextWrapped("NOTHING HERE. I CAN CARRY THREE THINGS.", textX, y, 150, VhsUI.Dim, 1, Align.Left, ui.TinyFont);

            var pr = new Rect(ui.Width * 0.5f - 20, 56, 128, 128);
            if (_model != null) ui.Image(_rt, pr, Color.white);

            UIStyle.Footer(ui, "EQUIP: SPACE   |   DROP: G   |   EXIT: TAB");

            if (!input) return;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) { _slot = (_slot + 2) % 3; AudioManager.Play2D(Snd.InventoryScroll, 0.6f, 1f, AudioCategory.Ui); }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) { _slot = (_slot + 1) % 3; AudioManager.Play2D(Snd.InventoryScroll, 0.6f, 1f, AudioCategory.Ui); }
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                inv.Select(_slot);
                AudioManager.Play2D(Snd.ItemEquip, 0.6f, 1f, AudioCategory.Ui);
                UIManager.Instance.Remove(this);
                return;
            }
            if (Input.GetKeyDown(KeyCode.G) && it != null)
            {
                inv.Select(_slot);
                var pos = _w.LocalAvatar != null ? _w.LocalAvatar.Position : Vector3.zero;
                if (_w.LocalAvatar != null)
                {
                    Vector3 f = _w.LocalAvatar.Forward;
                    if (Physics.Raycast(pos + Vector3.up + f * 0.6f, Vector3.down, out var hit, 3f, Layers.Solid, QueryTriggerInteraction.Ignore)) pos = hit.point;
                }
                _w.SendDrop(it.Id, pos, _w.LocalAvatar != null ? _w.LocalAvatar.State.Yaw : 0f, it.Charge);
            }
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Z))
                UIManager.Instance.Remove(this);
        }
    }

    /// <summary>Fallout shelter keypad: 4 digits.</summary>
    public sealed class KeypadScreen : UIScreen
    {
        readonly MatchWorld _w;
        string _code = "";
        float _resultTimer = -1f;
        bool _ok;
        static readonly string[] Keys = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "CLR", "0", "OK" };

        public KeypadScreen(MatchWorld w) { _w = w; }

        public void Result(bool ok)
        {
            _ok = ok;
            _resultTimer = ok ? 1.2f : 1f;
            AudioManager.Play2D(ok ? Snd.KeypadOk : Snd.KeypadWrong, 0.9f);
            if (!ok) _code = "";
        }

        void Press(string k)
        {
            if (_resultTimer > 0f) return;
            AudioManager.Play2D(Snd.KeypadBeep, 0.7f, 0.9f + _code.Length * 0.05f);
            if (k == "CLR") { _code = ""; return; }
            if (k == "OK") { if (_code.Length == 4) _w.SendKeypad(_code); return; }
            if (_code.Length < 4) _code += k;
            if (_code.Length == 4) _w.SendKeypad(_code);
        }

        public override void Draw(VhsUI ui, bool input)
        {
            UIStyle.Dim(ui, 0.55f);
            var panel = UIStyle.Panel(ui, new Rect(ui.Width * 0.5f - 60, ui.Height * 0.5f - 90, 120, 180), 0.92f);
            ui.Text("SHELTER", panel.center.x, panel.y + 6, VhsUI.Yellow, 1, Align.Center);
            string disp = "";
            for (int i = 0; i < 4; i++) disp += (i < _code.Length ? _code[i].ToString() : "_") + (i < 3 ? " " : "");
            Color dc = _resultTimer > 0f ? (_ok ? VhsUI.Green : VhsUI.Red) : VhsUI.Green;
            var dr = new Rect(panel.x + 12, panel.y + 22, panel.width - 24, 18);
            ui.Rect(dr, new Color(0.05f, 0.1f, 0.05f, 1f));
            ui.Text(_resultTimer > 0f ? (_ok ? "OPEN" : "DENIED") : disp, dr.center.x, dr.y + 3, dc, 1, Align.Center);
            for (int i = 0; i < Keys.Length; i++)
            {
                int cx = i % 3, cyy = i / 3;
                var r = new Rect(panel.x + 12 + cx * 33, panel.y + 48 + cyy * 30, 30, 26);
                bool hover = ui.Hover(r);
                ui.Rect(r, hover ? new Color(0.35f, 0.35f, 0.33f) : new Color(0.2f, 0.2f, 0.19f));
                ui.Frame(r, new Color(0, 0, 0, 0.8f));
                ui.Text(Keys[i], r.center.x, r.y + 7, VhsUI.White, 1, Align.Center);
                if (input && ui.Click && hover) Press(Keys[i]);
            }
            UIStyle.Footer(ui, "TYPE THE CODE   ESC CLOSE");

            if (_resultTimer > 0f)
            {
                _resultTimer -= Time.unscaledDeltaTime;
                if (_resultTimer <= 0f && _ok) { UIManager.Instance.Remove(this); return; }
            }
            if (!input) return;
            foreach (char ch in Input.inputString)
            {
                if (ch >= '0' && ch <= '9') Press(ch.ToString());
                else if (ch == '\b') { if (_code.Length > 0) _code = _code.Substring(0, _code.Length - 1); }
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) Press("OK");
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E) && _code.Length == 0 && Time.frameCount > 2)
                UIManager.Instance.Remove(this);
        }
    }

    /// <summary>Reading a note / writing on a wall.</summary>
    public sealed class NoteScreen : UIScreen
    {
        readonly string _title, _text;
        float _t;
        public NoteScreen(string title, string text) { _title = title; _text = text; }
        public override bool ShowCursor => false;

        public override void OnOpen() => AudioManager.Play2D(Snd.InventoryOpen, 0.6f, 0.9f, AudioCategory.Ui);

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            UIStyle.Dim(ui, 0.6f);
            float pw = Mathf.Min(240, ui.Width - 40), ph = Mathf.Min(200, ui.Height - 40);
            var r = new Rect(ui.Width * 0.5f - pw * 0.5f, ui.Height * 0.5f - ph * 0.5f, pw, ph);
            var paper = UITex.Get("Textures/UI/note_paper");
            if (paper != null) ui.Image(paper, r, Color.white);
            else ui.Rect(r, new Color(0.78f, 0.72f, 0.58f));
            ui.Text(_title, r.center.x, r.y + 10, new Color(0.35f, 0.05f, 0.05f), 1, Align.Center, null, false);
            var nf = ui.Font;
            if (ui.Wrap(_text, (int)pw - 30, 1, nf).Count * nf.LineHeight > ph - 50) nf = ui.TinyFont;
            var lines = ui.Wrap(_text, (int)pw - 30, 1, nf);
            float y = r.y + 30;
            foreach (var l in lines) { ui.Text(l, r.center.x, y, new Color(0.12f, 0.1f, 0.08f), 1, Align.Center, nf, false); y += nf.LineHeight; }
            UIStyle.Footer(ui, "E / ESC  CLOSE");
            if (input && _t > 0.25f && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || ui.Click))
            {
                AudioManager.Play2D(Snd.InventoryClose, 0.6f, 0.9f, AudioCategory.Ui);
                UIManager.Instance.Remove(this);
            }
        }
    }

    /// <summary>ESC menu during a match (the game keeps running).</summary>
    public sealed class PauseScreen : UIScreen
    {
        int _sel;

        public override void Draw(VhsUI ui, bool input)
        {
            UIStyle.Dim(ui, 0.7f);
            var s = NetSession.Instance;
            bool host = s != null && s.IsHost;
            UIStyle.Header(ui, "MENU", ui.Height * 0.16f);
            ui.Text("THE TAPE KEEPS PLAYING. HE IS STILL OUT THERE.", ui.Width * 0.5f, ui.Height * 0.16f + 14, VhsUI.Dim, 1, Align.Center);
            string[] items = host ? new[] { "RESUME", "SETTINGS", "END MATCH FOR EVERYONE", "LEAVE GAME" } : new[] { "RESUME", "SETTINGS", "LEAVE GAME" };
            int a = ui.Menu(items, ref _sel, ui.Width * 0.5f, ui.Height * 0.32f, 1, 3, input);
            var w = MatchWorld.Instance;
            string help = w != null && w.LocalIsOmar
                ? "WASD MOVE  SHIFT RUN  LMB CLEAVER  RMB SCREAM  E OPEN / SEARCH  T TRIPWIRE  G BEAR TRAP  Q SENSE"
                : "WASD MOVE  SHIFT RUN  C CROUCH  E INTERACT  F / LMB USE ITEM  1-3 / WHEEL SELECT  G DROP  TAB INVENTORY";
            ui.TextWrapped(help, ui.Width * 0.5f, ui.Height * 0.72f, ui.Width - 50, VhsUI.Dim, 1, Align.Center, ui.TinyFont);
            if (!input) return;
            if (Input.GetKeyDown(KeyCode.Escape) && Time.frameCount > 1) { UIManager.Instance.Remove(this); return; }
            if (a < 0) return;
            string choice = items[a];
            if (choice == "RESUME") UIManager.Instance.Remove(this);
            else if (choice == "SETTINGS") UIManager.Instance.Push(new SettingsScreen());
            else if (choice == "END MATCH FOR EVERYONE") { UIManager.Instance.Remove(this); w?.Host?.ForceEnd(); }
            else if (choice == "LEAVE GAME") GameRoot.Instance.LeaveSession();
        }
    }

    /// <summary>The ending card: still, title, epilogue (typewriter), everyone's fate.</summary>
    public sealed class EndingScreen : UIScreen
    {
        readonly EndingResult _r;
        float _t;
        bool _musicStarted;
        bool _results;
        int _sel;
        public EndingScreen(EndingResult r) { _r = r; }
        public override bool Opaque => true;

        public override void OnOpen()
        {
            AudioManager.StopAllSfx();
            AudioManager.Play2D(Snd.TapeStop, 0.9f, 1f, AudioCategory.Ui);
            bool good = _r.Escaped > 0;
            var w = MatchWorld.Instance;
            if (w != null && w.LocalIsOmar) good = !good;
            AudioManager.Play2D(good ? Snd.StingEndingGood : Snd.StingEndingBad, 1f, 1f, AudioCategory.Stinger);
            if (_r.OmarWon) AudioManager.Play2D(Snd.ScreamsLong, 0.35f, 1f, AudioCategory.Stinger);
            VhsEffect.Mode = VhsMode.Menu;
            VhsEffect.Interference = 0f; VhsEffect.Damage = 0f; VhsEffect.Hiding = 0f;
            AudioManager.SetMuffle(0f);
            AudioManager.SetDistortion(0f);
            GameInput.SetCursorLocked(false);
        }

        public override void Draw(VhsUI ui, bool input)
        {
            _t += Time.unscaledDeltaTime;
            UIStyle.Dim(ui, 1f);
            if (_t < 1.6f)
            {
                VhsEffect.StaticOverride = 1f - _t / 1.6f * 0.6f;
                UIStyle.Osd(ui, "■ STOP", UIStyle.TapeCounter());
                return;
            }
            VhsEffect.StaticOverride = 0f;
            if (!_musicStarted && _t > 4.5f) { _musicStarted = true; AudioManager.PlayMusic(Snd.MenuTheme, 4f); }

            var img = UITex.Get(_r.Image);
            if (img != null) ui.ImageCover(img, new Color(0.55f, 0.55f, 0.55f, Mathf.Clamp01((_t - 1.6f) / 1.5f)));
            UIStyle.Dim(ui, 0.35f);

            var w = MatchWorld.Instance;
            bool omarView = w != null && w.LocalIsOmar;
            string sub = omarView ? (_r.OmarWon ? "YOU KEPT THEM ALL" : _r.Escaped == 1 ? "ONE OF THEM GOT AWAY" : "THEY GOT AWAY") : "";
            ui.Text("ENDING:", ui.Width * 0.5f, 10, VhsUI.Dim, 1, Align.Center);
            ui.Text(_r.Title, ui.Width * 0.5f, 22, VhsUI.Red, 2, Align.Center, ui.BigFont);
            float y = 22 + ui.BigFont.LineHeight * 2 + 4;
            if (sub.Length > 0) { ui.Text(sub, ui.Width * 0.5f, y, VhsUI.White, 1, Align.Center); y += ui.LineHeight() + 4; }

            string text = _r.Text;
            int shown = Mathf.Clamp(Mathf.FloorToInt((_t - 2.2f) * 38f), 0, text.Length);
            bool textDone = shown >= text.Length;
            bool confirm = input && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || ui.Click);

            if (_results) ui.FontOverride = ui.TinyFont;
            if (!_results)
            {
                // page 1: the epilogue
                // pick the font from the full text so it does not switch while typing
                var ef = ui.Wrap(text, Mathf.Min(ui.Width - 30, 410), 1, ui.Font).Count * ui.LineHeight() > ui.Height - y - 22 ? ui.TinyFont : ui.Font;
                ui.TextWrapped(text.Substring(0, shown), ui.Width * 0.5f, y, Mathf.Min(ui.Width - 30, 410), VhsUI.White, 1, Align.Center, ef);
                if (textDone) UIStyle.Footer(ui, "PRESS ENTER");
                if (confirm) { if (!textDone) _t += 60f; else { _results = true; AudioManager.Play2D(Snd.UiSelect, 0.7f, 1f, AudioCategory.Ui); } }
            }
            else
            {
                // page 2: everyone's fate + what next
                foreach (var e in _r.Entries)
                {
                    string line = e.Name + "  -  " + (e.Role == PlayerRole.Omar ? (_r.OmarWon ? "OMAR. SATISFIED." : "OMAR. ENRAGED.") : Endings.Outcome(e));
                    Color c = e.Role == PlayerRole.Omar ? VhsUI.Red : e.Life == LifeState.Escaped ? VhsUI.Yellow : VhsUI.Dim;
                    ui.Text(line, ui.Width * 0.5f, y, c, 1, Align.Center);
                    y += ui.LineHeight();
                }
                int min = Mathf.FloorToInt(_r.Duration / 60f), sec = Mathf.FloorToInt(_r.Duration % 60f);
                ui.Text("TAPE LENGTH " + min + ":" + sec.ToString("00"), ui.Width * 0.5f, y + 4, VhsUI.Dim, 1, Align.Center);

                var s = NetSession.Instance;
                bool host = s != null && s.IsHost;
                string[] items = host ? new[] { "BACK TO THE LOBBY", "MAIN MENU" } : new[] { "MAIN MENU" };
                float my = ui.Height - items.Length * (ui.LineHeight() + 2) - 14;
                int a = ui.Menu(items, ref _sel, ui.Width * 0.5f, my, 1, 2, input);
                if (!host) ui.Text("WAITING FOR THE HOST...", ui.Width * 0.5f, my - ui.LineHeight() - 4, VhsUI.Dim, 1, Align.Center);
                if (a >= 0)
                {
                    if (items[a] == "BACK TO THE LOBBY") s.HostReturnToLobby();
                    else GameRoot.Instance.LeaveSession();
                }
            }
            UIStyle.Osd(ui, "PLAY ▶", UIStyle.TapeCounter());
        }
    }
}
