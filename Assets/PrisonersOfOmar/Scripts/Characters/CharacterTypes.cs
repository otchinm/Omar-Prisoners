namespace PrisonersOfOmar.Characters
{
    /// <summary>How the character holds the equipped item (third person + first person).</summary>
    public enum HoldPose : byte
    {
        None = 0,
        Lighter,       // right hand raised in front of the chest, flame up
        Flashlight,    // right hand forward, pointing
        OneHandSmall,  // keys, pills, fuse, meter... held low in right hand
        TwoHanded,     // gas can, car battery, bolt cutters, crowbar: both hands
        Cleaver,       // Omar: cleaver in right hand, arm hanging
        Bottle,        // ready to throw
    }

    /// <summary>One-shot animations. Values are sent over the network.</summary>
    public enum CharacterAction : byte
    {
        None = 0,
        Pickup,     // bend / reach down
        Interact,   // hands forward (doors, switches, keypad)
        UseItem,    // generic use (bandage wrap, pills)
        Attack,     // Omar: overhead cleaver chop (impact event at the down swing)
        Scream,     // Omar: head back, arms spread, shaking
        Search,     // Omar: look left/right, tilt head
        Grab,       // Omar: reach and grab a prisoner (capture)
        PlaceTrap,  // Omar: crouch, hands to the ground
        Stunned,    // stagger back holding face (explosion, crowbar)
        HitReact,   // flinch when hit
        Struggle,   // wriggle (bear trap, grabbed)
        Pour,       // tilt container (gas can, lighter fuel)
        Throw,      // throw a bottle
        Cut,        // bolt cutters squeeze
        Heal,       // wrap bandage around arm
        Wave,       // lobby / idle flavor
    }

    /// <summary>Persistent body states.</summary>
    public enum CharacterPose : byte
    {
        Normal = 0,
        CagedSit,  // sitting on the floor hugging knees (inside a cage)
        Downed,    // lying on the floor, crawling slightly
        Dead,      // limp on the floor
        Hidden,    // renderers hidden (inside a hiding spot)
        Seated,    // in a car seat
        Trapped,   // one leg caught (bear trap), kneeling
    }
}
