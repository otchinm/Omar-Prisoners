namespace PrisonersOfOmar
{
    /// <summary>Global constants shared by every module.</summary>
    public static class GameInfo
    {
        public const string Title = "THE PRISONERS OF OMAR";
        public const string Version = "0.1.0";
        /// <summary>Bump whenever the wire protocol changes; peers with a different value are rejected.</summary>
        public const int ProtocolVersion = 2;
        public const int MaxPrisoners = 4;
        public const int MaxPlayers = MaxPrisoners + 1; // 4 prisoners + 1 Omar
        public const int DefaultPort = 27015;
        public const int DiscoveryPort = 27016;
        public const int InventorySlots = 3;
        /// <summary>Seconds of sound that keep playing after Omar loses sight of a prisoner.</summary>
        public const float ChaseSoundLinger = 4f;

        /// <summary>(iteration 2) Every playable prisoner appearance, in lobby order.</summary>
        public static readonly CharacterSkin[] PrisonerSkins =
        {
            CharacterSkin.Prisoner1, CharacterSkin.Prisoner2, CharacterSkin.Prisoner3, CharacterSkin.Prisoner4,
            CharacterSkin.Prisoner5, CharacterSkin.Prisoner6, CharacterSkin.Prisoner7,
        };

        public static bool IsPrisonerSkin(CharacterSkin s) => System.Array.IndexOf(PrisonerSkins, s) >= 0;
    }

    /// <summary>Physics / rendering layers (must match ProjectSettings/TagManager.asset).</summary>
    public static class Layers
    {
        public const int Default = 0;
        public const int IgnoreRaycast = 2;
        public const int World = 8;        // static level geometry, props
        public const int Player = 9;       // prisoner character controllers + bodies
        public const int Omar = 10;        // Omar character controller + body
        public const int Interactable = 11;// trigger volumes the interaction ray can hit
        public const int ViewModel = 12;   // first person arms / held items (separate camera)
        public const int LocalBody = 13;   // the local player's own body (hidden from own camera)
        public const int Trigger = 14;     // gameplay trigger volumes (exit zones, trap wires...)
        public const int Preview = 15;     // inventory / lobby model preview
        public const int Foliage = 16;     // grass, bushes, tree billboards (no collision, partial cover)
        public const int Corpse = 17;      // decorative bodies, mannequins
        public const int Door = 18;        // dynamic door leaves (block movement & sight)
        public const int Item = 19;        // world item pickups

        public static int Mask(params int[] layers)
        {
            int m = 0;
            for (int i = 0; i < layers.Length; i++) m |= 1 << layers[i];
            return m;
        }

        /// <summary>Things that block line of sight.</summary>
        public static readonly int SightBlockers = Mask(World, Door, Default);
        /// <summary>Things characters stand on / collide with.</summary>
        public static readonly int Solid = Mask(World, Door, Default);
        /// <summary>What the interaction ray can hit (blockers included so you can't use through walls).</summary>
        public static readonly int InteractRay = Mask(World, Door, Default, Interactable, Item);
    }
}
