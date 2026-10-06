using System.Collections.Generic;
using PrisonersOfOmar.Net;

namespace PrisonersOfOmar.Gameplay
{
    // =====================================================================================
    //  (iteration 2) Admin panel contract. Only the owner of the admin password can use it.
    //  UI ("ui" work package) draws the panel from AdminCmds.All; the "session" work package
    //  implements authentication (AdminAuth) and executes the commands (host side for anything
    //  that changes shared state, locally for view-only toggles).
    // =====================================================================================

    public enum AdminCategory : byte { Self, Players, Items, Omar, Grandma, World, Objectives, Events, Match, Debug, Lobby }

    /// <summary>What argument a command takes (the UI shows the matching picker).</summary>
    public enum AdminArg : byte
    {
        None = 0,
        Toggle,     // on / off (Admin.IsOn shows the state)
        Player,     // a = player id
        Item,       // a = ItemType
        PlayerItem, // a = player id, b = ItemType
        Event,      // a = WorldEventKind
        Ending,     // a = EndingId
        Location,   // s = location name (from Admin.Locations())
        Number,     // f = value (seconds / minutes / multiplier, see Hint)
        PlayerRole, // a = player id, b = PlayerRole
        Difficulty, // a = Difficulty
    }

    public enum AdminCmd : byte
    {
        None = 0,
        // ---- self
        GodMode, Invisible, Noclip, Speed, InfiniteStamina, InfiniteLight, Fullbright, NoVhs, FreeCamera,
        HealSelf, TeleportToLocation, TeleportToPlayer, TeleportToCrosshair,
        // ---- players
        BringPlayer, FreePlayer, CagePlayer, KillPlayer, InjurePlayer, HealPlayer, KickPlayer, GiveItemToPlayer,
        // ---- items
        GiveItem, SpawnItemAtCrosshair, RefillLights, ClearInventory,
        // ---- Omar
        FreezeOmar, StunOmar, OmarBlind, OmarDeaf, TeleportOmarToMe, TeleportOmarAway, OmarScream, OmarHuntPlayer,
        OmarSleep, OmarChopMeat,
        // ---- grandmother
        GrandmaRoam, GrandmaReturn, GrandmaKill, GrandmaScream, GrandmaDisable,
        // ---- world
        OpenAllDoors, CloseAllDoors, UnlockAllDoors, OpenAllCages, DisarmAllTraps, SpawnTripwire, SpawnBearTrap,
        PowerOff, PowerOn, TriggerAlarm,
        // ---- objectives
        ShowShelterCode, CutGate, ReadyCar, InsertFuse, CallRadio, PrimeDrums, ExplodeDrums, OpenShelter,
        // ---- events / match
        TriggerEvent, AddMinutes, NearDawn, PauseClock, EndMatch, ReturnToLobby,
        // ---- debug overlays (local)
        ShowPlayers, ShowOmar, ShowGrandma, ShowItems, ShowTraps, ShowNav, ShowNetStats,
        // ---- lobby
        ForceStart, SetRole, SetDifficulty,
        // ---- self (added later: kept at the end so the byte values above never change)
        SeeMyself,
        // ---- iteration 3 puzzles (Docs/ITERATION3_PLAN.md)
        ShowCodes, SolveSockets, OpenCodeLocks, UnlockDrawers, WatchTape, ShowPuzzles,
    }

    public sealed class AdminCmdInfo
    {
        public AdminCmd Cmd;
        public AdminCategory Category;
        public string Label;
        public AdminArg Arg;
        /// <summary>Runs on this machine only (view / own movement); everything else goes to the host.</summary>
        public bool Local;
        /// <summary>Only meaningful in the lobby (otherwise only during a match).</summary>
        public bool Lobby;
        /// <summary>Default number for Number arguments / short hint shown next to the label.</summary>
        public float Default;
        public string Hint;
    }

    public static class AdminCmds
    {
        public static readonly List<AdminCmdInfo> All = new List<AdminCmdInfo>();
        static readonly Dictionary<AdminCmd, AdminCmdInfo> _byCmd = new Dictionary<AdminCmd, AdminCmdInfo>();

