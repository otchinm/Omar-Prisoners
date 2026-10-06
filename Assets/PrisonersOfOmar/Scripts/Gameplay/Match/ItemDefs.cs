using PrisonersOfOmar.Characters;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>Static description of an item type.</summary>
    public sealed class ItemDef
    {
        public ItemType Type;
        public string Name;
        public string Description;
        public HoldPose Hold;
        /// <summary>Has a charge (fuel / battery) shown as a percentage.</summary>
        public bool HasCharge;
        /// <summary>Key objective item (spawned once at "Key" spawn points).</summary>
        public bool Key;
        /// <summary>Noise radius when dropped.</summary>
        public float DropNoise = 4f;

        public string Icon => "Textures/UI/icon_" + Type.ToString().ToLowerInvariant();
    }

    public static class ItemDefs
    {
        static readonly ItemDef[] _defs = new ItemDef[32];

        static ItemDefs()
        {
            Add(ItemType.Lighter, "LIGHTER", HoldPose.Lighter, "An old brass lighter. It is all the light I have. Every flick eats fuel - and Omar can see the flame from far away.", charge: true);
            Add(ItemType.LighterFuel, "LIGHTER FUEL", HoldPose.OneHandSmall, "A can of lighter fluid. I can refill my lighter with it... or pour it on something that should burn.");
            Add(ItemType.Bandages, "BAND-AIDS", HoldPose.OneHandSmall, "A box of bandages. Enough to stop the bleeding once.");
            Add(ItemType.Flashlight, "FLASHLIGHT", HoldPose.Flashlight, "A heavy flashlight. Much brighter than the lighter, but the beam can be seen from across the field.", charge: true);
            Add(ItemType.Batteries, "BATTERIES", HoldPose.OneHandSmall, "Two D cell batteries. They fit the flashlight.");
            Add(ItemType.SoundMeter, "SOUND METER", HoldPose.OneHandSmall, "I can use this to monitor how much sound I'm making. I need to stay as quiet as possible.");
            Add(ItemType.BoltCutters, "BOLT CUTTERS", HoldPose.TwoHanded, "Long rusty bolt cutters. Strong enough for the padlock on the main gate... or a cage lock. Loud.", key: true, dropNoise: 7f);
            Add(ItemType.CarKeys, "CAR KEYS", HoldPose.OneHandSmall, "A ring of keys with a car fob. There is a sedan in the parking lot.", key: true);
            Add(ItemType.GasCan, "GAS CAN", HoldPose.TwoHanded, "A red jerry can, half full of gasoline. Heavy. It sloshes when I run.", key: true, dropNoise: 7f);
            Add(ItemType.CarBattery, "CAR BATTERY", HoldPose.TwoHanded, "A car battery. It might still hold a charge.", key: true, dropNoise: 8f);
            Add(ItemType.Fuse, "FUSE", HoldPose.OneHandSmall, "A big old cartridge fuse. Someone pulled it out of a fuse box on purpose.", key: true);
            Add(ItemType.CageKey, "CAGE KEY", HoldPose.OneHandSmall, "A heavy iron key on a string. It opens the cages upstairs.", key: true);
            Add(ItemType.Lockpick, "LOCKPICK", HoldPose.OneHandSmall, "A bent pick and a tension wrench. One lock, maybe two, if I'm patient.");
            Add(ItemType.Crowbar, "CROWBAR", HoldPose.TwoHanded, "A crowbar. It can pry boards off a door. If he grabs me, I can hit him with it. Once.", dropNoise: 8f);
            Add(ItemType.Bottle, "BOTTLE", HoldPose.Bottle, "An empty glass bottle. If I throw it, the noise might draw him away.");
            Add(ItemType.Pills, "PAINKILLERS", HoldPose.OneHandSmall, "Prescription painkillers. For a while I won't feel my wounds and I can run longer.");
            Add(ItemType.Screwdriver, "SCREWDRIVER", HoldPose.OneHandSmall, "A flathead screwdriver with a cracked amber handle. The vent cover in the cage room is held by four rusty screws...", key: true);
            Add(ItemType.Revolver, "REVOLVER", HoldPose.Pistol, "An old revolver with two rounds left. It won't stop him for long... but the old woman is a different story.", charge: true, key: true, dropNoise: 6f);
            Add(ItemType.Backpack, "BACKPACK", HoldPose.OneHandSmall, "An old canvas rucksack. With it on my back I can carry five things instead of three.", dropNoise: 3f);
            Add(ItemType.VhsTape, "VHS TAPE", HoldPose.OneHandSmall, "A home video cassette. The label says 'MAMA 10/31' in thick marker. There is a VCR on the TV in the living room...", key: true);
            Add(ItemType.SmallKey, "SMALL KEY", HoldPose.OneHandSmall, "A little brass key on a string. The tag only has a number on it. Some drawer in this house is locked with a padlock like this.");
        }

        /// <summary>Worn items go on the body instead of into a slot (see <see cref="LocalInventory.WornSlot"/>).</summary>
        public static bool IsWorn(ItemType t) => t == ItemType.Backpack;

        static void Add(ItemType t, string name, HoldPose hold, string desc, bool charge = false, bool key = false, float dropNoise = 4f)
        {
            _defs[(int)t] = new ItemDef { Type = t, Name = name, Hold = hold, Description = desc, HasCharge = charge, Key = key, DropNoise = dropNoise };
        }

        public static ItemDef Get(ItemType t)
        {
            int i = (int)t;
            if (i <= 0 || i >= _defs.Length || _defs[i] == null) return new ItemDef { Type = t, Name = t.ToString().ToUpperInvariant(), Description = "", Hold = HoldPose.OneHandSmall };
            return _defs[i];
        }
    }
}
