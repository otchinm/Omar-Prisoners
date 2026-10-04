namespace PrisonersOfOmar.Audio
{
    /// <summary>(iteration 2) Per-character voice lines (degraded VHS voices, one set per prisoner skin).</summary>
    public enum VoiceLine : byte
    {
        Hurt = 0,   // grunt when cut / falling           (_1.._3)
        Scream,     // dragged away / terrified scream     (_1.._2)
        Breath,     // exhausted panting after sprinting   (_1.._3)
        Gasp,       // spotted by Omar / the grandmother   (_1.._2)
        Struggle,   // straining against a lock / trap     (_1.._2)
        Death,      // last capture                        (_1)
    }

    /// <summary>Resource paths of every sound (see Docs/ASSETS.md).</summary>
    public static class Snd
    {
        // user supplied
        public const string MenuTheme = "Audio/Music/menu_theme";
        public const string OmarAlarm = "Audio/Omar/alarm";
        public const string OmarFind = "Audio/Omar/find";
        public const string OmarScreamBase = "Audio/Omar/scream"; // _1.._4
        public const string ScreamsLong = "Audio/Stingers/screams_long";

        // UI
        public const string UiMove = "Audio/UI/ui_move", UiSelect = "Audio/UI/ui_select", UiBack = "Audio/UI/ui_back",
            UiError = "Audio/UI/ui_error", UiType = "Audio/UI/ui_type", TapeInsert = "Audio/UI/tape_insert",
            TapeEject = "Audio/UI/tape_eject", TapePlay = "Audio/UI/tape_play", TapeStop = "Audio/UI/tape_stop",
            TapeRewindLoop = "Audio/UI/tape_rewind_loop", InventoryOpen = "Audio/UI/inventory_open",
            InventoryClose = "Audio/UI/inventory_close", InventoryScroll = "Audio/UI/inventory_scroll";

        // ambience loops
        public const string AmbExterior = "Audio/Ambience/amb_exterior_loop", AmbHouse = "Audio/Ambience/amb_house_loop",
            AmbBasement = "Audio/Ambience/amb_basement_loop", AmbBarn = "Audio/Ambience/amb_barn_loop",
            AmbTunnel = "Audio/Ambience/amb_tunnel_loop", AmbRestroom = "Audio/Ambience/amb_restroom_loop",
            AmbMenu = "Audio/Ambience/amb_menu_loop", StaticLoop = "Audio/Ambience/static_loop",
            StaticHeavyLoop = "Audio/Ambience/static_heavy_loop", ChaseLoop = "Audio/Ambience/chase_loop",
            AnomalyLoop = "Audio/Ambience/anomaly_loop", TvStaticLoop = "Audio/Ambience/tv_static_loop",
            RadioStaticLoop = "Audio/Ambience/radio_static_loop", GeneratorLoop = "Audio/Ambience/generator_loop",
            FireLoop = "Audio/Ambience/fire_loop", CarIdleLoop = "Audio/Ambience/car_idle_loop",
            HelicopterLoop = "Audio/Ambience/helicopter_loop", FlameLoop = "Audio/Ambience/flame_loop",
            OmarBreathLoop = "Audio/Ambience/omar_breath_loop", WindGustLoop = "Audio/Ambience/wind_gust_loop";

        // ambience one-shots (base names, add _N)
        public const string Creak = "Audio/Ambience/creak"; // 4
        public const string DistantScream = "Audio/Ambience/distant_scream"; // 3
        public const string Thump = "Audio/Ambience/thump"; // 3
        public const string MetalScrape = "Audio/Ambience/metal_scrape"; // 2
        public const string Whisper = "Audio/Ambience/whisper"; // 2
        public const string Thunder = "Audio/Ambience/thunder"; // 2
        public const string Drip = "Audio/Ambience/drip"; // 3
        public const string PhoneRing = "Audio/Ambience/phone_ring";
        public const string DogHowl = "Audio/Ambience/dog_howl";

        // steps
        public const string StepsBase = "Audio/Steps/"; // + surface + "_N"
        public const string OmarStep = "Audio/Steps/omar"; // 4

        // player
        public const string Heartbeat = "Audio/Player/heartbeat", BreathHeavy = "Audio/Player/breath_heavy", // 3
            Hurt = "Audio/Player/hurt", // 3
            BodyFall = "Audio/Player/body_fall", CageRattle = "Audio/Player/cage_rattle",
            Struggle = "Audio/Player/struggle", Gasp = "Audio/Player/gasp";

        // items
        public const string LighterOpen = "Audio/Items/lighter_open", LighterFlick = "Audio/Items/lighter_flick",
            LighterClose = "Audio/Items/lighter_close", FuelPour = "Audio/Items/fuel_pour",
            BandageRip = "Audio/Items/bandage_rip", FlashlightClick = "Audio/Items/flashlight_click",
            BatteryInsert = "Audio/Items/battery_insert", ItemPickup = "Audio/Items/item_pickup",
            ItemDrop = "Audio/Items/item_drop", ItemEquip = "Audio/Items/item_equip",
            BottleThrow = "Audio/Items/bottle_throw", GlassBreak = "Audio/Items/glass_break",
            KeyUnlock = "Audio/Items/key_unlock", LockedRattle = "Audio/Items/locked_rattle",
            Lockpick = "Audio/Items/lockpick", CrowbarPry = "Audio/Items/crowbar_pry", WoodBreak = "Audio/Items/wood_break",
            BoltCut = "Audio/Items/bolt_cut", ChainDrop = "Audio/Items/chain_drop", SoundMeterTick = "Audio/Items/soundmeter_tick",
            Pills = "Audio/Items/pills",
            VentScrew = "Audio/Items/vent_screw", // 3: turning a rusty screw out (while holding E)
            VentScrewDrop = "Audio/Items/vent_screw_drop", VentCoverOff = "Audio/Items/vent_cover_off",
            VentGrateFall = "Audio/Items/vent_grate_fall"; // the kitchen ceiling grate: pops, hisses down, dull heavy clang at 0.84 s

        // world
        public const string DoorOpen = "Audio/World/door_open", // 2
            DoorClose = "Audio/World/door_close", // 2
            DoorSlam = "Audio/World/door_slam", MetalDoorOpen = "Audio/World/metal_door_open",
            MetalDoorClose = "Audio/World/metal_door_close", WardrobeOpen = "Audio/World/wardrobe_open",
            WardrobeClose = "Audio/World/wardrobe_close", GateCreak = "Audio/World/gate_creak",
            CageOpen = "Audio/World/cage_open", CageClose = "Audio/World/cage_close", KeypadBeep = "Audio/World/keypad_beep",
            KeypadWrong = "Audio/World/keypad_wrong", KeypadOk = "Audio/World/keypad_ok",
            ShelterDoorOpen = "Audio/World/shelter_door_open", FuseInsert = "Audio/World/fuse_insert",
            PowerOn = "Audio/World/power_on", PowerOff = "Audio/World/power_off", RadioTune = "Audio/World/radio_tune",
            RadioSos = "Audio/World/radio_sos", RadioVoice = "Audio/World/radio_voice", CarDoor = "Audio/World/car_door",
            CarCrankFail = "Audio/World/car_crank_fail", CarStart = "Audio/World/car_start",
            CarDriveAway = "Audio/World/car_drive_away", GasPour = "Audio/World/gas_pour", HoodOpen = "Audio/World/hood_open",
            Explosion = "Audio/World/explosion", FireWhoosh = "Audio/World/fire_whoosh",
            HelicopterFlyby = "Audio/World/helicopter_flyby", FenceBreach = "Audio/World/fence_breach", TvOn = "Audio/World/tv_on";

        // Omar
        public const string CleaverSwing = "Audio/Omar/cleaver_swing", // 2
            CleaverHit = "Audio/Omar/cleaver_hit", // 2
            CleaverHitWall = "Audio/Omar/cleaver_hit_wall", Growl = "Audio/Omar/growl", // 2
            TrapPlace = "Audio/Omar/trap_place", TrapSnap = "Audio/Omar/trap_snap", TripwireSnap = "Audio/Omar/tripwire_snap",
            HidingRip = "Audio/Omar/hiding_rip", Grab = "Audio/Omar/grab";

        // stingers
        public const string StingSpotted = "Audio/Stingers/sting_spotted", StingJumpscare = "Audio/Stingers/sting_jumpscare",
            StingCapture = "Audio/Stingers/sting_capture", StingDeath = "Audio/Stingers/sting_death",
            StingAnomaly = "Audio/Stingers/sting_anomaly", // 2
            StingEscape = "Audio/Stingers/sting_escape", StingEndingBad = "Audio/Stingers/sting_ending_bad",
            StingEndingGood = "Audio/Stingers/sting_ending_good", StaticBurst = "Audio/Stingers/static_burst", // 3
            VhsGlitch = "Audio/Stingers/vhs_glitch", // 3
            DroneHit = "Audio/Stingers/drone_hit", // 2
            HeartbeatFast = "Audio/Stingers/heartbeat_fast";

        // ---- iteration 2 (see Docs/ITERATION2.md). Every path below MUST exist as a file under Resources/.
        // physical doors
        public const string DoorCreakLoop = "Audio/World/door_creak_loop", // _1.._2: wooden hinge creak loop, pitch follows the swing speed
            MetalDoorCreakLoop = "Audio/World/metal_door_creak_loop",
            DoorLatch = "Audio/World/door_latch", // _1.._2: latch clicks shut
            DoorBump = "Audio/World/door_bump",   // _1.._2: leaf hits its stop / a wall
            DoorUnlock = "Audio/World/door_unlock";
        // hiding
        public const string BedCrawlIn = "Audio/Player/bed_crawl_in", BedCrawlOut = "Audio/Player/bed_crawl_out",
            BedLift = "Audio/Omar/bed_lift", WardrobeEnter = "Audio/World/wardrobe_enter", WardrobeExit = "Audio/World/wardrobe_exit";
        // Omar
        public const string OmarChop = "Audio/Omar/chop", // _1.._3: cleaver into the meat on the block
            MeatSquelch = "Audio/Omar/meat_squelch",      // _1.._2
            OmarWindup = "Audio/Omar/windup",             // _1.._2: heavy grunt before the big swing
            OmarStunned = "Audio/Omar/stunned",
            OmarDoorPush = "Audio/Omar/door_push";
        // the grandmother
        public const string GrandmaScream = "Audio/Grandma/scream", // _1.._3
            GrandmaSpot = "Audio/Grandma/spot",                       // shriek when she first sees someone
            GrandmaMutter = "Audio/Grandma/mutter",                   // _1.._3
            GrandmaDeath = "Audio/Grandma/death",
            WheelchairLoop = "Audio/Grandma/wheelchair_loop",
            GrandmaTvLoop = "Audio/Grandma/tv_loop";
        // revolver
        public const string GunShot = "Audio/Items/gun_shot", GunEmpty = "Audio/Items/gun_empty", GunCock = "Audio/Items/gun_cock";
        // character voices: Audio/Voices/<prisonerN>/<line>_<n>
        public const string VoicesBase = "Audio/Voices/";

        public static int VoiceVariants(VoiceLine l)
        {
            switch (l)
            {
                case VoiceLine.Hurt: return 3;
                case VoiceLine.Breath: return 3;
                case VoiceLine.Scream: return 2;
                case VoiceLine.Gasp: return 2;
                case VoiceLine.Struggle: return 2;
                default: return 1;
            }
        }

        /// <summary>Folder name of a skin's voice set ("prisoner1".."prisoner7").</summary>
        public static string VoiceFolder(CharacterSkin skin) => skin.ToString().ToLowerInvariant();

        /// <summary>Random variant of one of a character's voice lines.</summary>
        public static string Voice(CharacterSkin skin, VoiceLine line)
            => AudioManager.Variant(VoicesBase + VoiceFolder(skin) + "/" + line.ToString().ToLowerInvariant(), VoiceVariants(line));

        public static string Step(SurfaceType s)
        {
            string name;
            switch (s)
            {
                case SurfaceType.Wood: name = "wood"; break;
                case SurfaceType.Concrete: name = "concrete"; break;
                case SurfaceType.Dirt: name = "dirt"; break;
                case SurfaceType.Grass: name = "grass"; break;
                case SurfaceType.Metal: name = "metal"; break;
                case SurfaceType.Asphalt: name = "asphalt"; break;
                case SurfaceType.Tile: name = "tile"; break;
                case SurfaceType.Carpet: name = "carpet"; break;
                case SurfaceType.Gravel: name = "gravel"; break;
                default: name = "concrete"; break;
            }
            return AudioManager.Variant(StepsBase + name, 4);
        }

        public static string OmarScream(int index) => OmarScreamBase + "_" + UnityEngine.Mathf.Clamp(index, 1, 4);
    }
}
