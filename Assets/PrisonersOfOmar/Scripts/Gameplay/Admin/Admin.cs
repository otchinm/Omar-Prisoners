using System.Collections.Generic;

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
    /// Admin access for this machine. Placeholder until the "session" work package implements it
    /// (PBKDF2 password check, host verification, command execution). The UI only uses this API.
    /// </summary>
    public static class Admin
    {
        /// <summary>This machine is an authenticated admin (and accepted by the host when we are a client).</summary>
        public static bool IsAdmin { get; private set; }
        /// <summary>Waiting for the host to accept our proof.</summary>
        public static bool Pending { get; private set; }
        public static string LastError { get; private set; } = "";
        /// <summary>Feedback lines (newest last), e.g. replies from the host or the shelter code.</summary>
        public static readonly List<string> Log = new List<string>();

        /// <summary>Checks the password; true when it is correct. <paramref name="remember"/> keeps the login on this PC.</summary>
        public static bool TryLogin(string password, bool remember) { LastError = "NOT AVAILABLE YET"; return false; }
        public static void Logout() { IsAdmin = false; Pending = false; }
        /// <summary>Runs a command (see AdminArg for the meaning of a / b / f / s).</summary>
        public static void Run(AdminCmd cmd, int a = 0, int b = 0, float f = 0f, string s = null) { }
        /// <summary>State of a Toggle command.</summary>
        public static bool IsOn(AdminCmd cmd) => false;
        /// <summary>Teleport destinations (area / marker names) for TeleportToLocation.</summary>
        public static IList<string> Locations() => new string[0];
        /// <summary>Debug overlays (ShowPlayers / ShowOmar / ShowItems ...). The HUD calls this every frame when IsAdmin.</summary>
        public static void DrawOverlay(UI.VhsUI ui) { }
    }
}
