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