        static AdminCmds()
        {
            var S = AdminCategory.Self; var P = AdminCategory.Players; var I = AdminCategory.Items; var O = AdminCategory.Omar;
            var G = AdminCategory.Grandma; var W = AdminCategory.World; var J = AdminCategory.Objectives; var E = AdminCategory.Events;
            var M = AdminCategory.Match; var D = AdminCategory.Debug; var L = AdminCategory.Lobby;
            Add(AdminCmd.GodMode, S, "GOD MODE", AdminArg.Toggle, hint: "OMAR CAN'T HURT OR CAGE YOU");
            Add(AdminCmd.Invisible, S, "INVISIBLE", AdminArg.Toggle, hint: "NOBODY SEES OR HEARS YOU");
            Add(AdminCmd.Noclip, S, "NOCLIP / FLY", AdminArg.Toggle, local: true, hint: "SPACE UP, CTRL DOWN");
            Add(AdminCmd.Speed, S, "SPEED", AdminArg.Number, local: true, def: 2f, hint: "x1 x2 x4");
            Add(AdminCmd.InfiniteStamina, S, "INFINITE STAMINA", AdminArg.Toggle, local: true);
            Add(AdminCmd.InfiniteLight, S, "INFINITE LIGHT", AdminArg.Toggle, local: true);
            Add(AdminCmd.Fullbright, S, "FULLBRIGHT", AdminArg.Toggle, local: true);
            Add(AdminCmd.NoVhs, S, "VHS OFF", AdminArg.Toggle, local: true);
            Add(AdminCmd.FreeCamera, S, "FREE CAMERA", AdminArg.Toggle, local: true);
            Add(AdminCmd.SeeMyself, S, "SEE MYSELF (FREE CAMERA)", AdminArg.Toggle, local: true, hint: "YOUR OWN BODY SHOWS IN THE FREE CAMERA");
            Add(AdminCmd.HealSelf, S, "HEAL ME", AdminArg.None);
            Add(AdminCmd.TeleportToLocation, S, "TELEPORT TO...", AdminArg.Location, local: true);
            Add(AdminCmd.TeleportToPlayer, S, "TELEPORT TO PLAYER", AdminArg.Player, local: true);
            Add(AdminCmd.TeleportToCrosshair, S, "TELEPORT TO CROSSHAIR", AdminArg.None, local: true);

            Add(AdminCmd.BringPlayer, P, "BRING PLAYER", AdminArg.Player);
            Add(AdminCmd.FreePlayer, P, "FREE / REVIVE", AdminArg.Player);
            Add(AdminCmd.CagePlayer, P, "LOCK IN A CAGE", AdminArg.Player);
            Add(AdminCmd.KillPlayer, P, "KILL", AdminArg.Player);
            Add(AdminCmd.InjurePlayer, P, "INJURE", AdminArg.Player);
            Add(AdminCmd.HealPlayer, P, "HEAL", AdminArg.Player);
            Add(AdminCmd.KickPlayer, P, "KICK", AdminArg.Player);
            Add(AdminCmd.GiveItemToPlayer, P, "GIVE ITEM", AdminArg.PlayerItem);

            Add(AdminCmd.GiveItem, I, "GIVE ME", AdminArg.Item);
            Add(AdminCmd.SpawnItemAtCrosshair, I, "SPAWN AT CROSSHAIR", AdminArg.Item);
            Add(AdminCmd.RefillLights, I, "REFILL LIGHTER / FLASHLIGHT", AdminArg.None);
            Add(AdminCmd.ClearInventory, I, "EMPTY MY POCKETS", AdminArg.None);

            Add(AdminCmd.FreezeOmar, O, "FREEZE", AdminArg.Toggle);
            Add(AdminCmd.StunOmar, O, "STUN", AdminArg.Number, def: 10f, hint: "SECONDS");
            Add(AdminCmd.OmarBlind, O, "BLIND", AdminArg.Toggle);
            Add(AdminCmd.OmarDeaf, O, "DEAF", AdminArg.Toggle);
            Add(AdminCmd.TeleportOmarToMe, O, "BRING HIM TO ME", AdminArg.None);
            Add(AdminCmd.TeleportOmarAway, O, "SEND HIM TO THE FURNACE", AdminArg.None);
            Add(AdminCmd.OmarScream, O, "SCREAM", AdminArg.None);
            Add(AdminCmd.OmarHuntPlayer, O, "HUNT PLAYER", AdminArg.Player);
            Add(AdminCmd.OmarSleep, O, "SLEEP", AdminArg.Number, def: 60f, hint: "SECONDS");
            Add(AdminCmd.OmarChopMeat, O, "GO CHOP MEAT", AdminArg.None);

            Add(AdminCmd.GrandmaRoam, G, "START ROAMING", AdminArg.None);
            Add(AdminCmd.GrandmaReturn, G, "BACK TO HER TV", AdminArg.None);
            Add(AdminCmd.GrandmaKill, G, "KILL", AdminArg.None);
            Add(AdminCmd.GrandmaScream, G, "SCREAM", AdminArg.None);
            Add(AdminCmd.GrandmaDisable, G, "DISABLE", AdminArg.Toggle);

            Add(AdminCmd.OpenAllDoors, W, "OPEN ALL DOORS", AdminArg.None);
            Add(AdminCmd.CloseAllDoors, W, "CLOSE ALL DOORS", AdminArg.None);
            Add(AdminCmd.UnlockAllDoors, W, "UNLOCK + UNBOARD ALL DOORS", AdminArg.None);
            Add(AdminCmd.OpenAllCages, W, "OPEN ALL CAGES", AdminArg.None);
            Add(AdminCmd.DisarmAllTraps, W, "DISARM ALL TRAPS", AdminArg.None);
            Add(AdminCmd.SpawnTripwire, W, "TRIPWIRE AT CROSSHAIR", AdminArg.None);
            Add(AdminCmd.SpawnBearTrap, W, "BEAR TRAP AT CROSSHAIR", AdminArg.None);
            Add(AdminCmd.PowerOff, W, "POWER CUT", AdminArg.None);
            Add(AdminCmd.PowerOn, W, "POWER ON", AdminArg.None);
            Add(AdminCmd.TriggerAlarm, W, "SOUND THE ALARM", AdminArg.None);

            Add(AdminCmd.ShowShelterCode, J, "SHOW SHELTER CODE", AdminArg.None);
            Add(AdminCmd.CutGate, J, "CUT THE GATE PADLOCK", AdminArg.None);
            Add(AdminCmd.ReadyCar, J, "CAR: FUEL + BATTERY", AdminArg.None);
            Add(AdminCmd.InsertFuse, J, "INSERT THE FUSE", AdminArg.None);
            Add(AdminCmd.CallRadio, J, "CALL FOR HELP (RADIO)", AdminArg.None);
            Add(AdminCmd.PrimeDrums, J, "POUR FUEL ON THE DRUMS", AdminArg.None);
            Add(AdminCmd.ExplodeDrums, J, "BLOW UP THE DRUMS", AdminArg.None);
            Add(AdminCmd.OpenShelter, J, "OPEN THE SHELTER", AdminArg.None);
            Add(AdminCmd.ShowCodes, J, "SHOW ALL CODES", AdminArg.None, hint: "SHELTER, PADLOCKS, CLOCK...");
            Add(AdminCmd.SolveSockets, J, "COMPLETE ALL ITEM SOCKETS", AdminArg.None, hint: "VCR, SCALES, CLOCK...");
            Add(AdminCmd.OpenCodeLocks, J, "OPEN ALL CODE LOCKS", AdminArg.None);
            Add(AdminCmd.UnlockDrawers, J, "UNLOCK ALL PADLOCKED DRAWERS", AdminArg.None);
            Add(AdminCmd.WatchTape, J, "WATCH THE TAPE", AdminArg.None, local: true, hint: "PLAYS IT HERE, NOTHING CHANGES");

            Add(AdminCmd.TriggerEvent, E, "TRIGGER EVENT", AdminArg.Event);
            Add(AdminCmd.AddMinutes, M, "SKIP TIME", AdminArg.Number, def: 10f, hint: "MINUTES OF MATCH TIME");
            Add(AdminCmd.NearDawn, M, "JUMP TO 5:59 AM", AdminArg.None);
            Add(AdminCmd.PauseClock, M, "PAUSE THE NIGHT", AdminArg.Toggle);
            Add(AdminCmd.EndMatch, M, "END WITH ENDING...", AdminArg.Ending);
            Add(AdminCmd.ReturnToLobby, M, "BACK TO THE LOBBY", AdminArg.None);

            Add(AdminCmd.ShowPlayers, D, "SHOW PLAYERS", AdminArg.Toggle, local: true);
            Add(AdminCmd.ShowOmar, D, "SHOW OMAR", AdminArg.Toggle, local: true);
            Add(AdminCmd.ShowGrandma, D, "SHOW GRANDMOTHER", AdminArg.Toggle, local: true);
            Add(AdminCmd.ShowItems, D, "SHOW ITEMS", AdminArg.Toggle, local: true);
            Add(AdminCmd.ShowTraps, D, "SHOW TRAPS", AdminArg.Toggle, local: true);
            Add(AdminCmd.ShowPuzzles, D, "SHOW PUZZLES + CODES", AdminArg.Toggle, local: true);
            Add(AdminCmd.ShowNav, D, "SHOW AI PATHS", AdminArg.Toggle, local: true);
            Add(AdminCmd.ShowNetStats, D, "NET / FPS STATS", AdminArg.Toggle, local: true);

            Add(AdminCmd.ForceStart, L, "FORCE START", AdminArg.None, lobby: true);
            Add(AdminCmd.SetRole, L, "SET ROLE", AdminArg.PlayerRole, lobby: true);
            Add(AdminCmd.SetDifficulty, L, "SET DIFFICULTY", AdminArg.Difficulty, lobby: true);
        }

