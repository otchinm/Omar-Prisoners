namespace PrisonersOfOmar
{
    /// <summary>The five playable appearances. Values are sent over the network.</summary>
    public enum CharacterSkin : byte
    {
        Prisoner1 = 0, // athlete: spiky bleached hair, white tank top "19", light blue shorts
        Prisoner2 = 1, // woman: long brown hair, red long-sleeve crop top, white sweatpants
        Prisoner3 = 2, // redhead with glasses: striped sweater, denim shorts
        Prisoner4 = 3, // lanky guy with round glasses: raglan tee (black sleeves), purple shorts
        Omar = 4,      // sack mask, rope noose, red plaid flannel, bloody apron, jeans, boots
        // (iteration 2) more prisoners, from the "players from another view" reference
        Prisoner5 = 5, // woman: short auburn bob, grey plaid long-sleeve mini dress, black mary-janes
        Prisoner6 = 6, // boy: short brown hair, loud colourful patterned 90s shirt, dark jeans
        Prisoner7 = 7, // man: dark side-parted hair, white dress shirt, dark slacks
        // secret (not in the lobby list: typing its code into the lobby's CODE field picks it, see GameInfo.SecretSkinFor)
        Prisoner8 = 8, // "HTN": tall young man, broad shoulders, a mop of dark curls, navy tee, black trousers, white sneakers
    }

    /// <summary>(iteration 2) Match difficulty, chosen by the host in the lobby. Values are sent over the network.</summary>
    public enum Difficulty : byte
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Nightmare = 3, // as dark as the first version of the game
    }

    /// <summary>(iteration 2) VHS filter presets (graphics settings), like the filter switch in Stay Out Of The House.</summary>
    public enum VhsPreset : byte
    {
        Default = 0,  // degraded tape, readable picture
        Clean = 1,    // light noise + scanlines only
        Worn = 2,     // old worn tape: tracking wobble, washed colours, dropouts
        Camcorder = 3,// home video: sharper, warm, date stamp feel
        BlackWhite = 4,
        Sepia = 5,
        Off = 6,      // no tape effect (PS1 look only)
    }

    public enum PlayerRole : byte
    {
        Prisoner = 0,
        Omar = 1,
        Spectator = 2,
    }

    /// <summary>Footstep / impact surface. Put a <see cref="SurfaceTag"/> on colliders.</summary>
    public enum SurfaceType : byte
    {
        Default = 0, Wood, Concrete, Dirt, Grass, Metal, Asphalt, Tile, Carpet, Gravel,
    }

    /// <summary>Every collectible item. Values are sent over the network.</summary>
    public enum ItemType : byte
    {
        None = 0,
        Lighter,      // default item; flame light, consumes fuel
        LighterFuel,  // refills lighter; needed to start the barn fire (ASHES)
        Bandages,     // heal one injury
        Flashlight,   // strong cone light, consumes battery
        Batteries,    // refill flashlight
        SoundMeter,   // shows how much noise you make
        BoltCutters,  // cut the main gate padlock (THE LONG ROAD) / cage locks
        CarKeys,      // drive the car (HEADLIGHTS)
        GasCan,       // fuel the car
        CarBattery,   // replace a dead car battery (random)
        Fuse,         // powers the radio room (SIGNAL)
        CageKey,      // opens cages silently
        Lockpick,     // opens a cage or locked door, consumed
        Crowbar,      // pry boarded doors, break free from Omar's grab once
        Bottle,       // throw to make noise somewhere else
        Pills,        // painkillers: stamina + ignore limp for a while
        Revolver,     // (iteration 2) 2 rounds: kills the grandmother, stuns Omar for a few seconds. Very loud.
        Screwdriver,  // unscrews the vent cover in the cage room (crawl through the ceiling duct down into the kitchen)
    }

    public enum DoorKind : byte
    {
        Wood = 0,     // interior paneled door
        WoodDirty,    // bloody / filthy interior door
        Metal,        // basement metal door
        Front,        // exterior house door
        Closet,       // small closet door
        Boarded,      // nailed shut: needs Crowbar, then behaves like Wood
        Shelter,      // fallout shelter door (keypad objective, not a regular door)
        Shed,         // plank door
        Restroom,     // steel door of the concrete block restroom
        Silo,         // rusty hatch door
        Cage,         // cage door (objective-ish, opened with key / lockpick / bolt cutters)
    }

    public enum HidingKind : byte
    {
        Wardrobe = 0,
        UnderBed,
        Locker,
        Barrel,
    }

    public enum TrapKind : byte
    {
        Tripwire = 0, // wire across a passage; triggers the alarm siren
        BearTrap,     // snaps on a leg: injures and holds the prisoner
    }

    public enum AmbientType : byte
    {
        Exterior = 0, House, Basement, Barn, Tunnel, Restroom, Shed, Silo,
    }

    /// <summary>Static decorative human figures (mannequins, bodies).</summary>
    public enum FigureKind : byte
    {
        BurntMannequin = 0, // charred, featureless mannequin (red-tile room)
        Mannequin,          // pale plastic mannequin
        Corpse,             // dead body in rags
    }

    public enum FigurePose : byte
    {
        Standing = 0,
        StandingHandsCrossed, // hands folded in front of the crotch
        LyingOnBack,
        Sitting,
        Hanging,
    }

    /// <summary>Escape routes / match endings.</summary>
    public enum EscapeRoute : byte
    {
        None = 0,
        Road,       // cut the main gate padlock and walk out to the road  -> "THE LONG ROAD"
        Car,        // fuel + keys (+battery) and drive through the vehicle gate -> "HEADLIGHTS"
        Shelter,    // keypad code from notes, fallout shelter tunnel -> "UNDERGROUND"
        Radio,      // fuse + radio call, survive until rescue -> "SIGNAL"
        Fire,       // ignite the fuel drums, escape through the breach -> "ASHES"
    }
}
