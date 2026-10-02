using System;
using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// "The base": the raised two-story farmhouse + basement (structure, stairs, exterior, doors).
    /// Room contents live in HouseRooms.cs (ground + upper) and BasementBuilder.cs.
    /// Wall centerlines: exterior x=±11, z=±8 (0.2 thick), interior 0.14 thick.
    /// </summary>
    internal static partial class HouseBuilder
    {
        public const float FB = -2.6f, CB = 0.4f, FG = 0.6f, CG = 3.6f, FU = 3.8f, CU = 6.6f, Eave = 6.8f, Ridge = 10f;
        public const float ExtT = 0.2f, IntT = 0.14f;
        public static readonly float[] FloorY = { FB, FG, FU };
        public static readonly float[] CeilY = { CB, CG, CU };

        public const string GlowBulbs = "HouseBulbs", GlowRed = "HouseRed", GlowBasement = "BasementBulbs", GlowRadio = "RadioRoom", GlowExterior = "ExteriorLights";

        public static readonly Color Warm = new Color(1f, 0.74f, 0.45f);
        public static readonly Color RedLamp = new Color(1f, 0.13f, 0.08f);

        internal sealed class Room
        {
            public string Area;
            public int Level;
            public float X0, Z0, X1, Z1;
            public WallSkin Skin;
            public Material FloorMat, CeilMat;
            public float FloorTile = 1.4f, CeilTile = 1.6f;
            public SurfaceType Surf = SurfaceType.Wood;
            public Color FloorTint = Color.white, CeilTint = Color.white;
            public readonly List<Rect> FloorHoles = new List<Rect>(), CeilHoles = new List<Rect>();
        }

        internal struct Opening
        {
            public int Level; public bool AlongX; public float C, Along, W, H;
        }

        /// <summary>Inner (finished) rect of a room = centerline rect minus half wall thicknesses.</summary>
        public static Rect Inner(Room r)
            => Rect.MinMaxRect(r.X0 + Thick(false, r.X0) * 0.5f, r.Z0 + Thick(true, r.Z0) * 0.5f, r.X1 - Thick(false, r.X1) * 0.5f, r.Z1 - Thick(true, r.Z1) * 0.5f);

        public static float Thick(bool alongX, float c)
            => alongX ? (Mathf.Abs(Mathf.Abs(c) - 8f) < 0.01f ? ExtT : IntT) : (Mathf.Abs(Mathf.Abs(c) - 11f) < 0.01f ? ExtT : IntT);

        static readonly Rect DownStairHole = Rect.MinMaxRect(-2.93f, -2f, -1.6f, 5f);
        static readonly Rect UpStairHole = Rect.MinMaxRect(1.6f, -2f, 2.93f, 5f);

        // ------------------------------------------------------------------ tables

        static List<Room> Rooms()
        {
            var green = Mat.Lit(Tex.WallpaperGreen);
            var wains = Mat.Lit(Tex.Wainscot);
            var trim = Mat.Lit(Tex.WoodFurniture, new Color(0.6f, 0.55f, 0.5f));
            var plaster = Mat.Lit(Tex.CeilingPlaster);
            var rooms = new List<Room>();
            Room R(string area, int lvl, float x0, float z0, float x1, float z1, WallSkin skin, string floor, SurfaceType s, string ceil = Tex.CeilingPlaster)
            {
                var r = new Room { Area = area, Level = lvl, X0 = x0, Z0 = z0, X1 = x1, Z1 = z1, Skin = skin, FloorMat = Mat.Lit(floor), CeilMat = Mat.Lit(ceil), Surf = s };
                rooms.Add(r);
                return r;
            }
            // ground floor
            var hall = R("House.Hall", 1, -3, -8, 3, 8, WallSkin.Of(green, wains, 1.0f, trim), Tex.FloorDark, SurfaceType.Wood);
            hall.FloorHoles.Add(DownStairHole); hall.CeilHoles.Add(UpStairHole);
            R("House.Dining", 1, -11, -8, -3, 0, WallSkin.Of(Mat.Lit(Tex.WallpaperGreen, new Color(0.85f, 0.9f, 0.8f)), Mat.Lit(Tex.WoodDarkWall), 1.1f, trim), Tex.FloorPlanks, SurfaceType.Wood);
            R("House.Kitchen", 1, -11, 0, -3, 8, WallSkin.Of(Mat.Lit(Tex.PlasterDirty, new Color(0.8f, 0.75f, 0.65f)), Mat.Lit(Tex.TileWhiteDirty), 1.4f, null), Tex.Linoleum, SurfaceType.Tile)
                .Skin.LowerTileU = 1.4f;
            R("House.Living", 1, 3, -8, 11, -1, WallSkin.Of(Mat.Lit(Tex.WallpaperRose, new Color(0.9f, 0.85f, 0.8f)), wains, 0.9f, trim), Tex.Carpet, SurfaceType.Carpet);
            var bath = R("House.Bathroom", 1, 3, -1, 11, 3, WallSkin.Of(Mat.Lit(Tex.PlasterDirty, new Color(0.85f, 0.85f, 0.8f)), Mat.Lit(Tex.TileWhiteDirty), 1.6f, null), Tex.FloorTile, SurfaceType.Tile);
            bath.Skin.LowerTileU = 1.6f; bath.FloorTile = 1.0f;
            R("House.ClockBedroom", 1, 3, 3, 11, 8, WallSkin.Of(Mat.Lit(Tex.WallpaperBlue), null, 0f, null, 1.3f), Tex.FloorPlanks, SurfaceType.Wood);
            // upper floor
            var uhall = R("House.UpperHall", 2, -3, -8, 3, 8, WallSkin.Of(green, wains, 1.0f, trim), Tex.FloorDark, SurfaceType.Wood);
            uhall.FloorHoles.Add(UpStairHole);
            var cage = R("House.CageRoom", 2, -11, -8, -3, 2, WallSkin.Of(Mat.Lit(Tex.WallpaperYellow), null, 0f, null, 1.4f), Tex.FloorPlanks, SurfaceType.Wood);
            cage.FloorTint = new Color(0.7f, 0.62f, 0.52f);
            R("House.Storage", 2, -11, 2, -3, 8, WallSkin.Of(Mat.Lit(Tex.PlasterDirty, new Color(0.75f, 0.7f, 0.65f)), Mat.Lit(Tex.WoodDarkWall), 1.0f, trim), Tex.FloorPlanks, SurfaceType.Wood);
            R("House.RadioRoom", 2, 3, -8, 11, -1, WallSkin.Of(Mat.Lit(Tex.WoodDarkWall), null, 0f, null, 1.6f), Tex.Carpet, SurfaceType.Carpet).FloorTint = new Color(0.6f, 0.65f, 0.6f);
            R("House.Closet", 2, 3, -1, 11, 3, WallSkin.Of(Mat.Lit(Tex.WoodPlanksWall), null, 0f, null, 1.6f), Tex.FloorPlanks, SurfaceType.Wood, Tex.CeilingWood);
            R("House.Study", 2, 3, 3, 11, 8, WallSkin.Of(Mat.Lit(Tex.WallpaperRose), null, 0f, null, 1.4f), Tex.FloorPlanks, SurfaceType.Wood);
            // basement
            var bplaster = Mat.Lit(Tex.PlasterDirty);
            var brick = Mat.Lit(Tex.BrickBasement);
            var corr = R("Basement.Corridor", 0, -3, -8, 3, 8, WallSkin.Of(bplaster, brick, 0.9f, null), Tex.FloorConcrete, SurfaceType.Concrete, Tex.CeilingWood);
            corr.CeilHoles.Add(DownStairHole);
            R("Basement.Generator", 0, 3, -8, 11, 0, WallSkin.Of(bplaster, brick, 0.9f, null), Tex.FloorConcrete, SurfaceType.Concrete, Tex.CeilingWood);
            R("Basement.Furnace", 0, 3, 0, 11, 8, WallSkin.Of(Mat.Lit(Tex.BrickBasement, new Color(0.7f, 0.62f, 0.58f)), null, 0f, null, 1.3f), Tex.FloorConcrete, SurfaceType.Concrete, Tex.CeilingWood);
            R("Basement.Antechamber", 0, -11, -8, -3, 0, WallSkin.Of(Mat.Lit(Tex.ConcreteBlock), null, 0f, null, 1.6f), Tex.FloorConcrete, SurfaceType.Concrete, Tex.Concrete);
            R("Basement.Exhibit", 0, -11, 0, -3, 8, WallSkin.Of(Mat.Lit(Tex.TileRed), null, 0f, null, 1.2f), Tex.FloorTile, SurfaceType.Tile);
            return rooms;
        }

        static List<DoorSpec> DoorSpecs(MeshBuilder[] mbs)
        {
            var l = new List<DoorSpec>();
            DoorSpec D(string name, DoorKind kind, int lvl, float x, float z, bool alongX, Vector3 swing, float rMin, float rMax, string swingArea, string otherArea)
            {
                var s = new DoorSpec
                {
                    Name = name, Kind = kind, Center = new Vector3(x, FloorY[lvl], z), AlongX = alongX, Swing = swing,
                    RoomMin = rMin, RoomMax = rMax, AreaSwing = swingArea, AreaOther = otherArea, FrameMb = mbs[lvl],
                    WallT = alongX ? Thick(true, z) : Thick(false, x),
                };
                l.Add(s);
                return s;
            }
            Vector3 px = Vector3.right, nx = Vector3.left, pz = Vector3.forward, nz = Vector3.back;
            // ground floor (ids 0..8)
            D("FrontDoor", DoorKind.Front, 1, 0, -8, true, pz, -3, 3, "House.Hall", "Porch").OpenDegrees = 100f;
            D("BackDoor", DoorKind.Front, 1, 0, 8, true, nz, -3, 3, "House.Hall", "Yard").OpenDegrees = 100f;
            D("Dining", DoorKind.Wood, 1, -3, -5, false, nx, -8, 0, "House.Dining", "House.Hall");
            D("Kitchen", DoorKind.WoodDirty, 1, -3, 6.5f, false, nx, 0, 8, "House.Kitchen", "House.Hall");
            D("KitchenDining", DoorKind.WoodDirty, 1, -7, 0, true, nz, -11, -3, "House.Dining", "House.Kitchen");
            D("KitchenSide", DoorKind.Front, 1, -11, 4, false, px, 0, 8, "House.Kitchen", "Yard").OpenDegrees = 100f;
            D("Living", DoorKind.Wood, 1, 3, -5, false, px, -8, -1, "House.Living", "House.Hall");
            D("Bathroom", DoorKind.Boarded, 1, 7, -1, true, pz, 3, 11, "House.Bathroom", "House.Living");
            D("ClockBedroom", DoorKind.Wood, 1, 3, 6.5f, false, px, 3, 8, "House.ClockBedroom", "House.Hall");
            // upper floor (9..13)
            D("CageRoom", DoorKind.WoodDirty, 2, -3, -5, false, nx, -8, 2, "House.CageRoom", "House.UpperHall");
            D("Storage", DoorKind.Wood, 2, -3, 6.5f, false, nx, 2, 8, "House.Storage", "House.UpperHall");
            var radio = D("RadioRoom", DoorKind.Wood, 2, 3, -5, false, px, -8, -1, "House.RadioRoom", "House.UpperHall");
            radio.Locked = true; radio.Key = ItemType.Lockpick;
            D("Study", DoorKind.Wood, 2, 3, 6.5f, false, px, 3, 8, "House.Study", "House.UpperHall");
            D("SupplyCloset", DoorKind.Closet, 2, 7, 3, true, nz, 3, 11, "House.Closet", "House.Study");
            // basement (14..17)
            D("Generator", DoorKind.Metal, 0, 3, -5, false, px, -8, 0, "Basement.Generator", "Basement.Corridor");
            D("FurnaceRoom", DoorKind.Metal, 0, 3, 6, false, px, 0, 8, "Basement.Furnace", "Basement.Corridor");
            D("Antechamber", DoorKind.Metal, 0, -3, -6, false, nx, -8, 0, "Basement.Antechamber", "Basement.Corridor");
            D("Exhibit", DoorKind.WoodDirty, 0, -3, 6.5f, false, nx, 0, 8, "Basement.Exhibit", "Basement.Corridor");
            return l;
        }

        /// <summary>Windows: level, side (S/N/E/W), coordinate along the wall, kind.</summary>
        static readonly (int lvl, char side, float along, WindowKind kind)[] Windows =
        {
            (1, 'S', -9f, WindowKind.Red), (1, 'S', -5f, WindowKind.Red), (1, 'S', 5f, WindowKind.Red), (1, 'S', 9f, WindowKind.Boarded),
            (1, 'N', -5.5f, WindowKind.Dark), (1, 'N', 5f, WindowKind.Red), (1, 'N', 9.7f, WindowKind.Dark),
            (1, 'E', -6.8f, WindowKind.Dark), (1, 'E', 1f, WindowKind.Dark), (1, 'E', 7f, WindowKind.Dark),
            (1, 'W', -6.5f, WindowKind.Dark), (1, 'W', 1.5f, WindowKind.Boarded), (1, 'W', 6.8f, WindowKind.Dark),
            (2, 'S', -9f, WindowKind.Red), (2, 'S', -5f, WindowKind.Red), (2, 'S', 0f, WindowKind.Red), (2, 'S', 5f, WindowKind.Dark), (2, 'S', 9f, WindowKind.Dark),
            (2, 'N', -9f, WindowKind.Red), (2, 'N', -5f, WindowKind.Dark), (2, 'N', 0f, WindowKind.Dark), (2, 'N', 5f, WindowKind.Red), (2, 'N', 9f, WindowKind.Dark),
            (2, 'E', -4.5f, WindowKind.Dark), (2, 'E', 1f, WindowKind.Boarded), (2, 'E', 7.2f, WindowKind.Dark),
            (2, 'W', -4f, WindowKind.Boarded), (2, 'W', 5f, WindowKind.Dark),
        };

        /// <summary>Red lamps behind the red curtains (inside the rooms).</summary>
        static readonly Vector3[] RedLamps =
        {
            new Vector3(-7f, FG + 1.7f, -7.2f), new Vector3(5f, FG + 1.7f, -7.2f), new Vector3(5f, FG + 1.7f, 7.2f),
            new Vector3(-7f, FU + 1.7f, -7.2f), new Vector3(0f, FU + 1.7f, -7.3f), new Vector3(-9f, FU + 2.2f, 7.0f), new Vector3(5f, FU + 1.7f, 7.2f),
        };

        // ------------------------------------------------------------------ build

        public static void Build(MapContext ctx)
        {
            var mbs = new[] { ctx.NewBuilder("House_Basement"), ctx.NewBuilder("House_Ground"), ctx.NewBuilder("House_Upper") };
            var ext = ctx.NewBuilder("House_Exterior");
            var specs = DoorSpecs(mbs);
            var openings = new List<Opening>();
            foreach (var s in specs)
            {
                int lvl = Array.IndexOf(FloorY, s.Center.y);
                openings.Add(new Opening { Level = lvl, AlongX = s.AlongX, C = s.AlongX ? s.Center.z : s.Center.x, Along = s.AlongX ? s.Center.x : s.Center.z, W = s.HoleW, H = s.HoleH });
            }
            openings.Add(new Opening { Level = 0, AlongX = false, C = -11f, Along = -4f, W = 1.1f, H = 2.2f }); // fallout shelter door
            var rooms = Rooms();

            Safe("house shells", () =>
            {
                foreach (var r in rooms) Shell(ctx, mbs[r.Level], r, openings);
                WallColliders(ctx, openings);
                foreach (var r in rooms) SlabColliders(ctx, r);
                Arch.SlabCollider(ctx, Rect.MinMaxRect(-11.1f, -8.1f, 11.1f, 8.1f), Eave, 0.2f, null, SurfaceType.Wood, "AtticFloor");
            });
            Safe("house stairs", () => Stairs(ctx, mbs));
            Safe("house exterior", () => ExteriorShell(ctx, ext, openings));
            Safe("house roof", () => Roof(ctx, ext));
            Safe("house windows", () => WindowsAndRedLamps(ctx, ext, mbs));
            Safe("porch", () => Porch(ctx, ext));
            Safe("house doors", () => { foreach (var s in specs) Dyn.Door(ctx, s); });
            Safe("house nav", () => HouseNav(ctx));
            Safe("ground floor rooms", () => GroundRooms(ctx, mbs[1]));
            Safe("upper floor rooms", () => UpperRooms(ctx, mbs[2]));
            Safe("basement rooms", () => BasementBuilder.Rooms(ctx, mbs[0]));
        }

        internal static void Safe(string what, Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogError("[MapBuilder] " + what + " failed: " + e); }
        }

        static List<Hole> HolesOn(List<Opening> ops, int level, bool alongX, float c, float lo, float hi)
        {
            var list = new List<Hole>();
            for (int i = 0; i < ops.Count; i++)
            {
                var o = ops[i];
                if (o.Level != level || o.AlongX != alongX || Mathf.Abs(o.C - c) > 0.01f) continue;
                if (o.Along < lo || o.Along > hi) continue;
                list.Add(new Hole(o.Along - o.W * 0.5f, o.Along + o.W * 0.5f, FloorY[level], FloorY[level] + o.H));
            }
            return list;
        }

        static void Shell(MapContext ctx, MeshBuilder mb, Room r, List<Opening> ops)
        {
            float fy = FloorY[r.Level], cy = CeilY[r.Level];
            var inner = Inner(r);
            float ix0 = inner.xMin, ix1 = inner.xMax, iz0 = inner.yMin, iz1 = inner.yMax;
            Arch.FaceN(mb, true, iz0, ix0, ix1, +1, fy, cy, HolesOn(ops, r.Level, true, r.Z0, r.X0, r.X1), r.Skin, fy, cy);
            Arch.FaceN(mb, true, iz1, ix0, ix1, -1, fy, cy, HolesOn(ops, r.Level, true, r.Z1, r.X0, r.X1), r.Skin, fy, cy);
            Arch.FaceN(mb, false, ix0, iz0, iz1, +1, fy, cy, HolesOn(ops, r.Level, false, r.X0, r.Z0, r.Z1), r.Skin, fy, cy);
            Arch.FaceN(mb, false, ix1, iz0, iz1, -1, fy, cy, HolesOn(ops, r.Level, false, r.X1, r.Z0, r.Z1), r.Skin, fy, cy);
            Arch.Flat(mb, ix0, iz0, ix1, iz1, fy, r.FloorMat, r.FloorTile, r.FloorHoles, true, r.FloorTint, 0.55f);
            Arch.Flat(mb, ix0, iz0, ix1, iz1, cy, r.CeilMat, r.CeilTile, r.CeilHoles, false, r.CeilTint, 0.65f, 0.7f);
            // baseboards (not in tiled / concrete rooms)
            if (r.Level > 0 && r.Surf != SurfaceType.Tile)
            {
                var bb = Mat.Lit(Tex.WoodFurniture, new Color(0.45f, 0.4f, 0.35f));
                Baseboard(mb, bb, true, iz0 + 0.012f, ix0, ix1, fy, HolesOn(ops, r.Level, true, r.Z0, r.X0, r.X1));
                Baseboard(mb, bb, true, iz1 - 0.012f, ix0, ix1, fy, HolesOn(ops, r.Level, true, r.Z1, r.X0, r.X1));
                Baseboard(mb, bb, false, ix0 + 0.012f, iz0, iz1, fy, HolesOn(ops, r.Level, false, r.X0, r.Z0, r.Z1));
                Baseboard(mb, bb, false, ix1 - 0.012f, iz0, iz1, fy, HolesOn(ops, r.Level, false, r.X1, r.Z0, r.Z1));
            }
            ctx.Area(r.Area, new Vector3(r.X0, fy - 0.1f, r.Z0), new Vector3(r.X1, cy + 0.1f, r.Z1));
        }

        static void Baseboard(MeshBuilder mb, Material m, bool alongX, float c, float lo, float hi, float fy, List<Hole> holes)
        {
            holes.Sort((a, b) => a.U0.CompareTo(b.U0));
            float cur = lo;
            mb.Material = m;
            mb.Color = Shade.Gray(0.6f);
            for (int i = 0; i <= holes.Count; i++)
            {
                float end = i < holes.Count ? holes[i].U0 : hi;
                if (end - cur > 0.05f)
                {
                    float mid = (cur + end) * 0.5f;
                    var center = alongX ? new Vector3(mid, fy + 0.06f, c) : new Vector3(c, fy + 0.06f, mid);
                    var size = alongX ? new Vector3(end - cur, 0.12f, 0.025f) : new Vector3(0.025f, 0.12f, end - cur);
                    mb.AddBox(center, size, BoxUV.Local, 0.6f, 0f, BoxFaces.NoBottom);
                }
                if (i < holes.Count) cur = Mathf.Max(cur, holes[i].U1);
            }
            mb.Color = Shade.Gray(1f);
        }

        static void WallColliders(MapContext ctx, List<Opening> ops)
        {
            // interior lines per level: (level, alongX, c, from, to)
            var lines = new List<(int, bool, float, float, float)>
            {
                (0, false, -3f, -7.9f, 7.9f), (0, false, 3f, -7.9f, 7.9f), (0, true, 0f, -10.9f, -3.07f), (0, true, 0f, 3.07f, 10.9f),
                (1, false, -3f, -7.9f, 7.9f), (1, false, 3f, -7.9f, 7.9f), (1, true, 0f, -10.9f, -3.07f), (1, true, -1f, 3.07f, 10.9f), (1, true, 3f, 3.07f, 10.9f),
                (2, false, -3f, -7.9f, 7.9f), (2, false, 3f, -7.9f, 7.9f), (2, true, 2f, -10.9f, -3.07f), (2, true, -1f, 3.07f, 10.9f), (2, true, 3f, 3.07f, 10.9f),
            };
            foreach (var (lvl, ax, c, a, b) in lines)
                Arch.WallColliderN(ctx, ax, c, a, b, FloorY[lvl], CeilY[lvl], IntT, HolesOn(ops, lvl, ax, c, a, b), lvl == 0 ? SurfaceType.Concrete : SurfaceType.Wood, "Wall");
            // exterior walls, one collider band per level (basement band reaches the ground + foundation)
            float[] y0 = { FB - 0.3f, FG, FU }, y1 = { FG, FU, Eave };
            for (int lvl = 0; lvl < 3; lvl++)
            {
                var s = lvl == 0 ? SurfaceType.Concrete : SurfaceType.Wood;
                Arch.WallColliderN(ctx, true, -8f, -11.1f, 11.1f, y0[lvl], y1[lvl], ExtT, HolesOn(ops, lvl, true, -8f, -11.1f, 11.1f), s, "ExtWall");
                Arch.WallColliderN(ctx, true, 8f, -11.1f, 11.1f, y0[lvl], y1[lvl], ExtT, HolesOn(ops, lvl, true, 8f, -11.1f, 11.1f), s, "ExtWall");
                Arch.WallColliderN(ctx, false, -11f, -7.9f, 7.9f, y0[lvl], y1[lvl], ExtT, HolesOn(ops, lvl, false, -11f, -7.9f, 7.9f), s, "ExtWall");
                Arch.WallColliderN(ctx, false, 11f, -7.9f, 7.9f, y0[lvl], y1[lvl], ExtT, HolesOn(ops, lvl, false, 11f, -7.9f, 7.9f), s, "ExtWall");
            }
        }

        static void SlabColliders(MapContext ctx, Room r)
        {
            float top = FloorY[r.Level];
            float thick = r.Level == 0 ? 0.3f : 0.2f;
            Arch.SlabCollider(ctx, Rect.MinMaxRect(r.X0, r.Z0, r.X1, r.Z1), top, thick, r.FloorHoles, r.Surf, "Floor_" + r.Area);
        }

        // ------------------------------------------------------------------ stairs

        static void Stairs(MapContext ctx, MeshBuilder[] mbs)
        {
            var tread = Mat.Lit(Tex.StairsWood);
            var side = Mat.Lit(Tex.WoodFurniture, new Color(0.7f, 0.65f, 0.6f));
            var rail = Mat.Lit(Tex.WoodWhite, new Color(0.75f, 0.73f, 0.66f));
            var slabEdge = WallSkin.Of(Mat.Lit(Tex.WoodFurniture, new Color(0.5f, 0.45f, 0.4f)));
            slabEdge.CornerAo = false; slabEdge.AoFloor = 1f; slabEdge.AoCeil = 1f;
            // up: ground hall east side, bottom z=-2 (y 0.6) -> top z=5 (y 3.8)
            Arch.Stair(ctx, mbs[1], new Vector3(2.265f, FG, -2f), Vector3.forward, 1.33f, FU - FG, 7f, 16, FG, tread, side, SurfaceType.Wood, "StairUp");
            Arch.Railing(ctx, mbs[1], new Vector3(1.6f, FG, -2f), new Vector3(1.6f, FU, 5f), 0.9f, rail, true, 0.3f, "StairUpRail");
            Arch.Railing(ctx, mbs[2], new Vector3(1.55f, FU, -2.05f), new Vector3(1.55f, FU, 5f), 0.95f, rail, true, 0.28f, "WellRail");
            Arch.Railing(ctx, mbs[2], new Vector3(1.55f, FU, -2.05f), new Vector3(2.93f, FU, -2.05f), 0.95f, rail, true, 0.28f, "WellRail");
            Arch.FaceN(mbs[2], false, 1.6f, -2f, 5f, +1, CG, FU, null, slabEdge);
            Arch.FaceN(mbs[2], true, -2f, 1.6f, 2.93f, +1, CG, FU, null, slabEdge);
            Arch.FaceN(mbs[2], false, 2.93f, -2f, 5f, -1, CG, FU, null, slabEdge);
            // down: ground hall west side, top z=5 (y 0.6) -> bottom z=-2 (y -2.6); solid underneath
            Arch.Stair(ctx, mbs[0], new Vector3(-2.265f, FB, -2f), Vector3.forward, 1.33f, FG - FB, 7f, 16, FB, tread, side, SurfaceType.Wood, "StairDown");
            Arch.Railing(ctx, mbs[1], new Vector3(-1.55f, FG, -2.05f), new Vector3(-1.55f, FG, 5f), 0.95f, rail, true, 0.28f, "OpeningRail");
            Arch.Railing(ctx, mbs[1], new Vector3(-2.93f, FG, -2.05f), new Vector3(-1.55f, FG, -2.05f), 0.95f, rail, true, 0.28f, "OpeningRail");
            Arch.FaceN(mbs[1], false, -1.6f, -2f, 5f, -1, CB, FG, null, slabEdge);
            Arch.FaceN(mbs[1], true, -2f, -2.93f, -1.6f, +1, CB, FG, null, slabEdge);
            Arch.FaceN(mbs[0], false, -2.93f, -2f, 5f, +1, CB, FG, null, slabEdge);
            // basement handrail on the wall + newel at the foot
            var mb = mbs[0];
            mb.Material = side;
            mb.Color = Shade.Gray(0.7f);
            mb.AddBeam(new Vector3(-2.86f, FB + 0.9f, -2f), new Vector3(-2.86f, FG + 0.9f, 5f), 0.05f);
            mb.AddBox(new Vector3(-1.6f, FB + 0.5f, -2.05f), new Vector3(0.1f, 1.0f, 0.1f), BoxUV.Local, 0.5f);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(new Vector3(-1.6f, FB + 0.5f, -2.05f), new Vector3(0.1f, 1.0f, 0.1f), SurfaceType.Wood, "Newel");
        }

        // ------------------------------------------------------------------ exterior shell

        static WallSkin SidingSkin()
        {
            var s = WallSkin.Of(Mat.Lit(Tex.SidingWhite), Mat.Lit(Tex.Concrete, new Color(0.6f, 0.58f, 0.55f)), FG, Mat.Lit(Tex.WoodWhite, new Color(0.7f, 0.7f, 0.66f)), 2.0f);
            s.AoFloor = 0.45f; s.AoCeil = 0.7f; s.AoCorner = 0.8f; s.Grime = 0.2f; s.LowerTileU = 1.5f;
            return s;
        }

        static void ExteriorShell(MapContext ctx, MeshBuilder ext, List<Opening> ops)
        {
            var skin = SidingSkin();
            Arch.FaceN(ext, true, -8.1f, -11.1f, 11.1f, -1, 0f, Eave, HolesOn(ops, 1, true, -8f, -11.1f, 11.1f), skin, 0f, Eave);
            Arch.FaceN(ext, true, 8.1f, -11.1f, 11.1f, +1, 0f, Eave, HolesOn(ops, 1, true, 8f, -11.1f, 11.1f), skin, 0f, Eave);
            Arch.FaceN(ext, false, -11.1f, -8.1f, 8.1f, -1, 0f, Eave, HolesOn(ops, 1, false, -11f, -8.1f, 8.1f), skin, 0f, Eave);
            Arch.FaceN(ext, false, 11.1f, -8.1f, 8.1f, +1, 0f, Eave, HolesOn(ops, 1, false, 11f, -8.1f, 8.1f), skin, 0f, Eave);
            var siding = Mat.Lit(Tex.SidingWhite);
            Arch.Gable(ext, new Vector2(-11.1f, 8.1f), new Vector2(-11.1f, -8.1f), Eave, Ridge, siding, 2f, new Color(0.85f, 0.85f, 0.85f));
            Arch.Gable(ext, new Vector2(11.1f, -8.1f), new Vector2(11.1f, 8.1f), Eave, Ridge, siding, 2f, new Color(0.85f, 0.85f, 0.85f));
            // corner boards + band between floors
            var trim = Mat.Lit(Tex.WoodWhite, new Color(0.8f, 0.8f, 0.76f));
            ext.Material = trim;
            ext.Color = Shade.Gray(0.75f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    ext.AddBox(new Vector3(sx * 11.12f, (FG + Eave) * 0.5f, sz * 8.12f), new Vector3(0.18f, Eave - FG, 0.18f), BoxUV.Local, 1f, 1.5f);
            ext.AddBox(new Vector3(0, CG + 0.1f, -8.14f), new Vector3(22.2f, 0.16f, 0.06f), BoxUV.Local, 1f, 2f, BoxFaces.All & ~BoxFaces.PosZ);
            ext.AddBox(new Vector3(0, CG + 0.1f, 8.14f), new Vector3(22.2f, 0.16f, 0.06f), BoxUV.Local, 1f, 2f, BoxFaces.All & ~BoxFaces.NegZ);
            ext.AddBox(new Vector3(-11.14f, CG + 0.1f, 0), new Vector3(0.06f, 0.16f, 16.2f), BoxUV.Local, 1f, 2f, BoxFaces.All & ~BoxFaces.PosX);
            ext.AddBox(new Vector3(11.14f, CG + 0.1f, 0), new Vector3(0.06f, 0.16f, 16.2f), BoxUV.Local, 1f, 2f, BoxFaces.All & ~BoxFaces.NegX);
            ext.Color = Shade.Gray(1f);
            // door frames on the outside are part of the door frames (casing on both faces) - add thresholds
            ext.Material = Mat.Lit(Tex.Concrete);
            ext.AddBox(new Vector3(0, FG - 0.02f, -8.2f), new Vector3(1.2f, 0.04f, 0.2f), BoxUV.Local, 0.5f);
            // grime / water stains on the siding
            var rng = ctx.Rng("house.grime");
            for (int i = 0; i < 18; i++)
            {
                int side = i % 4;
                float along = rng.Range(-10f, 10f) * (side < 2 ? 1f : 0.75f);
                float y = rng.Range(0.8f, 6.0f);
                string tex = rng.Chance(0.5f) ? "water_stain" : "grime";
                Vector3 p, n;
                switch (side)
                {
                    case 0: p = new Vector3(along, y, -8.1f); n = Vector3.back; break;
                    case 1: p = new Vector3(along, y, 8.1f); n = Vector3.forward; break;
                    case 2: p = new Vector3(-11.1f, y, along * 0.75f); n = Vector3.left; break;
                    default: p = new Vector3(11.1f, y, along * 0.75f); n = Vector3.right; break;
                }
                Arch.Decal(ext, Mat.Decal(tex), p, n, rng.Range(0.8f, 2.0f), rng.Range(1.0f, 2.5f), rng.Range(-10f, 10f));
            }
            ctx.Area("House", new Vector3(-11.2f, FB - 0.1f, -8.2f), new Vector3(11.2f, Ridge, 8.2f));
        }

        static void Roof(MapContext ctx, MeshBuilder ext)
        {
            float slope = (Ridge - Eave) / 8.1f;
            float overhang = 0.6f, rake = 0.45f;
            float zE = 8.1f + overhang, yE = Eave - overhang * slope;
            float x0 = -11.1f - rake, x1 = 11.1f + rake;
            var shingles = Mat.Lit(Tex.Shingles);
            var soffit = Mat.Lit(Tex.CeilingWood, new Color(0.55f, 0.52f, 0.48f));
            var fascia = Mat.Lit(Tex.WoodWhite, new Color(0.6f, 0.6f, 0.58f));
            ext.Material = shingles;
            ext.Color = Shade.Gray(0.8f);
            ext.AddPlane(new Vector3(x0, yE, -zE), new Vector3(x1 - x0, 0, 0), new Vector3(0, Ridge - yE, zE), 2.2f, 2.0f);
            ext.AddPlane(new Vector3(x1, yE, zE), new Vector3(x0 - x1, 0, 0), new Vector3(0, Ridge - yE, -zE), 2.2f, 2.0f);
            // ridge cap
            ext.AddBox(new Vector3(0, Ridge + 0.04f, 0), new Vector3(x1 - x0, 0.1f, 0.35f), BoxUV.Local, 1f, 2f);
            // soffits (eave strips) facing down
            ext.Material = soffit;
            ext.Color = Shade.Gray(0.5f);
            float t = 0.14f;
            ext.AddPlane(new Vector3(x1, yE - t, -zE), new Vector3(x0 - x1, 0, 0), new Vector3(0, overhang * slope, overhang), 1.5f, 2f);
            ext.AddPlane(new Vector3(x0, yE - t, zE), new Vector3(x1 - x0, 0, 0), new Vector3(0, overhang * slope, -overhang), 1.5f, 2f);
            // rake undersides (gable overhangs) facing down
            for (int s = -1; s <= 1; s += 2)
            {
                float xa = s < 0 ? x0 : 11.1f, xb = s < 0 ? -11.1f : x1;
                ext.AddPlane(new Vector3(xb, yE - t, -zE), new Vector3(xa - xb, 0, 0), new Vector3(0, Ridge - yE, zE), 1.5f, 2f);
                ext.AddPlane(new Vector3(xa, yE - t, zE), new Vector3(xb - xa, 0, 0), new Vector3(0, Ridge - yE, -zE), 1.5f, 2f);
            }
            // fascia along eaves and rakes
            ext.Material = fascia;
            ext.Color = Shade.Gray(0.7f);
            ext.AddBox(new Vector3(0, yE - 0.06f, -zE), new Vector3(x1 - x0, 0.2f, 0.05f), BoxUV.Local, 1f, 2f);
            ext.AddBox(new Vector3(0, yE - 0.06f, zE), new Vector3(x1 - x0, 0.2f, 0.05f), BoxUV.Local, 1f, 2f);
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s < 0 ? x0 : x1;
                ext.AddBeam(new Vector3(x, yE - 0.06f, -zE), new Vector3(x, Ridge - 0.06f, 0), 0.12f);
                ext.AddBeam(new Vector3(x, yE - 0.06f, zE), new Vector3(x, Ridge - 0.06f, 0), 0.12f);
            }
            // chimney
            ext.Material = Mat.Lit(Tex.BrickBasement, new Color(0.55f, 0.35f, 0.3f));
            ext.Color = Shade.Gray(0.75f);
            ext.AddBox(new Vector3(-7f, 9.2f, 3.2f), new Vector3(0.95f, 4.6f, 0.95f), BoxUV.Local, 1f, 1.2f);
            ext.Material = Mat.Lit(Tex.Concrete);
            ext.AddBox(new Vector3(-7f, 11.55f, 3.2f), new Vector3(1.1f, 0.12f, 1.1f), BoxUV.Local, 1f);
            ext.Color = Shade.Gray(1f);
            // colliders
            float len = Mathf.Sqrt((Ridge - yE) * (Ridge - yE) + zE * zE);
            var dirS = new Vector3(0, Ridge - yE, zE).normalized;
            var rotS = Quaternion.LookRotation(dirS, Vector3.up);
            ctx.Solid(new Vector3(0, (yE + Ridge) * 0.5f, -zE * 0.5f) - rotS * Vector3.up * 0.1f, new Vector3(x1 - x0, 0.2f, len), rotS, SurfaceType.Wood, "Roof");
            var dirN = new Vector3(0, Ridge - yE, -zE).normalized;
            var rotN = Quaternion.LookRotation(dirN, Vector3.up);
            ctx.Solid(new Vector3(0, (yE + Ridge) * 0.5f, zE * 0.5f) - rotN * Vector3.up * 0.1f, new Vector3(x1 - x0, 0.2f, len), rotN, SurfaceType.Wood, "Roof");
            ctx.Solid(new Vector3(-11.1f, (Eave + Ridge) * 0.5f, 0), new Vector3(0.2f, Ridge - Eave, 16.2f), SurfaceType.Wood, "Gable");
            ctx.Solid(new Vector3(11.1f, (Eave + Ridge) * 0.5f, 0), new Vector3(0.2f, Ridge - Eave, 16.2f), SurfaceType.Wood, "Gable");
        }

        static void WindowsAndRedLamps(MapContext ctx, MeshBuilder ext, MeshBuilder[] mbs)
        {
            var glow = ctx.GlowBuilder(GlowRed);
            for (int i = 0; i < RedLamps.Length; i++)
                ctx.Light(RedLamps[i], RedLamp, 1.05f, 4.6f, PsxFlicker.Pulse, LightGroup.Power, "RedLamp_" + i, 0.12f, 0.6f, GlowRed);
            foreach (var (lvl, side, along, kind) in Windows)
            {
                float y = FloorY[lvl] + 1.5f;
                Vector3 outN, outP, inP;
                switch (side)
                {
                    case 'S': outN = Vector3.back; outP = new Vector3(along, y, -8.1f); inP = new Vector3(along, y, -7.9f); break;
                    case 'N': outN = Vector3.forward; outP = new Vector3(along, y, 8.1f); inP = new Vector3(along, y, 7.9f); break;
                    case 'W': outN = Vector3.left; outP = new Vector3(-11.1f, y, along); inP = new Vector3(-10.9f, y, along); break;
                    default: outN = Vector3.right; outP = new Vector3(11.1f, y, along); inP = new Vector3(10.9f, y, along); break;
                }
                Arch.Window(ctx, ext, glow, outP, outN, 0.95f, 1.25f, kind, true);
                Arch.Window(ctx, mbs[lvl], glow, inP, -outN, 0.95f, 1.25f, kind, false);
            }
            // attic gable windows
            Arch.Window(ctx, ext, glow, new Vector3(-11.1f, 8.2f, 0), Vector3.left, 0.6f, 0.8f, WindowKind.Dark, true);
            Arch.Window(ctx, ext, glow, new Vector3(11.1f, 8.2f, 0), Vector3.right, 0.6f, 0.8f, WindowKind.Red, true);
            ctx.Light(new Vector3(10.5f, 8.2f, 0), RedLamp, 0.8f, 3.5f, PsxFlicker.Pulse, LightGroup.Power, "RedLamp_Attic", 0.15f, 0.4f, GlowRed);
        }

        // ------------------------------------------------------------------ porch + stoops

        static void Porch(MapContext ctx, MeshBuilder ext)
        {
            var deck = Mat.Lit(Tex.FloorPlanks, new Color(0.75f, 0.72f, 0.66f));
            var white = Mat.Lit(Tex.WoodWhite, new Color(0.78f, 0.77f, 0.72f));
            var skirt = Mat.Lit(Tex.WoodPlanksWall, new Color(0.6f, 0.58f, 0.55f));
            // front porch deck x[-6,6] z[-11,-8.1]
            PorchDeck(ctx, ext, -6f, -11f, 6f, -8.1f, deck, skirt, "Porch");
            float roofY0 = 3.55f, roofY1 = 3.05f, rz0 = -8.1f, rz1 = -11.35f;
            ext.Material = white;
            ext.Color = Shade.Gray(0.78f);
            float[] postX = { -5.85f, -2f, 2f, 5.85f };
            foreach (float px in postX)
            {
                float top = Mathf.Lerp(roofY0, roofY1, (-10.85f - rz0) / (rz1 - rz0));
                ext.AddBox(new Vector3(px, (FG + top) * 0.5f, -10.85f), new Vector3(0.14f, top - FG, 0.14f), BoxUV.Local, 0.6f, 1.2f);
                ctx.Solid(new Vector3(px, (FG + top) * 0.5f, -10.85f), new Vector3(0.16f, top - FG, 0.16f), SurfaceType.Wood, "PorchPost");
            }
            ext.AddBox(new Vector3(0, 3.0f, -10.85f), new Vector3(12.1f, 0.18f, 0.16f), BoxUV.Local, 0.8f, 2f);
            // roof (top shingles, underside beadboard)
            ext.Material = Mat.Lit(Tex.Shingles);
            ext.Color = Shade.Gray(0.75f);
            ext.AddPlane(new Vector3(-6.3f, roofY1, rz1), new Vector3(12.6f, 0, 0), new Vector3(0, roofY0 - roofY1, rz0 - rz1), 2.2f, 2f);
            ext.Material = Mat.Lit(Tex.CeilingWood, new Color(0.6f, 0.58f, 0.52f));
            ext.Color = Shade.Gray(0.55f);
            ext.AddPlane(new Vector3(6.3f, roofY1 - 0.1f, rz1), new Vector3(-12.6f, 0, 0), new Vector3(0, roofY0 - roofY1, rz0 - rz1), 1.4f, 1.5f);
            ext.Material = white;
            ext.Color = Shade.Gray(0.7f);
            ext.AddBox(new Vector3(0, roofY1 - 0.05f, rz1), new Vector3(12.6f, 0.16f, 0.05f), BoxUV.Local, 1f, 2f);
            ext.AddBeam(new Vector3(-6.3f, roofY1 - 0.05f, rz1), new Vector3(-6.3f, roofY0 - 0.05f, rz0), 0.06f);
            ext.AddBeam(new Vector3(6.3f, roofY1 - 0.05f, rz1), new Vector3(6.3f, roofY0 - 0.05f, rz0), 0.06f);
            ext.Color = Shade.Gray(1f);
            var rd = new Vector3(0, roofY0 - roofY1, rz0 - rz1);
            var rrot = Quaternion.LookRotation(rd.normalized, Vector3.up);
            ctx.Solid(new Vector3(0, (roofY0 + roofY1) * 0.5f, (rz0 + rz1) * 0.5f) - rrot * Vector3.up * 0.1f, new Vector3(12.6f, 0.2f, rd.magnitude), rrot, SurfaceType.Wood, "PorchRoof");
            // railings + steps
            Arch.Railing(ctx, ext, new Vector3(-5.85f, FG, -10.9f), new Vector3(-1.3f, FG, -10.9f), 0.9f, white, true);
            Arch.Railing(ctx, ext, new Vector3(1.3f, FG, -10.9f), new Vector3(5.85f, FG, -10.9f), 0.9f, white, true);
            Arch.Railing(ctx, ext, new Vector3(-5.92f, FG, -10.9f), new Vector3(-5.92f, FG, -8.2f), 0.9f, white, true);
            Arch.Railing(ctx, ext, new Vector3(5.92f, FG, -10.9f), new Vector3(5.92f, FG, -8.2f), 0.9f, white, true);
            var tread = Mat.Lit(Tex.StairsWood, new Color(0.8f, 0.78f, 0.72f));
            Arch.Stair(ctx, ext, new Vector3(0, 0, -12.2f), Vector3.forward, 2.4f, FG, 1.2f, 3, 0f, tread, white, SurfaceType.Wood, "PorchSteps");
            Arch.Railing(ctx, ext, new Vector3(-1.3f, 0, -12.25f), new Vector3(-1.3f, FG, -11.0f), 0.9f, white, true);
            Arch.Railing(ctx, ext, new Vector3(1.3f, 0, -12.25f), new Vector3(1.3f, FG, -11.0f), 0.9f, white, true);
            Arch.WallLamp(ctx, ext, GlowExterior, new Vector3(1.0f, 2.65f, -8.1f), Vector3.back, new Color(1f, 0.85f, 0.55f), 1.0f, 6f, PsxFlicker.FaultyBulb, LightGroup.Power, "PorchLight", 0.4f);
            ctx.Area("Porch", new Vector3(-6.1f, 0f, -12.4f), new Vector3(6.1f, 3.6f, -8.1f));

            // back stoop (back door z=8): deck x[-1.3,1.3] z[8.1,9.6], steps north to z=10.8
            PorchDeck(ctx, ext, -1.3f, 8.1f, 1.3f, 9.6f, deck, skirt, "BackStoop");
            Arch.Stair(ctx, ext, new Vector3(0, 0, 10.8f), Vector3.back, 2.0f, FG, 1.2f, 3, 0f, tread, white, SurfaceType.Wood, "BackSteps");
            Arch.Railing(ctx, ext, new Vector3(-1.3f, FG, 8.2f), new Vector3(-1.3f, FG, 9.6f), 0.9f, white, true);
            Arch.Railing(ctx, ext, new Vector3(1.3f, FG, 8.2f), new Vector3(1.3f, FG, 9.6f), 0.9f, white, true);
            Arch.WallLamp(ctx, ext, GlowExterior, new Vector3(0.95f, 2.7f, 8.1f), Vector3.forward, new Color(1f, 0.8f, 0.5f), 0.8f, 5f, PsxFlicker.FaultyBulb, LightGroup.Power, "BackLight", 0.5f);

            // side stoop (kitchen door x=-11, z=4): deck x[-12.6,-11.1] z[3,5], steps west to x=-13.8
            PorchDeck(ctx, ext, -12.6f, 3f, -11.1f, 5f, deck, skirt, "SideStoop");
            Arch.Stair(ctx, ext, new Vector3(-13.8f, 0, 4f), Vector3.right, 1.6f, FG, 1.2f, 3, 0f, tread, white, SurfaceType.Wood, "SideSteps");
            Arch.Railing(ctx, ext, new Vector3(-12.6f, FG, 3.0f), new Vector3(-11.2f, FG, 3.0f), 0.9f, white, true);
            Arch.Railing(ctx, ext, new Vector3(-12.6f, FG, 5.0f), new Vector3(-11.2f, FG, 5.0f), 0.9f, white, true);
        }

        static void PorchDeck(MapContext ctx, MeshBuilder mb, float x0, float z0, float x1, float z1, Material deck, Material skirt, string name)
        {
            Arch.Flat(mb, x0, z0, x1, z1, FG, deck, 1.4f, null, true, Color.white, 0.75f, 0.6f);
            mb.Material = skirt;
            mb.Color = Shade.Gray(0.55f);
            float h = FG;
            mb.AddBox(new Vector3((x0 + x1) * 0.5f, h * 0.5f - 0.01f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, h, z1 - z0), BoxUV.Local, 1.2f, 1.5f, BoxFaces.Sides);
            mb.Color = Shade.Gray(1f);
            Arch.SlabCollider(ctx, Rect.MinMaxRect(x0, z0, x1, z1), FG, FG, null, SurfaceType.Wood, name);
        }

        // ------------------------------------------------------------------ nav (structure nodes)

        static void HouseNav(MapContext ctx)
        {
            var n = ctx.Nav;
            // ground hall
            n.Add(new Vector3(0, FG, -6.3f), "House.Hall");
            n.Add(new Vector3(0, FG, -3.4f), "House.Hall");
            n.Add(new Vector3(0, FG, 0f), "House.Hall");
            n.Add(new Vector3(0, FG, 3.2f), "House.Hall");
            n.Add(new Vector3(0, FG, 6.3f), "House.Hall");
            n.Add(new Vector3(-1.9f, FG, -6.8f), "House.Hall");
            // up stair: bottom (ground) - middle (ramp) - top (upper hall)
            int ub = n.Add(new Vector3(2.27f, FG, -2.7f), "House.Hall", true);
            int um = n.Add(new Vector3(2.27f, (FG + FU) * 0.5f + 0.02f, 1.5f), "House.Stairs", true);
            int ut = n.Add(new Vector3(2.27f, FU, 5.7f), "House.UpperHall", true);
            n.Chain(ub, um, ut);
            // down stair: top (ground) - middle - bottom (basement)
            int dt = n.Add(new Vector3(-2.27f, FG, 5.7f), "House.Hall", true);
            int dm = n.Add(new Vector3(-2.27f, (FG + FB) * 0.5f + 0.02f, 1.5f), "Basement.Stairs", true);
            int db = n.Add(new Vector3(-2.27f, FB, -2.7f), "Basement.Corridor", true);
            n.Chain(dt, dm, db);
            // upper hall
            n.Add(new Vector3(0, FU, -6.3f), "House.UpperHall");
            n.Add(new Vector3(0, FU, -3.4f), "House.UpperHall");
            n.Add(new Vector3(-0.3f, FU, 0f), "House.UpperHall");
            n.Add(new Vector3(-0.3f, FU, 3.2f), "House.UpperHall");
            n.Add(new Vector3(0, FU, 6.5f), "House.UpperHall");
            // porch / stoops (+ steps to the ground)
            n.Add(new Vector3(-4f, FG, -9.6f), "Porch");
            n.Add(new Vector3(4f, FG, -9.6f), "Porch");
            int pTop = n.Add(new Vector3(0, FG, -10.5f), "Porch", true);
            int pBot = n.Add(new Vector3(0, 0, -12.9f), "Yard", true);
            n.Link(pTop, pBot);
            int bTop = n.Add(new Vector3(0, FG, 9.3f), "Yard", true);
            int bBot = n.Add(new Vector3(0, 0, 11.5f), "Yard", true);
            n.Link(bTop, bBot);
            int sTop = n.Add(new Vector3(-12.2f, FG, 4f), "Yard", true);
            int sBot = n.Add(new Vector3(-14.5f, 0, 4f), "Yard", true);
            n.Link(sTop, sBot);
        }
    }
}