        static void Add(AdminCmd c, AdminCategory cat, string label, AdminArg arg, bool local = false, bool lobby = false, float def = 0f, string hint = null)
        {
            var i = new AdminCmdInfo { Cmd = c, Category = cat, Label = label, Arg = arg, Local = local, Lobby = lobby, Default = def, Hint = hint };
            All.Add(i);
            _byCmd[c] = i;
        }

        public static AdminCmdInfo Get(AdminCmd c) => _byCmd.TryGetValue(c, out var i) ? i : null;
    }

    /// <summary>
    /// Admin access for this machine. The password is checked locally (PBKDF2, <see cref="AdminAuth"/>); a client then
    /// proves itself to the host with the derived key (Msg.AdminAuthReq) and the host keeps admin rights per player id.
    /// Commands that change shared state run on the host (Msg.AdminCmdReq); view / own-movement ones run here.
    /// </summary>
    public static class Admin
    {
        const string PrefKey = "admin_dk";
        static byte[] _dk;            // derived key of a verified password (null = not logged in)
        static bool _accepted;        // a client: the host accepted our proof
        static NetSession _session;
        static readonly Dictionary<AdminCmd, bool> _toggles = new Dictionary<AdminCmd, bool>();
        /// <summary>Host: players whose proof was accepted.</summary>
        internal static readonly HashSet<int> HostAdmins = new HashSet<int>();
        static float _savedBrightness = -1f;
        static VhsPreset _savedPreset;
        static bool _vhsSaved;

