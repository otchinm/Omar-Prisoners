using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>Ground and upper floor room contents (props, lights, gameplay spots).</summary>
    internal static partial class HouseBuilder
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        // ================================================================== GROUND FLOOR (y = 0.6)

        static void GroundRooms(MapContext ctx, MeshBuilder mb)
        {
            Safe("hall", () => Hall(ctx, mb));
            Safe("dining", () => Dining(ctx, mb));
            Safe("kitchen", () => Kitchen(ctx, mb));
            Safe("living", () => Living(ctx, mb));
            Safe("bathroom", () => Bathroom(ctx, mb));
            Safe("clock bedroom", () => ClockBedroom(ctx, mb));
        }

        static void Hall(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Hall";
            float F = FG;
            Props.Rug(mb, V(0, F, -4.6f), 0, 1.2f, 4.4f, new Color(0.5f, 0.25f, 0.2f));
            // the portrait of Omar on the west wall, facing the front door
            Props.WallPicture(mb, V(-2.93f, F + 1.65f, -6.9f), Vector3.right, 0.55f, 0.85f, Mat.Lit(Tex.PortraitOmar), Mat.Lit(Tex.WoodFurniture, new Color(0.6f, 0.45f, 0.3f)), 0.08f);
            ctx.Marker("Portrait", V(-2.88f, F + 1.65f, -6.9f), Quaternion.LookRotation(Vector3.right));
            Props.CoatRack(mb, V(2.45f, F, -7.45f));
            Props.Table(ctx, mb, V(2.68f, F, -6.55f), 90f, 0.9f, 0.4f, 0.8f, Tex.TableWood, 0.7f);
            Props.Papers(mb, V(2.68f, F + 0.8f, -6.4f), 101, 2, 0.1f);
            ctx.Common(A, V(2.66f, F + 0.8f, -6.8f));
            Props.Crate(ctx, mb, V(-2.5f, F, 7.45f), 12f, 0.5f, true);
            ctx.Key(A, V(-2.5f, F + 0.4f, 7.45f));
            Props.Papers(mb, V(0.6f, F, 2.2f), 102, 4, 0.5f);
            Props.Bottles(mb, V(-1.1f, F, -7.3f), 103, 3, 0.2f);
            ctx.Common(A, V(1.0f, F, -7.5f));
            // drag marks: from the front door towards the basement stairs
            var smear = Mat.Decal("blood_smear");
            for (int i = 0; i < 5; i++)
                Arch.FloorDecal(mb, smear, V(-0.2f - i * 0.25f, F, -6.5f + i * 2.4f), 0.6f, 1.3f, 8f + i * 4f);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(-0.9f, F, 4.6f), 1.0f, 0.8f, 30f);
            Arch.Decal(mb, Mat.Decal("writing_omar"), V(2.92f, F + 2.3f, 1.2f), Vector3.left, 1.6f, 0.8f, -4f);
            Arch.Decal(mb, Mat.Decal("blood_handprint"), V(-2.92f, F + 1.2f, -2.8f), Vector3.right, 0.22f, 0.22f, 15f);
            Arch.Decal(mb, Mat.Decal("water_stain"), V(1.0f, CG - 0.01f, -5f), Vector3.down, 1.6f, 1.6f, 40f);
            ctx.WallNote(A, V(-2.92f, F + 1.5f, -3.4f), Vector3.right);
            Arch.Bulb(ctx, mb, GlowBulbs, V(0, CG, -4.6f), 0.55f, Warm, 1.1f, 6.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Hall_Bulb_S", false, 0.35f);
            Arch.Bulb(ctx, mb, GlowBulbs, V(0, CG, 5.8f), 0.5f, Warm, 1.0f, 6f, PsxFlicker.FaultyBulb, LightGroup.Power, "Hall_Bulb_N", false, 0.5f);
            // tripwires: foot of the up stairs, top of the down stairs
            ctx.Tripwire(A, V(1.66f, F + 0.12f, -2.25f), V(2.92f, F + 0.12f, -2.25f));
            ctx.Tripwire(A, V(-2.92f, F + 0.12f, 5.35f), V(-1.62f, F + 0.12f, 5.35f));
        }

        static void Dining(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Dining";
            float F = FG;
            Props.Rug(mb, V(-7f, F, -4.2f), 0, 4.2f, 2.5f, new Color(0.45f, 0.18f, 0.15f));
            Props.Table(ctx, mb, V(-7f, F, -4.2f), 0f, 3.2f, 1.1f, Props.TableTop, Tex.TableWood, 0.75f);
            Arch.Blob(mb, V(-7f, F, -4.2f), 3.8f, 1.8f);
            float[] cx = { -8f, -7f, -6f };
            for (int i = 0; i < 3; i++)
            {
                Props.Chair(ctx, mb, V(cx[i], F, -3.35f), 0f);
                if (i == 2) Props.Chair(ctx, mb, V(-5.8f, F, -5.6f), 160f, true);
                else Props.Chair(ctx, mb, V(cx[i], F, -5.05f), 180f);
            }
            Props.Chair(ctx, mb, V(-8.95f, F, -4.2f), -90f);
            Props.Chair(ctx, mb, V(-5.05f, F, -4.2f), 90f);
            Props.Plates(mb, V(-7f, F + Props.TableTop, -4.2f), 201, 5, 1.2f);
            Props.Bottles(mb, V(-6.4f, F + Props.TableTop, -4.0f), 202, 3, 0.2f);
            Props.Candle(mb, null, V(-7.3f, F + Props.TableTop, -4.3f));
            Props.Candle(mb, null, V(-7.1f, F + Props.TableTop, -4.05f));
            Arch.FloorDecal(mb, Mat.Decal("blood_splatter_2"), V(-7.4f, F + Props.TableTop + 0.002f, -4.3f), 0.9f, 0.7f, 20f);
            ctx.Key(A, V(-7.9f, F + Props.TableTop, -3.95f));
            ctx.Common(A, V(-6.1f, F + Props.TableTop, -4.45f));
            Props.ChinaCabinet(ctx, mb, V(-10.65f, F, -3.6f), -90f);
            Props.Sideboard(ctx, mb, V(-5.25f, F, -0.33f), 0f, 1.5f);
            Props.Bottles(mb, V(-5.6f, F + Props.SideboardTop, -0.35f), 203, 2, 0.15f);
            ctx.Key(A, V(-4.85f, F + Props.SideboardTop, -0.33f));
            VentDuct(ctx, mb);
            Props.WallPicture(mb, V(-7f, F + 1.8f, -7.9f), Vector3.forward, 1.0f, 0.55f, Mat.Lit(Tex.PaintingLandscape), Mat.Lit(Tex.WoodFurniture, new Color(0.7f, 0.55f, 0.3f)), 0.07f);
            Arch.Decal(mb, Mat.Decal("blood_splatter_1"), V(-5.5f, F + 1.4f, -0.08f), Vector3.back, 1.2f, 1.2f, 10f);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(-6.6f, F, -4.6f), 1.4f, 1.1f, 50f);
            ctx.Common(A, V(-10.4f, F, -7.45f));
            ctx.DeskNote(A, V(-7.5f, F + Props.TableTop + 0.003f, -4.5f), 10f);
            Arch.Bulb(ctx, mb, GlowBulbs, V(-7f, CG, -4.2f), 0.75f, Warm, 1.0f, 5.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Dining_Chandelier", true, 0.3f);
            ctx.Nav.Add(V(-7f, F, -6.6f), A);
            ctx.Nav.Add(V(-7.2f, F, -1.7f), A);
            ctx.Nav.Add(V(-4.2f, F, -2.1f), A);
            ctx.Nav.Add(V(-9.8f, F, -6.2f), A);
        }

        /// <summary>Hole in the dining / kitchen wall (z = 0) at crouch height: the vent grate.</summary>
        internal static readonly Opening VentGrate = new Opening { Level = 1, AlongX = true, C = 0f, Along = -9.3f, W = 0.7f, H = 0.95f, Sill = 0.42f };

        /// <summary>
        /// A crouch-only sheet-metal duct boxed in along the dining room's north wall (inside 2.3 x 0.9 x 1.25 m,
        /// Omar's 1.95 m capsule does not fit). Floor-level entrance at its east end; from the far end a grate in the
        /// wall looks across the kitchen onto the butcher table where Omar chops meat.
        /// </summary>
        static void VentDuct(MapContext ctx, MeshBuilder mb)
        {
            float F = FG;
            float x0 = -10.9f, x1 = -8.45f, z0 = -1.05f, z1 = -0.07f, top = F + 1.25f, t = 0.05f;
            var sheet = Mat.Lit(Tex.Galvanized, new Color(0.62f, 0.62f, 0.6f));
            var dark = Mat.Lit(Tex.MetalDark);
            mb.Color = Shade.Gray(0.7f);
            mb.Material = sheet;
            // south side, lid, and the inside faces of both (dark, dented)
            mb.AddBox(V((x0 + x1) * 0.5f, (F + top) * 0.5f, z0 + t * 0.5f), V(x1 - x0, top - F, t), BoxUV.Local, 0.9f);
            mb.AddBox(V((x0 + x1) * 0.5f, top + t * 0.5f, (z0 + z1) * 0.5f + 0.01f), V(x1 - x0, t, z1 - z0 + 0.02f), BoxUV.Local, 0.9f);
            mb.Material = dark;
            mb.Color = Shade.Gray(0.45f);
            mb.AddBox(V((x0 + x1) * 0.5f, F + 0.012f, (z0 + z1) * 0.5f), V(x1 - x0, 0.024f, z1 - z0 - t), BoxUV.Local, 0.9f); // duct floor
            // rivet seams every 0.6 m
            mb.Material = sheet;
            mb.Color = Shade.Gray(0.55f);
            for (float x = x0 + 0.6f; x < x1 - 0.1f; x += 0.6f)
            {
                mb.AddBox(V(x, (F + top) * 0.5f, z0 - 0.004f), V(0.03f, top - F, 0.012f), BoxUV.Local, 0.5f);
                mb.AddBox(V(x, top + t + 0.004f, (z0 + z1) * 0.5f), V(0.03f, 0.012f, z1 - z0), BoxUV.Local, 0.5f);
            }
            // the grate taken off the entrance, leaning against the duct
            mb.Material = dark;
            mb.Color = Shade.Gray(0.6f);
            mb.Push(V(x1 - 0.55f, F + 0.45f, z0 - 0.09f), Quaternion.Euler(-12f, 0f, 0f));
            mb.AddBox(Vector3.zero, V(0.8f, 0.9f, 0.02f), BoxUV.Local, 0.5f, 0f, BoxFaces.PosX | BoxFaces.NegX | BoxFaces.PosY | BoxFaces.NegY);
            for (float bx = -0.35f; bx <= 0.36f; bx += 0.07f) mb.AddBox(V(bx, 0f, 0f), V(0.012f, 0.88f, 0.012f), BoxUV.Local, 0.3f);
            mb.Pop();
            // grate in the wall hole (kitchen side) with bars you peer through
            var g = VentGrate;
            float gy0 = F + g.Sill, gy1 = F + g.H;
            for (float bx = g.Along - g.W * 0.5f + 0.06f; bx < g.Along + g.W * 0.5f - 0.03f; bx += 0.085f)
                mb.AddBox(V(bx, (gy0 + gy1) * 0.5f, 0.05f), V(0.014f, gy1 - gy0, 0.014f), BoxUV.Local, 0.3f);
            mb.AddBox(V(g.Along, gy0 + 0.01f, 0.05f), V(g.W, 0.02f, 0.03f), BoxUV.Local, 0.3f);
            mb.AddBox(V(g.Along, gy1 - 0.01f, 0.05f), V(g.W, 0.02f, 0.03f), BoxUV.Local, 0.3f);
            // reveal of the hole (the wall is 14 cm thick)
            mb.Material = Mat.Lit(Tex.PlasterDirty, new Color(0.6f, 0.58f, 0.52f));
            mb.AddBox(V(g.Along, gy0 - 0.005f, 0f), V(g.W, 0.01f, IntT), BoxUV.Local, 0.5f);
            mb.AddBox(V(g.Along, gy1 + 0.005f, 0f), V(g.W, 0.01f, IntT), BoxUV.Local, 0.5f);
            mb.AddBox(V(g.Along - g.W * 0.5f - 0.005f, (gy0 + gy1) * 0.5f, 0f), V(0.01f, gy1 - gy0, IntT), BoxUV.Local, 0.5f);
            mb.AddBox(V(g.Along + g.W * 0.5f + 0.005f, (gy0 + gy1) * 0.5f, 0f), V(0.01f, gy1 - gy0, IntT), BoxUV.Local, 0.5f);
            mb.Color = Shade.Gray(1f);
            // colliders: side + lid (crouch only inside), bars so nothing passes through the grate
            ctx.Solid(V((x0 + x1) * 0.5f, (F + top) * 0.5f, z0 + t * 0.5f), V(x1 - x0, top - F, t), SurfaceType.Metal, "VentDuctSide");
            ctx.Solid(V((x0 + x1) * 0.5f, top + 0.15f, (z0 + z1) * 0.5f), V(x1 - x0, 0.3f, z1 - z0 + 0.06f), SurfaceType.Metal, "VentDuctLid");
            // the bars stop things but not eyes (Corpse layer: solid for bodies, not a sight blocker)
            ctx.Solid(V(g.Along, (gy0 + gy1) * 0.5f, 0.05f), V(g.W, gy1 - gy0, 0.02f), SurfaceType.Metal, "VentGrateBars").gameObject.layer = Layers.Corpse;
            ctx.Data.CrawlSpaces.Add(MapMath.MinMax(V(x0, F, z0), V(x1, top, z1)));
        }

        /// <summary>Omar's butcher routine at the kitchen table, watched through the vent grate.</summary>
        static void KitchenRoutine(MapContext ctx, MeshBuilder mb)
        {
            float F = FG;
            var interact = GeoUtil.AddBox(ctx.Dynamic, V(-7.4f, F + Props.ButcherTop + 0.15f, 4.4f), V(2.1f, 0.45f, 1.1f), Quaternion.identity,
                Layers.Interactable, SurfaceType.Default, true, "ChopInteract");
            var g = VentGrate;
            Vector3 eye = V(g.Along, F + g.Sill + 0.25f, -0.35f);
            Vector3 table = V(-7.4f, F + 1.1f, 4.4f);
            ctx.Data.Kitchen = new KitchenInfo
            {
                Area = "House.Kitchen",
                ChopPose = new Pose(V(-7.45f, F, 5.28f), MapMath.Yaw(180f)), // right against the table edge (z 4.9) + his radius
                BlockTop = V(-7.55f, F + Props.ButcherTop + 0.1f, 4.45f),
                ChopInteract = interact,
                VentArea = ctx.Data.CrawlSpaces.Count > 0 ? ctx.Data.CrawlSpaces[ctx.Data.CrawlSpaces.Count - 1] : new Bounds(eye, Vector3.one),
                VentView = new Pose(eye, Quaternion.LookRotation(table - eye)),
            };
        }

        static void Kitchen(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Kitchen";
            float F = FG;
            // fridge with the static TV on top (like the reference video)
            Props.Fridge(ctx, mb, V(-9.6f, F, 7.53f), 0f, true);
            Props.CrtTv(ctx, mb, V(-9.6f, F + Props.FridgeTop, 7.5f), 0f);
            var tvPos = V(-9.6f, F + Props.FridgeTop, 7.5f) + Props.TvScreenLocal;
            BuildTvScreen(ctx, "TvKitchen", tvPos, 0f, out var tvLight);
            ctx.Emitter(tvPos, "Audio/Ambience/tv_static_loop", 0.45f, 9f, true);
            Props.ButcherTable(ctx, mb, V(-7.4f, F, 4.4f), 0f, 301);
            KitchenRoutine(ctx, mb);
            Props.MeatHooks(mb, V(-7.0f, CG, 7.35f), 0f, 3.4f, 6, 302);
            Props.Counter(ctx, mb, V(-10.58f, F, 1.65f), -90f, 2.4f, true);
            Props.Counter(ctx, mb, V(-5.0f, F, 0.39f), 180f, 2.2f);
            Props.Stove(ctx, mb, V(-3.5f, F, 0.41f), 180f);
            Props.UpperCabinet(mb, V(-5.0f, F + 1.5f, 0.24f), 180f, 2.2f);
            Props.Cans(mb, V(-5.4f, F + Props.CounterTop, 0.4f), 303, 4, 0.3f);
            Props.Bottles(mb, V(-10.6f, F + Props.CounterTop, 2.4f), 304, 3, 0.15f);
            Props.TrashBags(mb, V(-5.2f, F, 7.35f), 305, 3);
            Props.Bucket(mb, V(-8.9f, F, 3.2f), 20f, false, 0.6f, false);
            Props.WallPhone(mb, V(-3.07f, F + 1.5f, 3.0f), Vector3.left);
            ctx.Marker("PhoneKitchen", V(-3.17f, F + 1.5f, 3.0f), Quaternion.LookRotation(Vector3.left));
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(-9.0f, F, 5.6f), 1.6f, 1.2f, 70f);
            for (int i = 0; i < 4; i++)
                Arch.FloorDecal(mb, Mat.Decal("blood_smear"), V(-8.6f - i * 0.55f, F, 4.0f - i * 0.05f), 0.5f, 0.9f, 90f + i * 6f);
            Arch.Decal(mb, Mat.Decal("blood_splatter_3"), V(-7.0f, F + 1.6f, 7.89f), Vector3.back, 1.6f, 1.4f, 0f);
            Arch.Decal(mb, Mat.Decal("blood_splatter_1"), V(-3.08f, F + 1.0f, 1.5f), Vector3.left, 1.0f, 1.0f, 30f);
            Arch.Decal(mb, Mat.Decal("blood_handprint"), V(-10.89f, F + 1.3f, 5.3f), Vector3.right, 0.22f, 0.22f, -20f);
            Arch.Decal(mb, Mat.Decal("grime"), V(-6f, F, 2.4f), Vector3.up, 2.2f, 2.0f, 10f);
            ctx.Key(A, V(-10.55f, F + Props.CounterTop, 0.85f));
            ctx.Common(A, V(-5.6f, F + Props.CounterTop, 0.36f));
            ctx.Key(A, V(-6.65f, F + Props.ButcherTop, 4.78f));
            ctx.Common(A, V(-8.6f, F, 7.45f));
            ctx.Common(A, V(-3.5f, F + 0.93f, 0.25f));
            ctx.DeskNote(A, V(-4.4f, F + Props.CounterTop + 0.003f, 0.42f), 80f);
            Arch.Bulb(ctx, mb, GlowBulbs, V(-7.4f, CG, 4.4f), 0.6f, Warm, 1.15f, 6f, PsxFlicker.FaultyBulb, LightGroup.Power, "Kitchen_Bulb", true, 0.45f);
            ctx.Nav.Add(V(-7.4f, F, 2.2f), A);
            ctx.Nav.Add(V(-7.4f, F, 6.2f), A);
            ctx.Nav.Add(V(-4.6f, F, 5.4f), A);
            ctx.Nav.Add(V(-9.7f, F, 5.4f), A);
            ctx.Tripwire("House.Hall", V(-2.92f, F + 0.12f, 6.04f), V(-2.92f, F + 0.12f, 6.96f));
        }

        /// <summary>Separate renderer for a CRT screen quad + TvScreen animator + a flickering bluish glow light.</summary>
        static void BuildTvScreen(MapContext ctx, string marker, Vector3 center, float yaw, out PsxLight light)
        {
            var rot = MapMath.Yaw(yaw);
            var smb = new MeshBuilder();
            smb.Material = Mat.Unlit(Tex.TvStatic0);
            float hw = Props.TvScreenW * 0.5f, hh = Props.TvScreenH * 0.5f;
            smb.Push(center, rot);
            smb.AddQuad(V(-hw, -hh, -0.005f), V(-hw, hh, -0.005f), V(hw, hh, -0.005f), V(hw, -hh, -0.005f));
            smb.Pop();
            var go = smb.Build("TvScreen_" + marker, ctx.Static, Layers.World);
            ctx.CountRenderer(smb);
            Vector3 front = rot * Vector3.back;
            light = ctx.Light(center + front * 0.6f, new Color(0.55f, 0.62f, 0.75f), 0.75f, 3.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "TvGlow_" + marker, 0.6f, 14f);
            var tv = go.AddComponent<TvScreen>();
            tv.PowerProbe = light;
            tv.Screen = go.GetComponent<MeshRenderer>();
            ctx.Marker(marker, center + front * 0.01f, Quaternion.LookRotation(front));
        }

        /// <summary>The living room is the grandmother's TV room: she sits in her wheelchair in front of the CRT,
        /// crosses on the walls, papers and dirt everywhere, the TV is the main light.</summary>
        static void Living(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Living";
            float F = FG;
            Props.Rug(mb, V(7.6f, F, -4.5f), 90f, 2.8f, 2.2f, new Color(0.5f, 0.42f, 0.34f));
            Props.TvStand(ctx, mb, V(10.62f, F, -4.5f), 90f);
            Props.CrtTv(ctx, mb, V(10.6f, F + Props.TvStandTop, -4.5f), 90f);
            var tvPos = V(10.6f, F + Props.TvStandTop, -4.5f) + MapMath.Yaw(90f) * Props.TvScreenLocal;
            BuildTvScreen(ctx, "TvLivingRoom", tvPos, 90f, out _);
            Props.Sofa(ctx, mb, V(5.6f, F, -4.5f), -90f);
            Arch.Blob(mb, V(5.6f, F, -4.5f), 1.2f, 2.4f);
            // her side table with pills and a glass, pushed against the wall
            Props.CoffeeTable(ctx, mb, V(8.3f, F, -7.3f), 0f);
            Props.Cans(mb, V(8.2f, F + Props.CoffeeTop, -7.3f), 401, 2, 0.12f);
            Props.Armchair(ctx, mb, V(8.4f, F, -2.05f), 17f);
            Props.Bookshelf(ctx, mb, V(9.9f, F, -1.24f), 0f, 1.0f, 2.0f, 402);
            Props.FloorLampProp(mb, V(3.55f, F, -7.45f));
            ctx.Light(V(3.55f, F + 1.45f, -7.45f), new Color(0.85f, 0.8f, 0.72f), 0.45f, 4f, PsxFlicker.FaultyBulb, LightGroup.Power, "Living_Lamp", 0.25f, 7f, GlowBulbs);
            var g = ctx.GlowBuilder(GlowBulbs);
            g.Material = ctx.GlowColor(new Color(0.9f, 0.82f, 0.7f));
            g.AddSphere(V(3.55f, F + 1.4f, -7.45f), new Vector3(0.06f, 0.08f, 0.06f), 5, 3);
            // clutter: papers, dirt, bottles, pill boxes
            Props.Papers(mb, V(5.2f, F, -2.3f), 403, 5, 0.6f);
            Props.Papers(mb, V(8.9f, F, -5.8f), 405, 4, 0.5f);
            Props.Papers(mb, V(6.6f, F, -6.9f), 406, 3, 0.45f);
            Props.Bottles(mb, V(4.4f, F, -6.9f), 404, 4, 0.25f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(7.2f, F, -3.2f), 2.2f, 1.8f, 20f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(9.6f, F, -6.6f), 1.6f, 1.4f, 70f);
            Arch.Decal(mb, Mat.Decal("grime"), V(10.89f, F + 2.2f, -6.0f), Vector3.left, 1.5f, 1.8f, 0f);
            // crosses on the walls (her room only)
            Arch.Decal(mb, Mat.Decal("crosses_1"), V(10.89f, F + 2.05f, -2.4f), Vector3.left, 1.3f, 1.3f, 0f);
            Arch.Decal(mb, Mat.Decal("crosses_1"), V(6.2f, F + 2.0f, -7.89f), Vector3.forward, 1.1f, 1.1f, 6f);
            Arch.Decal(mb, Mat.Decal("crosses_2"), V(3.11f, F + 2.1f, -6.6f), Vector3.right, 0.9f, 0.9f, -4f);
            ctx.Key(A, V(8.45f, F + Props.CoffeeTop, -7.15f));
            ctx.Common(A, V(9.9f, F + Props.BookshelfLevel(1), -1.32f));
            ctx.Key(A, V(9.9f, F + Props.BookshelfLevel(3), -1.32f));
            ctx.Common(A, V(5.0f, F, -7.45f));
            ctx.Common(A, V(5.65f, F + 0.485f, -5.2f));
            Dyn.Wardrobe(ctx, "Living.Wardrobe", V(4.3f, F, -1.39f), 0f);
            ctx.WallNote(A, V(3.08f, F + 1.5f, -2.6f), Vector3.right);
            ctx.Nav.Add(V(4.6f, F, -3.0f), A);
            ctx.Nav.Add(V(7.2f, F, -6.5f), A);
            ctx.Nav.Add(V(7.0f, F, -2.0f), A);
            ctx.Nav.Add(V(9.4f, F, -6.9f), A);

            ctx.Data.Grandma = new GrandmaInfo
            {
                Area = A,
                Room = MapMath.MinMax(V(3.1f, F, -7.9f), V(10.9f, F + 3f, -1.1f)),
                ChairPose = new Pose(V(8.25f, F, -4.5f), MapMath.Yaw(90f)),
                TvSoundPosition = tvPos,
            };
            if (ctx.Data.Markers.TryGetValue("TvLivingRoom", out var screen)) ctx.Data.Grandma.TvScreen = screen;
        }

        static void Bathroom(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Bathroom";
            float F = FG;
            Props.Tub(ctx, mb, V(9.2f, F, 2.55f), 0f, true);
            Props.Toilet(ctx, mb, V(4.2f, F, 2.58f), 0f);
            Props.PedestalSink(ctx, mb, V(3.33f, F, 0.6f), -90f);
            Props.Mirror(mb, V(3.08f, F + 1.55f, 0.6f), Vector3.right, 0.5f, 0.65f);
            Arch.Decal(mb, Mat.Decal("writing_dont_look"), V(9.0f, F + 1.6f, -0.92f), Vector3.forward, 1.3f, 0.65f, 3f);
            Arch.Decal(mb, Mat.Decal("blood_splatter_3"), V(10.89f, F + 1.0f, 2.2f), Vector3.left, 1.0f, 1.0f, 0f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(6f, F, 1f), 2.5f, 2.0f, 0f);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(8.2f, F, 1.8f), 1.0f, 0.8f, 10f);
            Props.Bucket(mb, V(5.4f, F, 2.6f), 0f, false, 0.7f, false);
            ctx.Key(A, V(3.36f, F + Props.SinkTop, 0.75f));
            ctx.Common(A, V(4.2f, F + Props.ToiletTankTop, 2.78f));
            ctx.Common(A, V(7.6f, F, 2.55f));
            ctx.Key(A, V(10.5f, F, -0.55f));
            Arch.Bulb(ctx, mb, GlowBulbs, V(7f, CG, 1f), 0.4f, new Color(0.9f, 0.85f, 0.7f), 0.9f, 5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Bath_Bulb", false, 0.6f);
            ctx.Nav.Add(V(7.0f, F, 0.7f), A);
            ctx.Nav.Add(V(5.0f, F, 1.0f), A);
            ctx.Nav.Add(V(9.6f, F, 0.6f), A);
        }

        static void ClockBedroom(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.ClockBedroom";
            float F = FG;
            Props.Rug(mb, V(7.0f, F, 5.3f), 0f, 2.6f, 1.9f, new Color(0.65f, 0.45f, 0.38f));
            var bedPos = V(9.9f, F, 5.2f);
            var bedLift = Dyn.LiftableBed(ctx, "ClockBedroom.Bed", bedPos, 90f, Vector3.back, false, true, true, true);
            Arch.Blob(mb, bedPos, 2.4f, 1.4f);
            Dyn.Figure(ctx, bedLift, FigureKind.Corpse, FigurePose.LyingOnBack, bedPos + Vector3.up * Props.BedTop, MapMath.Yaw(90f), ctx.Seed ^ 0x51);
            Dyn.UnderBed(ctx, "ClockBedroom.Bed", bedPos, 90f, Vector3.back, 0.5f, 2.0f, 0.36f, bedLift);
            Props.Nightstand(ctx, mb, V(10.62f, F, 6.15f), 90f);
            ctx.Gun(A, V(10.66f, F + Props.NightstandTop, 6.0f), 75f);
            Props.Candle(mb, null, V(10.55f, F + Props.NightstandTop, 6.25f));
            // grandfather clock + radiator on the north wall (like Ddf9Y0Y)
            GrandfatherClock(ctx, mb, V(7.5f, F, 7.72f));
            Radiator(ctx, mb, V(6.2f, F, 7.8f));
            Props.Dresser(ctx, mb, V(7.0f, F, 3.33f), 180f, 1.0f);
            ctx.Gun(A, V(6.75f, F + Props.DresserTop, 3.36f), 200f);
            Dyn.Wardrobe(ctx, "ClockBedroom.Wardrobe", V(3.39f, F, 4.2f), -90f);
            // black occult scrawls
            Arch.Decal(mb, Mat.Decal("graffiti_scrawl_1"), V(10.89f, F + 2.0f, 5.2f), Vector3.left, 1.7f, 1.7f, 0f);
            Arch.Decal(mb, Mat.Decal("graffiti_scrawl_2"), V(4.6f, F + 2.0f, 7.89f), Vector3.back, 1.4f, 1.4f, 8f);
            Arch.Decal(mb, Mat.Decal("graffiti_scrawl_1"), V(9.2f, F + 1.9f, 3.08f), Vector3.forward, 1.5f, 1.5f, 20f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(8.6f, F, 6.6f), 1.5f, 1.2f, 20f);
            ctx.Key(A, V(7.2f, F + Props.DresserTop, 3.33f));
            ctx.Common(A, V(6.2f, F + 0.74f, 7.8f));
            ctx.Common(A, V(8.5f, F, 6.5f));
            ctx.Key(A, V(10.62f, F + Props.NightstandTop, 6.05f));
            ctx.WallNote(A, V(10.89f, F + 1.5f, 3.9f), Vector3.left);
            Arch.Bulb(ctx, mb, GlowBulbs, V(7f, CG, 5.5f), 0.5f, Warm, 1.0f, 5.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Clock_Bulb", false, 0.4f);
            ctx.Nav.Add(V(5.5f, F, 5.5f), A);
            ctx.Nav.Add(V(7.6f, F, 6.65f), A);
            ctx.Nav.Add(V(7.8f, F, 4.3f), A);
        }

        static void GrandfatherClock(MapContext ctx, MeshBuilder mb, Vector3 pos)
        {
            mb.Push(pos, Quaternion.identity);
            var wood = Mat.Lit(Tex.WoodFurniture, new Color(0.8f, 0.6f, 0.45f));
            mb.Color = Shade.Gray(0.8f);
            mb.Material = wood;
            mb.AddBox(V(0, 0.22f, 0), V(0.52f, 0.44f, 0.36f), BoxUV.Local, 0.4f);
            mb.Material = Mat.Lit(Tex.ClockBody);
            mb.AddBox(V(0, 0.98f, 0), V(0.42f, 1.08f, 0.3f), BoxUVRects.Front(new Rect(0, 0, 1, 1), new Rect(0, 0, 0.2f, 0.2f)), BoxFaces.All & ~BoxFaces.NegY);
            mb.Material = wood;
            mb.AddBox(V(0, 1.78f, 0), V(0.5f, 0.52f, 0.36f), BoxUV.Local, 0.4f);
            mb.AddBox(V(0, 2.1f, 0), V(0.56f, 0.12f, 0.4f), BoxUV.Local, 0.4f);
            mb.Material = Mat.Lit(Tex.ClockFace);
            mb.Color = Shade.Gray(1f);
            mb.AddQuad(V(-0.19f, 1.59f, -0.185f), V(-0.19f, 1.97f, -0.185f), V(0.19f, 1.97f, -0.185f), V(0.19f, 1.59f, -0.185f));
            mb.Pop();
            ctx.Solid(pos + V(0, 1.08f, 0), V(0.52f, 2.16f, 0.38f), SurfaceType.Wood, "Clock");
            ctx.Marker("GrandfatherClock", pos + V(0, 1.78f, -0.19f), Quaternion.LookRotation(Vector3.back));
        }

        static void Radiator(MapContext ctx, MeshBuilder mb, Vector3 pos)
        {
            mb.Push(pos, Quaternion.identity);
            mb.Color = Shade.Gray(0.75f);
            mb.Material = Mat.Lit(Tex.Radiator);
            mb.AddBox(V(0, 0.42f, 0), V(1.0f, 0.62f, 0.16f), BoxUVRects.All(new Rect(0, 0, 1, 1)), BoxFaces.NegZ | BoxFaces.PosZ | BoxFaces.PosY);
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.6f, 0.55f, 0.5f));
            mb.AddBox(V(0, 0.42f, 0), V(1.0f, 0.62f, 0.16f), BoxUV.Local, 0.3f, 0f, BoxFaces.PosX | BoxFaces.NegX);
            mb.AddBox(V(-0.42f, 0.05f, 0), V(0.06f, 0.1f, 0.12f), BoxUV.Local, 0.3f);
            mb.AddBox(V(0.42f, 0.05f, 0), V(0.06f, 0.1f, 0.12f), BoxUV.Local, 0.3f);
            mb.AddBeam(V(0.45f, 0.2f, 0.04f), V(0.55f, -0.02f, 0.04f), 0.035f);
            mb.Color = Shade.Gray(1f);
            mb.Pop();
            ctx.Solid(pos + V(0, 0.37f, 0), V(1.0f, 0.74f, 0.18f), SurfaceType.Metal, "Radiator");
        }

        // ================================================================== UPPER FLOOR (y = 3.8)

        static void UpperRooms(MapContext ctx, MeshBuilder mb)
        {
            Safe("upper hall", () => UpperHall(ctx, mb));
            Safe("cage room", () => CageRoom(ctx, mb));
            Safe("storage", () => Storage(ctx, mb));
            Safe("radio room", () => RadioRoom(ctx, mb));
            Safe("supply closet", () => SupplyCloset(ctx, mb));
            Safe("study", () => Study(ctx, mb));
        }

        static void UpperHall(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.UpperHall";
            float F = FU;
            Props.Crate(ctx, mb, V(-2.45f, F, 7.4f), 5f, 0.6f, true);
            Props.Crate(ctx, mb, V(-2.45f, F + 0.48f, 7.4f), 35f, 0.45f, true, true);
            Props.Bucket(mb, V(-2.5f, F, -7.45f), 0f, false, 0.7f, false);
            Props.Chair(ctx, mb, V(1.0f, F, -7.35f), 200f);
            Props.WallPicture(mb, V(-2.93f, F + 1.7f, -2.8f), Vector3.right, 0.6f, 0.45f, Mat.Lit(Tex.PaintingLandscape, new Color(0.7f, 0.7f, 0.7f)), Mat.Lit(Tex.WoodFurniture), 0.05f);
            Arch.Decal(mb, Mat.Decal("writing_he_sees_you"), V(-2.92f, F + 1.4f, 3.6f), Vector3.right, 1.3f, 0.65f, -6f);
            Arch.Decal(mb, Mat.Decal("blood_smear"), V(-1.0f, F, -5.4f), Vector3.up, 0.6f, 1.6f, 80f);
            Arch.Decal(mb, Mat.Decal("water_stain"), V(-0.5f, CU - 0.01f, 2f), Vector3.down, 2.0f, 1.6f, 20f);
            ctx.Key(A, V(-2.45f, F + 0.48f + 0.36f, 7.4f));
            ctx.Common(A, V(-2.4f, F, -6.9f));
            ctx.WallNote(A, V(-2.92f, F + 1.5f, 0.6f), Vector3.right);
            Arch.Bulb(ctx, mb, GlowBulbs, V(0, CU, -4.5f), 0.55f, Warm, 1.0f, 6.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "UpperHall_Bulb_S", false, 0.5f);
            Arch.Bulb(ctx, mb, GlowBulbs, V(0, CU, 6.0f), 0.5f, Warm, 0.95f, 6f, PsxFlicker.FaultyBulb, LightGroup.Power, "UpperHall_Bulb_N", false, 0.35f);
            ctx.Tripwire(A, V(1.62f, F + 0.12f, 5.3f), V(2.92f, F + 0.12f, 5.3f));
        }

        static void CageRoom(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.CageRoom";
            float F = FU;
            float[] cz = { -6.55f, -4.15f, -1.75f, 0.65f };
            var hay = Mat.Cutout(Tex.GrassTall2, new Color(0.8f, 0.7f, 0.5f));
            int room = ctx.CellRoom("CageRoom", A, V(-6.5f, F, -3.0f));
            for (int i = 0; i < 4; i++)
            {
                var c = V(-9.8f, F, cz[i]);
                var cage = Dyn.Cage(ctx, mb, room, c, -90f);
                // inside: straw, a piss bucket, a stain - part of the cage slot, so an unused slot leaves nothing behind
                var inside = Dyn.CageDressing(ctx, cage);
                Props.Billboard(inside, hay, c + V(-0.5f, 0, 0.4f), 0.9f, 0.3f, 2, 30f + i * 20f, 0.5f, 0.8f);
                Props.Billboard(inside, hay, c + V(-0.2f, 0, -0.6f), 0.7f, 0.25f, 2, 70f + i * 15f, 0.5f, 0.8f);
                Props.Bucket(inside, c + V(-0.65f, 0, 0.7f * (i % 2 == 0 ? 1 : -1)), i * 40f, false, 0.55f, false);
                Arch.FloorDecal(inside, Mat.Decal(i % 2 == 0 ? "blood_splatter_1" : "grime"), c + V(0.2f, 0.035f, 0), 1.2f, 1.2f, i * 70f);
                // each cage has its own marks: tallies, fingernail scratches, a list of names, crosses
                string[] marks = { "graffiti_scrawl_2", "nail_scratches", "writing_names", "crosses_2" };
                Arch.Decal(mb, Mat.Decal(marks[i]), V(-10.89f, F + 1.0f, cz[i]), Vector3.right, 0.8f, 0.8f, i == 2 ? 0f : i * 25f);
                ctx.Nav.Add(V(-7.6f, F, cz[i]), A);
            }
            Props.Chair(ctx, mb, V(-5.5f, F, -3.0f), 90f);
            Props.Table(ctx, mb, V(-3.5f, F, -0.5f), 90f, 1.4f, 0.7f, Props.TableTop, Tex.TableWood, 0.6f);
            Props.Toolbox(mb, V(-3.5f, F + Props.TableTop, 0.0f), 80f);
            Props.Bottles(mb, V(-3.45f, F + Props.TableTop, -0.9f), 501, 2, 0.1f);
            Props.Bucket(mb, V(-6.0f, F, 1.35f), 0f, false, 0.6f, false);
            Props.FloorMattress(mb, V(-5.6f, F, 0.9f), 90f, true);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(-6.4f, F, -2.5f), 1.6f, 1.3f, 20f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(-7.0f, F, -5.0f), 3.0f, 2.6f, 0f);
            Arch.Decal(mb, Mat.Decal("writing_let_me_out"), V(-3.08f, F + 1.6f, -2.4f), Vector3.left, 1.4f, 0.7f, 4f);
            Arch.Decal(mb, Mat.Decal("blood_handprint"), V(-7.5f, F + 1.0f, 1.92f), Vector3.back, 0.25f, 0.25f, 0f);
            ctx.Key(A, V(-3.5f, F + Props.TableTop, -1.0f));
            ctx.Common(A, V(-4.0f, F, -7.45f));
            ctx.Common(A, V(-5.0f, F, -1.6f));
            ctx.WallNote(A, V(-3.08f, F + 1.45f, -6.9f), Vector3.left);
            Arch.Bulb(ctx, mb, GlowBulbs, V(-6.5f, CU, -3.0f), 0.65f, Warm, 1.05f, 5.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Cage_Bulb", false, 0.75f);
            ctx.Marker("CageRoom", V(-5.0f, F + 1.6f, -3.0f), Quaternion.LookRotation(Vector3.left));
            ctx.Marker("OmarIntro", V(-3.8f, F, -5.0f), Quaternion.LookRotation(Vector3.left));
            ctx.Nav.Add(V(-5.0f, F, -6.0f), A);
            ctx.Nav.Add(V(-5.0f, F, -1.3f), A);
            ctx.Tripwire("House.UpperHall", V(-2.92f, F + 0.12f, -5.46f), V(-2.92f, F + 0.12f, -4.54f));
        }

        static void Storage(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Storage";
            float F = FU;
            float[] ax = { -9.9f, -8.0f, -6.1f };
            for (int i = 0; i < 3; i++) Props.MetalShelf(ctx, mb, V(ax[i], F, 7.64f), 0f, 1.8f, 600 + i, 0.85f, true);
            float[] bx = { -9.4f, -7.5f };
            for (int i = 0; i < 2; i++)
            {
                Props.MetalShelf(ctx, mb, V(bx[i], F, 4.9f), 0f, 1.8f, 610 + i, 0.8f, true);
                Props.MetalShelf(ctx, mb, V(bx[i], F, 5.42f), 180f, 1.8f, 620 + i, 0.8f, true);
            }
            Props.MetalShelf(ctx, mb, V(-9.9f, F, 2.33f), 180f, 1.8f, 630, 0.9f, true);
            Props.MetalShelf(ctx, mb, V(-8.0f, F, 2.33f), 180f, 1.8f, 631, 0.9f, true);
            var black = Tex.BarrelWater;
            Props.Barrel(ctx, mb, V(-5.9f, F, 2.6f), 10f, black, 0.35f);
            Props.Barrel(ctx, mb, V(-5.25f, F, 2.55f), 40f, black, 0.35f);
            Props.Barrel(ctx, mb, V(-5.6f, F, 3.2f), 75f, black, 0.35f);
            for (int i = 0; i < 4; i++) Props.Bucket(mb, V(-4.4f + (i % 2) * 0.35f, F + (i / 2) * 0.41f, 2.45f), i * 30f, i % 2 == 0);
            Props.Bucket(mb, V(-4.25f, F, 3.1f), 50f, true);
            Arch.FloorDecal(mb, Mat.Decal("water_stain"), V(-7f, F, 3.6f), 2.0f, 1.5f, 0f);
            // the shelves keep the left 0.5 m of their 2nd level free for items
            ctx.Key(A, V(-9.9f - 0.62f, F + Props.ShelfLevel(1), 7.52f));
            ctx.Common(A, V(-8.0f - 0.62f, F + Props.ShelfLevel(1), 7.52f));
            ctx.Key(A, V(-9.4f - 0.62f, F + Props.ShelfLevel(1), 4.78f));
            ctx.Common(A, V(-7.5f + 0.62f, F + Props.ShelfLevel(1), 5.54f));
            ctx.Common(A, V(-5.6f, F + Props.BarrelTop, 3.2f));
            ctx.DeskNote(A, V(-5.25f, F + Props.BarrelTop + 0.003f, 2.55f), 30f);
            Arch.Bulb(ctx, mb, GlowBulbs, V(-7.0f, CU, 3.6f), 0.5f, Warm, 0.95f, 5.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Storage_Bulb", false, 0.35f);
            ctx.Nav.Add(V(-4.6f, F, 3.9f), A);
            ctx.Nav.Add(V(-8.0f, F, 3.65f), A);
            ctx.Nav.Add(V(-8.0f, F, 6.6f), A);
            ctx.Nav.Add(V(-5.0f, F, 6.0f), A);
        }

        static void RadioRoom(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.RadioRoom";
            float F = FU;
            Props.Desk(ctx, mb, V(10.55f, F, -4.5f), 90f, 1.6f, 0.7f);
            var radioPos = V(10.62f, F + Props.DeskTop, -4.35f);
            Props.RadioSet(mb, radioPos, 90f);
            ctx.Data.Radio.RadioSet = ctx.Interact(null, V(10.42f, F + Props.DeskTop + 0.2f, -4.35f), V(0.55f, 0.5f, 0.85f), Quaternion.identity, "RadioSet");
            ctx.Marker("RadioDesk", radioPos + V(-0.3f, 0.2f, 0), Quaternion.LookRotation(Vector3.left));
            Props.Chair(ctx, mb, V(9.7f, F, -4.5f), -90f);
            Props.Papers(mb, V(10.5f, F + Props.DeskTop, -5.0f), 701, 3, 0.15f);
            // maps and papers pinned on the walls
            var paper = Mat.TwoSided(Tex.PaperNote);
            var map = Mat.Lit(Tex.PaperNote, new Color(0.75f, 0.8f, 0.65f));
            Arch.Quad(mb, map, V(7.0f, F + 1.7f, -1.09f), Vector3.back, Vector3.up, 1.6f, 1.1f, new Rect(0, 0, 1, 1));
            var rng = ctx.Rng("radio.papers");
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = i < 5 ? V(5.2f + i * 0.45f + rng.Range(-0.1f, 0.1f), F + rng.Range(1.2f, 2.2f), -1.1f) : V(10.88f, F + rng.Range(1.3f, 2.1f), -6.8f + (i - 5) * 0.4f);
                Vector3 n = i < 5 ? Vector3.back : Vector3.left;
                mb.Color = Shade.Gray(rng.Range(0.6f, 0.9f));
                Arch.Quad(mb, paper, p + n * 0.01f, n, Vector3.up, 0.21f, 0.28f, new Rect(0, 0, 1, 1), rng.Range(-12f, 12f));
            }
            mb.Color = Shade.Gray(1f);
            Props.WallPicture(mb, V(3.08f, F + 1.75f, -2.4f), Vector3.right, 0.5f, 0.8f, Mat.Lit(Tex.PortraitOmar), Mat.Lit(Tex.WoodFurniture), 0.07f);
            var cotPos = V(6.0f, F, -1.52f);
            var cotLift = Dyn.LiftableBed(ctx, "RadioRoom.Cot", cotPos, 90f, Vector3.back, true);
            Dyn.UnderBed(ctx, "RadioRoom.Cot", cotPos, 90f, Vector3.back, 0.4f, 1.9f, 0.38f, cotLift);
            Props.Locker(ctx, mb, V(7.65f, F, -7.65f), 180f, true);
            Dyn.Locker(ctx, "RadioRoom.Locker", V(7.0f, F, -7.65f), 180f, true);
            Props.Bookshelf(ctx, mb, V(9.5f, F, -1.24f), 0f, 1.0f, 2.0f, 702);
            Props.Crate(ctx, mb, V(4.0f, F, -7.35f), 10f, 0.55f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(8f, F, -4.5f), 2.5f, 2.5f, 0f);
            ctx.Key(A, V(10.55f, F + Props.DeskTop, -5.1f));
            ctx.Common(A, V(9.5f, F + Props.BookshelfLevel(1), -1.32f));
            ctx.Key(A, V(6.0f, F + Props.CotTop + 0.05f, -1.4f));
            ctx.Common(A, V(4.0f, F + 0.55f, -7.35f));
            ctx.Common(A, V(7.65f, F + 1.9f, -7.65f));
            ctx.DeskNote(A, V(10.45f, F + Props.DeskTop + 0.003f, -3.75f), 90f);
            ctx.WallNote(A, V(8.6f, F + 1.5f, -1.09f), Vector3.back);
            // radio room lights: off until the fuse is inserted
            Arch.Bulb(ctx, mb, GlowRadio, V(7.0f, CU, -4.5f), 0.5f, Warm, 1.1f, 6f, PsxFlicker.FaultyBulb, LightGroup.Radio, "Radio_Bulb", false, 0.25f);
            mb.Material = Mat.Lit(Tex.MetalGreen);
            mb.Color = Shade.Gray(0.8f);
            mb.AddCylinder(V(10.65f, F + Props.DeskTop, -5.45f), 0.08f, 0.06f, 0.03f, 6, true, false, null, false);
            mb.AddBeam(V(10.65f, F + Props.DeskTop, -5.45f), V(10.55f, F + Props.DeskTop + 0.4f, -5.4f), 0.02f);
            mb.AddCylinder(V(10.5f, F + Props.DeskTop + 0.35f, -5.4f), 0.1f, 0.04f, 0.1f, 6, false, false, null, false);
            mb.Color = Shade.Gray(1f);
            ctx.Light(V(10.45f, F + Props.DeskTop + 0.3f, -5.35f), new Color(1f, 0.8f, 0.55f), 0.9f, 3.5f, PsxFlicker.None, LightGroup.Radio, "Radio_DeskLamp", 0f, 1f, GlowRadio);
            var rg = ctx.GlowBuilder(GlowRadio);
            rg.Material = ctx.GlowColor(new Color(1f, 0.85f, 0.6f));
            rg.AddSphere(V(10.5f, F + Props.DeskTop + 0.32f, -5.4f), new Vector3(0.04f, 0.03f, 0.04f), 5, 3);
            ctx.Nav.Add(V(6.0f, F, -3.2f), A);
            ctx.Nav.Add(V(8.6f, F, -3.2f), A);
            ctx.Nav.Add(V(5.0f, F, -6.3f), A);
            ctx.Nav.Add(V(8.8f, F, -6.6f), A);
            ctx.Tripwire("House.UpperHall", V(2.92f, F + 0.12f, -5.46f), V(2.92f, F + 0.12f, -4.54f));
        }

        static void SupplyCloset(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Closet";
            float F = FU;
            var w = Dyn.Wardrobe(ctx, "Closet.Wardrobe", V(5.0f, F, -0.6f), 180f);
            Props.Bucket(mb, V(5.0f, F + 2.11f, -0.62f), 15f, true);
            Props.Barrel(ctx, mb, V(6.35f, F, -0.5f), 200f, Tex.BarrelWater, 1f);
            Props.MetalShelf(ctx, mb, V(10.64f, F, 0.0f), 90f, 1.8f, 801, 0.85f, true);
            Props.MetalShelf(ctx, mb, V(10.64f, F, 1.88f), 90f, 1.8f, 802, 0.85f, true);
            Props.Crate(ctx, mb, V(3.5f, F, 2.35f), 5f, 0.6f);
            Props.Crate(ctx, mb, V(3.55f, F + 0.6f, 2.35f), 30f, 0.45f, true, true);
            Props.Crate(ctx, mb, V(3.5f, F, 1.65f), 0f, 0.55f, true);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(7f, F, 1f), 2.0f, 2.0f, 0f);
            ctx.Common(A, V(6.35f, F + Props.BarrelTop, -0.5f));
            ctx.Key(A, V(10.52f, F + Props.ShelfLevel(1), 0.62f));
            ctx.Key(A, V(8.2f, F, -0.6f));
            ctx.Common(A, V(3.55f, F + 0.6f + 0.36f, 2.35f));
            Arch.Bulb(ctx, mb, GlowBulbs, V(7.0f, CU, 1.0f), 0.45f, Warm, 0.9f, 4.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Closet_Bulb", false, 0.3f);
            ctx.Nav.Add(V(7.4f, F, 0.8f), A);
            ctx.Nav.Add(V(4.6f, F, 0.9f), A);
            ctx.Nav.Add(V(9.2f, F, 0.9f), A);
        }

        static void Study(MapContext ctx, MeshBuilder mb)
        {
            const string A = "House.Study";
            float F = FU;
            Props.Rug(mb, V(7.0f, F, 5.4f), 0f, 2.4f, 1.6f, new Color(0.5f, 0.35f, 0.45f));
            Props.Desk(ctx, mb, V(8.8f, F, 7.54f), 0f, 1.4f, 0.7f);
            Props.Chair(ctx, mb, V(8.8f, F, 6.85f), 180f);
            Props.Papers(mb, V(9.15f, F + Props.DeskTop, 7.6f), 901, 2, 0.12f);
            Props.Bookshelf(ctx, mb, V(10.74f, F, 4.0f), 90f, 1.0f, 2.0f, 902);
            Props.Bookshelf(ctx, mb, V(10.74f, F, 5.1f), 90f, 1.0f, 2.0f, 903);
            var bedPos = V(4.1f, F, 4.07f);
            var studyLift = Dyn.LiftableBed(ctx, "Study.Bed", bedPos, 180f, Vector3.right, false, true, false);
            Arch.Blob(mb, bedPos, 1.4f, 2.4f);
            Dyn.UnderBed(ctx, "Study.Bed", bedPos, 180f, Vector3.right, 0.5f, 2.0f, 0.36f, studyLift);
            Props.FloorLampProp(mb, V(10.5f, F, 7.4f), 1.5f);
            Arch.Decal(mb, Mat.Decal("grime"), V(5.0f, F + 2.0f, 7.89f), Vector3.back, 2.2f, 2.0f, 0f);
            Arch.Decal(mb, Mat.Decal("water_stain"), V(10.89f, F + 2.2f, 6.6f), Vector3.left, 1.6f, 2.0f, 0f);
            // desk items like Ddf9Z9 (band-aids / lighter fuel)
            ctx.Key(A, V(8.45f, F + Props.DeskTop, 7.48f));
            ctx.Gun(A, V(8.26f, F + Props.DeskTop, 7.73f), 110f);
            ctx.Common(A, V(9.0f, F + Props.DeskTop, 7.42f));
            ctx.Common(A, V(10.74f - 0.12f, F + Props.BookshelfLevel(2), 4.0f));
            ctx.Common(A, V(10.4f, F, 6.6f));
            ctx.Key(A, V(4.1f, F + Props.BedTop, 4.5f));
            ctx.DeskNote(A, V(8.2f, F + Props.DeskTop + 0.003f, 7.5f), -15f);
            ctx.Light(V(10.5f, F + 1.4f, 7.4f), new Color(1f, 0.72f, 0.5f), 0.9f, 5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Study_Lamp", 0.2f, 6f, GlowBulbs);
            var g = ctx.GlowBuilder(GlowBulbs);
            g.Material = ctx.GlowColor(new Color(1f, 0.75f, 0.5f));
            g.AddSphere(V(10.5f, F + 1.36f, 7.4f), new Vector3(0.06f, 0.08f, 0.06f), 5, 3);
            ctx.Nav.Add(V(6.0f, F, 5.6f), A);
            ctx.Nav.Add(V(8.6f, F, 5.2f), A);
            ctx.Nav.Add(V(7.0f, F, 3.75f), A);
            ctx.Nav.Add(V(5.6f, F, 7.2f), A);
        }
    }
}
