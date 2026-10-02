using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    // =====================================================================================
    //  Output of MapBuilder.Build(). Everything the gameplay layer needs to know about the
    //  level. The builder is DETERMINISTIC for a given seed: every peer builds the same map
    //  and gets the same list orders (list index = network id of the object).
    //
    //  "Interact" colliders: trigger BoxColliders on Layers.Interactable that the player's
    //  interaction ray hits. Gameplay attaches its components to the collider's GameObject.
    // =====================================================================================

    public sealed class MapData
    {
        public Transform Root;
        public int Seed;

        public readonly List<DoorInfo> Doors = new List<DoorInfo>();
        public readonly List<HidingSpotInfo> HidingSpots = new List<HidingSpotInfo>();
        public readonly List<ItemSpawnInfo> ItemSpawns = new List<ItemSpawnInfo>();
        public readonly List<TrapSpotInfo> TrapSpots = new List<TrapSpotInfo>();
        public readonly List<CageInfo> Cages = new List<CageInfo>();
        public readonly List<NoteSpotInfo> NoteSpots = new List<NoteSpotInfo>();
        public readonly List<AmbientZoneInfo> AmbientZones = new List<AmbientZoneInfo>();
        public readonly List<MannequinInfo> Mannequins = new List<MannequinInfo>();
        public readonly List<AnomalyZoneInfo> AnomalyZones = new List<AnomalyZoneInfo>();
        public readonly List<SoundEmitterInfo> SoundEmitters = new List<SoundEmitterInfo>();

        /// <summary>Lights that go dark during a power outage (house + basement bulbs, NOT the moon / fires).</summary>
        public readonly List<PsxLight> PowerLights = new List<PsxLight>();
        /// <summary>Lights in the radio room that turn on once the fuse is inserted (start Off).</summary>
        public readonly List<PsxLight> RadioRoomLights = new List<PsxLight>();

        /// <summary>One per prisoner, inside the cages (index 0..3 matches Cages[0..3]).</summary>
        public readonly List<Pose> PrisonerSpawns = new List<Pose>();
        public Pose OmarSpawn;
        /// <summary>Overview camera poses for spectators / ending shots.</summary>
        public readonly List<Pose> SpectatorCameras = new List<Pose>();

        public GateInfo MainGate;
        public CarInfo Car;
        public ShelterInfo Shelter;
        public RadioInfo Radio;
        public FireInfo FuelDepot;

        public NavGraph Nav;
        /// <summary>Everything a player can reach (used to clamp / sanity check positions).</summary>
        public Bounds PlayableBounds;
        /// <summary>Named points for scripted events ("TvLivingRoom", "PhoneKitchen", "Portrait", ...).</summary>
        public readonly Dictionary<string, Transform> Markers = new Dictionary<string, Transform>();
    }

    public sealed class DoorInfo
    {
        public string Name;
        public DoorKind Kind;
        /// <summary>Hinge. Rotating Pivot.localRotation around local Y by OpenAngle opens the door. Closed = initial rotation.</summary>
        public Transform Pivot;
        /// <summary>Signed degrees (+/-90 typical) chosen so the door swings away from the more common approach.</summary>
        public float OpenAngle = 90f;
        /// <summary>The leaf's solid collider (Layers.Door). The interaction ray hits this.</summary>
        public Collider Leaf;
        public bool StartsOpen;
        public bool StartsLocked;
        /// <summary>Item that unlocks it (Crowbar for Boarded, Lockpick works on any Locked).</summary>
        public ItemType KeyItem = ItemType.None;
        /// <summary>Planks GameObject for Boarded doors (gameplay hides it when pried open).</summary>
        public GameObject Boards;
        /// <summary>Center of the doorway at floor height.</summary>
        public Vector3 Center;
    }

    public sealed class HidingSpotInfo
    {
        public string Name;
        public HidingKind Kind;
        public Transform Root;
        /// <summary>Trigger collider to interact with (enter / Omar search).</summary>
        public Collider Interact;
        /// <summary>Camera pose while hidden (inside the wardrobe looking out through the slats, under the bed...).</summary>
        public Pose HiddenView;
        /// <summary>Where the player stands after leaving (in front of the spot, facing away from it).</summary>
        public Pose ExitPose;
        /// <summary>Optional animated doors (wardrobe doors / locker door). Each opens by rotating local Y by DoorOpenAngles[i].</summary>
        public Transform[] Doors = new Transform[0];
        public float[] DoorOpenAngles = new float[0];
    }

    public enum ItemSpawnTier : byte
    {
        Common = 0,   // supplies (fuel, bandages, batteries, bottles, pills)
        Key = 1,      // objective items (keys, bolt cutters, gas can, fuse...)
        Any = 2,
    }

    public sealed class ItemSpawnInfo
    {
        /// <summary>Surface point (table top, floor, shelf) where an item may rest.</summary>
        public Vector3 Position;
        public float Yaw;
        /// <summary>Area name: "House.Kitchen", "House.Basement.Generator", "Shed", "Barn", "Restroom", "Yard", ...</summary>
        public string Area;
        public ItemSpawnTier Tier = ItemSpawnTier.Any;
    }

    public sealed class TrapSpotInfo
    {
        public TrapKind Kind;
        /// <summary>Tripwire: the two anchor points at ankle height (~0.12m) across a passage. BearTrap: A = ground position.</summary>
        public Vector3 A, B;
        public string Area;
    }

    public sealed class CageInfo
    {
        public Transform Root;
        /// <summary>Cage door hinge; open = rotate local Y by OpenAngle.</summary>
        public Transform DoorPivot;
        public float OpenAngle = -100f;
        /// <summary>Solid collider of the door (Layers.Door).</summary>
        public Collider DoorCollider;
        /// <summary>Trigger collider in front of the door, OUTSIDE the cage (rescuers use it) - also hit from inside for "struggle".</summary>
        public Collider Interact;
        /// <summary>Where a caged prisoner sits.</summary>
        public Pose Inside;
        /// <summary>Where a released prisoner is placed (just outside the door).</summary>
        public Pose Outside;
    }

    public sealed class NoteSpotInfo
    {
        /// <summary>Center of the note / writing on the surface.</summary>
        public Vector3 Position;
        /// <summary>Rotation whose forward points OUT of the surface (towards the reader).</summary>
        public Quaternion Rotation;
        /// <summary>true = scrawled on a wall (blood/marker), false = paper lying on furniture.</summary>
        public bool OnWall;
        public string Area;
    }

    public sealed class GateInfo
    {
        /// <summary>Gate leaf hinges; open = rotate local Y by LeftOpenAngle / RightOpenAngle.</summary>
        public Transform LeftLeaf, RightLeaf;
        public float LeftOpenAngle = -100f, RightOpenAngle = 100f;
        /// <summary>Colliders that keep the gate shut (disabled once open).</summary>
        public Collider[] Blockers = new Collider[0];
        /// <summary>Padlock + chain model (hidden when cut).</summary>
        public GameObject Padlock;
        /// <summary>Trigger collider on the padlock (bolt cutters).</summary>
        public Collider Interact;
        /// <summary>Reaching this zone (down the road) = escaped via THE LONG ROAD.</summary>
        public Bounds ExitZone;
    }

    public sealed class CarInfo
    {
        public Transform Root;
        /// <summary>Trigger colliders: fuel cap (gas can), hood (battery), driver door (keys / start), passenger doors (board).</summary>
        public Collider FuelCap, Hood, DriverDoor;
        public Collider[] PassengerDoors = new Collider[0];
        /// <summary>Seat poses: [0] driver, [1..3] passengers (camera position + facing).</summary>
        public Pose[] Seats = new Pose[4];
        /// <summary>Vehicle gate hinge(s) the car smashes through, and the chain on it.</summary>
        public Transform VehicleGateLeft, VehicleGateRight;
        public GameObject VehicleGateChain;
        /// <summary>Waypoints (world) the car follows in the escape drive: from parking spot through the vehicle gate and down the road.</summary>
        public Vector3[] DrivePath = new Vector3[0];
        /// <summary>Head light anchors (forward = beam). Gameplay adds PsxLights when the engine starts.</summary>
        public Transform[] Headlights = new Transform[0];
        /// <summary>Hood pivot (opens around local X) for the battery swap animation; may be null.</summary>
        public Transform HoodPivot;
    }

    public sealed class ShelterInfo
    {
        /// <summary>Heavy door hinge; open = rotate local Y by OpenAngle.</summary>
        public Transform DoorPivot;
        public float OpenAngle = 95f;
        public Collider DoorCollider;
        /// <summary>Keypad trigger collider next to the door.</summary>
        public Collider Keypad;
        /// <summary>Walking into this zone at the far end of the tunnel = escaped via UNDERGROUND.</summary>
        public Bounds TunnelExitZone;
    }

    public sealed class RadioInfo
    {
        /// <summary>Radio set on the desk (trigger collider).</summary>
        public Collider RadioSet;
        /// <summary>Fuse box in the basement (trigger collider).</summary>
        public Collider FuseBox;
        /// <summary>Clearing where the rescue helicopter lands; prisoners inside after rescue arrives = SIGNAL.</summary>
        public Bounds LandingZone;
        /// <summary>Where the helicopter hovers (lights / sound source).</summary>
        public Vector3 HelicopterPosition;
    }

    public sealed class FireInfo
    {
        /// <summary>The fuel drums (trigger collider): pour lighter fuel, then ignite with the lighter.</summary>
        public Collider Barrels;
        public Transform BarrelsRoot;
        public Vector3 ExplosionCenter;
        /// <summary>Fence section destroyed by the explosion (hidden / thrown).</summary>
        public GameObject BreachFence;
        /// <summary>Colliders of that fence section (disabled after explosion).</summary>
        public Collider[] BreachBlockers = new Collider[0];
        /// <summary>Walking through the breach into this zone = escaped via ASHES.</summary>
        public Bounds BreachExitZone;
    }

    public sealed class AmbientZoneInfo
    {
        public AmbientType Type;
        public Bounds Bounds;
    }

    public sealed class MannequinInfo
    {
        /// <summary>The figure; gameplay may teleport/rotate it while nobody is looking (anomaly).</summary>
        public Transform Root;
        /// <summary>Alternative standing poses in the same room.</summary>
        public Pose[] AltPoses = new Pose[0];
    }

    public sealed class AnomalyZoneInfo
    {
        public Vector3 Center;
        public float Radius;
        public float Strength;
        public string Name;
    }

    /// <summary>Positional ambient loops placed in the level (TV static, buzzing bulb, generator...).</summary>
    public sealed class SoundEmitterInfo
    {
        public Vector3 Position;
        /// <summary>Resources path, e.g. "Audio/Ambience/tv_static_loop".</summary>
        public string Clip;
        public float Volume = 0.6f;
        public float MaxDistance = 12f;
        /// <summary>Powered emitters stop during power outages.</summary>
        public bool NeedsPower;
    }
}