        /// <summary>This machine is an authenticated admin (and accepted by the host when we are a client).</summary>
        public static bool IsAdmin => _dk != null && (_session == null || _session.State == SessionState.Offline || _session.IsHost || _accepted);
        /// <summary>Waiting for the host to accept our proof.</summary>
        public static bool Pending { get; private set; }
        /// <summary>The password was entered on this PC (even if a host hasn't accepted it yet).</summary>
        public static bool LoggedIn => _dk != null;
        public static string LastError { get; private set; } = "";
        /// <summary>Feedback lines (newest last), e.g. replies from the host or the shelter code.</summary>
        public static readonly List<string> Log = new List<string>();

        // ------------------------------------------------------------------ session plumbing

        /// <summary>Called by NetSession.Awake: registers the admin messages (lobby and match) and restores a remembered login.</summary>
        public static void Attach(NetSession s)
        {
            _session = s;
            s.On(Msg.AdminAuthReq, OnAuthReq);
            s.On(Msg.AdminAuthResult, OnAuthResult);
            s.On(Msg.AdminCmdReq, OnCmdReq);
            s.On(Msg.AdminLog, (id, r) => AddLog(r.ReadString()));
            s.StateChanged += OnStateChanged;
            s.RosterChanged += OnRosterChanged;
            try
            {
                string hex = UnityEngine.PlayerPrefs.GetString(PrefKey, "");
                var dk = AdminAuth.FromHex(hex);
                if (AdminAuth.Verify(dk)) _dk = dk;
                else if (hex.Length > 0) UnityEngine.PlayerPrefs.DeleteKey(PrefKey);
            }
            catch { }
        }

        static void OnStateChanged(SessionState st)
        {
            if (st == SessionState.Offline || st == SessionState.Connecting)
            {
                _accepted = false; Pending = false;
                _authSentFor = -1;   // rejoining (often with the same player id) must prove ourselves again
                HostAdmins.Clear();
                ResetLocal();
            }
            if (_session != null && _session.IsHost && _dk != null) HostAdmins.Add(_session.LocalId);
        }

