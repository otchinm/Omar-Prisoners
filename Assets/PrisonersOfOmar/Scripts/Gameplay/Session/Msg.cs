namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// First byte of every game message. "Req" messages go client -> host (the host validates);
    /// everything else is host -> clients (the host also handles its own copy locally).
    /// </summary>
    public enum Msg : byte
    {
        // ---- lobby / session
        Roster = 1,          // host: full player list + settings
        LobbyReq = 2,        // client: role, skin, ready
        StartMatch = 3,      // host: seed, settings, roster
        LoadedReq = 4,       // client: map built
        MatchBegin = 5,      // host: everyone loaded, clock starts
        ReturnToLobby = 6,   // host
        Kick = 7,            // host -> client (reason text)

        // ---- avatars
        AvatarStateReq = 20, // owner -> host (unreliable)
        Snapshot = 21,       // host -> all (unreliable): match time + all avatars
        ActionReq = 22,      // owner -> host: one-shot animation
        Action = 23,         // host -> all
        NoiseReq = 24,       // prisoner -> host: noise at position
        Noise = 25,          // host -> Omar's owner
        PlayerStatus = 26,   // host -> all: health/status of one player

        // ---- items
        PickupReq = 30,
        ItemPicked = 31,
        DropReq = 32,
        ItemDropped = 33,
        UseReq = 34,         // generic "use item / interact with target" request
        ItemConsumed = 35,
        ItemCharge = 36,
        ThrowReq = 37,
        BottleImpact = 38,   // thrower -> host
        BottleShatter = 39,  // host -> all

        // ---- world interactables
        DoorReq = 40,
        DoorState = 41,
        HideReq = 42,
        HideState = 43,
        CageState = 44,
        StruggleReq = 45,
        TrapTriggerReq = 46,
        TrapState = 47,
        TrapPlaceReq = 48,
        TrapSpawned = 49,

        // ---- Omar
        AttackReq = 60,      // Omar owner -> host: hit target
        AttackFx = 61,       // host -> all: swing / hit effects
        DetectReq = 62,      // Omar owner -> host: spotted / lost a prisoner
        ChaseState = 63,     // host -> all
        ScreamReq = 64,
        Scream = 65,
        SearchReq = 66,      // Omar searches a hiding spot
        OmarStun = 67,
        SenseReq = 68,
        TrapCharges = 69,    // host -> Omar's owner: authoritative tripwire / bear trap counts

        // ---- objectives / events / endings
        ObjectiveState = 80, // host -> all: full objective struct
        Escaped = 81,
        EscapeReq = 82,
        KeypadReq = 83,      // (unused since iteration 3: the shelter keypad is a code lock, CodeReq / CodeResult)
        KeypadResult = 84,   // (unused)
        WorldEvent = 85,
        MatchEnd = 86,
        Message = 87,        // host -> all: HUD message
        CarDrive = 88,       // host -> all: car escape cinematic

        // ================== iteration 2: each work package may ONLY add values inside its own range ==================
        // ---- "player" package (doors, hiding, local player): 100..119
        DoorGrabReq = 100,   // client -> host: grab / release a door leaf
        DoorGrab = 101,      // host -> all: who holds which door
        DoorDragReq = 102,   // grabber -> host (unreliable): door angle + angular velocity while dragging
        DoorAngles = 103,    // host -> all (unreliable): angles of the doors that are moving
        DoorFx = 104,        // host -> all: slam / latch / bump sounds + noise
        BedLift = 105,       // host -> all: Omar tips a bed up (hiding spot index, up/down)
        ChargeReq = 106,     // owner -> host: current fuel / battery of a held light (throttled)
        DrawerReq = 107,     // client -> host: open / close a drawer
        DrawerState = 108,   // host -> all: a drawer slides open / shut

        // ---- "world" package (Omar behaviour, grandmother, kitchen, revolver): 120..139
        GrandmaState = 120,  // host -> all: mode changes (watching / roaming / screaming / dead)
        GrandmaSnap = 121,   // host -> all (unreliable): position + yaw while she moves
        ShootReq = 122,      // client -> host: fired the revolver (origin, direction)
        ShotFx = 123,        // host -> all: muzzle flash + bang at the shooter, impact / hit result
        KitchenState = 124,  // host -> all: Omar starts / stops chopping meat
        ChopReq = 125,       // human Omar -> host: chop at the butcher table
        OmarHitReq = 126,    // prisoner -> host: hit the distracted Omar from behind (bottle / crowbar)
        OmarRush = 127,      // host -> Omar's owner: someone is under the bed he checks (stage 0 = hold still, 1 = flipped: surge)

        // ---- "session" package (spawns, cages, auto AI, difficulty, admin): 140..159
        CageActive = 140,    // host -> all: activate / deactivate a cage slot
        AdminAuthReq = 141,  // client -> host: admin proof
        AdminAuthResult = 142, // host -> client: admin accepted / refused
        AdminCmdReq = 143,   // admin client -> host: run a command
        AdminLog = 144,      // host -> admin: command feedback text
        AdminState = 145,    // host -> admin: debug state (positions, codes...)
        AdminTeleport = 146, // host -> one player: move yourself here (admin "bring player" / "bring Omar")
        AdminItemSpawn = 147, // host -> all: a new item appears (admin give / spawn)
        CageRattle = 148,    // host -> all: a caged prisoner rattles the lock (cage, prisoner) - loud, Omar may come to punish them

        // ================== iteration 3 (Docs/ITERATION3_PLAN.md): 160..199 ==================
        SocketState = 160,   // host -> all: an item socket took an item / completed (socket, count, solved, item type, player)
        CodeReq = 161,       // client -> host: try a code on a code lock (lock, code)
        CodeResult = 162,    // host -> that client: right / wrong (lock, ok)
        CodeLockState = 163, // host -> all: a code lock opened (lock, open, player)
    }

    public static class MsgRules
    {
        /// <summary>
        /// Messages a client may send to the host. Everything else is host -&gt; clients only: the host drops it when a
        /// client sends it (otherwise a modified client could push a fake status, objective, match end...).
        /// </summary>
        public static bool FromClient(Msg m)
        {
            switch (m)
            {
                case Msg.LobbyReq: case Msg.LoadedReq:
                case Msg.AvatarStateReq: case Msg.ActionReq: case Msg.NoiseReq:
                case Msg.PickupReq: case Msg.DropReq: case Msg.UseReq: case Msg.ThrowReq: case Msg.BottleImpact:
                case Msg.DoorReq: case Msg.HideReq: case Msg.StruggleReq: case Msg.TrapTriggerReq: case Msg.TrapPlaceReq:
                case Msg.AttackReq: case Msg.DetectReq: case Msg.ScreamReq: case Msg.SearchReq: case Msg.SenseReq:
                case Msg.EscapeReq: case Msg.CodeReq:
                case Msg.DoorGrabReq: case Msg.DoorDragReq: case Msg.ChargeReq: case Msg.DrawerReq:
                case Msg.ShootReq: case Msg.ChopReq: case Msg.OmarHitReq:
                case Msg.AdminAuthReq: case Msg.AdminCmdReq:
                    return true;
                default:
                    return false;
            }
        }
    }
}
