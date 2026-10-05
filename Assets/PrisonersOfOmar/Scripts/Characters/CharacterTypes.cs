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
        Pistol,        // (iteration 2) revolver held forward in the right hand
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
        // ---- iteration 2
        BedLift,    // Omar: stand beside a bed, grab the frame with both hands and tip it up on one side (~1.6 s)
        ChopMeat,   // Omar: one big overhead cleaver slam onto the butcher table (loopable, ~1.1 s)
        CrawlUnder, // prisoner: drop down and slide under a bed (~0.9 s, ends lying flat)
        CrawlOut,   // prisoner: slide out from under a bed and stand up (~0.9 s)
        Shoot,      // prisoner: fire the revolver (recoil)
        Cower,      // prisoner: flinch back raising both arms to protect the head
        Push,       // shoulder / hand push (Omar shoving a door open while walking)
        PeekUnder,  // Omar: drops onto one knee and jerks his head down sideways to look under a bed (~0.85 s)
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