        static int _authSentFor = -1;

        static void OnRosterChanged()
        {
            // a client: prove ourselves once we know our id
            if (_session == null || _session.IsHost || _dk == null || _accepted) return;
            if (_session.LocalId < 0 || _authSentFor == _session.LocalId) return;
            _authSentFor = _session.LocalId;
            SendAuth();
        }

        static void SendAuth()
        {
            if (_session == null || _dk == null) return;
            Pending = true;
            var w = _session.Begin(Msg.AdminAuthReq);
            w.WriteByteArray(_dk);
            _session.SendToHost(NetChannel.Reliable);
        }

        static void OnAuthReq(int sender, NetReader r)
        {
            var dk = r.ReadByteArray();
            if (_session == null || !_session.IsHost) return;
            bool ok = AdminAuth.Verify(dk);
            if (ok) HostAdmins.Add(sender); else HostAdmins.Remove(sender);
            var w = _session.Begin(Msg.AdminAuthResult);
            w.WriteBool(ok);
            _session.SendTo(sender, NetChannel.Reliable);
        }

        static void OnAuthResult(int sender, NetReader r)
        {
            bool ok = r.ReadBool();
            Pending = false;
            _accepted = ok;
            if (!ok) LastError = "THE HOST REFUSED THE ADMIN KEY";
            AddLog(ok ? "ADMIN ACCESS GRANTED" : "ADMIN ACCESS REFUSED");
        }

        public static void AddLogPublic(string line) => AddLog(line);

        internal static void AddLog(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            Log.Add(line);
            while (Log.Count > 14) Log.RemoveAt(0);
        }

        /// <summary>Host: send a feedback line to an admin.</summary>
        internal static void HostLog(int to, string text)
        {
            if (_session == null || !_session.IsHost) return;
            var w = _session.Begin(Msg.AdminLog);
            w.WriteString(text);
            _session.SendTo(to, NetChannel.Reliable);
        }

        // ------------------------------------------------------------------ API used by the panel

        /// <summary>Checks the password; true when it is correct. <paramref name="remember"/> keeps the login on this PC.</summary>
        public static bool TryLogin(string password, bool remember)
        {
            byte[] dk;
            try { dk = AdminAuth.DeriveKey(password); }
            catch (System.Exception e) { LastError = "ERROR: " + e.Message; return false; }
            if (!AdminAuth.Verify(dk)) { LastError = "WRONG PASSWORD"; return false; }
            _dk = dk;
            LastError = "";
            try
            {
                if (remember) UnityEngine.PlayerPrefs.SetString(PrefKey, AdminAuth.ToHex(dk));
                else UnityEngine.PlayerPrefs.DeleteKey(PrefKey);
                UnityEngine.PlayerPrefs.Save();
            }
            catch { }
            if (_session != null)
            {
                if (_session.IsHost) HostAdmins.Add(_session.LocalId);
                else if (_session.State != SessionState.Offline && _session.LocalId >= 0) { _authSentFor = _session.LocalId; SendAuth(); }
            }
            AddLog("LOGGED IN");
            return true;
        }

        public static void Logout()
        {
            _dk = null; _accepted = false; Pending = false;
            ResetLocal();
            _toggles.Clear();
            try { UnityEngine.PlayerPrefs.DeleteKey(PrefKey); UnityEngine.PlayerPrefs.Save(); } catch { }
            if (_session != null && _session.IsHost) HostAdmins.Remove(_session.LocalId);
        }

        /// <summary>State of a Toggle command.</summary>
        public static bool IsOn(AdminCmd cmd) => _toggles.TryGetValue(cmd, out var v) && v;

        /// <summary>Runs a command (see AdminArg for the meaning of a / b / f / s).</summary>
        public static void Run(AdminCmd cmd, int a = 0, int b = 0, float f = 0f, string s = null)
        {
            if (!IsAdmin) { LastError = Pending ? "WAITING FOR THE HOST..." : "NOT LOGGED IN"; return; }
            var info = AdminCmds.Get(cmd);
            if (info == null) return;
            if (info.Arg == AdminArg.Toggle)
            {
                bool on = !IsOn(cmd);
                _toggles[cmd] = on;
                a = on ? 1 : 0;
            }
            if (info.Local) { RunLocal(cmd, a, b, f, s); return; }
            if (_session == null || _session.State == SessionState.Offline) { AddLog("START OR JOIN A GAME FIRST"); return; }
            var w = _session.Begin(Msg.AdminCmdReq);
            w.WriteByte((byte)cmd);
            w.WriteInt(a);
            w.WriteInt(b);
            w.WriteFloat(f);
            w.WriteString(s ?? "");
            w.WriteVector3(CrosshairPoint(out var normal));
            w.WriteVector3(normal);
            _session.SendToHost(NetChannel.Reliable);
        }

