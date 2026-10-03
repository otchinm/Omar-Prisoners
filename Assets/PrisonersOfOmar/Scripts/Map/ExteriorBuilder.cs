using System;
using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>Everything outside the house except the north farm: ground, fence/gates, forest, yard, shed, road, parking lot, restroom.</summary>
    internal static class ExteriorBuilder
    {
        public const float FenceX0 = -75f, FenceX1 = 50f, FenceZ0 = -45f, FenceZ1 = 85f, FenceH = 2.6f;
        public const float RoadZ0 = -62.5f, RoadZ1 = -55.5f;
        public const float BoundX0 = -110f, BoundX1 = 95f, BoundZ0 = -85f, BoundZ1 = 125f;
        const string GlowExt = HouseBuilder.GlowExterior;
        static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

        public static void Build(MapContext ctx)
        {
            HouseBuilder.Safe("ground", () => Ground(ctx));
            HouseBuilder.Safe("overlays", () => Overlays(ctx));
            HouseBuilder.Safe("fence", () => Fence(ctx));
            HouseBuilder.Safe("forest", () => Forest(ctx));
            HouseBuilder.Safe("treeline", () => Treeline(ctx));
            HouseBuilder.Safe("boundary", () => Boundary(ctx));
            HouseBuilder.Safe("shed", () => Shed(ctx));
            HouseBuilder.Safe("restroom", () => Restroom(ctx));
            HouseBuilder.Safe("parking lot", () => ParkingLot(ctx));
            HouseBuilder.Safe("yard", () => Yard(ctx));
            HouseBuilder.Safe("outside gate", () => OutsideGate(ctx));
        }

        // ================================================================== ground

        static float[] Lines(float min, float max, float step, params float[] extra)
        {
            var l = new List<float>();
            for (float v = min; v <= max + 0.001f; v += step) l.Add(v);
            foreach (var e in extra) if (e > min && e < max) l.Add(e);
            l.Sort();
            var o = new List<float>();
            foreach (var v in l) if (o.Count == 0 || v - o[o.Count - 1] > 0.05f) o.Add(v);
            return o.ToArray();
        }

        /// <summary>Shared-vertex ground grid; cellMat returns null to skip a cell.</summary>
        static void Grid(MeshBuilder mb, float[] xs, float[] zs, float y, float tile, Func<float, float, Material> cellMat, Func<float, float, float> shade)
        {
            int nx = xs.Length, nz = zs.Length;
            var idx = new int[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = xs[i], z = zs[j];
                    idx[j * nx + i] = mb.AddVertex(new Vector3(x, y, z), Vector3.up, new Vector2(x / tile, z / tile), Shade.Gray(shade(x, z)));
                }
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    var m = cellMat((xs[i] + xs[i + 1]) * 0.5f, (zs[j] + zs[j + 1]) * 0.5f);
                    if (m == null) continue;
                    mb.Material = m;
                    int a = idx[j * nx + i], b = idx[(j + 1) * nx + i], c = idx[(j + 1) * nx + i + 1], d = idx[j * nx + i + 1];
                    mb.AddTriangle(a, b, c);
                    mb.AddTriangle(a, c, d);
                }
        }

        static bool InHouse(float x, float z) => x > -11.1f && x < 11.1f && z > -8.1f && z < 8.1f;
        static bool InYard(float x, float z) => x > -25f && x < 30f && z > -45f && z < 25f;

        static void Ground(MapContext ctx)
        {
            var grass = Mat.Lit(Tex.GrassDead);
            var dirt = Mat.Lit(Tex.Dirt);
            var fine = ctx.NewBuilder("Ground_Fine");
            var xs = Lines(-82f, 58f, 2f, -11.1f, 11.1f, -25f, 30f);
            var zs = Lines(-68f, 92f, 2f, -8.1f, 8.1f, -45f, 25f);
            Grid(fine, xs, zs, 0f, 3f, (x, z) => InHouse(x, z) ? null : (InYard(x, z) ? dirt : grass), (x, z) =>
            {
                float d = Mathf.Max(Mathf.Abs(x) - 11.1f, Mathf.Abs(z) - 8.1f);
                float f = 0.72f + 0.28f * Shade.Hash(Mathf.Round(x / 2f), 0, Mathf.Round(z / 2f), 41);
                if (d < 2.5f) f *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(d / 2.5f));
                return f;
            });
            var coarse = ctx.NewBuilder("Ground_Far");
            var cx = Lines(-200f, 180f, 10f, -82f, 58f);
            var cz = Lines(-180f, 210f, 10f, -68f, 92f);
            Grid(coarse, cx, cz, -0.02f, 4f, (x, z) => (x > -82f && x < 58f && z > -68f && z < 92f) ? null : grass,
                (x, z) => 0.55f + 0.3f * Shade.Hash(Mathf.Round(x / 10f), 1, Mathf.Round(z / 10f), 43));
            // colliders: dirt yard (minus the house = basement air space), grass everywhere else
            var house = new List<Rect> { Rect.MinMaxRect(-11.1f, -8.1f, 11.1f, 8.1f) };
            Arch.SlabCollider(ctx, Rect.MinMaxRect(-25f, -45f, 30f, 25f), 0f, 0.2f, house, SurfaceType.Dirt, "Ground_Dirt");
            Arch.SlabCollider(ctx, Rect.MinMaxRect(BoundX0, BoundZ0, BoundX1, BoundZ1), 0f, 0.2f, new List<Rect> { Rect.MinMaxRect(-25f, -45f, 30f, 25f) }, SurfaceType.Grass, "Ground_Grass");
        }

        /// <summary>Flat surface strip slightly above the ground with its own collider (footstep surface).</summary>
        public static void Overlay(MapContext ctx, MeshBuilder mb, float x0, float z0, float x1, float z1, float y, Material m, float tile, SurfaceType s, string name, float ao = 0.8f)
        {
            Arch.Flat(mb, x0, z0, x1, z1, y, m, tile, null, true, Color.white, ao, 0.8f, 0.2f, 2.0f);
            Arch.SlabCollider(ctx, Rect.MinMaxRect(x0, z0, x1, z1), y, 0.12f, null, s, name);
        }

        /// <summary>Straight strip from a to b (XZ) of the given width (rotated), with collider.</summary>
        public static void Strip(MapContext ctx, MeshBuilder mb, Vector3 a, Vector3 b, float width, float y, Material m, float tile, SurfaceType s, string name)
        {
            Vector3 d = b - a; d.y = 0;
            float L = d.magnitude;
            if (L < 0.01f) return;
            Vector3 dir = d / L, side = new Vector3(dir.z, 0, -dir.x) * (width * 0.5f);
            int n = Mathf.Max(1, Mathf.CeilToInt(L / 2f));
            mb.Material = m;
            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = a + dir * (L * i / n), p1 = a + dir * (L * (i + 1) / n);
                p0.y = p1.y = y;
                Vector3 q0 = p0 - side, q1 = p1 - side, q2 = p1 + side, q3 = p0 + side;
                float f0 = 0.75f + 0.2f * Shade.Hash(p0, 47), f1 = 0.75f + 0.2f * Shade.Hash(p1, 47);
                int i0 = mb.AddVertex(q0, Vector3.up, new Vector2(q0.x / tile, q0.z / tile), Shade.Gray(f0 * 0.85f));
                int i1 = mb.AddVertex(q1, Vector3.up, new Vector2(q1.x / tile, q1.z / tile), Shade.Gray(f1 * 0.85f));
                int i2 = mb.AddVertex(q2, Vector3.up, new Vector2(q2.x / tile, q2.z / tile), Shade.Gray(f1));
                int i3 = mb.AddVertex(q3, Vector3.up, new Vector2(q3.x / tile, q3.z / tile), Shade.Gray(f0));
                // winding: viewed from above, q0 -> q1 -> q2 must be clockwise
                if (Vector3.Cross(q1 - q0, q2 - q0).y > 0) { mb.AddTriangle(i0, i1, i2); mb.AddTriangle(i0, i2, i3); }
                else { mb.AddTriangle(i0, i2, i1); mb.AddTriangle(i0, i3, i2); }
            }
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            ctx.Solid((a + b) * 0.5f + Vector3.up * (y - 0.06f - (a.y + b.y) * 0.5f), new Vector3(width, 0.12f, L), rot, s, name);
        }

        static void Overlays(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Ground_Overlays");
            var gravel = Mat.Lit(Tex.Gravel);
            var asphalt = Mat.Lit(Tex.Asphalt);
            var road = Mat.Lit(Tex.AsphaltCracked);
            var dirt = Mat.Lit(Tex.Dirt, new Color(0.85f, 0.8f, 0.75f));
            // driveway: porch steps -> main gate -> road
            Overlay(ctx, mb, -2f, RoadZ1, 2f, -12.25f, 0.02f, gravel, 2.5f, SurfaceType.Gravel, "Driveway");
            Overlay(ctx, mb, -5f, -16f, -2f, -12.25f, 0.02f, gravel, 2.5f, SurfaceType.Gravel, "DrivewayPad");
            // the road (east-west), wider than the playable area
            Overlay(ctx, mb, -150f, RoadZ0, 150f, RoadZ1, 0.03f, road, 3.5f, SurfaceType.Asphalt, "Road", 0.65f);
            var dash = Mat.Decal("road_dashes");
            for (float x = -96f; x <= 96f; x += 6f)
                Arch.FloorDecal(mb, dash, V(x, 0.03f, -59f), 0.18f, 2.6f, 90f);
            var edge = Mat.Decal("road_dashes", new Color(0.8f, 0.8f, 0.75f, 0.7f));
            for (float x = -96f; x <= 96f; x += 2.9f)
            {
                Arch.FloorDecal(mb, edge, V(x, 0.03f, RoadZ0 + 0.35f), 0.1f, 2.95f, 90f);
                Arch.FloorDecal(mb, edge, V(x, 0.03f, RoadZ1 - 0.35f), 0.1f, 2.95f, 90f);
            }
            // parking lot + vehicle lane to the vehicle gate + walkway to the restroom
            Overlay(ctx, mb, -70f, -32f, -40f, -2f, 0.025f, asphalt, 3f, SurfaceType.Asphalt, "ParkingLot", 0.7f);
            Overlay(ctx, mb, -58.5f, RoadZ1, -53.5f, -32f, 0.022f, gravel, 2.5f, SurfaceType.Gravel, "VehicleLane");
            Overlay(ctx, mb, -63.2f, -2f, -60.8f, 2.0f, 0.022f, Mat.Lit(Tex.Concrete), 1.5f, SurfaceType.Concrete, "RestroomWalk");
            // dirt path: back door -> barn
            var path = new[] { V(0, 0, 10.8f), V(3f, 0, 18f), V(8f, 0, 26f), V(13f, 0, 37f), V(18f, 0, 46f), V(21f, 0, 52.2f) };
            for (int i = 0; i + 1 < path.Length; i++) Strip(ctx, mb, path[i], path[i + 1], 2.6f, 0.015f, dirt, 2.5f, SurfaceType.Dirt, "BarnPath");
            // track from the barn to the silo and to the water tower (worn grass)
            Strip(ctx, mb, V(30f, 0, 56f), V(36f, 0, 58f), 2.2f, 0.015f, dirt, 2.5f, SurfaceType.Dirt, "SiloPath");
            Strip(ctx, mb, V(13f, 0, 10f), V(34f, 0, 17f), 2.2f, 0.015f, dirt, 2.5f, SurfaceType.Dirt, "TowerPath");
            ctx.Area("Road", V(-150f, -1f, RoadZ0), V(150f, 6f, RoadZ1));
            ctx.Area("OutsideGate", V(-10f, -1f, RoadZ1), V(10f, 6f, FenceZ0));
        }

        // ================================================================== fence

        /// <summary>Chain-link fence run with posts every ≤3 m, top rail, 3 barbed wire strands and one solid collider.</summary>
        public static void FenceRun(MapContext ctx, MeshBuilder mb, Vector3 a, Vector3 b, bool postA, bool postB, int colliderLayer, Transform colliderParent, List<Collider> outColliders)
        {
            Vector3 d = b - a; d.y = 0;
            float L = d.magnitude;
            if (L < 0.05f) return;
            Vector3 dir = d / L;
            int n = Mathf.Max(1, Mathf.CeilToInt(L / 3f));
            var post = Mat.Lit(Tex.Galvanized, new Color(0.7f, 0.7f, 0.68f));
            var link = Mat.Cutout(Tex.Chainlink, new Color(0.8f, 0.8f, 0.78f));
            var barb = Mat.Cutout(Tex.BarbedWire, new Color(0.6f, 0.6f, 0.58f));
            Vector3 side = new Vector3(-dir.z, 0, dir.x);
            for (int k = 0; k <= n; k++)
            {
                if ((k == 0 && !postA) || (k == n && !postB)) continue;
                Vector3 p = a + dir * (L * k / n);
                mb.Material = post;
                mb.Color = Shade.Gray(0.75f);
                mb.AddBox(p + Vector3.up * 1.55f, new Vector3(0.07f, 3.1f, 0.07f), BoxUV.Local, 0.5f, 1.5f, BoxFaces.NoBottom);
            }
            for (int k = 0; k < n; k++)
            {
                Vector3 p0 = a + dir * (L * k / n), p1 = a + dir * (L * (k + 1) / n);
                float u0 = L * k / n, u1 = L * (k + 1) / n;
                mb.Material = link;
                var cb = Shade.Gray(0.55f); var ct = Shade.Gray(0.95f);
                for (int r = 0; r < 2; r++)
                {
                    float y0 = 0.03f + (FenceH - 0.03f) * r / 2f, y1 = 0.03f + (FenceH - 0.03f) * (r + 1) / 2f;
                    var c0 = r == 0 ? cb : Shade.Gray(0.8f); var c1 = r == 0 ? Shade.Gray(0.8f) : ct;
                    int i0 = mb.AddVertex(p0 + Vector3.up * y0, side, new Vector2(u0, y0), c0);
                    int i1 = mb.AddVertex(p0 + Vector3.up * y1, side, new Vector2(u0, y1), c1);
                    int i2 = mb.AddVertex(p1 + Vector3.up * y1, side, new Vector2(u1, y1), c1);
                    int i3 = mb.AddVertex(p1 + Vector3.up * y0, side, new Vector2(u1, y0), c0);
                    mb.AddTriangle(i0, i1, i2); mb.AddTriangle(i0, i2, i3);
                }
                mb.Material = barb;
                mb.Color = Shade.Gray(0.8f);
                for (int s = 0; s < 3; s++)
                {
                    float y = 2.72f + s * 0.14f;
                    var off = side * (0.04f + s * 0.05f);
                    mb.AddQuad(p0 + off + Vector3.up * (y - 0.04f), p0 + off + Vector3.up * (y + 0.04f), p1 + off + Vector3.up * (y + 0.04f), p1 + off + Vector3.up * (y - 0.04f),
                        new Rect(u0 / 0.5f, 0, (u1 - u0) / 0.5f, 1));
                }
            }
            mb.Material = post;
            mb.Color = Shade.Gray(0.7f);
            mb.AddBeam(a + Vector3.up * FenceH, b + Vector3.up * FenceH, 0.045f);
            mb.AddBeam(a + Vector3.up * 0.15f, b + Vector3.up * 0.15f, 0.03f);
            mb.Color = Shade.Gray(1f);
            var c = (a + b) * 0.5f + Vector3.up * 1.6f;
            var bc = GeoUtil.AddBox(colliderParent != null ? colliderParent : ctx.Colliders, Vector3.zero, new Vector3(0.16f, 3.2f, L), Quaternion.identity, colliderLayer, SurfaceType.Metal, false, "Fence");
            bc.transform.position = c;
            bc.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            outColliders?.Add(bc);
        }

        static void GatePost(MapContext ctx, MeshBuilder mb, Vector3 p)
        {
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.6f, 0.6f, 0.58f));
            mb.Color = Shade.Gray(0.7f);
            mb.AddBox(p + Vector3.up * 1.5f, new Vector3(0.16f, 3.0f, 0.16f), BoxUV.Local, 0.5f, 1.5f, BoxFaces.NoBottom);
            mb.AddBox(p + Vector3.up * 3.04f, new Vector3(0.2f, 0.08f, 0.2f), BoxUV.Local, 0.5f);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(p + Vector3.up * 1.5f, new Vector3(0.2f, 3.0f, 0.2f), SurfaceType.Metal, "GatePost");
        }

        static void Fence(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Fence");
            int W = Layers.World;
            // south (gates at x[-60,-52] and x[-3,3])
            FenceRun(ctx, mb, V(FenceX0, 0, FenceZ0), V(-60.1f, 0, FenceZ0), true, false, W, null, null);
            FenceRun(ctx, mb, V(-51.9f, 0, FenceZ0), V(-3.1f, 0, FenceZ0), false, false, W, null, null);
            FenceRun(ctx, mb, V(3.1f, 0, FenceZ0), V(FenceX1, 0, FenceZ0), false, true, W, null, null);
            // east, west
            FenceRun(ctx, mb, V(FenceX1, 0, FenceZ0), V(FenceX1, 0, FenceZ1), false, true, W, null, null);
            FenceRun(ctx, mb, V(FenceX0, 0, FenceZ1), V(FenceX0, 0, FenceZ0), true, false, W, null, null);
            // north, with the breach section as a separate object
            FenceRun(ctx, mb, V(FenceX0, 0, FenceZ1), V(15f, 0, FenceZ1), false, true, W, null, null);
            FenceRun(ctx, mb, V(27f, 0, FenceZ1), V(FenceX1, 0, FenceZ1), true, false, W, null, null);
            var breachGo = GeoUtil.CreateChild(ctx.Dynamic, "BreachFence", Vector3.zero, Quaternion.identity, Layers.World).gameObject;
            var bmb = new MeshBuilder();
            var blockers = new List<Collider>();
            FenceRun(ctx, bmb, V(15f, 0, FenceZ1), V(27f, 0, FenceZ1), false, false, W, breachGo.transform, blockers);
            bmb.Build("Mesh", breachGo.transform, Layers.World);
            ctx.CountRenderer(bmb);
            ctx.Data.FuelDepot.BreachFence = breachGo;
            ctx.Data.FuelDepot.BreachBlockers = blockers.ToArray();
            ctx.Data.FuelDepot.BreachExitZone = MapMath.MinMax(V(14f, -1f, FenceZ1 + 0.6f), V(28f, 5f, FenceZ1 + 7f));
            ctx.Area("Breach", V(14f, -1f, FenceZ1 - 0.5f), V(28f, 5f, FenceZ1 + 7f));
            ctx.Nav.GateLine(new Vector2(15f, FenceZ1), new Vector2(27f, FenceZ1), NavGraph.EdgeBreach);

            // main gate (double leaf, chained + padlocked)
            GatePost(ctx, mb, V(-3.1f, 0, FenceZ0));
            GatePost(ctx, mb, V(3.1f, 0, FenceZ0));
            var gate = ctx.Data.MainGate;
            gate.LeftLeaf = Dyn.GateLeaf(ctx, "MainGate_Left", V(-3.0f, 0, FenceZ0), 0f, 3.0f, 2.45f, out var lc);
            gate.RightLeaf = Dyn.GateLeaf(ctx, "MainGate_Right", V(3.0f, 0, FenceZ0), 180f, 3.0f, 2.45f, out var rc);
            gate.LeftOpenAngle = OpenTowards(gate.LeftLeaf, Vector3.forward, 100f);
            gate.RightOpenAngle = OpenTowards(gate.RightLeaf, Vector3.forward, 100f);
            gate.Blockers = new Collider[] { lc, rc };
            var padlock = Dyn.ChainAndPadlock(ctx, "MainGate_Padlock", V(0, 0, FenceZ0), 0.16f, true);
            gate.Padlock = padlock;
            gate.Interact = GeoUtil.AddBox(padlock.transform, V(0, 1.02f, 0), V(0.5f, 0.55f, 0.5f), Quaternion.identity, Layers.Interactable, SurfaceType.Default, true, "Interact");
            gate.ExitZone = MapMath.MinMax(V(-150f, -1f, RoadZ0), V(150f, 6f, RoadZ1 + 0.5f));
            ctx.Marker("MainGate", V(0, 1.1f, FenceZ0 + 0.3f), Quaternion.LookRotation(Vector3.back));
            ctx.Nav.GateLine(new Vector2(-3.1f, FenceZ0), new Vector2(3.1f, FenceZ0), NavGraph.EdgeMainGate);
            Props.SignPlate(mb, V(-5.5f, 1.5f, FenceZ0 - 0.06f), Vector3.back, Tex.SignRestricted, 1.0f, 0.5f);
            Props.SignPlate(mb, V(6.0f, 1.5f, FenceZ0 - 0.06f), Vector3.back, Tex.SignRestricted, 1.0f, 0.5f);
            Props.SignPlate(mb, V(-56f + 6.5f, 1.4f, FenceZ0 - 0.06f), Vector3.back, Tex.SignRestricted, 1.0f, 0.5f);
            Props.SignPlate(mb, V(FenceX1 + 0.06f, 1.5f, 20f), Vector3.right, Tex.SignRestricted, 1.0f, 0.5f);
            Props.SignPlate(mb, V(FenceX0 - 0.06f, 1.5f, 0f), Vector3.left, Tex.SignRestricted, 1.0f, 0.5f);

            // vehicle gate (chained; the car smashes through it)
            GatePost(ctx, mb, V(-60.1f, 0, FenceZ0));
            GatePost(ctx, mb, V(-51.9f, 0, FenceZ0));
            var car = ctx.Data.Car;
            car.VehicleGateLeft = Dyn.GateLeaf(ctx, "VehicleGate_Left", V(-60f, 0, FenceZ0), 0f, 4.0f, 2.45f, out var vl);
            car.VehicleGateRight = Dyn.GateLeaf(ctx, "VehicleGate_Right", V(-52f, 0, FenceZ0), 180f, 4.0f, 2.45f, out var vr);
            car.VehicleGateLeftOpenAngle = OpenTowards(car.VehicleGateLeft, Vector3.back, 100f);
            car.VehicleGateRightOpenAngle = OpenTowards(car.VehicleGateRight, Vector3.back, 100f);
            car.VehicleGateBlockers = new Collider[] { vl, vr };
            car.VehicleGateChain = Dyn.ChainAndPadlock(ctx, "VehicleGate_Chain", V(-56f, 0, FenceZ0), 0.16f, true);
            ctx.Nav.GateLine(new Vector2(-60.1f, FenceZ0), new Vector2(-51.9f, FenceZ0), NavGraph.EdgeVehicleGate);
            ctx.Tripwire("Yard", V(-0.98f, 0.14f, -42.6f), V(0.98f, 0.14f, -42.6f));
            Props.Stake(mb, V(-51.85f, 0, -43.1f));
            ctx.Tripwire("ParkingLot", V(-51.85f, 0.14f, -44.88f), V(-51.85f, 0.14f, -43.1f));
        }

        static float OpenTowards(Transform pivot, Vector3 dir, float deg)
        {
            Vector3 l = Quaternion.Inverse(pivot.rotation) * dir;
            return l.z < 0f ? deg : -deg;
        }

        // ================================================================== forest + far tree line + boundary

        static float OutsideFence(float x, float z)
        {
            float dx = Mathf.Max(FenceX0 - x, Mathf.Max(0f, x - FenceX1));
            float dz = Mathf.Max(FenceZ0 - z, Mathf.Max(0f, z - FenceZ1));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static void Forest(MapContext ctx)
        {
            var rng = ctx.Rng("forest");
            var sectors = new MeshBuilder[8];
            for (int i = 0; i < 8; i++) sectors[i] = ctx.NewBuilder("Forest_" + i, Layers.Foliage);
            var pine1 = Mat.Cutout(Tex.TreePine1); var pine2 = Mat.Cutout(Tex.TreePine2);
            var dead = new[] { Mat.Cutout(Tex.TreeDead1), Mat.Cutout(Tex.TreeDead2), Mat.Cutout(Tex.TreeDead3) };
            var bush = new[] { Mat.Cutout(Tex.Bush1), Mat.Cutout(Tex.Bush2) };
            float step = 3.6f;
            int count = 0;
            for (float z = FenceZ0 - 34f; z <= FenceZ1 + 34f; z += step)
                for (float x = FenceX0 - 34f; x <= FenceX1 + 34f; x += step)
                {
                    float px = x + rng.Range(-1.6f, 1.6f), pz = z + rng.Range(-1.6f, 1.6f);
                    float d = OutsideFence(px, pz);
                    float r1 = rng.NextFloat(), r2 = rng.NextFloat(), r3 = rng.NextFloat();
                    if (d < 3f || d > 30f + r1 * 4f) continue;
                    if (pz > RoadZ0 - 1.8f && pz < RoadZ1 + 1.5f) continue;                       // road
                    if (pz > RoadZ1 && pz < FenceZ0 && px > -5f && px < 5f) continue;             // gravel road to the gate
                    if (pz > RoadZ1 && pz < FenceZ0 && px > -62.5f && px < -49.5f) continue;      // vehicle lane
                    bool breach = px > 12f && px < 30f && pz > FenceZ1 && pz < FenceZ1 + 9f;
                    if (r2 > 0.86f) continue;
                    float ang = Mathf.Atan2(pz - 20f, px + 12f) * Mathf.Rad2Deg + 180f;
                    var mb = sectors[Mathf.Clamp((int)(ang / 45f), 0, 7)];
                    var p = V(px, 0, pz);
                    float rot = r3 * 180f;
                    if (d < 7f || r3 < 0.1f || breach)
                    {
                        if (breach && r1 < 0.6f) continue;
                        float h = rng.Range(1.4f, 2.8f);
                        Props.Billboard(mb, bush[(int)(r1 * 2f) % 2], p, h * 1.4f, h, 2, rot, 0.3f, 0.8f);
                    }
                    else if (r1 < 0.55f)
                    {
                        float h = rng.Range(9f, 16f);
                        Props.Billboard(mb, r3 < 0.5f ? pine1 : pine2, p, h * 0.42f, h, 2, rot, 0.25f, 0.75f);
                    }
                    else
                    {
                        float h = rng.Range(8f, 14f);
                        Props.Billboard(mb, dead[(int)(r3 * 3f) % 3], p, h * 0.9f, h, 2, rot, 0.3f, 0.8f);
                    }
                    count++;
                }
            ctx.BuildLog.Add("forest billboards: " + count);
        }

        static void Treeline(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Treeline", Layers.Foliage);
            var m = Mat.Cutout(Tex.Treeline, new Color(0.35f, 0.38f, 0.42f));
            var center = V(-12f, 0, 20f);
            for (int ring = 0; ring < 2; ring++)
            {
                float r = ring == 0 ? 165f : 182f, h = ring == 0 ? 20f : 27f, w = 52f;
                int n = Mathf.CeilToInt(2f * Mathf.PI * r / (w * 0.92f));
                for (int i = 0; i < n; i++)
                {
                    float a = (i + ring * 0.5f) / n * Mathf.PI * 2f;
                    var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    var p = center + dir * r - Vector3.up * 1.5f;
                    var tangent = new Vector3(-dir.z, 0, dir.x) * (w * 0.5f);
                    Vector3 a0 = p - tangent, a1 = p + tangent;
                    var u0 = (i % 4) * 0.25f;
                    mb.Material = m;
                    var c0 = Shade.Gray(0.5f); var c1 = Shade.Gray(0.9f);
                    int i0 = mb.AddVertex(a0, Vector3.up, new Vector2(u0, 0), c0);
                    int i1 = mb.AddVertex(a0 + Vector3.up * h, Vector3.up, new Vector2(u0, 1), c1);
                    int i2 = mb.AddVertex(a1 + Vector3.up * h, Vector3.up, new Vector2(u0 + 0.5f, 1), c1);
                    int i3 = mb.AddVertex(a1, Vector3.up, new Vector2(u0 + 0.5f, 0), c0);
                    mb.AddTriangle(i0, i1, i2); mb.AddTriangle(i0, i2, i3);
                }
            }
        }

        static void Boundary(MapContext ctx)
        {
            float h = 40f;
            ctx.Solid(V(BoundX0 - 0.5f, h * 0.5f - 1f, (BoundZ0 + BoundZ1) * 0.5f), V(1f, h, BoundZ1 - BoundZ0 + 2f), SurfaceType.Default, "Boundary");
            ctx.Solid(V(BoundX1 + 0.5f, h * 0.5f - 1f, (BoundZ0 + BoundZ1) * 0.5f), V(1f, h, BoundZ1 - BoundZ0 + 2f), SurfaceType.Default, "Boundary");
            ctx.Solid(V((BoundX0 + BoundX1) * 0.5f, h * 0.5f - 1f, BoundZ0 - 0.5f), V(BoundX1 - BoundX0 + 2f, h, 1f), SurfaceType.Default, "Boundary");
            ctx.Solid(V((BoundX0 + BoundX1) * 0.5f, h * 0.5f - 1f, BoundZ1 + 0.5f), V(BoundX1 - BoundX0 + 2f, h, 1f), SurfaceType.Default, "Boundary");
        }

        // ================================================================== shed (like DrFYpp: planks + rusty tin roof)

        static void Shed(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Shed");
            const string A = "Shed";
            var planks = Mat.Lit(Tex.WoodPlanksWall, new Color(0.85f, 0.8f, 0.72f));
            var spec = new BuildingSpec
            {
                Name = "Shed", Area = A, X0 = 14f, Z0 = -4f, X1 = 21f, Z1 = 2f, WallH = 2.5f, T = 0.15f,
                Outside = WallSkin.Of(planks, null, 0f, null, 1.6f), Inside = WallSkin.Of(Mat.Lit(Tex.WoodPlanksWall, new Color(0.6f, 0.56f, 0.5f)), null, 0f, null, 1.6f),
                Floor = Mat.Lit(Tex.FloorConcrete, new Color(0.7f, 0.66f, 0.6f)), Roof = Mat.Lit(Tex.TinRusty), Soffit = Mat.Lit(Tex.WoodRaw, new Color(0.45f, 0.4f, 0.35f)),
                Trim = Mat.Lit(Tex.WoodRaw, new Color(0.5f, 0.45f, 0.4f)), FloorSurf = SurfaceType.Concrete, WallSurf = SurfaceType.Wood,
                Gable = true, RidgeAlongX = true, RoofRise = 1.15f, Overhang = 0.45f,
            };
            spec.Outside.AoFloor = 0.5f;
            spec.Openings.Add(('W', -1f, 1.0f, 2.2f));
            Buildings.Build(ctx, mb, spec);
            Dyn.Door(ctx, new DoorSpec
            {
                Name = "Shed", Kind = DoorKind.Shed, Center = V(14f, 0.02f, -1f), AlongX = false, Swing = Vector3.right, RoomMin = -4f, RoomMax = 2f,
                WallT = 0.15f, AreaSwing = A, AreaOther = "Yard", FrameMb = null,
            });
            float F = 0.02f;
            Props.Workbench(ctx, mb, V(20.55f, F, -1.2f), 90f, 2.2f, true, 1601);
            Props.MetalShelf(ctx, mb, V(17.6f, F, 1.65f), 0f, 1.8f, 1602, 0.7f, false);
            Dyn.Locker(ctx, "Shed.Locker", V(19.9f, F, -3.66f), 180f, false);
            Props.Tire(mb, V(14.7f, F, 1.3f), 0f);
            Props.Tire(mb, V(14.7f, F + 0.22f, 1.3f), 20f);
            Props.Tire(mb, V(15.5f, F, -3.3f), 0f, false);
            Props.Lawnmower(mb, V(16.2f, F, -3.2f), 40f);
            Props.GasCanProp(mb, V(20.4f, F, 1.4f), 10f);
            Props.Crate(ctx, mb, V(18.4f, F, -3.45f), 12f, 0.5f, true);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(17.5f, F, -1f), 3f, 3f, 0f);
            Arch.FloorDecal(mb, Mat.Decal("water_stain"), V(19.5f, F, 0.6f), 1.2f, 1.0f, 30f);
            ctx.Key(A, V(20.5f, F + Props.WorkbenchTop, -1.9f));
            ctx.Common(A, V(20.6f, F + Props.WorkbenchTop, -0.5f));
            ctx.Key(A, V(17.6f - 0.62f, F + Props.ShelfLevel(1), 1.52f));
            ctx.Common(A, V(14.5f, F, -3.55f));
            ctx.DeskNote(A, V(20.45f, F + Props.WorkbenchTop + 0.003f, -1.2f), 90f);
            Arch.Bulb(ctx, mb, GlowExt, V(17.5f, 2.5f + 0.5f, -1f), 0.55f, HouseBuilder.Warm, 0.85f, 4.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Shed_Bulb", false, 0.55f);
            ctx.Nav.Add(V(16.8f, 0f, -1.0f), A);
            ctx.Nav.Add(V(18.4f, 0f, 0.3f), A);
            ctx.Nav.Add(V(18.0f, 0f, -2.3f), A);
            ctx.Ambient(AmbientType.Shed, V(14f, -0.2f, -4f), V(21f, 3.6f, 2f));
            ctx.Area(A, V(14f, -0.2f, -4f), V(21f, 3.6f, 2f));
            ctx.Tripwire(A, V(13.9f, 0.12f, -1.46f), V(13.9f, 0.12f, -0.54f));
        }

        // ================================================================== restroom (concrete block, D7qZJWs)

        static void Restroom(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Restroom");
            const string A = "Restroom";
            var block = Mat.Lit(Tex.ConcreteBlock);
            var spec = new BuildingSpec
            {
                Name = "Restroom", Area = A, X0 = -66f, Z0 = 2f, X1 = -58f, Z1 = 8f, WallH = 2.95f, T = 0.2f,
                Outside = WallSkin.Of(block, Mat.Lit(Tex.Concrete, new Color(0.6f, 0.6f, 0.58f)), 0.3f, null, 1.4f),
                Inside = WallSkin.Of(block, Mat.Lit(Tex.TileWhiteDirty), 1.5f, null, 1.4f),
                Floor = Mat.Lit(Tex.FloorTile), Ceiling = Mat.Lit(Tex.Concrete, new Color(0.7f, 0.7f, 0.68f)), Roof = Mat.Lit(Tex.Concrete, new Color(0.5f, 0.5f, 0.48f)),
                Trim = Mat.Lit(Tex.MetalDark), FloorSurf = SurfaceType.Tile, WallSurf = SurfaceType.Concrete, Gable = false, Overhang = 0.35f, FloorTile = 0.9f,
            };
            spec.Inside.LowerTileU = 1.5f;
            spec.Openings.Add(('S', -62f, 1.0f, 2.2f));
            Buildings.Build(ctx, mb, spec);
            Dyn.Door(ctx, new DoorSpec
            {
                Name = "Restroom", Kind = DoorKind.Restroom, Center = V(-62f, 0.02f, 2f), AlongX = true, Swing = Vector3.forward, RoomMin = -66f, RoomMax = -58f,
                WallT = 0.2f, AreaSwing = A, AreaOther = "ParkingLot", FrameMb = null,
            });
            float F = 0.02f;
            // two stalls (green metal partitions) along the north wall
            var green = Mat.Lit(Tex.MetalGreen, new Color(0.7f, 0.8f, 0.7f));
            mb.Material = green;
            mb.Color = Shade.Gray(0.7f);
            float[] px = { -64.6f, -63.2f };
            foreach (float x in px)
            {
                mb.AddBox(V(x, F + 1.1f, 7.1f), V(0.04f, 1.8f, 1.55f), BoxUV.Local, 1f);
                ctx.Solid(V(x, F + 1.1f, 7.1f), V(0.06f, 1.8f, 1.55f), SurfaceType.Metal, "Stall");
            }
            mb.AddBox(V(-64.6f, F + 1.1f, 6.34f), V(0.25f, 1.8f, 0.04f), BoxUV.Local, 1f);
            mb.AddBox(V(-63.35f, F + 1.1f, 6.34f), V(0.25f, 1.8f, 0.04f), BoxUV.Local, 1f);
            // stall doors hanging ajar
            mb.Push(V(-65.85f, F + 0.2f, 6.34f), MapMath.Yaw(-60f));
            mb.AddBox(V(0.55f, 0.8f, 0), V(1.1f, 1.6f, 0.03f), BoxUV.Local, 1f);
            mb.Pop();
            mb.Push(V(-64.45f, F + 0.2f, 6.34f), MapMath.Yaw(-25f));
            mb.AddBox(V(0.55f, 0.8f, 0), V(1.1f, 1.6f, 0.03f), BoxUV.Local, 1f);
            mb.Pop();
            mb.Color = Shade.Gray(1f);
            Props.Toilet(ctx, mb, V(-65.25f, F, 7.55f), 0f);
            Props.Toilet(ctx, mb, V(-63.9f, F, 7.55f), 0f);
            Props.PedestalSink(ctx, mb, V(-58.36f, F, 4.2f), 90f);
            Props.PedestalSink(ctx, mb, V(-58.36f, F, 5.6f), 90f);
            Props.Mirror(mb, V(-58.11f, F + 1.55f, 4.2f), Vector3.left, 0.5f, 0.6f);
            Props.Mirror(mb, V(-58.11f, F + 1.55f, 5.6f), Vector3.left, 0.5f, 0.6f);
            Dyn.Locker(ctx, "Restroom.JanitorLocker", V(-65.64f, F, 3.2f), -90f, false);
            Props.Bucket(mb, V(-65.5f, F, 4.2f), 0f, false, 0.55f, false);
            Arch.FloorDecal(mb, Mat.Decal("grime"), V(-62f, F, 5f), 4f, 3.5f, 0f);
            Arch.FloorDecal(mb, Mat.Decal("blood_smear"), V(-61f, F, 4.2f), 0.6f, 1.6f, 60f);
            Arch.Decal(mb, Mat.Decal("writing_help"), V(-58.11f, F + 2.1f, 6.8f), Vector3.left, 1.0f, 0.5f, 0f);
            Arch.Decal(mb, Mat.Decal("graffiti_scrawl_2"), V(-65.89f, F + 1.7f, 5.2f), Vector3.right, 0.9f, 0.9f, 0f);
            ctx.Key(A, V(-58.4f, F + Props.SinkTop, 4.36f));
            ctx.Common(A, V(-65.25f, F, 6.75f));
            ctx.Common(A, V(-63.9f, F + Props.ToiletTankTop, 7.75f));
            ctx.Common(A, V(-65.64f, F + 1.9f, 3.2f));
            ctx.Key(A, V(-58.6f, F, 7.5f));
            ctx.WallNote(A, V(-62f, F + 1.6f, 7.89f), Vector3.back);
            // fluorescent tube (green-white, buzzing)
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBox(V(-62f, 2.9f, 5f), V(0.25f, 0.06f, 1.3f), BoxUV.Local, 0.5f);
            var fl = ctx.Light(V(-62f, 2.7f, 5f), new Color(0.72f, 0.95f, 0.85f), 1.05f, 6.5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Restroom_Fluorescent", 0.5f, 18f, "RestroomTube");
            var g = ctx.GlowBuilder("RestroomTube");
            g.Material = ctx.GlowColor(new Color(0.8f, 1f, 0.9f));
            g.AddBox(V(-62f, 2.85f, 5f), V(0.08f, 0.05f, 1.2f), BoxUV.Local, 1f);
            ctx.Emitter(V(-62f, 2.7f, 5f), "Audio/Ambience/fluorescent_buzz_loop", 0.5f, 9f, true);
            Arch.WallLamp(ctx, mb, GlowExt, V(-61.1f, 2.45f, 1.9f), Vector3.back, new Color(0.85f, 0.9f, 0.75f), 0.75f, 5f, PsxFlicker.FaultyBulb, LightGroup.Power, "Restroom_DoorLight", 0.5f);
            Props.Dumpster(ctx, mb, V(-55.6f, 0, 4.2f), 90f);
            Props.TrashBags(mb, V(-56.2f, 0, 1.8f), 1701, 2);
            ctx.Nav.Add(V(-62f, 0f, 3.6f), A);
            ctx.Nav.Add(V(-62f, 0f, 5.4f), A);
            ctx.Nav.Add(V(-59.8f, 0f, 7.0f), A);
            ctx.Ambient(AmbientType.Restroom, V(-66f, -0.2f, 2f), V(-58f, 3.0f, 8f));
            ctx.Area(A, V(-66f, -0.2f, 2f), V(-58f, 3.0f, 8f));
            ctx.Tripwire(A, V(-62.46f, 0.14f, 1.88f), V(-61.54f, 0.14f, 1.88f));
        }

        // ================================================================== parking lot + the car

        static void ParkingLot(MapContext ctx)
        {
            var mb = ctx.NewBuilder("ParkingLot");
            const string A = "ParkingLot";
            var line = Mat.Decal("parking_line");
            for (int k = 0; k <= 9; k++)
            {
                float x = -68f + 2.8f * k;
                Arch.FloorDecal(mb, line, V(x, 0.025f, -5f), 0.14f, 5f, 0f);
                Arch.FloorDecal(mb, line, V(x, 0.025f, -15f), 0.14f, 5f, 0f);
                Arch.FloorDecal(mb, line, V(x, 0.025f, -20f), 0.14f, 5f, 0f);
            }
            for (float x = -67.5f; x < -42f; x += 2.5f) Arch.FloorDecal(mb, line, V(x, 0.025f, -17.5f), 0.14f, 2.5f, 90f);
            var oil = Mat.Decal("grime");
            var rng = ctx.Rng("lot");
            for (int i = 0; i < 9; i++) Arch.FloorDecal(mb, oil, V(rng.Range(-68f, -42f), 0.025f, rng.Range(-30f, -4f)), rng.Range(1f, 2.5f), rng.Range(0.8f, 2f), rng.Range(0f, 180f));
            for (int i = 0; i < 5; i++) Arch.FloorDecal(mb, Mat.Decal("cracks"), V(rng.Range(-68f, -42f), 0.026f, rng.Range(-30f, -4f)), 2.5f, 2.5f, rng.Range(0f, 180f));
            // sodium lamp (orange, faulty)
            var lampGlow = ctx.GlowBuilder(GlowExt);
            var sodium = new Color(1f, 0.55f, 0.2f);
            var head = Props.LampPost(ctx, mb, lampGlow, ctx.GlowColor(new Color(1f, 0.65f, 0.3f)), V(-43.2f, 0, -15f), -90f, 8f, 1.6f);
            ctx.Light(head - Vector3.up * 0.3f, sodium, 1.45f, 21f, PsxFlicker.FaultyBulb, LightGroup.Power, "Lot_Sodium", 0.45f, 6f, GlowExt);
            // wrecks
            Props.CarWreck(ctx, mb, V(-63.5f, 0, -5.2f), 3f, 1801);
            Props.CarWreck(ctx, mb, V(-46.4f, 0, -20f), 183f, 1802);
            Props.CarWreck(ctx, mb, V(-66.2f, 0, -27.5f), 74f, 1803);
            BuildCar(ctx);
            Props.Tire(mb, V(-41.6f, 0, -30.8f), 0f);
            Props.Tire(mb, V(-41.5f, 0.22f, -30.7f), 30f);
            Props.Barrel(ctx, mb, V(-69.2f, 0, -3.1f), 20f, Tex.BarrelFuel, 0.7f);
            Props.Barrel(ctx, mb, V(-68.6f, 0, -3.6f), 70f, Tex.BarrelWater, 0.4f);
            ctx.Common(A, V(-41.2f, 0.025f, -29.6f));
            ctx.Key(A, V(-60.8f, 0.025f, -6.6f));
            ctx.Common(A, V(-48.1f, 0.025f, -22.2f));
            ctx.Key(A, V(-64.3f, 0.025f, -25.2f));
            ctx.Common(A, V(-69.2f, Props.BarrelTop, -3.1f));
            Props.Barrel(ctx, mb, V(-66.35f, 0, -5.2f), 40f, Tex.BarrelFuel, 0.6f);
            ctx.Tripwire(A, V(-64.45f, 0.14f, -5.2f), V(-66.05f, 0.14f, -5.2f));
            ctx.BearTrap(A, V(-56f, 0.025f, -36f));
            ctx.Area(A, V(-70f, -1f, -45f), V(-40f, 8f, -2f));
        }

        static void BuildCar(MapContext ctx)
        {
            var pos = V(-52f, 0, -20f);
            var rot = Quaternion.Euler(0, 180f, 0);   // front (+Z local) faces south
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Car", pos, rot, Layers.World);
            var car = ctx.Data.Car;
            car.Root = root;
            var mb = new MeshBuilder();
            Props.CarShell(mb, false, false, 1900);
            mb.Build("Body", root, Layers.World);
            ctx.CountRenderer(mb);
            var hood = GeoUtil.CreateChild(root, "HoodPivot", V(0, 0.955f, 0.95f), Quaternion.identity, Layers.World);
            var hmb = new MeshBuilder();
            Props.CarHood(hmb, false);
            hmb.Build("Hood", hood, Layers.World);
            ctx.CountRenderer(hmb);
            car.HoodPivot = hood;
            car.HoodOpenAngle = -60f;
            GeoUtil.AddBox(root, V(0, 0.63f, 0), V(1.82f, 0.66f, 4.72f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "BodyCol");
            GeoUtil.AddBox(root, V(0, 1.18f, -0.15f), V(1.6f, 0.46f, 2.1f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CabinCol");
            Collider T(string name, Vector3 c, Vector3 s) => GeoUtil.AddBox(root, c, s, Quaternion.identity, Layers.Interactable, SurfaceType.Default, true, name);
            car.FuelCap = T("FuelCap", V(-0.98f, 0.82f, -1.65f), V(0.3f, 0.3f, 0.45f));
            car.Hood = T("Hood", V(0, 1.0f, 1.9f), V(1.7f, 0.5f, 1.25f));
            car.DriverDoor = T("DriverDoor", V(-1.0f, 1.0f, 0.2f), V(0.3f, 0.75f, 0.95f));
            car.PassengerDoors = new Collider[]
            {
                T("PassengerDoor_FR", V(1.0f, 1.0f, 0.2f), V(0.3f, 0.75f, 0.95f)),
                T("PassengerDoor_RL", V(-1.0f, 1.0f, -0.85f), V(0.3f, 0.75f, 0.85f)),
                T("PassengerDoor_RR", V(1.0f, 1.0f, -0.85f), V(0.3f, 0.75f, 0.85f)),
            };
            var seatLocal = new[] { V(-0.38f, 1.12f, -0.12f), V(0.38f, 1.12f, -0.12f), V(-0.38f, 1.12f, -1.0f), V(0.38f, 1.12f, -1.0f) };
            car.Seats = new Pose[4];
            car.SeatAnchors = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                car.SeatAnchors[i] = GeoUtil.CreateChild(root, "Seat_" + i, seatLocal[i], Quaternion.identity, Layers.World);
                car.Seats[i] = new Pose(pos + rot * seatLocal[i], rot);
            }
            car.Headlights = new[]
            {
                GeoUtil.CreateChild(root, "Headlight_L", V(-0.62f, 0.72f, 2.37f), Quaternion.Euler(4f, 0, 0), Layers.World),
                GeoUtil.CreateChild(root, "Headlight_R", V(0.62f, 0.72f, 2.37f), Quaternion.Euler(4f, 0, 0), Layers.World),
            };
            car.DrivePath = new[]
            {
                V(-52f, 0, -20f), V(-52.6f, 0, -26f), V(-54.6f, 0, -32f), V(-56f, 0, -38f), V(-56f, 0, -45f), V(-56f, 0, -51f),
                V(-54f, 0, -56f), V(-49f, 0, -59.6f), V(-40f, 0, -60.4f), V(-10f, 0, -60.5f), V(30f, 0, -60.5f), V(85f, 0, -60.5f),
            };
            ctx.Marker("Car", pos + V(0, 1f, 0), rot);
            Arch.Blob(ctx.SharedBuilder("PropShadows"), pos, 2.4f, 5.2f, 180f);
        }

        // ================================================================== front yard (House.webp: flamingos, tall grass, big tree)

        static readonly Vector3[] PoleLineFront = { V(4.6f, 0, -16f), V(4.6f, 0, -28f), V(4.6f, 0, -40f), V(6.5f, 0, -51.5f), V(30f, 0, -51.5f) };
        static readonly Vector3[] PoleLineNorth = { V(12.5f, 0, 11f), V(13f, 0, 25f), V(17f, 0, 39f), V(25f, 0, 47.5f) };
        static readonly Vector3[] PoleLineWest = { V(-17f, 0, -11f), V(-31f, 0, -10.5f), V(-45f, 0, 0f), V(-55.5f, 0, 0.6f) };

        static void Yard(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Yard");
            var fol = ctx.NewBuilder("Yard_Foliage", Layers.Foliage);
            var rng = ctx.Rng("yard");
            const string A = "Yard";
            // the big dark tree beside the house
            var bigTree = V(-15f, 0, -20f);
            Props.Billboard(fol, Mat.Cutout(Tex.TreeBig), bigTree, 13f, 13.5f, 3, 15f, 0.3f, 0.85f);
            mb.Material = Mat.Lit(Tex.Bark);
            mb.Color = Shade.Gray(0.7f);
            mb.AddCylinder(bigTree, 0.5f, 0.32f, 5.5f, 8, false, false, new Rect(0, 0, 2, 3), true, 3);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(bigTree + Vector3.up * 2.5f, V(0.9f, 5f, 0.9f), SurfaceType.Wood, "BigTree");
            Arch.Blob(mb, bigTree + Vector3.up * 0.01f, 8f, 8f);
            // dead trees inside the fence (trunk colliders)
            Vector3[] deadTrees =
            {
                V(-22f, 0, -36f), V(15f, 0, -31f), V(28f, 0, -13f), V(33f, 0, 6f), V(-30f, 0, 16f), V(-51f, 0, 13f), V(-68f, 0, -39f),
                V(-36f, 0, -28f), V(44f, 0, -30f), V(46f, 0, 72f), V(-61f, 0, 40f), V(-70f, 0, 72f), V(5f, 0, 72f), V(35f, 0, 40f),
                V(-48f, 0, 71f), V(-72f, 0, -14f), V(42f, 0, -8f), V(-20f, 0, 70f),
            };
            string[] deadTex = { Tex.TreeDead1, Tex.TreeDead2, Tex.TreeDead3 };
            for (int i = 0; i < deadTrees.Length; i++)
            {
                float h = rng.Range(8f, 13f);
                Props.Tree(ctx, fol, deadTex[i % 3], deadTrees[i], h, h * 0.85f, rng.Range(0f, 180f), true, 2);
                mb.Material = Mat.Lit(Tex.Bark, new Color(0.55f, 0.55f, 0.55f));
                mb.AddCylinder(deadTrees[i], 0.22f, 0.12f, 3.2f, 6, false, false, new Rect(0, 0, 1, 2), true);
            }
            // flamingos (12-16, mostly in front of the porch)
            int fl = 0;
            var flamingos = new List<Vector3>();
            for (int tries = 0; tries < 400 && fl < 15; tries++)
            {
                bool near = fl < 11;
                var p = near ? V(rng.Range(-11f, 11f), 0, rng.Range(-21f, -12.6f)) : V(rng.Range(-18f, 18f), 0, rng.Range(-36f, -21f));
                if (Mathf.Abs(p.x) < 2.8f) continue;
                if (p.x < -1.8f && p.x > -5.6f && p.z > -16.6f) continue;
                if ((p - V(9f, 0, -15f)).sqrMagnitude < 1.5f || (p - V(4.6f, 0, -16f)).sqrMagnitude < 1.5f || (p - V(4.6f, 0, -28f)).sqrMagnitude < 1.5f) continue;
                if ((p - bigTree).sqrMagnitude < 2f || (p - V(-10f, 0, -30f)).sqrMagnitude < 12f) continue;
                bool ok = true;
                foreach (var q in flamingos) if ((q - p).sqrMagnitude < 1.4f) { ok = false; break; }
                if (!ok) continue;
                flamingos.Add(p);
                Props.Flamingo(mb, p, rng.Range(0f, 360f), rng.Range(0.95f, 1.1f), fl % 7 == 6);
                fl++;
            }
            ctx.BuildLog.Add("flamingos: " + fl);
            // tall dead grass clumps (not on paths / driveway / lot / buildings)
            int grass = 0;
            for (int tries = 0; tries < 1400 && grass < 340; tries++)
            {
                float x = rng.Range(FenceX0 + 1f, FenceX1 - 1f), z = rng.Range(FenceZ0 + 1f, FenceZ1 - 1f);
                float size = rng.Range(0.6f, 1.4f);
                if (BlockedForGrass(x, z)) continue;
                bool front = z < -12f && z > -40f && Mathf.Abs(x) < 25f;
                if (!front && rng.Chance(0.35f)) continue;
                Props.GrassClump(fol, V(x, 0, z), size * (front ? 1.15f : 1f), rng.Range(0f, 180f), grass);
                grass++;
            }
            // grass hugging the fence line
            for (float x = FenceX0 + 2f; x < FenceX1 - 2f; x += rng.Range(2f, 5f))
            {
                if (!(x > -62f && x < -50f) && !(x > -5f && x < 5f)) Props.GrassClump(fol, V(x, 0, FenceZ0 + rng.Range(0.6f, 1.6f)), rng.Range(0.8f, 1.5f), rng.Range(0f, 180f), (int)x);
                if (!(x > 13f && x < 29f)) Props.GrassClump(fol, V(x, 0, FenceZ1 - rng.Range(0.6f, 1.6f)), rng.Range(0.8f, 1.5f), rng.Range(0f, 180f), (int)x + 1);
            }
            // floodlight pole aimed at the house front (DrFYpp)
            FloodLight(ctx, mb, V(9f, 0, -15f), V(0, 1.6f, -8.1f));
            // utility poles + sagging wires
            PoleLine(ctx, mb, PoleLineFront, V(8f, 5.9f, -8.15f));
            var northEnd = PoleLine(ctx, mb, PoleLineNorth, V(9f, 5.9f, 8.15f));
            var westEnd = PoleLine(ctx, mb, PoleLineWest, V(-11.15f, 5.9f, -6f));
            Props.Wire(mb, northEnd[1], V(25f, 4.6f, 51.85f), 0.5f, 6);
            Props.Wire(mb, westEnd[1], V(-58.1f, 2.6f, 4.5f), 0.4f, 6);
            // rusty pickup wreck + junk
            Props.PickupWreck(ctx, mb, V(-10f, 0, -30f), 35f, 2001);
            Props.Tire(mb, V(-6.2f, 0, -26.5f), 0f);
            Props.Tire(mb, V(-6.3f, 0.22f, -26.4f), 40f);
            Props.Tire(mb, V(-5.4f, 0, -27.2f), 0f, false);
            Props.Barrel(ctx, mb, V(8.2f, 0, -24.2f), 15f, Tex.BarrelFuel, 0.65f);
            Props.Barrel(ctx, mb, V(8.9f, 0, -24.8f), 60f, Tex.BarrelWater, 0.4f);
            Props.Chair(ctx, mb, V(-5.5f, 0, -19f), 120f, true);
            Props.PorchCouch(ctx, mb, V(-3.6f, HouseBuilder.FG, -8.62f), 0f);
            Props.Cooler(mb, V(4.2f, HouseBuilder.FG, -9.1f), 15f);
            ctx.SolidLocal(V(4.2f, HouseBuilder.FG, -9.1f), 15f, V(0, 0.22f, 0), V(0.62f, 0.44f, 0.4f), SurfaceType.Default, "Cooler");
            Props.Bottles(mb, V(-2.2f, HouseBuilder.FG, -10.4f), 2002, 3, 0.2f);
            Props.Crate(ctx, mb, V(12.6f, 0, -20.5f), 25f, 0.6f);
            Props.HayStack(ctx, mb, V(24f, 0, 28f), 20f, 2003, 7);
            Props.Tire(mb, V(27f, 0, -26f), 0f);
            Props.JerseyBarrier(ctx, mb, V(-2.0f, 0, -42.6f), 0f, 2.0f);
            Props.JerseyBarrier(ctx, mb, V(2.0f, 0, -42.6f), 0f, 2.0f);
            // gameplay spots in the yard / porch
            ctx.Common("Porch", V(4.2f, HouseBuilder.FG + Props.CoolerTop, -9.1f));
            ctx.Key("Porch", V(-3.6f, HouseBuilder.FG + Props.SofaSeat + 0.04f, -8.75f));
            ctx.Common("Porch", V(5.3f, HouseBuilder.FG, -10.6f));
            ctx.Key(A, V(-8.0f, 0, -26.8f));
            ctx.Common(A, V(8.2f, Props.BarrelTop, -24.2f));
            ctx.Key(A, V(12.6f, 0.6f, -20.5f));
            ctx.Common(A, V(-14.2f, 0, -18.8f));
            ctx.Common(A, V(-6.6f, 0, -27.6f));
            ctx.Key(A, V(24f, Props.HayH * 2f, 28f));
            ctx.Common(A, V(-13.2f, 0, 2.5f));
            ctx.Tripwire("Porch", V(-1.24f, 0.12f, -12.3f), V(1.24f, 0.12f, -12.3f));
            ctx.Tripwire(A, V(-12.3f, HouseBuilder.FG + 0.12f, 3.05f), V(-12.3f, HouseBuilder.FG + 0.12f, 4.95f));
            ctx.Tripwire(A, V(-1.24f, HouseBuilder.FG + 0.12f, 9.3f), V(1.24f, HouseBuilder.FG + 0.12f, 9.3f));
            ctx.BearTrap(A, V(-7.5f, 0, -16.5f));
            ctx.BearTrap(A, V(7.2f, 0, -20.5f));
            ctx.BearTrap(A, V(-16.5f, 0, -2.0f));
            ctx.BearTrap(A, V(15.5f, 0, 6.5f));
            ctx.BearTrap(A, V(-24f, 0, -40.5f));
            ctx.WallNote(A, V(14.0f - 0.09f, 1.5f, 0.8f), Vector3.left);
            ctx.DeskNote("Porch", V(4.25f, HouseBuilder.FG + Props.CoolerTop + 0.003f, -8.95f), 20f);
        }

        static bool BlockedForGrass(float x, float z)
        {
            if (x > -13f && x < 13f && z > -12.8f && z < 11.5f) return true;      // house + porch + stoops
            if (Mathf.Abs(x) < 2.6f && z < -11f) return true;                       // driveway
            if (x > -71f && x < -39f && z > -33f && z < -1f) return true;           // parking lot
            if (x > -59f && x < -53f && z < -31f) return true;                       // vehicle lane
            if (x > 13f && x < 22f && z > -5f && z < 3f) return true;               // shed
            if (x > -67f && x < -54f && z > 1f && z < 9f) return true;              // restroom + dumpster
            if (x > 11f && x < 31f && z > 51f && z < 69f) return true;              // barn
            if (x > -46f && x < 6f && z > 21f && z < 63f) return true;              // corn field (own foliage)
            if ((x - 40f) * (x - 40f) + (z - 58f) * (z - 58f) < 30f) return true;  // silo
            if (Mathf.Abs(x - 21f) < 4f && z > 77f) return true;                    // fuel depot
            if (Mathf.Abs(x + 15f) < 1.2f && Mathf.Abs(z + 20f) < 1.2f) return true;
            if (z > 10f && z < 53f)
            {
                float t = Mathf.Clamp01((z - 10.8f) / 41.4f);
                float cx = Mathf.Lerp(0f, 21f, t);
                if (Mathf.Abs(x - cx) < 2.5f) return true;
            }
            return false;
        }

        static Vector3[] PoleLine(MapContext ctx, MeshBuilder mb, Vector3[] poles, Vector3 houseAnchor)
        {
            Vector3[] prev = null;
            for (int i = 0; i < poles.Length; i++)
            {
                float yaw = 0f;
                if (i + 1 < poles.Length) yaw = MapMath.YawFacing(poles[i + 1] - poles[i]) + 90f;
                else if (i > 0) yaw = MapMath.YawFacing(poles[i] - poles[i - 1]) + 90f;
                var tops = Props.UtilityPole(ctx, mb, poles[i], yaw, 8.5f, (i % 3 - 1) * 1.5f);
                if (prev != null)
                    for (int k = 0; k < 3; k++) Props.Wire(mb, prev[k], tops[k], 0.7f, 7);
                else Props.Wire(mb, tops[1], houseAnchor, 0.5f, 6);
                prev = tops;
            }
            return prev;
        }

        static void FloodLight(MapContext ctx, MeshBuilder mb, Vector3 pos, Vector3 target)
        {
            float h = 6.4f;
            mb.Material = Mat.Lit(Tex.UtilityPole);
            mb.Color = Shade.Gray(0.75f);
            mb.AddCylinder(pos, 0.13f, 0.1f, h, 6, true, false, new Rect(0, 0, 1, 2), true, 2);
            Vector3 head = pos + Vector3.up * (h - 0.25f);
            Vector3 dir = (target - head).normalized;
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBeam(pos + Vector3.up * (h - 0.6f), head + dir * 0.1f, 0.06f);
            mb.Push(head, Quaternion.LookRotation(dir, Vector3.up));
            mb.AddBox(V(0, 0, 0), V(0.45f, 0.35f, 0.3f), BoxUV.Local, 0.4f);
            mb.Pop();
            mb.Material = Mat.Flat(0.05f, 0.05f, 0.05f);
            mb.AddBeam(pos + Vector3.up * 2.5f, pos + Vector3.up * (h - 0.6f), 0.025f);
            mb.Color = Shade.Gray(1f);
            var g = ctx.GlowBuilder(GlowExt);
            g.Push(head + dir * 0.16f, Quaternion.LookRotation(dir, Vector3.up));
            g.Material = ctx.GlowColor(new Color(1f, 0.95f, 0.8f));
            g.AddQuad(V(0.2f, -0.15f, 0), V(0.2f, 0.15f, 0), V(-0.2f, 0.15f, 0), V(-0.2f, -0.15f, 0));
            g.Pop();
            ctx.Spot(head + dir * 0.3f, dir, new Color(1f, 0.93f, 0.78f), 1.8f, 27f, 78f, PsxFlicker.FaultyBulb, LightGroup.Power, "Floodlight", 0.15f, GlowExt);
            ctx.Solid(pos + Vector3.up * (h * 0.5f), V(0.28f, h, 0.28f), SurfaceType.Wood, "FloodPole");
            ctx.Marker("Floodlight", head, Quaternion.LookRotation(dir));
        }

        // ================================================================== outside the main gate (road, sign, ditches)

        static void OutsideGate(MapContext ctx)
        {
            var mb = ctx.NewBuilder("Roadside");
            var fol = ctx.NewBuilder("Roadside_Foliage", Layers.Foliage);
            Props.RoadSign(ctx, mb, V(6.2f, 0, -53.8f), 180f, Tex.SignRoad, 1.7f, 0.85f, 2.3f);
            Props.RoadSign(ctx, mb, V(-30f, 0, -54.2f), 200f, Tex.SignRestricted, 1.4f, 0.7f, 2.1f);
            // grassy banks on both sides of the road (D7qZKJL), open where the gravel road / vehicle lane come out
            var bank = Mat.Lit(Tex.GrassDead, new Color(0.85f, 0.82f, 0.7f));
            float[,] north = { { -150f, -62f }, { -50f, -4.6f }, { 4.6f, 150f } };
            for (int i = 0; i < north.GetLength(0); i++) Berm(ctx, mb, north[i, 0], north[i, 1], RoadZ1 + 0.8f, +1f, 3.0f, 2.2f, 0.75f, bank);
            Berm(ctx, mb, -150f, 150f, RoadZ0 - 0.8f, -1f, 3.4f, 2.4f, 0.95f, bank);
            var rng = ctx.Rng("roadside");
            var bushes = new[] { Mat.Cutout(Tex.Bush1), Mat.Cutout(Tex.Bush2) };
            for (int i = 0; i < 90; i++)
            {
                float x = rng.Range(-100f, 100f);
                bool south = rng.Chance(0.5f);
                float z = south ? rng.Range(RoadZ0 - 7f, RoadZ0 - 1.6f) : rng.Range(RoadZ1 + 1.4f, FenceZ0 - 1.5f);
                if (!south && ((x > -5f && x < 5f) || (x > -62.5f && x < -49.5f))) continue;
                if ((V(x, 0, z) - V(6.2f, 0, -53.8f)).sqrMagnitude < 4f) continue;
                if (i % 3 == 0) Props.Billboard(fol, bushes[i % 2], V(x, 0, z), rng.Range(1.6f, 2.8f), rng.Range(1.0f, 1.8f), 2, rng.Range(0f, 180f), 0.3f, 0.8f);
                else Props.GrassClump(fol, V(x, 0, z), rng.Range(0.7f, 1.5f), rng.Range(0f, 180f), i);
            }
            ctx.Common("OutsideGate", V(4.2f, 0, -52.6f));
            ctx.BearTrap("OutsideGate", V(3.6f, 0, -49f));
            ctx.WallNote("OutsideGate", V(6.2f, 1.2f, -53.83f), Vector3.forward);
            // nav nodes outside the fence: gravel road, the road itself, the vehicle lane
            // inside the main gate (the barrier-blocked grid nodes are dropped): auto edges link it to the yard and,
            // through the gate line, to the node outside with EdgeMainGate
            ctx.Nav.Add(V(0, 0, -43.8f), "Yard", true);
            ctx.Nav.Add(V(0, 0, -47.5f), "OutsideGate");
            ctx.Nav.Add(V(0, 0, -51.5f), "OutsideGate");
            ctx.Nav.Add(V(-56f, 0, -48f), "OutsideGate");
            ctx.Nav.Add(V(-56f, 0, -52f), "OutsideGate");
            for (float x = -66f; x <= 66f; x += 6f) ctx.Nav.Add(V(x, 0, -59f), "Road");
        }

        /// <summary>
        /// Earth berm along X: rises from the ground at zStart (towards dir = ±Z) over 'up' meters to 'height', then falls
        /// back over 'down' meters. Subdivided every 4 m, with two sloped colliders.
        /// </summary>
        static void Berm(MapContext ctx, MeshBuilder mb, float x0, float x1, float zStart, float dir, float up, float down, float height, Material m)
        {
            float zTop = zStart + dir * up, zEnd = zTop + dir * down;
            int n = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / 4f));
            mb.Material = m;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(x0, x1, (float)i / n), b = Mathf.Lerp(x0, x1, (float)(i + 1) / n);
                float ha = height * (0.8f + 0.2f * Shade.Hash(a, 0, zStart, 71)), hb = height * (0.8f + 0.2f * Shade.Hash(b, 0, zStart, 71));
                var p0 = V(a, 0, zStart); var p1 = V(b, 0, zStart);
                var t0 = V(a, ha, zTop); var t1 = V(b, hb, zTop);
                var e0 = V(a, 0, zEnd); var e1 = V(b, 0, zEnd);
                BermQuad(mb, p0, t0, t1, p1, dir);
                BermQuad(mb, t0, e0, e1, t1, dir);
            }
            mb.Color = Shade.Gray(1f);
            float len = x1 - x0;
            var s1 = V(0, height * 0.9f, zTop - zStart);
            var r1 = Quaternion.LookRotation(s1.normalized, Vector3.up);
            ctx.Solid(V((x0 + x1) * 0.5f, height * 0.45f, (zStart + zTop) * 0.5f) - r1 * Vector3.up * 0.15f, V(len, 0.3f, s1.magnitude), r1, SurfaceType.Grass, "Berm");
            var s2 = V(0, -height * 0.9f, zEnd - zTop);
            var r2 = Quaternion.LookRotation(s2.normalized, Vector3.up);
            ctx.Solid(V((x0 + x1) * 0.5f, height * 0.45f, (zTop + zEnd) * 0.5f) - r2 * Vector3.up * 0.15f, V(len, 0.3f, s2.magnitude), r2, SurfaceType.Grass, "Berm");
        }

        /// <summary>Upward-facing quad given in order (near-left, far-left, far-right, near-right) for either berm direction.</summary>
        static void BermQuad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float dir)
        {
            var n = Vector3.Cross(b - a, c - a).normalized;
            if (n.y < 0) { var t = a; a = d; d = t; t = b; b = c; c = t; n = -n; }
            var ca = Shade.Gray(0.55f + 0.5f * Mathf.Clamp01(a.y)); var cb = Shade.Gray(0.55f + 0.5f * Mathf.Clamp01(b.y));
            var cc = Shade.Gray(0.55f + 0.5f * Mathf.Clamp01(c.y)); var cd = Shade.Gray(0.55f + 0.5f * Mathf.Clamp01(d.y));
            int i0 = mb.AddVertex(a, n, new Vector2(a.x / 3f, a.z / 3f), ca), i1 = mb.AddVertex(b, n, new Vector2(b.x / 3f, b.z / 3f), cb);
            int i2 = mb.AddVertex(c, n, new Vector2(c.x / 3f, c.z / 3f), cc), i3 = mb.AddVertex(d, n, new Vector2(d.x / 3f, d.z / 3f), cd);
            mb.AddTriangle(i0, i1, i2);
            mb.AddTriangle(i0, i2, i3);
        }

        // ================================================================== exterior nav grid

        public static void NavGrid(MapContext ctx)
        {
            for (float z = FenceZ0 + 2.5f; z < FenceZ1; z += 5f)
                for (float x = FenceX0 + 2.5f; x < FenceX1; x += 5f)
                {
                    if (x > -11.3f && x < 11.3f && z > -8.3f && z < 8.3f) continue;   // house footprint (porch / stoop nodes are dropped by validation)
                    ctx.Nav.Add(V(x, 0, z), null);
                }
        }
    }
}
