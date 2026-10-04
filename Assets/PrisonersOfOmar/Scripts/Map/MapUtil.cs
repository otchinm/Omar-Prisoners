using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>Texture paths used by the map (see Docs/ASSETS.md). Missing files fall back to gray in PsxMaterials.</summary>
    internal static class Tex
    {
        public const string Env = "Textures/Env/";
        public const string Props = "Textures/Props/";
        public const string Decals = "Textures/Decals/";
        public const string Foliage = "Textures/Foliage/";
        public const string FX = "Textures/FX/";

        // walls
        public const string WallpaperBlue = Env + "wall_wallpaper_blue";
        public const string WallpaperGreen = Env + "wall_wallpaper_green";
        public const string WallpaperRose = Env + "wall_wallpaper_rose";
        public const string WallpaperYellow = Env + "wall_wallpaper_yellow";
        public const string Wainscot = Env + "wall_wainscot";
        public const string PlasterDirty = Env + "wall_plaster_dirty";
        public const string BrickBasement = Env + "wall_brick_basement";
        public const string WoodPlanksWall = Env + "wall_wood_planks";
        public const string WoodDarkWall = Env + "wall_wood_dark";
        public const string TileRed = Env + "wall_tile_red";
        public const string TileWhiteDirty = Env + "wall_tile_white_dirty";
        public const string ConcreteBlock = Env + "wall_concrete_block";
        public const string SidingWhite = Env + "wall_siding_white";
        public const string BarnRed = Env + "wall_barn_red";
        public const string CorrugatedMetal = Env + "wall_corrugated_metal";
        // floors / ceilings / ground
        public const string FloorPlanks = Env + "floor_wood_planks";
        public const string FloorDark = Env + "floor_wood_dark";
        public const string Linoleum = Env + "floor_linoleum_dirty";
        public const string FloorConcrete = Env + "floor_concrete";
        public const string FloorTile = Env + "floor_tile_dirty";
        public const string Carpet = Env + "floor_carpet_stained";
        public const string CeilingPlaster = Env + "ceiling_plaster_stained";
        public const string CeilingWood = Env + "ceiling_wood";
        public const string Dirt = Env + "ground_dirt";
        public const string GrassDead = Env + "ground_grass_dead";
        public const string Gravel = Env + "ground_gravel";
        public const string Asphalt = Env + "ground_asphalt";
        public const string AsphaltCracked = Env + "ground_asphalt_cracked";
        public const string Shingles = Env + "roof_shingles";
        public const string TinRusty = Env + "roof_tin_rusty";
        // materials
        public const string MetalRusty = Env + "metal_rusty";
        public const string MetalGreen = Env + "metal_painted_green";
        public const string MetalDark = Env + "metal_dark";
        public const string Galvanized = Env + "metal_galvanized";
        public const string WoodRaw = Env + "wood_raw";
        public const string WoodFurniture = Env + "wood_furniture";
        public const string WoodWhite = Env + "wood_painted_white";
        public const string Mattress = Env + "fabric_mattress_stained";
        public const string Sofa = Env + "fabric_sofa";
        public const string Cloth = Env + "fabric_dirty";
        public const string Concrete = Env + "concrete_rough";
        public const string Bark = Env + "bark";
        public const string Meat = Env + "meat";
        public const string Hay = Env + "hay";
        public const string Tarp = Env + "tarp_blue";
        public const string Chainlink = Env + "chainlink";
        public const string BarbedWire = Env + "barbed_wire";
        // props
        public const string WardrobeFront = Props + "wardrobe_front";
        public const string DresserFront = Props + "dresser_front";
        public const string ClockFace = Props + "clock_face";
        public const string ClockBody = Props + "clock_body";
        public const string Radiator = Props + "radiator";
        public const string TvStatic0 = Props + "tv_static_0";
        public const string TvBody = Props + "tv_body";
        public const string FridgeFront = Props + "fridge_front";
        public const string PortraitOmar = Props + "portrait_omar";
        public const string PaintingLandscape = Props + "painting_landscape";
        public const string SignFallout = Props + "sign_fallout";
        public const string SignRestricted = Props + "sign_restricted";
        public const string SignRoad = Props + "sign_road";
        public const string BarrelWater = Props + "barrel_water";
        public const string BarrelFuel = Props + "barrel_fuel";
        public const string BucketFood = Props + "bucket_food";
        public const string Cardboard = Props + "box_cardboard";
        public const string Crate = Props + "crate_wood";
        public const string CarBody = Props + "car_body";
        public const string CarWreck = Props + "car_body_wreck";
        public const string CarFront = Props + "car_front";
        public const string CarRear = Props + "car_rear";
        public const string CarSide = Props + "car_side";
        public const string CarTire = Props + "car_tire";
        public const string Keypad = Props + "keypad";
        public const string FuseBox = Props + "fusebox";
        public const string RadioSet = Props + "radio_set";
        public const string Generator = Props + "generator";
        public const string CageBars = Props + "cage_bars";
        public const string MannequinBurnt = Props + "mannequin_burnt";
        public const string Flamingo = Props + "flamingo_pink";
        public const string WaterTowerTank = Props + "water_tower_tank";
        public const string SiloMetal = Props + "silo_metal";
        public const string UtilityPole = Props + "utility_pole";
        public const string WindowRed = Props + "window_red_glow";
        public const string WindowDim = Props + "window_dim_glow";
        public const string WindowDark = Props + "window_dark";
        public const string WindowBoarded = Props + "window_boarded";
        public const string DoorWood = Props + "door_wood";
        public const string DoorWoodDirty = Props + "door_wood_dirty";
        public const string DoorFront = Props + "door_front";
        public const string DoorShelter = Props + "door_metal_shelter";
        public const string DoorMetal = Props + "door_metal";
        public const string DoorPlanks = Props + "door_planks";
        public const string StairsWood = Props + "stairs_wood";
        public const string BedMetal = Props + "bed_frame_metal";
        public const string TableWood = Props + "table_wood";
        public const string ShelfMetal = Props + "shelf_metal";
        public const string PaperNote = Props + "paper_note";
        public const string HayBale = Props + "hay_bale";
        public const string MeatSlab = Props + "meat_slab";
        public const string BookSpines = Props + "book_spines";
        public const string BoardsNailed = Props + "boards_nailed";
        public const string PorchScreen = Props + "porch_screen";
        // foliage
        public const string TreeDead1 = Foliage + "tree_dead_1";
        public const string TreeDead2 = Foliage + "tree_dead_2";
        public const string TreeDead3 = Foliage + "tree_dead_3";
        public const string TreePine1 = Foliage + "tree_pine_1";
        public const string TreePine2 = Foliage + "tree_pine_2";
        public const string TreeBig = Foliage + "tree_big";
        public const string Bush1 = Foliage + "bush_dark_1";
        public const string Bush2 = Foliage + "bush_dark_2";
        public const string GrassTall1 = Foliage + "grass_tall_1";
        public const string GrassTall2 = Foliage + "grass_tall_2";
        public const string Cornstalks = Foliage + "cornstalks";
        public const string Weeds = Foliage + "weeds";
        public const string Treeline = Foliage + "treeline";
        // fx
        public const string BlobShadow = FX + "blob_shadow";
        public const string Glow = FX + "glow";
        public const string Fire0 = FX + "fire_0";
    }

    /// <summary>Material shortcuts (all cached by PsxMaterials).</summary>
    internal static class Mat
    {
        public static Material Lit(string tex, Color? tint = null) => PsxMaterials.Get(tex, PsxSurface.Lit, tint);
        public static Material Lit(string tex, float r, float g, float b) => PsxMaterials.Get(tex, PsxSurface.Lit, new Color(r, g, b, 1f));
        public static Material Cutout(string tex, Color? tint = null) => PsxMaterials.Get(tex, PsxSurface.LitCutout, tint);
        public static Material TwoSided(string tex, Color? tint = null) => PsxMaterials.Get(tex, PsxSurface.LitDoubleSided, tint);
        public static Material Decal(string name, Color? tint = null) => PsxMaterials.Get(Tex.Decals + name, PsxSurface.Decal, tint);
        public static Material Emissive(string tex, Color? tint = null) => PsxMaterials.Get(tex, PsxSurface.Emissive, tint);
        public static Material Unlit(string tex, Color? tint = null) => PsxMaterials.Get(tex, PsxSurface.Unlit, tint);
        public static Material Transparent(string tex, Color? tint = null) => PsxMaterials.Get(tex, PsxSurface.Transparent, tint);
        public static Material Flat(float r, float g, float b) => PsxMaterials.GetColor(new Color(r, g, b, 1f), PsxSurface.Lit);
        public static Material Glow(float r, float g, float b) => PsxMaterials.GetColor(new Color(r, g, b, 1f), PsxSurface.Emissive);
        public static Material Shadow() => PsxMaterials.Get(Tex.BlobShadow, PsxSurface.Decal, new Color(1f, 1f, 1f, 0.85f));
    }

    /// <summary>Vertex-color helpers: fake ambient occlusion and grime variation (lighting is per vertex).</summary>
    internal static class Shade
    {
        public static Color32 Gray(float v)
        {
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);
            return new Color32(b, b, b, 255);
        }

        public static Color32 Tinted(Color tint, float v)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(tint.r * v * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(tint.g * v * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(tint.b * v * 255f), 0, 255), 255);
        }

        /// <summary>Deterministic hash noise in [0,1) from a position (pure function, identical everywhere).</summary>
        public static float Hash(float x, float y, float z, int salt = 0)
        {
            unchecked
            {
                int ix = Mathf.FloorToInt(x * 7.31f), iy = Mathf.FloorToInt(y * 5.17f), iz = Mathf.FloorToInt(z * 6.93f);
                uint h = (uint)(ix * 73856093) ^ (uint)(iy * 19349663) ^ (uint)(iz * 83492791) ^ (uint)(salt * 2654435761u);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Hash(Vector3 p, int salt = 0) => Hash(p.x, p.y, p.z, salt);

        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Darkening factor near a contact edge: 'min' at distance 0, 1 at 'range'.</summary>
        public static float Edge(float distance, float min, float range) => Mathf.Lerp(min, 1f, Smooth01(distance / Mathf.Max(0.001f, range)));
    }

    internal static class MapMath
    {
        /// <summary>Yaw (degrees) for a prop whose FRONT (local -Z, MeshBuilder convention) must face <paramref name="dir"/>.</summary>
        public static float PropYawFacing(Vector3 dir) => Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;

        /// <summary>Yaw (degrees) for a character / camera whose forward (+Z) must face <paramref name="dir"/>.</summary>
        public static float YawFacing(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        public static Quaternion Yaw(float deg) => Quaternion.Euler(0f, deg, 0f);

        public static Pose LookPose(Vector3 eye, Vector3 target)
        {
            Vector3 d = target - eye;
            if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
            return new Pose(eye, Quaternion.LookRotation(d.normalized, Vector3.up));
        }

        public static Pose FacingPose(Vector3 feet, Vector3 dir)
        {
            dir.y = 0;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            return new Pose(feet, Quaternion.LookRotation(dir.normalized, Vector3.up));
        }

        public static Bounds MinMax(Vector3 min, Vector3 max)
        {
            var b = new Bounds();
            b.SetMinMax(Vector3.Min(min, max), Vector3.Max(min, max));
            return b;
        }

        public static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    }
}