        /// <summary>Where the admin is looking (for spawns / traps / teleports).</summary>
        static UnityEngine.Vector3 CrosshairPoint(out UnityEngine.Vector3 normal)
        {
            normal = UnityEngine.Vector3.up;
            var rig = PrisonersOfOmar.Rendering.PsxCameraRig.Instance;
            if (rig == null) return UnityEngine.Vector3.zero;
            var t = rig.transform;
            if (UnityEngine.Physics.Raycast(t.position, t.forward, out var hit, 60f, Layers.Solid, UnityEngine.QueryTriggerInteraction.Ignore))
            {
                normal = hit.normal;
                return hit.point;
            }
            return t.position + t.forward * 4f;
        }

        // ------------------------------------------------------------------ host side

        static void OnCmdReq(int sender, NetReader r)
        {
            var cmd = (AdminCmd)r.ReadByte();
            int a = r.ReadInt(), b = r.ReadInt();
            float f = r.ReadFloat();
            string s = r.ReadString();
            var point = r.ReadVector3();
            var normal = r.ReadVector3();
            if (_session == null || !_session.IsHost || !HostAdmins.Contains(sender)) return;
            var info = AdminCmds.Get(cmd);
            if (info == null) return;
            var w = MatchWorld.Instance;
            if (info.Lobby)
            {
                if (_session.State != SessionState.Lobby) { HostLog(sender, "ONLY IN THE LOBBY"); return; }
                RunLobby(sender, cmd, a, b);
                return;
            }
            if (cmd == AdminCmd.KickPlayer) { _session.AdminKick(a, "KICKED BY THE ADMIN"); HostLog(sender, "KICKED"); return; }
            if (w == null || w.Host == null) { HostLog(sender, "ONLY DURING A MATCH"); return; }
            try { w.Host.OnAdminCmd(sender, cmd, a, b, f, s, point, normal); }
            catch (System.Exception e) { UnityEngine.Debug.LogException(e); HostLog(sender, "FAILED: " + cmd); }
        }

        static void RunLobby(int sender, AdminCmd cmd, int a, int b)
        {
            switch (cmd)
            {
                case AdminCmd.ForceStart:
                    if (!_session.AdminForceStart(out string why)) HostLog(sender, why);
                    break;
                case AdminCmd.SetRole:
                    _session.AdminSetRole(a, (PlayerRole)b);
                    break;
                case AdminCmd.SetDifficulty:
                    Settings.Difficulty = (Difficulty)UnityEngine.Mathf.Clamp(a, 0, 3);
                    _session.HostSettingsChanged();
                    HostLog(sender, "DIFFICULTY: " + Settings.Difficulty.ToString().ToUpperInvariant());
                    break;
            }
        }

        // ------------------------------------------------------------------ local commands

