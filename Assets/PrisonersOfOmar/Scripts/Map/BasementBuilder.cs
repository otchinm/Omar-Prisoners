using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Basement contents (corridor with beams/posts like DqdpNIV, generator room, furnace room, shelter antechamber,
    /// the red-tile "exhibit") and the escape tunnel behind the fallout shelter door.
    /// Basement floor y=-2.6, ceiling y=0.4. Tunnel floor y=-2.6, ceiling y=-0.3.
    /// </summary>
    internal static class BasementBuilder
    {
        const float F = HouseBuilder.FB, C = HouseBuilder.CB;
        const float TunnelCeil = -0.3f;
        const string Glow = HouseBuilder.GlowBasement;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static void Rooms(MapContext ctx, MeshBuilder mb)
        {
            HouseBuilder.Safe("basement corridor", () => Corridor(ctx, mb));
            HouseBuilder.Safe("generator room", () => Generator(ctx, mb));
            HouseBuilder.Safe("furnace room", () => Furnace(ctx, mb));
            HouseBuilder.Safe("antechamber", () => Antechamber(ctx, mb));
            HouseBuilder.Safe("exhibit", () => Exhibit(ctx, mb));
            ctx.Ambient(AmbientType.Basement, V(-11.2f, F - 0.2f, -8.2f), V(11.2f, C + 0.15f, 8.2f));
            ctx.Area("Basement", V(-11.2f, F - 0.2f, -8.2f), V(11.2f, C + 0.15f, 8.2f));
        }

        // ------------------------------------------------------------------ beams like the reference basement

        static void Beams(MapContext ctx, MeshBuilder mb, float x0, float x1, float z0, float z1, float skipX0 = 99f, float skipZ0 = 99f, float skipZ1 = -99f)
        {
            var wood = Mat.Lit(Tex.WoodRaw, new Color(0.75f, 0.55f, 0.45f));
            mb.Material = wood;
            mb.Color = Shade.Gray(0.6f);
            for (float z = z0 + 0.6f; z < z1 - 0.3f; z += 1.5f)
            {
                float a = x0;
                if (z > skipZ0 && z < skipZ1) a = Mathf.Max(a, skipX0);
                mb.AddBox(V((a + x1) * 0.5f, C - 0.1f, z), V(x1 - a, 0.2f, 0.18f), BoxUV.Local, 0.8f, 1.5f, BoxFaces.All & ~BoxFaces.PosY);
            }
            mb.Color = Shade.Gray(1f);
        }

        static void Corridor(MapContext ctx, MeshBuilder mb)
        {
            const string A = "Basement.Corridor";
            Beams(ctx, mb, -2.93f, 2.93f, -7.9f, 7.9f, -1.6f, -2.1f, 5.1f);
            var wood = Mat.Lit(Tex.WoodRaw, new Color(0.75f, 0.55f, 0.45f));
            mb.Material = wood;
            mb.Color = Shade.Gray(0.65f);
            mb.AddBox(V(1.2f, C - 0.3f, 0), V(0.22f, 0.2f, 15.8f), BoxUV.Local, 0.8f, 1.5f);
            float[] posts = { -6.0f, -2.5f, 1.5f, 4.8f };
            foreach (float pz in posts)
            {
                mb.AddBox(V(1.2f, (F + C - 0.4f) * 0.5f, pz), V(0.2f, C - 0.4f - F, 0.2f), BoxUV.Local, 0.8f, 1.0f);
                ctx.Solid(V(1.2f, (F + C) * 0.5f, pz), V(0.2f, C - F, 0.2f), SurfaceType.Wood, "Post");
            }
            mb.Color = Shade.Gray(1f);
            Props.Bookshelf(ctx, mb, V(2.76f, F, 1.0f), 90f, 1.4f, 1.8f, 1001);
            var jar = Mat.Lit(null, new Color(0.45f, 0.4f, 0.25f));
            mb.Material = jar;
            for (int lvl = 1; lvl < 4; lvl++)
                for (int i = 0; i < 4; i++)
                    if ((lvl + i) % 3 != 0) mb.AddCylinder(V(2.72f, F + Props.BookshelfLevel(lvl, 1.8f), 0.45f + i * 0.28f), 0.05f, 0.05f, 0.14f, 6, true, false, null, true);
            Props.Crate(ctx, mb, V(-2.4f, F, -7.35f), 8f, 0.6f);
            Props.Crate(ctx, mb, V(-2.35f, F + 0.6f, -7.35f), 30f, 0.4f, true, false);
            Props.FloorMattress(mb, V(-2.25f, F, 7.0f), 90f, true);
            Props.Bucket(mb, V(2.5f, F, -7.5f), 0f, false, 0.6f, false);
            Props.Chains(mb, V(-0.5f, C - 0.2f, -6.5f), 1.2f);
            Props.Chains(mb, V(0.6f, C - 0.2f, -6.2f), 1.0f);
            Arch.FloorDecal(mb, Mat.Decal("blood_smear"), V(-0.6f, F, -4.5f), 0.7f, 2.4f, 5f);
            Arch.FloorDecal(mb, Mat.Decal("water_stain"), V(0.4f, F, 2.5f), 2.0f, 1.6f, 0f);
            Arch.Decal(mb, Mat.Decal("writing_omar"), V(-1.61f, F + 1.0f, 2.8f), Vector3.right, 1.4f, 0.7f, 2f);
            ctx.Common(A, V(2.72f, F + Props.BookshelfLevel(2, 1.8f), 1.55f));
            ctx.Key(A, V(-2.4f, F + 0.6f, -7.0f));
            ctx.Common(A, V(2.45f, F, -6.9f));
            ctx.WallNote(A, V(-2.92f, F + 1.5f, -4.0f), Vector3.right);
            Arch.Bulb(ctx, mb, Glow, V(0.3f, C, -5.4f), 0.35f, HouseBuilder.Warm, 1.05f, 6f, PsxFlicker.FaultyBulb, LightGroup.Power, "Corridor_Bulb_S", false, 0.4f);
            var buzz = Arch.Bulb(ctx, mb, Glow, V(0.3f, C, 3.3f), 0.35f, new Color(0.85f, 0.9f, 0.75f), 0.95f, 6f, PsxFlicker.FaultyBulb, LightGroup.Power, "Corridor_Bulb_N", false, 0.7f);
            ctx.Emitter(V(0.3f, C - 0.4f, 3.3f), "Audio/Ambience/fluorescent_buzz_loop", 0.35f, 7f, true);
            ctx.Nav.Add(V(0, F, -6.5f), A);
            ctx.Nav.Add(V(0, F, -3.6f), A);
            ctx.Nav.Add(V(0.1f, F, 0f), A);
            ctx.Nav.Add(V(0.1f, F, 3.4f), A);
            ctx.Nav.Add(V(0, F, 6.6f), A);
            ctx.Nav.Add(V(-2.1f, F, 6.4f), A);
            ctx.Nav.Add(V(-2.0f, F, -5.0f), A);
            ctx.Tripwire(A, V(-2.92f, F + 0.12f, -2.12f), V(-1.66f, F + 0.12f, -2.12f));
        }

        static void Generator(MapContext ctx, MeshBuilder mb)
        {
            const string A = "Basement.Generator";
            Beams(ctx, mb, 3.07f, 10.9f, -7.9f, -0.07f);
            Props.GeneratorProp(ctx, mb, V(8.0f, F, -5.0f), 0f, C);
            ctx.Emitter(V(8.0f, F + 0.6f, -5.0f), "Audio/Ambience/generator_loop", 0.55f, 14f, true);
            Props.FuseBox(mb, V(5.5f, F + 1.5f, -0.07f), Vector3.back, C);
            ctx.Data.Radio.FuseBox = ctx.Interact(null, V(5.5f, F + 1.5f, -0.4f), V(0.6f, 0.75f, 0.3f), Quaternion.identity, "FuseBox");
            ctx.Marker("FuseBox", V(5.5f, F + 1.5f, -0.25f), Quaternion.LookRotation(Vector3.back));
            Props.Workbench(ctx, mb, V(5.6f, F, -7.54f), 180f, 2.0f, true, 1101);
            Dyn.Locker(ctx, "Generator.Locker1", V(10.64f, F, -2.2f), 90f, true);
            Dyn.Locker(ctx, "Generator.Locker2", V(10.64f, F, -1.5f), 90f, false);
            Props.Barrel(ctx, mb, V(9.7f, F, -6.9f), 30f, Tex.BarrelFuel);
            Props.GasCanProp(mb, V(9.1f, F, -7.4f), 20f);
            Props.Tire(mb, V(10.3f, F, -7.3f), 0f);
            // pipes along the ceiling
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.55f, 0.5f, 0.45f));
            mb.Color = Shade.Gray(0.6f);
            mb.AddBeam(V(3.2f, C - 0.35f, -0.4f), V(10.8f, C - 0.35f, -0.4f), 0.09f);
            mb.AddBeam(V(10.6f, C - 0.25f, -0.4f), V(10.6f, C - 0.25f, -7.8f), 0.07f);
            mb.Color = Shade.Gray(1f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(8.0f, F, -5.0f), 2.6f, 2.0f, 0f);
            Arch.FloorDecal(mb, Mat.Decal("water_stain"), V(9.6f, F, -6.8f), 1.2f, 1.2f, 0f);
            ctx.Key(A, V(5.0f, F + Props.WorkbenchTop, -7.45f));
            ctx.Common(A, V(6.2f, F + Props.WorkbenchTop, -7.62f));
            ctx.Common(A, V(10.3f, F, -0.5f));
            ctx.Key(A, V(9.7f, F + Props.BarrelTop, -6.9f));
            ctx.DeskNote(A, V(5.4f, F + Props.WorkbenchTop + 0.003f, -7.35f), 5f);
            Arch.Bulb(ctx, mb, Glow, V(6.4f, C, -3.6f), 0.4f, HouseBuilder.Warm, 1.0f, 6f, PsxFlicker.FaultyBulb, LightGroup.Power, "Generator_Bulb", false, 0.45f);
            ctx.Nav.Add(V(5.4f, F, -2.8f), A);
            ctx.Nav.Add(V(8.2f, F, -2.8f), A);
            ctx.Nav.Add(V(5.3f, F, -6.3f), A);
            ctx.Nav.Add(V(9.8f, F, -4.0f), A);
            ctx.Tripwire(A, V(9.0f, F + 0.12f, -4.3f), V(10.88f, F + 0.12f, -4.3f));
        }

        static void Furnace(MapContext ctx, MeshBuilder mb)
        {
            const string A = "Basement.Furnace";
            Beams(ctx, mb, 3.07f, 10.9f, 0.07f, 7.9f);
            var furnacePos = V(9.95f, F, 4.0f);
            var grateLocal = Props.Furnace(ctx, mb, furnacePos, 90f, C);
            var grate = furnacePos + MapMath.Yaw(90f) * grateLocal;
            ctx.Light(grate + V(-0.5f, 0.1f, 0), new Color(1f, 0.45f, 0.15f), 1.5f, 6.5f, PsxFlicker.Fire, LightGroup.None, "Furnace_Fire", 0.45f, 3f);
            ctx.Emitter(grate, "Audio/Ambience/fire_loop", 0.6f, 10f, false);
            ctx.Marker("Furnace", grate, Quaternion.LookRotation(Vector3.left));
            Props.CoalPile(mb, V(9.5f, F, 1.25f), 2.2f, 1.6f, 1201);
            Props.Bones(mb, V(7.4f, F, 2.0f), 1202, 7, true);
            Props.Bones(mb, V(8.4f, F, 6.6f), 1203, 5, false);
            Props.Table(ctx, mb, V(3.62f, F, 1.25f), 90f, 1.0f, 0.6f, Props.TableTop, Tex.TableWood, 0.5f);
            Props.Toolbox(mb, V(3.6f, F + Props.TableTop, 1.5f), 95f);
            // shovel leaning on the wall
            mb.Material = Mat.Lit(Tex.WoodRaw);
            mb.Color = Shade.Gray(0.7f);
            mb.AddBeam(V(10.75f, F, 0.6f), V(10.82f, F + 1.3f, 0.4f), 0.035f);
            mb.Material = Mat.Lit(Tex.MetalRusty);
            mb.AddBox(V(10.74f, F + 0.13f, 0.62f), V(0.04f, 0.28f, 0.24f), BoxUV.Local, 0.3f);
            mb.Color = Shade.Gray(1f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(8.6f, F, 4.0f), 2.5f, 3.0f, 0f);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(7.6f, F, 4.6f), 1.4f, 1.0f, 60f);
            Arch.Decal(mb, Mat.Decal("graffiti_scrawl_1"), V(5.5f, F + 1.6f, 7.89f), Vector3.back, 1.5f, 1.5f, 0f);
            ctx.Key(A, V(3.6f, F + Props.TableTop, 0.95f));
            ctx.Common(A, V(8.0f, F, 0.6f));
            ctx.Common(A, V(4.0f, F, 7.45f));
            ctx.WallNote(A, V(3.08f, F + 1.5f, 3.2f), Vector3.right);
            // Omar sleeps by the fire
            var spawn = V(6.2f, F, 4.4f);
            ctx.Data.OmarSpawn = MapMath.FacingPose(spawn, V(3f, F, 6f) - spawn);
            ctx.Nav.Add(V(6.0f, F, 2.4f), A);
            ctx.Nav.Add(V(6.4f, F, 4.4f), A);
            ctx.Nav.Add(V(6.4f, F, 6.9f), A);
            ctx.Nav.Add(V(8.0f, F, 7.2f), A);
        }

        static void Antechamber(MapContext ctx, MeshBuilder mb)
        {
            const string A = "Basement.Antechamber";
            // fallout shelter door (west wall, x=-11, z=-4), opens into the antechamber
            var metal = Mat.Lit(Tex.MetalDark);
            Arch.DoorFrame(mb, V(-11f, F, -4f), false, 1.1f, 2.2f, HouseBuilder.ExtT, metal);
            var pivot = Dyn.HingedLeaf(ctx, ctx.Dynamic, "ShelterDoor", V(-11f, F, -4.5f), Vector3.forward, Vector3.right, 1.0f, 2.12f, 0.1f,
                Tex.DoorShelter, metal, Layers.Door, 95f, out var leafCol, out float angle, 0.03f, true);
            ctx.Data.Shelter.DoorPivot = pivot;
            ctx.Data.Shelter.OpenAngle = angle;
            ctx.Data.Shelter.DoorCollider = leafCol;
            Props.SignPlate(mb, V(-10.88f, F + 2.55f, -4f), Vector3.right, Tex.SignFallout, 0.4f, 0.4f);
            // keypad beside the door
            mb.Push(V(-10.9f, F + 1.35f, -5.15f), MapMath.Yaw(MapMath.PropYawFacing(Vector3.right)));
            mb.Color = Shade.Gray(0.9f);
            mb.Material = Mat.Lit(Tex.Keypad);
            mb.AddBox(V(0, 0, -0.025f), V(0.13f, 0.24f, 0.05f), BoxUVRects.Front(new Rect(0, 0, 1, 1), new Rect(0, 0, 0.1f, 0.1f)));
            mb.Pop();
            mb.Material = Mat.Lit(Tex.Galvanized);
            mb.AddBeam(V(-10.88f, F + 1.47f, -5.15f), V(-10.88f, C, -5.15f), 0.03f);
            mb.Color = Shade.Gray(1f);
            ctx.Data.Shelter.Keypad = ctx.Interact(null, V(-10.79f, F + 1.35f, -5.15f), V(0.2f, 0.36f, 0.3f), Quaternion.identity, "ShelterKeypad");
            ctx.Marker("ShelterKeypad", V(-10.85f, F + 1.35f, -5.15f), Quaternion.LookRotation(Vector3.right));
            ctx.Light(V(-10.6f, F + 2.45f, -2.9f), new Color(1f, 0.12f, 0.06f), 0.9f, 4.2f, PsxFlicker.Pulse, LightGroup.Power, "Shelter_Emergency", 0.5f, 1.2f, Glow);
            var g = ctx.GlowBuilder(Glow);
            g.Material = ctx.GlowColor(new Color(1f, 0.15f, 0.08f));
            g.AddBox(V(-10.84f, F + 2.45f, -2.9f), V(0.1f, 0.12f, 0.18f), BoxUV.Local, 1f);
            // shelter supplies
            Props.Cot(ctx, mb, V(-9.0f, F, -7.45f), 90f);
            Props.Cot(ctx, mb, V(-6.6f, F, -7.45f), 90f);
            Props.Barrel(ctx, mb, V(-10.4f, F, -0.45f), 0f, Tex.BarrelWater, 0.4f);
            Props.Barrel(ctx, mb, V(-9.78f, F, -0.45f), 45f, Tex.BarrelWater, 0.4f);
            Props.Barrel(ctx, mb, V(-9.16f, F, -0.45f), 90f, Tex.BarrelWater, 0.4f);
            Props.MetalShelf(ctx, mb, V(-6.5f, F, -0.33f), 0f, 1.8f, 1301, 0.8f, true);
            Props.Crate(ctx, mb, V(-4.0f, F, -7.4f), 0f, 0.6f);
            Props.Bucket(mb, V(-4.6f, F, -7.5f), 0f, true);
            Arch.FloorDecal(mb, Mat.Decal("cracks"), V(-7f, F, -4f), 3.0f, 3.0f, 0f);
            Arch.Decal(mb, Mat.Decal("cracks"), V(-7f, F + 1.8f, -7.89f), Vector3.forward, 2.0f, 2.0f, 0f);
            ctx.Common(A, V(-9.0f, F + Props.CotTop + 0.05f, -7.4f));
            ctx.Key(A, V(-6.5f - 0.62f, F + Props.ShelfLevel(1), -0.45f));
            ctx.Common(A, V(-9.78f, F + Props.BarrelTop, -0.45f));
            ctx.Key(A, V(-4.0f, F + 0.6f, -7.4f));
            ctx.WallNote(A, V(-10.89f, F + 1.6f, -6.6f), Vector3.right);
            ctx.WallNote(A, V(-7.5f, F + 1.5f, -0.08f), Vector3.back);
            Arch.Bulb(ctx, mb, Glow, V(-7.0f, C, -4.0f), 0.35f, HouseBuilder.Warm, 0.95f, 5.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Antechamber_Bulb", true, 0.3f);
            ctx.Nav.Add(V(-7.0f, F, -4.0f), A);
            ctx.Nav.Add(V(-4.6f, F, -3.0f), A);
            ctx.Nav.Add(V(-9.4f, F, -2.4f), A);
            ctx.Nav.Add(V(-6.0f, F, -6.3f), A);
            int inSide = ctx.Nav.Add(V(-10.2f, F, -4.0f), A, true);
            int outSide = ctx.Nav.Add(V(-11.8f, F, -4.0f), "Tunnel", true);
            ctx.Nav.Link(inSide, outSide, NavGraph.EdgeShelter);
            ctx.Tripwire("Basement.Corridor", V(-2.92f, F + 0.12f, -6.46f), V(-2.92f, F + 0.12f, -5.54f));
            // the tunnel itself
            Tunnel(ctx);
        }

        static void Exhibit(MapContext ctx, MeshBuilder mb)
        {
            const string A = "Basement.Exhibit";
            // six burnt mannequins, hands crossed, like Burnt.jpg
            var spots = new[]
            {
                V(-10.1f, F, 1.4f), V(-10.1f, F, 3.0f), V(-10.1f, F, 4.6f), V(-10.1f, F, 6.2f), V(-8.0f, F, 0.75f), V(-6.2f, F, 0.75f),
            };
            var facing = new[] { Vector3.right, Vector3.right, Vector3.right, Vector3.right, Vector3.forward, Vector3.forward };
            var alts = new[]
            {
                new[] { V(-4.4f, F, 2.2f), V(-8.6f, F, 7.2f) },
                new[] { V(-5.6f, F, 5.4f), V(-9.2f, F, 2.2f) },
                new[] { V(-4.4f, F, 7.35f), V(-7.0f, F, 2.4f) },
                new[] { V(-6.0f, F, 7.2f), V(-8.4f, F, 4.8f) },
                new[] { V(-5.2f, F, 3.2f), V(-9.0f, F, 6.0f) },
                new[] { V(-7.2f, F, 6.0f), V(-4.2f, F, 1.0f) },
            };
            var door = V(-3f, F, 6.5f);
            for (int i = 0; i < spots.Length; i++)
            {
                var ap = new Pose[alts[i].Length];
                for (int k = 0; k < ap.Length; k++)
                {
                    var toDoor = door - alts[i][k];
                    var toCenter = V(-7f, F, 4f) - alts[i][k];
                    ap[k] = MapMath.FacingPose(alts[i][k], k == 0 ? toDoor : toCenter);
                }
                Dyn.Mannequin(ctx, i, MapMath.FacingPose(spots[i], facing[i]), ap, ctx.Seed * 31 + i);
                Arch.Blob(mb, spots[i], 0.7f, 0.5f);
            }
            Props.SeveredHand(mb, V(-6.3f, F, 4.2f), 35f);
            // scorched walls / floor where they were burnt, an old bench and a wheelchair-less tray of tools
            Arch.Decal(mb, Mat.Decal("grime"), V(-10.89f, F + 1.1f, 2.2f), Vector3.right, 2.2f, 2.2f, 0f);
            Arch.Decal(mb, Mat.Decal("grime"), V(-10.89f, F + 1.0f, 5.4f), Vector3.right, 2.0f, 2.4f, 90f);
            Arch.Decal(mb, Mat.Decal("grime"), V(-7.1f, F + 1.0f, 0.08f), Vector3.forward, 2.6f, 2.0f, 0f);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(-9.6f, F, 1.9f), 0.9f, 0.7f, 70f);
            Arch.FloorDecal(mb, Mat.Decal("blood_splatter_2"), V(-5.6f, F, 6.4f), 1.1f, 1.1f, 15f);
            Props.Table(ctx, mb, V(-7.0f, F, 7.55f), 0f, 1.6f, 0.45f, 0.45f, Tex.TableWood, 0.4f);
            Props.Bottles(mb, V(-7.4f, F + 0.45f, 7.55f), 1402, 2, 0.1f);
            Props.Candle(mb, null, V(-6.6f, F + 0.45f, 7.5f));
            Props.Candle(mb, null, V(-6.45f, F + 0.45f, 7.62f));
            Props.Table(ctx, mb, V(-3.62f, F, 2.0f), 90f, 1.0f, 0.6f, Props.TableTop, Tex.TableWood, 0.5f);
            Props.Bottles(mb, V(-3.6f, F + Props.TableTop, 2.3f), 1401, 2, 0.08f);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(-7.5f, F, 3.5f), 1.6f, 1.2f, 15f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(-9.5f, F, 4.0f), 2.0f, 5.5f, 0f);
            Arch.Decal(mb, Mat.Decal("writing_omar"), V(-7.0f, F + 1.9f, 7.89f), Vector3.back, 2.0f, 1.0f, -3f);
            Arch.Decal(mb, Mat.Decal("blood_splatter_3"), V(-10.89f, F + 1.5f, 3.8f), Vector3.right, 1.6f, 1.6f, 0f);
            Arch.Decal(mb, Mat.Decal("blood_handprint"), V(-3.08f, F + 1.2f, 4.2f), Vector3.left, 0.25f, 0.25f, 10f);
            ctx.Key(A, V(-3.62f, F + Props.TableTop, 1.7f));
            ctx.Common(A, V(-5.0f, F, 7.5f));
            ctx.Common(A, V(-10.5f, F, 7.5f));
            ctx.WallNote(A, V(-9.0f, F + 1.5f, 7.89f), Vector3.back);
            Arch.Bulb(ctx, mb, Glow, V(-7.0f, C, 4.0f), 0.45f, new Color(1f, 0.16f, 0.1f), 1.25f, 6.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Exhibit_RedBulb", false, 0.5f);
            ctx.Marker("Exhibit", V(-4.0f, F + 1.6f, 5.0f), Quaternion.LookRotation(V(-1, 0, -0.3f)));
            ctx.Anomaly("Exhibit", V(-7f, F + 1.1f, 4f), 7f, 0.35f);
            ctx.Nav.Add(V(-5.0f, F, 4.2f), A);
            ctx.Nav.Add(V(-7.0f, F, 5.6f), A);
            ctx.Nav.Add(V(-7.8f, F, 2.6f), A);
            ctx.Nav.Add(V(-4.6f, F, 1.6f), A);
            ctx.Tripwire("Basement.Corridor", V(-2.92f, F + 0.12f, 6.04f), V(-2.92f, F + 0.12f, 6.96f));
            ctx.BearTrap(A, V(-9.2f, F, 7.2f));
        }

        // ================================================================== tunnel

        struct Seg { public bool AlongX; public float C, From, To, N; public Seg(bool ax, float c, float f, float t, float n) { AlongX = ax; C = c; From = f; To = t; N = n; } }

        static void Tunnel(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Tunnel");
            const string A = "Tunnel";
            var skin = WallSkin.Of(Mat.Lit(Tex.Concrete, new Color(0.62f, 0.6f, 0.58f)), Mat.Lit(Tex.BrickBasement, new Color(0.7f, 0.68f, 0.62f)), 1.0f, null, 1.6f);
            skin.AoFloor = 0.4f; skin.AoCeil = 0.6f; skin.Grime = 0.25f;
            var segs = new List<Seg>
            {
                new Seg(true, -5.1f, -27.1f, -11.1f, +1), new Seg(true, -2.9f, -24.9f, -11.1f, -1),
                new Seg(false, -24.9f, -2.9f, 7.1f, -1), new Seg(false, -27.1f, -5.1f, 4.9f, +1),
                new Seg(true, 7.1f, -40f, -24.9f, -1), new Seg(true, 4.9f, -40f, -27.1f, +1),
                new Seg(false, -44f, 3.5f, 8.5f, +1), new Seg(true, 3.5f, -44f, -40f, +1), new Seg(true, 8.5f, -44f, -40f, -1),
                new Seg(false, -40f, 3.5f, 4.9f, -1), new Seg(false, -40f, 7.1f, 8.5f, -1),
            };
            foreach (var s in segs)
            {
                Arch.FaceN(mb, s.AlongX, s.C, s.From, s.To, s.N, F, TunnelCeil, null, skin, F, TunnelCeil);
                Arch.WallColliderN(ctx, s.AlongX, s.C - s.N * 0.15f, s.From, s.To, F - 0.3f, TunnelCeil + 0.3f, 0.3f, null, SurfaceType.Concrete, "TunnelWall");
            }
            // the house wall seen from the tunnel, with the shelter door hole
            Arch.FaceN(mb, false, -11.1f, -5.1f, -2.9f, -1, F, TunnelCeil, new[] { new Hole(-4.55f, -3.45f, F, F + 2.2f) }, skin, F, TunnelCeil);
            var rects = new[] { Arch.R(-27.1f, -5.1f, -11.1f, -2.9f), Arch.R(-27.1f, -2.9f, -24.9f, 7.1f), Arch.R(-40f, 4.9f, -27.1f, 7.1f), Arch.R(-44f, 3.5f, -40f, 8.5f) };
            var floor = Mat.Lit(Tex.FloorConcrete, new Color(0.7f, 0.68f, 0.65f));
            var ceil = Mat.Lit(Tex.Concrete, new Color(0.55f, 0.55f, 0.55f));
            foreach (var r in rects)
            {
                Arch.Flat(mb, r.xMin, r.yMin, r.xMax, r.yMax, F, floor, 1.5f, null, true, Color.white, 0.45f, 0.6f);
                Arch.Flat(mb, r.xMin, r.yMin, r.xMax, r.yMax, TunnelCeil, ceil, 1.6f, null, false, Color.white, 0.5f, 0.6f);
                Arch.SlabCollider(ctx, r, F, 0.3f, null, SurfaceType.Concrete, "TunnelFloor");
                Arch.SlabCollider(ctx, r, TunnelCeil + 0.2f, 0.2f, null, SurfaceType.Concrete, "TunnelCeiling");
            }
            // timber support frames + pipes + cables
            var wood = Mat.Lit(Tex.WoodRaw, new Color(0.55f, 0.45f, 0.38f));
            mb.Color = Shade.Gray(0.6f);
            void Frame(Vector3 c, bool alongX)
            {
                mb.Material = wood;
                Vector3 side = alongX ? Vector3.forward : Vector3.right;
                Vector3 l = c - side * 1.0f, r = c + side * 1.0f;
                mb.AddBox(V(l.x, (F + TunnelCeil) * 0.5f, l.z), V(0.16f, TunnelCeil - F, 0.16f), BoxUV.Local, 0.8f, 1f);
                mb.AddBox(V(r.x, (F + TunnelCeil) * 0.5f, r.z), V(0.16f, TunnelCeil - F, 0.16f), BoxUV.Local, 0.8f, 1f);
                mb.AddBeam(V(l.x, TunnelCeil - 0.09f, l.z), V(r.x, TunnelCeil - 0.09f, r.z), 0.18f);
            }
            for (float x = -13.5f; x > -26f; x -= 3f) Frame(V(x, 0, -4f), true);
            for (float z = -1.5f; z < 5f; z += 3f) Frame(V(-26f, 0, z), false);
            for (float x = -29.5f; x > -40f; x -= 3f) Frame(V(x, 0, 6f), true);
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.45f, 0.42f, 0.4f));
            mb.AddBeam(V(-11.2f, TunnelCeil - 0.12f, -3.1f), V(-25.1f, TunnelCeil - 0.12f, -3.1f), 0.09f);
            mb.AddBeam(V(-25.1f, TunnelCeil - 0.12f, -3.1f), V(-25.1f, TunnelCeil - 0.12f, 6.9f), 0.09f);
            mb.AddBeam(V(-25.1f, TunnelCeil - 0.12f, 6.9f), V(-40f, TunnelCeil - 0.12f, 6.9f), 0.09f);
            mb.Material = Mat.Flat(0.05f, 0.05f, 0.05f);
            mb.AddBeam(V(-11.2f, TunnelCeil - 0.2f, -4.9f), V(-26.9f, TunnelCeil - 0.25f, -4.9f), 0.03f);
            mb.AddBeam(V(-26.9f, TunnelCeil - 0.25f, -4.9f), V(-26.9f, TunnelCeil - 0.2f, 5.1f), 0.03f);
            mb.Color = Shade.Gray(1f);
            // puddles, rubble, junk
            var puddle = Mat.Decal("water_stain", new Color(0.35f, 0.4f, 0.45f));
            var rng = ctx.Rng("tunnel");
            Vector3[] wet = { V(-14f, F, -3.8f), V(-20.5f, F, -4.4f), V(-26.2f, F, 1.0f), V(-31f, F, 6.2f), V(-37f, F, 5.6f) };
            foreach (var p in wet) Arch.FloorDecal(mb, puddle, p, rng.Range(1.0f, 1.8f), rng.Range(0.8f, 1.4f), rng.Range(0f, 180f));
            Props.Crate(ctx, mb, V(-24.0f, F, -4.76f), 8f, 0.55f);
            Props.Barrel(ctx, mb, V(-26.75f, F, 6.75f), 0f, Tex.BarrelWater, 0.35f);
            Props.Bones(mb, V(-33f, F, 5.5f), 1501, 4, false);
            Arch.Decal(mb, Mat.Decal("writing_help"), V(-27.09f, F + 1.4f, -1.0f), Vector3.right, 1.2f, 0.6f, -5f);
            Arch.Decal(mb, Mat.Decal("graffiti_scrawl_2"), V(-24.91f, F + 1.5f, 3.0f), Vector3.left, 0.9f, 0.9f, 0f);
            // lights (some broken)
            Arch.Bulb(ctx, mb, Glow, V(-15f, TunnelCeil, -4f), 0.25f, HouseBuilder.Warm, 0.9f, 5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Tunnel_Bulb_0", false, 0.4f);
            Arch.Bulb(ctx, mb, Glow, V(-21.5f, TunnelCeil, -4f), 0.25f, HouseBuilder.Warm, 0.9f, 5f, PsxFlicker.None, LightGroup.Power, "Tunnel_Bulb_1", false, 0f, true);
            Arch.Bulb(ctx, mb, Glow, V(-26f, TunnelCeil, 0f), 0.25f, HouseBuilder.Warm, 0.85f, 5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Tunnel_Bulb_2", false, 0.75f);
            Arch.Bulb(ctx, mb, Glow, V(-26f, TunnelCeil, 5.5f), 0.25f, HouseBuilder.Warm, 0.9f, 5f, PsxFlicker.None, LightGroup.Power, "Tunnel_Bulb_3", false, 0f, true);
            Arch.Bulb(ctx, mb, Glow, V(-33.5f, TunnelCeil, 6f), 0.25f, HouseBuilder.Warm, 0.8f, 5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Tunnel_Bulb_4", false, 0.5f);
            Arch.Bulb(ctx, mb, Glow, V(-42f, TunnelCeil, 6f), 0.25f, new Color(0.8f, 0.85f, 0.7f), 0.7f, 4.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Tunnel_Bulb_5", false, 0.6f);
            // exit chamber: ladder up to a hatch
            mb.Material = Mat.Lit(Tex.MetalRusty);
            mb.Color = Shade.Gray(0.7f);
            float lx = -43.82f;
            mb.AddBox(V(lx, (F + TunnelCeil) * 0.5f, 5.72f), V(0.05f, TunnelCeil - F, 0.05f), BoxUV.Local, 0.5f);
            mb.AddBox(V(lx, (F + TunnelCeil) * 0.5f, 6.28f), V(0.05f, TunnelCeil - F, 0.05f), BoxUV.Local, 0.5f);
            for (float y = F + 0.3f; y < TunnelCeil - 0.1f; y += 0.3f) mb.AddBeam(V(lx, y, 5.72f), V(lx, y, 6.28f), 0.03f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddCylinder(V(-43.45f, TunnelCeil - 0.06f, 6f), 0.5f, 0.5f, 0.05f, 10, false, true, null, false);
            mb.AddCylinder(V(-43.45f, TunnelCeil - 0.12f, 6f), 0.15f, 0.15f, 0.04f, 6, false, true, null, false);
            mb.Color = Shade.Gray(1f);
            Arch.Decal(mb, Mat.Decal("writing_help"), V(-43.99f, F + 1.5f, 4.4f), Vector3.right, 0.9f, 0.45f, 0f);
            ctx.Data.Shelter.TunnelExitZone = MapMath.MinMax(V(-44f, F - 0.1f, 4.9f), V(-42.4f, TunnelCeil + 0.1f, 7.1f));
            ctx.Marker("TunnelExit", V(-43.5f, F + 1.6f, 6f), Quaternion.LookRotation(Vector3.left));
            // gameplay
            ctx.Key(A, V(-24.0f, F + 0.55f, -4.76f));
            ctx.Common(A, V(-26.75f, F + Props.BarrelTop, 6.75f));
            ctx.Common(A, V(-41.0f, F, 8.1f));
            ctx.WallNote(A, V(-26.9f, F + 1.5f, 3.6f), Vector3.right);
            ctx.Tripwire(A, V(-18f, F + 0.12f, -5.08f), V(-18f, F + 0.12f, -2.92f));
            ctx.Tripwire(A, V(-34f, F + 0.12f, 4.92f), V(-34f, F + 0.12f, 7.08f));
            ctx.BearTrap(A, V(-26.1f, F, 3.0f));
            ctx.Anomaly("Tunnel", V(-26f, F + 1.1f, 1f), 10f, 0.3f);
            ctx.Ambient(AmbientType.Tunnel, V(-44.2f, F - 0.2f, -5.3f), V(-11.0f, TunnelCeil + 0.05f, 8.7f));
            ctx.Area(A, V(-44.2f, F - 0.2f, -5.3f), V(-11.0f, TunnelCeil + 0.05f, 8.7f));
            float[] ax = { -12.6f, -16f, -20f, -23.6f };
            foreach (var x in ax) ctx.Nav.Add(V(x, F, -3.85f), A);
            ctx.Nav.Add(V(-26f, F, -4f), A);
            ctx.Nav.Add(V(-26f, F, 0f), A);
            ctx.Nav.Add(V(-26f, F, 3.6f), A);
            ctx.Nav.Add(V(-26f, F, 6f), A);
            ctx.Nav.Add(V(-30f, F, 6f), A);
            ctx.Nav.Add(V(-34f, F, 6f), A);
            ctx.Nav.Add(V(-38f, F, 6f), A);
            ctx.Nav.Add(V(-41.6f, F, 6f), A);
        }
    }
}
