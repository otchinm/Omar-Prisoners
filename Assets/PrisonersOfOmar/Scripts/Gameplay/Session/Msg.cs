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

        // ---- objectives / events / endings
        ObjectiveState = 80, // host -> all: full objective struct
        Escaped = 81,
        EscapeReq = 82,
        KeypadReq = 83,
        KeypadResult = 84,
        WorldEvent = 85,
        MatchEnd = 86,
        Message = 87,        // host -> all: HUD message
        CarDrive = 88,       // host -> all: car escape cinematic
    }
}