        static void RunLocal(AdminCmd cmd, int a, int b, float f, string s)
        {
            var w = MatchWorld.Instance;
            switch (cmd)
            {
                case AdminCmd.Noclip: AdminState.Noclip = a != 0; break;
                case AdminCmd.Speed: AdminState.SpeedMultiplier = UnityEngine.Mathf.Clamp(f <= 0f ? 1f : f, 0.25f, 8f); AddLog("SPEED x" + AdminState.SpeedMultiplier.ToString("0.#")); break;
                case AdminCmd.InfiniteStamina: AdminState.InfiniteStamina = a != 0; break;
                case AdminCmd.InfiniteLight: AdminState.InfiniteLight = a != 0; break;
                case AdminCmd.Fullbright:
                    if (a != 0) { if (_savedBrightness < 0f) _savedBrightness = PrisonersOfOmar.Rendering.PsxEnvironment.Brightness; PrisonersOfOmar.Rendering.PsxEnvironment.Brightness = 4f; }
                    else if (_savedBrightness >= 0f) { PrisonersOfOmar.Rendering.PsxEnvironment.Brightness = _savedBrightness; _savedBrightness = -1f; }
                    break;
                case AdminCmd.NoVhs:
                    if (a != 0) { if (!_vhsSaved) { _savedPreset = PrisonersOfOmar.Rendering.VhsEffect.Preset; _vhsSaved = true; } PrisonersOfOmar.Rendering.VhsEffect.Preset = VhsPreset.Off; }
                    else if (_vhsSaved) { PrisonersOfOmar.Rendering.VhsEffect.Preset = _savedPreset; _vhsSaved = false; }
                    break;
                case AdminCmd.FreeCamera: AdminFreeCam.Set(a != 0); break;
                case AdminCmd.SeeMyself:
                    AdminFreeCam.ShowSelf = a != 0;
                    if (a != 0 && !AdminFreeCam.Active)
                    {
                        // turn the free camera on, in front of us and looking back at us
                        AdminFreeCam.Set(true);
                        _toggles[AdminCmd.FreeCamera] = AdminFreeCam.Active;
                        var me = w != null ? w.LocalAvatar : null;
                        if (me != null) AdminFreeCam.Frame(me.Position + UnityEngine.Vector3.up * 1.15f, me.Forward);
                        if (!AdminFreeCam.Active) AddLog("NO CAMERA YET: START A MATCH");
                    }
                    break;
                case AdminCmd.TeleportToLocation:
                    if (w != null && TryLocation(w, s, out var lp)) TeleportSelf(w, lp); else AddLog("UNKNOWN PLACE");
                    break;
                case AdminCmd.TeleportToPlayer:
                    {
                        var av = w != null ? w.AvatarOf(a) : null;
                        if (av != null) TeleportSelf(w, av.Position + av.Forward * -1.2f); else AddLog("NO SUCH PLAYER");
                        break;
                    }
                case AdminCmd.TeleportToCrosshair:
                    if (w != null) { var p = CrosshairPoint(out var n); TeleportSelf(w, p + n * 0.5f); }
                    break;
                case AdminCmd.WatchTape:
                    if (w != null && w.TapeShots.Length > 0) UI.UIManager.Instance?.Push(new UI.TapeScreen(w.TapeShots)); else AddLog("NO TAPE ON THIS MAP");
                    break;
            }
        }

        static void TeleportSelf(MatchWorld w, UnityEngine.Vector3 p)
        {
            if (UnityEngine.Physics.Raycast(p + UnityEngine.Vector3.up * 1.2f, UnityEngine.Vector3.down, out var hit, 4f, Layers.Solid, UnityEngine.QueryTriggerInteraction.Ignore)) p = hit.point;
            w.TeleportLocal(p);
        }

        /// <summary>Undo every local effect (match torn down / logout).</summary>
        public static void ResetLocal()
        {
            AdminState.Noclip = AdminState.InfiniteStamina = AdminState.InfiniteLight = false;
            AdminState.SpeedMultiplier = 1f;
            if (_savedBrightness >= 0f) { PrisonersOfOmar.Rendering.PsxEnvironment.Brightness = _savedBrightness; _savedBrightness = -1f; }
            if (_vhsSaved) { PrisonersOfOmar.Rendering.VhsEffect.Preset = _savedPreset; _vhsSaved = false; }
            AdminFreeCam.Set(false);
            AdminFreeCam.ShowSelf = false;
            foreach (var c in new List<AdminCmd>(_toggles.Keys))
            {
                var i = AdminCmds.Get(c);
                if (i != null && i.Local && c != AdminCmd.ShowPlayers && c != AdminCmd.ShowOmar && c != AdminCmd.ShowGrandma &&
                    c != AdminCmd.ShowItems && c != AdminCmd.ShowTraps && c != AdminCmd.ShowNav && c != AdminCmd.ShowNetStats &&
                    c != AdminCmd.ShowPuzzles) _toggles[c] = false;
            }
        }

        /// <summary>A match ended: host-side toggles start fresh next time.</summary>
        public static void OnMatchEnded()
        {
            foreach (var c in new List<AdminCmd>(_toggles.Keys))
            {
                var i = AdminCmds.Get(c);
                if (i != null && !i.Local) _toggles[c] = false;
            }
            ResetLocal();
        }

