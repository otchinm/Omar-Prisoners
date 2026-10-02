using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>North half of the compound (D7qZKJF): dead corn field, red barn, fuel depot, silo, windmill, red water tower.</summary>
    internal static class FarmBuilder
    {
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public const float FieldX0 = -45f, FieldX1 = 5f, FieldZ0 = 22f, FieldZ1 = 62f;
        public static readonly Rect Clearing = Rect.MinMaxRect(-33f, 40f, -17f, 56f);
        static readonly Rect[] Lanes =
        {
            Rect.MinMaxRect(FieldX0, 29f, FieldX1, 31.5f),     // east-west lane
            Rect.MinMaxRect(-26.25f, FieldZ0, -23.75f, 40f),   // south lane to the clearing
            Rect.MinMaxRect(-26.25f, 56f, -23.75f, FieldZ1),   // north lane
            Rect.MinMaxRect(-17f, 47f, FieldX1, 49.5f),        // east lane from the clearing
        };

        public static void Build(MapContext ctx)
        {
            HouseBuilder.Safe("corn field", () => CornField(ctx));
            HouseBuilder.Safe("barn", () => Barn(ctx));
            HouseBuilder.Safe("fuel depot", () => FuelDepot(ctx));
            HouseBuilder.Safe("silo", () => Silo(ctx));
            HouseBuilder.Safe("windmill", () => Windmill(ctx));
            HouseBuilder.Safe("water tower", () => WaterTower(ctx));
        }

        // ================================================================== corn field

        static bool InLaneOrClearing(float x, float z, float margin)
        {
            if (x > Clearing.xMin - margin && x < Clearing.xMax + margin && z > Clearing.yMin - margin && z < Clearing.yMax + margin) return true;
            foreach (var l in Lanes)
                if (x > l.xMin - margin && x < l.xMax + margin && z > l.yMin - margin && z < l.yMax + margin) return true;
            return false;
        }

        static void CornField(MapContext ctx)
        {
            const string A = "Field";
            var mb = ctx.NewBuilder("Field_Ground");
            ExteriorBuilder.Overlay(ctx, mb, FieldX0, FieldZ0, FieldX1, FieldZ1, 0.012f, Mat.Lit(Tex.Dirt, new Color(0.7f, 0.66f, 0.6f)), 2.5f, SurfaceType.Dirt, "FieldGround", 0.85f);
            var furrow = Mat.Decal("grime", new Color(1f, 1f, 1f, 0.5f));
            for (float x = FieldX0 + 1.1f; x < FieldX1; x += 2.2f)
                for (float z = FieldZ0 + 2f; z < FieldZ1; z += 4f)
                    if (!InLaneOrClearing(x, z, 0f)) Arch.FloorDecal(mb, furrow, V(x, 0.012f, z), 0.7f, 4.2f, 0f);
            var corn = Mat.Cutout(Tex.Cornstalks);
            var rng = ctx.Rng("corn");
            var fol = new[] { ctx.NewBuilder("Corn_W", Layers.Foliage), ctx.NewBuilder("Corn_E", Layers.Foliage) };
            int plants = 0;
            for (float x = FieldX0 + 0.5f; x < FieldX1 - 0.3f; x += 1.1f)
                for (float z = FieldZ0 + 0.4f; z < FieldZ1 - 0.3f; z += 0.95f)
                {
                    float px = x + rng.Range(-0.25f, 0.25f), pz = z + rng.Range(-0.3f, 0.3f);
                    float h = rng.Range(1.9f, 2.7f), rot = rng.Range(0f, 180f);
                    if (InLaneOrClearing(px, pz, 0.15f)) continue;
                    if (rng.Chance(0.06f)) continue;
                    Props.Billboard(fol[px < -20f ? 0 : 1], corn, V(px, 0, pz), 1.05f, h, 2, rot, 0.35f, 0.85f);
                    plants++;
                }
            ctx.BuildLog.Add("corn plants: " + plants);
            // tall grass on the field edges
            for (float x = FieldX0; x <= FieldX1; x += rng.Range(1.5f, 3.5f))
            {
                Props.GrassClump(fol[x < -20f ? 0 : 1], V(x, 0, FieldZ0 - rng.Range(0.3f, 1.2f)), rng.Range(0.9f, 1.5f), rng.Range(0f, 180f), (int)x);
                Props.GrassClump(fol[x < -20f ? 0 : 1], V(x, 0, FieldZ1 + rng.Range(0.3f, 1.2f)), rng.Range(0.9f, 1.5f), rng.Range(0f, 180f), (int)x + 1);
            }
            // a scarecrow in the clearing, an old wheelbarrow
            Scarecrow(ctx, mb, V(-27.5f, 0, 44.5f));
            mb.Material = Mat.Lit(Tex.MetalRusty);
            mb.Color = Shade.Gray(0.7f);
            mb.Push(V(-19.0f, 0, 54.6f), Quaternion.Euler(0, 35f, 6f));
            mb.AddBox(V(0, 0.45f, 0), V(0.7f, 0.35f, 1.0f), BoxUV.Local, 0.6f);
            mb.AddBeam(V(-0.25f, 0.45f, -0.5f), V(-0.3f, 0.6f, -1.3f), 0.04f);
            mb.AddBeam(V(0.25f, 0.45f, -0.5f), V(0.3f, 0.6f, -1.3f), 0.04f);
            mb.Material = Mat.Lit(Tex.CarTire);
            mb.Push(V(0, 0.22f, 0.55f), Quaternion.Euler(0, 0, 90f));
            mb.AddCylinder(V(0, -0.05f, 0), 0.22f, 0.22f, 0.1f, 8, true, true, null, true);
            mb.Pop();
            mb.Pop();
            mb.Color = Shade.Gray(1f);
            // the helicopter landing clearing
            var lz = MapMath.MinMax(V(Clearing.xMin, -1f, Clearing.yMin), V(Clearing.xMax, 6f, Clearing.yMax));
            ctx.Data.Radio.LandingZone = lz;
            ctx.Data.Radio.HelicopterPosition = V(Clearing.center.x, 18f, Clearing.center.y);
            ctx.Marker("LandingZone", V(Clearing.center.x, 0.1f, Clearing.center.y), Quaternion.identity);
            ctx.Anomaly("CornField", V(-20f, 1f, 42f), 15f, 0.25f);
            ctx.Area(A, V(FieldX0, -1f, FieldZ0), V(FieldX1, 6f, FieldZ1));
            // gameplay
            ctx.Key(A, V(-27.0f, 0.012f, 45.3f));
            ctx.Common(A, V(-41.5f, 0.012f, 30.6f));
            ctx.Common(A, V(2.0f, 0.012f, 48.3f));
            ctx.Key(A, V(-24.8f, 0.012f, 60.6f));
            ctx.Common(A, V(-19.6f, 0.012f, 51.8f));
            ctx.Tripwire(A, V(-10f, 0.12f, 29.0f), V(-10f, 0.12f, 31.5f));
            ctx.Tripwire(A, V(-26.25f, 0.12f, 34f), V(-23.75f, 0.12f, 34f));
            ctx.Tripwire(A, V(-5f, 0.12f, 47.0f), V(-5f, 0.12f, 49.5f));
            ctx.BearTrap(A, V(-35f, 0.012f, 30.4f));
            ctx.BearTrap(A, V(-25.1f, 0.012f, 26.0f));
            ctx.BearTrap(A, V(-12f, 0.012f, 48.2f));
            ctx.WallNote(A, V(-27.5f, 1.45f, 44.32f), Vector3.back);
            for (float x = -42f; x <= 3f; x += 5f) ctx.Nav.Add(V(x, 0, 30.25f), A);
            for (float z = 24f; z < 40f; z += 4f) ctx.Nav.Add(V(-25f, 0, z), A);
            ctx.Nav.Add(V(-30f, 0, 43.5f), A); ctx.Nav.Add(V(-20f, 0, 43.5f), A); ctx.Nav.Add(V(-25f, 0, 48f), A);
            ctx.Nav.Add(V(-30f, 0, 52.5f), A); ctx.Nav.Add(V(-20f, 0, 52.5f), A);
            ctx.Nav.Add(V(-25f, 0, 58.5f), A); ctx.Nav.Add(V(-25f, 0, 61.5f), A);
            for (float x = -15f; x <= 3f; x += 4.5f) ctx.Nav.Add(V(x, 0, 48.25f), A);
        }

        static void Scarecrow(MapContext ctx, MeshBuilder mb, Vector3 p)
        {
            mb.Material = Mat.Lit(Tex.WoodRaw, new Color(0.5f, 0.45f, 0.38f));
            mb.Color = Shade.Gray(0.7f);
            mb.AddBox(p + V(0, 1.2f, 0), V(0.1f, 2.4f, 0.1f), BoxUV.Local, 0.6f);
            mb.AddBox(p + V(0, 1.75f, 0), V(1.5f, 0.08f, 0.08f), BoxUV.Local, 0.6f);
            mb.Material = Mat.TwoSided(Tex.Cloth, new Color(0.45f, 0.3f, 0.25f));
            mb.AddBox(p + V(0, 1.45f, 0), V(0.55f, 0.7f, 0.28f), BoxUV.Local, 0.5f);
            mb.AddBox(p + V(0, 1.72f, 0), V(1.35f, 0.18f, 0.2f), BoxUV.Local, 0.5f);
            mb.Material = Mat.Lit(Tex.Cloth, new Color(0.75f, 0.7f, 0.55f));
            mb.AddSphere(p + V(0, 2.08f, 0), V(0.18f, 0.22f, 0.18f), 6, 4);
            mb.Material = Mat.Flat(0.05f, 0.04f, 0.04f);
            mb.AddBox(p + V(-0.06f, 2.12f, -0.17f), V(0.05f, 0.05f, 0.02f), BoxUV.Local, 1f);
            mb.AddBox(p + V(0.06f, 2.12f, -0.17f), V(0.05f, 0.05f, 0.02f), BoxUV.Local, 1f);
            Props.Billboard(mb, Mat.Cutout(Tex.GrassTall2, new Color(0.9f, 0.8f, 0.5f)), p + V(0, 0.8f, 0), 0.6f, 0.5f, 2, 0f, 0.6f, 0.9f);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(p + V(0, 1.2f, 0), V(0.25f, 2.4f, 0.3f), SurfaceType.Wood, "Scarecrow");
        }

        // ================================================================== barn

        static void Barn(MapContext ctx)
        {
            const string A = "Barn";
            var mb = ctx.NewBuilder("Barn");
            var red = Mat.Lit(Tex.BarnRed);
            var spec = new BuildingSpec
            {
                Name = "Barn", Area = A, X0 = 12f, Z0 = 52f, X1 = 30f, Z1 = 68f, WallH = 5.5f, T = 0.2f,
                Outside = WallSkin.Of(red, Mat.Lit(Tex.Concrete, new Color(0.5f, 0.5f, 0.48f)), 0.4f, null, 2.0f),
                Inside = WallSkin.Of(Mat.Lit(Tex.BarnRed, new Color(0.55f, 0.45f, 0.42f)), null, 0f, null, 2.0f),
                Floor = Mat.Lit(Tex.Dirt, new Color(0.75f, 0.68f, 0.58f)), Roof = Mat.Lit(Tex.TinRusty), Soffit = Mat.Lit(Tex.WoodRaw, new Color(0.4f, 0.35f, 0.3f)),
                Trim = Mat.Lit(Tex.WoodRaw, new Color(0.55f, 0.5f, 0.45f)), FloorSurf = SurfaceType.Dirt, WallSurf = SurfaceType.Wood,
                Gable = true, RidgeAlongX = false, RoofRise = 4.0f, Overhang = 0.6f, FloorTile = 2.5f,
            };
            spec.Inside.AoFloor = 0.35f; spec.Inside.AoCeil = 0.6f;
            spec.Openings.Add(('S', 21f, 5.0f, 4.2f));
            spec.Openings.Add(('N', 21f, 1.0f, 2.2f));
            Buildings.Build(ctx, mb, spec);
            var nd = Dyn.Door(ctx, new DoorSpec
            {
                Name = "BarnNorth", Kind = DoorKind.Boarded, Center = V(21f, 0.02f, 68f), AlongX = true, Swing = Vector3.back, RoomMin = 12f, RoomMax = 30f,
                WallT = 0.2f, AreaSwing = A, AreaOther = "FuelDepot", FrameMb = null,
            });
            // big sliding doors pushed open (static) + rail
            var plank = Mat.Lit(Tex.BarnRed, new Color(0.8f, 0.75f, 0.7f));
            mb.Material = plank;
            mb.Color = Shade.Gray(0.75f);
            mb.AddBox(V(16.9f, 2.25f, 51.75f), V(2.7f, 4.4f, 0.1f), BoxUV.Local, 2f, 1.5f);
            mb.AddBox(V(25.2f, 2.25f, 51.75f), V(2.7f, 4.4f, 0.1f), BoxUV.Local, 2f, 1.5f);
            mb.Material = Mat.Lit(Tex.WoodWhite, new Color(0.7f, 0.68f, 0.62f));
            mb.AddBeam(V(15.5f, 1.2f, 51.69f), V(18.3f, 3.3f, 51.69f), 0.12f);
            mb.AddBeam(V(23.8f, 3.3f, 51.69f), V(26.6f, 1.2f, 51.69f), 0.12f);
            mb.Material = Mat.Lit(Tex.MetalRusty);
            mb.AddBox(V(21f, 4.55f, 51.7f), V(14f, 0.1f, 0.08f), BoxUV.Local, 1f);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(V(16.9f, 2.25f, 51.75f), V(2.7f, 4.4f, 0.12f), SurfaceType.Wood, "BarnSlider");
            ctx.Solid(V(25.2f, 2.25f, 51.75f), V(2.7f, 4.4f, 0.12f), SurfaceType.Wood, "BarnSlider");
            // tie beams + chains
            var beam = Mat.Lit(Tex.WoodRaw, new Color(0.45f, 0.38f, 0.32f));
            mb.Material = beam;
            mb.Color = Shade.Gray(0.55f);
            for (float z = 54f; z < 67f; z += 3f) mb.AddBox(V(21f, 5.35f, z), V(17.6f, 0.22f, 0.22f), BoxUV.Local, 1f, 1.5f);
            mb.AddBox(V(21f, 5.6f, 60f), V(0.22f, 0.22f, 15.6f), BoxUV.Local, 1f, 1.5f);
            mb.Color = Shade.Gray(1f);
            Props.Chains(mb, V(19f, 5.24f, 57f), 2.3f);
            Props.Chains(mb, V(22f, 5.24f, 60f), 1.9f);
            Props.Chains(mb, V(18f, 5.24f, 63f), 2.5f);
            // stalls on the west side
            var stall = Mat.Lit(Tex.WoodRaw, new Color(0.55f, 0.5f, 0.42f));
            mb.Material = stall;
            mb.Color = Shade.Gray(0.65f);
            float[] wallsZ = { 55f, 58.5f, 62f, 65.5f };
            foreach (float z in wallsZ)
            {
                mb.AddBox(V(13.8f, 0.7f, z), V(3.4f, 1.4f, 0.1f), BoxUV.Local, 1f, 1.5f);
                ctx.Solid(V(13.8f, 0.7f, z), V(3.4f, 1.4f, 0.12f), SurfaceType.Wood, "Stall");
            }
            // stall fronts with gates openings (z of each opening = middle of the stall)
            float[] openings = { 56.75f, 60.25f, 63.75f };
            float cur = 55f;
            foreach (float oz in openings)
            {
                float a = oz - 0.6f;
                if (a - cur > 0.1f) { mb.AddBox(V(15.5f, 0.7f, (cur + a) * 0.5f), V(0.1f, 1.4f, a - cur), BoxUV.Local, 1f); ctx.Solid(V(15.5f, 0.7f, (cur + a) * 0.5f), V(0.12f, 1.4f, a - cur), SurfaceType.Wood, "StallFront"); }
                cur = oz + 0.6f;
            }
            mb.AddBox(V(15.5f, 0.7f, (cur + 65.5f) * 0.5f), V(0.1f, 1.4f, 65.5f - cur), BoxUV.Local, 1f);
            ctx.Solid(V(15.5f, 0.7f, (cur + 65.5f) * 0.5f), V(0.12f, 1.4f, 65.5f - cur), SurfaceType.Wood, "StallFront");
            mb.Color = Shade.Gray(1f);
            // hay, tractor wreck, tool locker, workbench
            Props.HayStack(ctx, mb, V(28.4f, 0.02f, 55.6f), 90f, 2101, 9);
            Props.HayStack(ctx, mb, V(28.4f, 0.02f, 59.6f), 90f, 2102, 6);
            Props.HayBale(ctx, mb, V(13.8f, 0.02f, 53.6f), 10f);
            Props.HayBale(ctx, mb, V(13.0f, 0.02f, 64.85f), 10f);
            var hayDecal = Mat.Cutout(Tex.GrassTall2, new Color(0.85f, 0.75f, 0.5f));
            var rng = ctx.Rng("barn");
            for (int i = 0; i < 26; i++)
                Props.Billboard(mb, hayDecal, V(rng.Range(12.5f, 29.5f), 0.02f, rng.Range(52.5f, 67.5f)), rng.Range(0.5f, 1.0f), rng.Range(0.15f, 0.3f), 2, rng.Range(0f, 180f), 0.5f, 0.8f);
            Props.TractorWreck(ctx, mb, V(25.2f, 0.02f, 63.2f), 8f);
            Dyn.Locker(ctx, "Barn.ToolLocker", V(27.6f, 0.02f, 67.65f), 0f, true);
            Props.Workbench(ctx, mb, V(17.0f, 0.02f, 67.55f), 0f, 2.0f, true, 2103);
            Props.Barrel(ctx, mb, V(19.0f, 0.02f, 66.9f), 0f, Tex.BarrelFuel, 0.6f);
            Props.Bones(mb, V(13.8f, 0.02f, 60.3f), 2104, 4, true);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), V(21.5f, 0.02f, 59.5f), 1.4f, 1.2f, 30f);
            // oil lantern hanging from a beam (not electric)
            mb.Material = Mat.Flat(0.08f, 0.08f, 0.08f);
            mb.AddBeam(V(20.5f, 5.24f, 60f), V(20.5f, 3.75f, 60f), 0.015f);
            mb.Material = Mat.Lit(Tex.MetalRusty);
            mb.AddBox(V(20.5f, 3.62f, 60f), V(0.16f, 0.25f, 0.16f), BoxUV.Local, 0.3f);
            ctx.Light(V(20.5f, 3.5f, 60f), new Color(1f, 0.62f, 0.3f), 0.8f, 6.5f, PsxFlicker.Candle, LightGroup.None, "Barn_Lantern", 0.25f, 6f);
            var lg = ctx.NewBuilder("Barn_LanternGlow");
            lg.Material = Mat.Glow(1f, 0.7f, 0.35f);
            lg.AddSphere(V(20.5f, 3.58f, 60f), V(0.04f, 0.06f, 0.04f), 5, 3);
            // gameplay
            ctx.Key(A, V(16.4f, 0.02f + Props.WorkbenchTop, 67.45f));
            ctx.Common(A, V(28.4f, 0.02f + Props.HayH * 2f, 55.0f));
            ctx.Common(A, V(13.4f, 0.02f, 56.9f));
            ctx.Key(A, V(23.3f, 0.02f, 66.6f));
            ctx.Common(A, V(19.0f, 0.02f + Props.BarrelTop, 66.9f));
            ctx.Key(A, V(13.4f, 0.02f, 60.4f) + V(0, 0, 0.9f));
            ctx.Tripwire(A, V(20.54f, 0.14f, 68.14f), V(21.46f, 0.14f, 68.14f));
            ctx.Tripwire(A, V(15.5f, 0.14f, 59.65f), V(15.5f, 0.14f, 60.85f));
            ctx.BearTrap(A, V(29.2f, 0.02f, 66.8f));
            ctx.WallNote(A, V(29.89f, 1.6f, 64.5f), Vector3.left);
            ctx.Ambient(AmbientType.Barn, V(12f, -0.2f, 52f), V(30f, 9.5f, 68f));
            ctx.Area(A, V(12f, -0.2f, 52f), V(30f, 9.5f, 68f));
            ctx.Marker("BarnDoor", V(21f, 1.6f, 51.5f), Quaternion.LookRotation(Vector3.forward));
            ctx.Nav.Add(V(21f, 0, 54.4f), A);
            ctx.Nav.Add(V(20.5f, 0, 57.8f), A);
            ctx.Nav.Add(V(18.5f, 0, 61.5f), A);
            ctx.Nav.Add(V(21f, 0, 65.2f), A);
            ctx.Nav.Add(V(17.3f, 0, 55.5f), A);
            ctx.Nav.Add(V(25.4f, 0, 57.4f), A);
            ctx.Nav.Add(V(21f, 0, 50.5f), "Yard");
            foreach (float oz in openings) { ctx.Nav.Add(V(13.9f, 0, oz), A); ctx.Nav.Add(V(16.7f, 0, oz), A); }
            ctx.Nav.Add(V(13.6f, 0, 66.7f), A);
            ctx.Nav.Add(V(17.0f, 0, 66.2f), A);
        }

        // ================================================================== fuel depot (FireInfo)

        static void FuelDepot(MapContext ctx)
        {
            const string A = "FuelDepot";
            var fire = ctx.Data.FuelDepot;
            var root = GeoUtil.CreateChild(ctx.Dynamic, "FuelDrums", V(21f, 0, 82.6f), Quaternion.identity, Layers.World);
            var mb = new MeshBuilder();
            // pallets + drums in local space of the root
            Props.Pallet(mb, V(-1.25f, 0, 0), 90f);
            Props.Pallet(mb, V(0.0f, 0, 0), 90f);
            Props.Pallet(mb, V(1.25f, 0, 0), 90f);
            var rng = ctx.Rng("fuel");
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 7; i++)
                    Props.Barrel(null, mb, V(-1.86f + i * 0.62f, Props.PalletTop, -0.33f + row * 0.64f), rng.Range(0f, 360f), Tex.BarrelFuel, rng.Range(0.75f, 1f), false);
            for (int i = 0; i < 3; i++)
                Props.Barrel(null, mb, V(-1.0f + i * 1.0f, Props.PalletTop + Props.BarrelTop, 0.0f), rng.Range(0f, 360f), Tex.BarrelFuel, 0.85f, false);
            Props.Barrel(null, mb, V(2.9f, 0, 0.6f), 30f, Tex.BarrelFuel, 0.7f, false);
            mb.Push(V(-2.9f, 0.29f, 0.2f), Quaternion.Euler(0, 20f, 90f));
            Props.Barrel(null, mb, V(0, -0.44f, 0), 0f, Tex.BarrelFuel, 0.65f, false);
            mb.Pop();
            mb.Build("Drums", root, Layers.World);
            ctx.CountRenderer(mb);
            GeoUtil.AddBox(root, V(0, 1.0f, 0.15f), V(4.6f, 2.0f, 1.5f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "DrumsCol");
            GeoUtil.AddBox(root, V(2.9f, 0.45f, 0.6f), V(0.6f, 0.9f, 0.6f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "DrumCol");
            GeoUtil.AddBox(root, V(-2.9f, 0.3f, 0.2f), V(0.9f, 0.6f, 0.6f), Quaternion.Euler(0, 20f, 0), Layers.World, SurfaceType.Metal, false, "DrumCol");
            fire.BarrelsRoot = root;
            fire.Barrels = GeoUtil.AddBox(root, V(0, 1.05f, 0.15f), V(5.0f, 2.1f, 1.9f), Quaternion.identity, Layers.Interactable, SurfaceType.Default, true, "Interact");
            fire.ExplosionCenter = V(21f, 1.0f, 82.7f);
            var smb = ctx.NewBuilder("FuelDepot");
            Arch.FloorDecal(smb, Mat.Decal("grime", new Color(0.6f, 0.5f, 0.4f, 1f)), V(21f, 0.01f, 82.6f), 7f, 3.5f, 0f);
            Props.SignPlate(smb, V(21f, 1.6f, 84.93f), Vector3.back, Tex.SignRestricted, 1.0f, 0.5f);
            Props.GasCanProp(smb, V(24.6f, 0, 80.2f), 40f);
            ctx.Marker("FuelDepot", fire.ExplosionCenter, Quaternion.identity);
            ctx.Key(A, V(24.9f, 0, 81.4f));
            ctx.Common(A, V(17.0f, 0, 81.0f));
            ctx.BearTrap(A, V(17.4f, 0, 78.9f));
            ctx.BearTrap(A, V(24.8f, 0, 78.4f));
            ctx.Area(A, V(16f, -1f, 76f), V(26f, 5f, 85f));
            ctx.Nav.Add(V(21f, 0, 77.2f), A);
            ctx.Nav.Add(V(16.8f, 0, 80.2f), A);
            ctx.Nav.Add(V(25.6f, 0, 80.6f), A);
        }

        // ================================================================== silo

        static void Silo(MapContext ctx)
        {
            const string A = "Silo";
            var mb = ctx.NewBuilder("Silo");
            var c = V(40f, 0, 58f);
            float r = 4f, h = 14f;
            int sides = 16, doorSide = 8;
            var metal = Mat.Lit(Tex.SiloMetal);
            var inner = Mat.Lit(Tex.SiloMetal, new Color(0.45f, 0.43f, 0.4f));
            float step = 360f / sides;
            Vector3 Dir(float deg) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad), 0, Mathf.Sin(deg * Mathf.Deg2Rad));
            int rows = 7;
            for (int k = 0; k < sides; k++)
            {
                float a0 = k * step - step * 0.5f, a1 = k * step + step * 0.5f;
                Vector3 p0 = c + Dir(a0) * r, p1 = c + Dir(a1) * r;
                if (k == doorSide)
                {
                    // flat door panel on the west side (both faces) with a 1.0 x 2.2 hole
                    float along0 = Mathf.Min(p0.z, p1.z), along1 = Mathf.Max(p0.z, p1.z);
                    float px = (p0.x + p1.x) * 0.5f;
                    var skin = WallSkin.Of(metal, null, 0f, null, 2f); skin.CornerAo = false;
                    var skinIn = WallSkin.Of(inner, null, 0f, null, 2f); skinIn.CornerAo = false;
                    var hole = new[] { new Hole(c.z - 0.5f, c.z + 0.5f, 0f, 2.2f) };
                    Arch.FaceN(mb, false, px, along0, along1, -1, 0f, h, hole, skin, 0f, h);
                    Arch.FaceN(mb, false, px + 0.04f, along0, along1, +1, 0.02f, h, hole, skinIn, 0.02f, h);
                    Arch.WallColliderN(ctx, false, px + 0.05f, along0, along1, 0f, h, 0.15f, hole, SurfaceType.Metal, "SiloWall");
                    continue;
                }
                for (int y = 0; y < rows; y++)
                {
                    float y0 = h * y / rows, y1 = h * (y + 1) / rows;
                    float u0 = k * 0.25f, u1 = (k + 1) * 0.25f;
                    float sh0 = y == 0 ? 0.55f : 0.8f, sh1 = 0.85f;
                    Vector3 n0 = Dir(a0), n1 = Dir(a1);
                    // outside: seen from outside, a1 is to the right? (angles grow counter-clockwise from above)
                    mb.Material = metal;
                    // seen from outside the larger angle (p1) is on the viewer's right
                    int i0 = mb.AddVertex(p0 + Vector3.up * y0, n0, new Vector2(u0, y0 / 3f), Shade.Gray(sh0));
                    int i1 = mb.AddVertex(p0 + Vector3.up * y1, n0, new Vector2(u0, y1 / 3f), Shade.Gray(sh1));
                    int i2 = mb.AddVertex(p1 + Vector3.up * y1, n1, new Vector2(u1, y1 / 3f), Shade.Gray(sh1));
                    int i3 = mb.AddVertex(p1 + Vector3.up * y0, n1, new Vector2(u1, y0 / 3f), Shade.Gray(sh0));
                    mb.AddTriangle(i0, i1, i2); mb.AddTriangle(i0, i2, i3);
                    mb.Material = inner;
                    Vector3 q0 = c + Dir(a0) * (r - 0.05f), q1 = c + Dir(a1) * (r - 0.05f);
                    int j0 = mb.AddVertex(q1 + Vector3.up * y0, -n1, new Vector2(u0, y0 / 3f), Shade.Gray(sh0 * 0.7f));
                    int j1 = mb.AddVertex(q1 + Vector3.up * y1, -n1, new Vector2(u0, y1 / 3f), Shade.Gray(sh1 * 0.7f));
                    int j2 = mb.AddVertex(q0 + Vector3.up * y1, -n0, new Vector2(u1, y1 / 3f), Shade.Gray(sh1 * 0.7f));
                    int j3 = mb.AddVertex(q0 + Vector3.up * y0, -n0, new Vector2(u1, y0 / 3f), Shade.Gray(sh0 * 0.7f));
                    mb.AddTriangle(j0, j1, j2); mb.AddTriangle(j0, j2, j3);
                }
                Vector3 mid = (p0 + p1) * 0.5f;
                ctx.Solid(mid + Vector3.up * (h * 0.5f), V(0.2f, h, Vector3.Distance(p0, p1) + 0.1f), Quaternion.LookRotation(p1 - p0, Vector3.up), SurfaceType.Metal, "SiloWall");
            }
            // galvanized bands + conical roof (outside + underside)
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.6f, 0.6f, 0.58f));
            mb.Color = Shade.Gray(0.7f);
            for (float y = 3f; y < h; y += 3.5f) mb.AddCylinder(c + Vector3.up * y, r + 0.07f, r + 0.07f, 0.14f, sides, false, false, null, true);
            mb.Material = Mat.Lit(Tex.TinRusty);
            mb.AddCylinder(c + Vector3.up * h, r + 0.2f, 0.25f, 2.4f, sides, true, false, new Rect(0, 0, 4, 1), true, 2);
            mb.Material = inner;
            mb.Color = Shade.Gray(0.35f);
            mb.AddCylinder(c + Vector3.up * (h - 0.03f), r, r, 0.01f, sides, false, true, null, false);
            mb.Color = Shade.Gray(1f);
            ExteriorBuilder.Overlay(ctx, mb, c.x - 4.6f, c.z - 4.6f, c.x + 4.6f, c.z + 4.6f, 0.02f, Mat.Lit(Tex.FloorConcrete, new Color(0.6f, 0.58f, 0.55f)), 1.6f, SurfaceType.Concrete, "SiloFloor", 0.7f);
            Dyn.Door(ctx, new DoorSpec
            {
                Name = "Silo", Kind = DoorKind.Silo, Center = V(c.x - r * Mathf.Cos(step * 0.5f * Mathf.Deg2Rad) + 0.05f, 0.02f, c.z), AlongX = false, Swing = Vector3.right,
                RoomMin = c.z - 4f, RoomMax = c.z + 4f, WallT = 0.12f, AreaSwing = A, AreaOther = "Yard", FrameMb = mb, FrameMat = Mat.Lit(Tex.MetalRusty),
            });
            // inside: grain mound, ladder, junk
            mb.Material = Mat.Lit(Tex.Hay, new Color(0.7f, 0.6f, 0.45f));
            mb.AddSphere(c + V(1.2f, 0.0f, 1.0f), V(1.8f, 1.1f, 1.6f), 8, 4);
            mb.Material = Mat.Lit(Tex.MetalRusty);
            mb.Color = Shade.Gray(0.65f);
            mb.AddBox(c + V(r - 0.3f, h * 0.5f, -0.25f), V(0.05f, h, 0.05f), BoxUV.Local, 0.5f);
            mb.AddBox(c + V(r - 0.3f, h * 0.5f, 0.25f), V(0.05f, h, 0.05f), BoxUV.Local, 0.5f);
            for (float y = 0.3f; y < h; y += 0.35f) mb.AddBeam(c + V(r - 0.3f, y, -0.25f), c + V(r - 0.3f, y, 0.25f), 0.03f);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(c + V(1.2f, 0.4f, 1.0f), V(2.6f, 0.8f, 2.3f), SurfaceType.Dirt, "Grain");
            Props.Crate(ctx, mb, c + V(2.6f, 0.02f, -1.8f), 20f, 0.6f);
            Props.Barrel(ctx, mb, c + V(-1.0f, 0.02f, -2.6f), 0f, Tex.BarrelWater, 0.4f);
            Props.Chains(mb, c + V(-0.5f, 6f, 0.5f), 3.5f);
            Props.Bones(mb, c + V(-1.5f, 0.02f, 1.8f), 2201, 4, true);
            ctx.Common(A, c + V(2.6f, 0.62f, -1.8f));
            ctx.Key(A, c + V(-1.6f, 0.02f, -1.2f));
            ctx.Common(A, c + V(-1.0f, 0.02f + Props.BarrelTop, -2.6f));
            ctx.WallNote(A, c + V(0.6f, 1.5f, -r + 0.12f), Vector3.forward);
            ctx.Tripwire(A, V(c.x - r * Mathf.Cos(step * 0.5f * Mathf.Deg2Rad) - 0.12f, 0.14f, c.z - 0.48f), V(c.x - r * Mathf.Cos(step * 0.5f * Mathf.Deg2Rad) - 0.12f, 0.14f, c.z + 0.48f));
            ctx.Ambient(AmbientType.Silo, c + V(-4f, -0.2f, -4f), c + V(4f, h, 4f));
            ctx.Area(A, c + V(-4f, -0.2f, -4f), c + V(4f, h, 4f));
            ctx.Nav.Add(c + V(-1.4f, 0, 0f), A);
            ctx.Nav.Add(c + V(0.6f, 0, -1.8f), A);
            ctx.Marker("Silo", c + V(0, 2f, 0), Quaternion.identity);
        }

        // ================================================================== windmill (rotating blades + creak)

        static void Windmill(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Windmill");
            var p = V(-8f, 0, 74f);
            float H = 12f, b = 1.6f, t = 0.45f;
            var steel = Mat.Lit(Tex.Galvanized, new Color(0.6f, 0.6f, 0.58f));
            mb.Material = steel;
            mb.Color = Shade.Gray(0.7f);
            Vector3 Leg(int sx, int sz, float y) { float f = y / H; float w = Mathf.Lerp(b, t, f); return p + V(sx * w, y, sz * w); }
            int[] sxs = { -1, 1, 1, -1 }, szs = { -1, -1, 1, 1 };
            for (int i = 0; i < 4; i++)
            {
                mb.AddBeam(Leg(sxs[i], szs[i], 0f), Leg(sxs[i], szs[i], H), 0.09f);
                var a0 = Leg(sxs[i], szs[i], 0f); var a1 = Leg(sxs[i], szs[i], H);
                var d = a1 - a0;
                ctx.Solid((a0 + a1) * 0.5f, V(0.14f, d.magnitude, 0.14f), Quaternion.FromToRotation(Vector3.up, d.normalized), SurfaceType.Metal, "WindmillLeg");
            }
            for (float y = 0.3f; y < H; y += 2.4f)
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    mb.AddBeam(Leg(sxs[i], szs[i], y), Leg(sxs[j], szs[j], y), 0.05f);
                    float y2 = Mathf.Min(H, y + 2.4f);
                    mb.AddBeam(Leg(sxs[i], szs[i], y), Leg(sxs[j], szs[j], y2), 0.035f);
                }
            mb.Material = Mat.Lit(Tex.WoodRaw, new Color(0.45f, 0.4f, 0.35f));
            mb.AddBox(p + V(0, H + 0.05f, 0), V(1.3f, 0.1f, 1.3f), BoxUV.Local, 0.8f);
            mb.Material = Mat.Lit(Tex.MetalRusty);
            mb.AddBox(p + V(0, H + 0.45f, 0.1f), V(0.45f, 0.5f, 1.1f), BoxUV.Local, 0.5f);
            // tail vane pointing north
            mb.AddBeam(p + V(0, H + 0.5f, 0.6f), p + V(0, H + 0.7f, 2.6f), 0.06f);
            mb.Material = Mat.TwoSided(Tex.MetalRusty, new Color(0.7f, 0.5f, 0.4f));
            mb.AddQuad(p + V(0, H + 0.2f, 2.0f), p + V(0, H + 1.3f, 2.0f), p + V(0, H + 1.3f, 3.2f), p + V(0, H + 0.2f, 3.2f));
            // pump rod + water trough at the base
            mb.Material = steel;
            mb.AddBeam(p + V(0, 0.5f, 0), p + V(0, H, 0), 0.04f);
            mb.Material = Mat.Lit(Tex.MetalRusty, new Color(0.6f, 0.55f, 0.5f));
            mb.AddBox(p + V(2.6f, 0.3f, 0), V(0.8f, 0.6f, 2.2f), BoxUV.Local, 0.6f);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(p + V(2.6f, 0.3f, 0), V(0.8f, 0.6f, 2.2f), SurfaceType.Metal, "Trough");
            // rotor: separate object turning around its local Z (facing south)
            var hub = p + V(0, H + 0.45f, -0.55f);
            var rotor = GeoUtil.CreateChild(ctx.Dynamic, "WindmillRotor", hub, Quaternion.LookRotation(Vector3.back, Vector3.up), Layers.World);
            var rmb = new MeshBuilder();
            rmb.Color = Shade.Gray(0.75f);
            rmb.Material = Mat.TwoSided(Tex.Galvanized, new Color(0.65f, 0.62f, 0.58f));
            int blades = 16;
            for (int i = 0; i < blades; i++)
            {
                float a = i * Mathf.PI * 2f / blades;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0), perp = new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0);
                Vector3 r0 = dir * 0.45f, r1 = dir * 2.5f;
                Vector3 tw = Vector3.forward * 0.08f;
                rmb.AddQuad(r0 - perp * 0.1f - tw, r1 - perp * 0.2f - tw, r1 + perp * 0.2f + tw, r0 + perp * 0.1f + tw);
            }
            rmb.Material = Mat.Lit(Tex.MetalRusty);
            rmb.AddCylinder(V(0, 0, -0.1f), 0.25f, 0.25f, 0.2f, 8, true, true, null, true);
            for (int i = 0; i < 8; i++)
            {
                float a0 = i * Mathf.PI / 4f, a1 = (i + 1) * Mathf.PI / 4f;
                rmb.AddBeam(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 2.45f, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 2.45f, 0.04f);
                rmb.AddBeam(new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 1.4f, new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 1.4f, 0.03f);
            }
            rmb.Build("Blades", rotor, Layers.World);
            ctx.CountRenderer(rmb);
            var wr = rotor.gameObject.AddComponent<WindmillRotor>();
            wr.Speed = 26f;
            wr.Phase = (ctx.Seed & 255) * 0.1f;
            ctx.Emitter(hub, "Audio/Ambience/windmill_creak_loop", 0.6f, 32f, false);
            ctx.Marker("Windmill", hub, Quaternion.LookRotation(Vector3.back));
            ctx.Common("Yard", p + V(2.6f, 0.6f, 0.5f));
            ctx.Key("Yard", p + V(-0.6f, 0, -0.7f));
            ctx.BearTrap("Yard", p + V(-2.6f, 0, 1.8f));
            ctx.Area("Windmill", p + V(-3f, -1f, -3f), p + V(4f, 16f, 4f));
        }

        // ================================================================== red water tower (D7qZKJF right side)

        static void WaterTower(MapContext ctx)
        {
            const string A = "WaterTower";
            var mb = ctx.NewBuilder("WaterTower");
            var c = V(38f, 0, 18f);
            float H = 12f;
            var legMat = Mat.Lit(Tex.MetalRusty, new Color(0.85f, 0.45f, 0.35f));
            mb.Material = legMat;
            mb.Color = Shade.Gray(0.7f);
            Vector3 L(int i, float y) { float f = y / H; float w = Mathf.Lerp(3f, 2.2f, f); int sx = (i == 0 || i == 3) ? -1 : 1, sz = i < 2 ? -1 : 1; return c + V(sx * w, y, sz * w); }
            for (int i = 0; i < 4; i++)
            {
                var a0 = L(i, 0f); var a1 = L(i, H);
                mb.AddBeam(a0, a1, 0.24f);
                var d = a1 - a0;
                ctx.Solid((a0 + a1) * 0.5f, V(0.3f, d.magnitude, 0.3f), Quaternion.FromToRotation(Vector3.up, d.normalized), SurfaceType.Metal, "TowerLeg");
                mb.Material = Mat.Lit(Tex.Concrete);
                mb.AddBox(a0 + V(0, 0.15f, 0), V(0.6f, 0.3f, 0.6f), BoxUV.Local, 0.6f);
                mb.Material = legMat;
            }
            for (float y = 4f; y < H; y += 4f)
                for (int i = 0; i < 4; i++)
                {
                    int j = (i + 1) % 4;
                    mb.AddBeam(L(i, y), L(j, y), 0.1f);
                    mb.AddBeam(L(i, y - 4f + 0.3f), L(j, y), 0.05f);
                    mb.AddBeam(L(j, y - 4f + 0.3f), L(i, y), 0.05f);
                }
            // tank
            mb.Material = Mat.Lit(Tex.WaterTowerTank);
            mb.Color = Shade.Gray(0.8f);
            mb.AddCylinder(c + Vector3.up * H, 3.2f, 3.2f, 4.5f, 16, false, false, new Rect(0, 0, 4, 1.5f), true, 3);
            mb.AddCylinder(c + Vector3.up * (H - 0.7f), 0.7f, 3.2f, 0.7f, 16, false, false, new Rect(0, 0, 4, 0.3f), true, 1);
            mb.AddCylinder(c + Vector3.up * (H + 4.5f), 3.35f, 0.2f, 1.7f, 16, true, false, new Rect(0, 0, 4, 0.6f), true, 1);
            // catwalk + railing
            mb.Material = legMat;
            mb.Color = Shade.Gray(0.6f);
            mb.AddCylinder(c + Vector3.up * (H - 0.05f), 3.9f, 3.9f, 0.08f, 16, true, true, null, false);
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI * 2f / 16f;
                var q = c + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 3.85f + Vector3.up * H;
                mb.AddBeam(q, q + Vector3.up * 1.0f, 0.04f);
            }
            mb.AddCylinder(c + Vector3.up * (H + 0.95f), 3.85f, 3.85f, 0.06f, 16, false, false, null, true);
            // ladder up the south-west leg
            mb.Material = Mat.Lit(Tex.Galvanized);
            var l0 = L(0, 0f) + V(0.35f, 0, -0.1f); var l1 = L(0, H) + V(0.35f, 0, -0.1f);
            mb.AddBeam(l0 + V(-0.2f, 0, 0), l1 + V(-0.2f, 0, 0), 0.04f);
            mb.AddBeam(l0 + V(0.2f, 0, 0), l1 + V(0.2f, 0, 0), 0.04f);
            for (float f = 0.03f; f < 1f; f += 0.025f) mb.AddBeam(Vector3.Lerp(l0, l1, f) + V(-0.2f, 0, 0), Vector3.Lerp(l0, l1, f) + V(0.2f, 0, 0), 0.025f);
            mb.Color = Shade.Gray(1f);
            Arch.FloorDecal(mb, Mat.Decal("water_stain"), c + V(0, 0.01f, 0), 5f, 5f, 0f);
            Props.Crate(ctx, mb, c + V(-2.0f, 0, -2.6f), 15f, 0.6f);
            Props.Barrel(ctx, mb, c + V(1.6f, 0, 2.5f), 0f, Tex.BarrelWater, 0.4f);
            ctx.Key(A, c + V(-2.0f, 0.6f, -2.6f));
            ctx.Common(A, c + V(2.5f, 0, -1.2f));
            ctx.Common(A, c + V(1.6f, Props.BarrelTop, 2.5f));
            ctx.BearTrap(A, c + V(-2.5f, 0, 2.2f));
            ctx.WallNote(A, L(1, 1.5f) + V(-0.13f, 0, 0), Vector3.left);
            ctx.Anomaly("WaterTower", c + V(0, 6f, 0), 12f, 0.5f);
            ctx.Area(A, c + V(-5f, -1f, -5f), c + V(5f, 18f, 5f));
            ctx.Marker("WaterTower", c + V(0, 14f, 0), Quaternion.identity);
            ctx.Nav.Add(c + V(0, 0, 0), A);
        }
    }
}