        /// <summary>Teleport destinations (area / marker names) for TeleportToLocation.</summary>
        public static IList<string> Locations()
        {
            var list = new List<string>();
            var w = MatchWorld.Instance;
            if (w == null || w.Map == null) return list;
            list.Add("OMAR'S SPAWN");
            foreach (var area in w.Map.AreaOrder) if (!list.Contains(area)) list.Add(area);
            foreach (var kv in w.Map.Markers) if (!list.Contains(kv.Key)) list.Add(kv.Key);
            // (iteration 3) every puzzle: item sockets, code locks, padlocked drawers
            foreach (var p in PuzzlePoints(w)) if (!list.Contains(p.Key)) list.Add(p.Key);
            return list;
        }

        /// <summary>(iteration 3) Teleport names of the puzzles and the point to look at.</summary>
        static List<KeyValuePair<string, UnityEngine.Vector3>> PuzzlePoints(MatchWorld w)
        {
            var list = new List<KeyValuePair<string, UnityEngine.Vector3>>();
            foreach (var s in w.Sockets) list.Add(new KeyValuePair<string, UnityEngine.Vector3>("PUZZLE: " + s.Info.Name.ToUpperInvariant(), s.InteractPoint));
            foreach (var lk in w.CodeLocks)
                if (!lk.Embedded) list.Add(new KeyValuePair<string, UnityEngine.Vector3>("PUZZLE: " + lk.Info.Name.ToUpperInvariant(), lk.InteractPoint));
            foreach (var d in w.Drawers)
                if (d.Lock != DrawerLockKind.None)
                    list.Add(new KeyValuePair<string, UnityEngine.Vector3>("PADLOCK " + d.Index + ": " + NoteTexts.PlaceName(w.Map.AreaAt(d.InteractPoint)) + (d.Lock == DrawerLockKind.Code ? " (CODE)" : " (KEY)"), d.InteractPoint));
            return list;
        }

        /// <summary>A floor spot in front of a puzzle: halfway from the closest nav node of its room, or that node.</summary>
        static UnityEngine.Vector3 InFrontOf(MatchWorld w, UnityEngine.Vector3 target)
        {
            var nav = w.Map.Nav;
            var best = target;
            if (nav != null)
            {
                float bd = float.MaxValue;
                foreach (int n in nav.NodesInArea(w.Map.AreaAt(target)))
                {
                    float d = (nav.Nodes[n] - target).sqrMagnitude;
                    if (d < bd) { bd = d; best = nav.Nodes[n]; }
                }
                if (bd == float.MaxValue) { int n = nav.Nearest(target); if (n >= 0) best = nav.Nodes[n]; }
            }
            var mid = UnityEngine.Vector3.Lerp(best, new UnityEngine.Vector3(target.x, best.y, target.z), 0.55f);
            return UnityEngine.Physics.CheckSphere(mid + UnityEngine.Vector3.up * 1f, 0.3f, Layers.Solid, UnityEngine.QueryTriggerInteraction.Ignore) ? best : mid;
        }

        static bool TryLocation(MatchWorld w, string name, out UnityEngine.Vector3 p)
        {
            p = UnityEngine.Vector3.zero;
            if (string.IsNullOrEmpty(name) || w.Map == null) return false;
            if (name == "OMAR'S SPAWN") { p = w.Map.OmarSpawn.position; return true; }
            if (w.Map.AreaBounds.TryGetValue(name, out var bnd))
            {
                var c = bnd.center; c.y = bnd.min.y + 0.2f;
                p = c;
                // land on the floor near the middle of the area (nav node when there is one)
                var nav = w.Map.Nav;
                if (nav != null)
                {
                    var nodes = nav.NodesInArea(name);
                    if (nodes.Count > 0) p = nav.Nodes[nodes[0]];
                }
                return true;
            }
            if (name.StartsWith("PUZZLE: ") || name.StartsWith("PADLOCK "))
                foreach (var pp in PuzzlePoints(w))
                    if (pp.Key == name) { p = InFrontOf(w, pp.Value); return true; }
            if (w.Map.Markers.TryGetValue(name, out var t) && t != null)
            {
                p = t.position + t.forward * 1.2f;
                p.y = t.position.y - 1f;
                return true;
            }
            return false;
        }

        /// <summary>Debug overlays (ShowPlayers / ShowOmar / ShowItems ...). The HUD calls this every frame when IsAdmin.</summary>
        public static void DrawOverlay(UI.VhsUI ui) => AdminOverlay.Draw(ui);
    }
}
