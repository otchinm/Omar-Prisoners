using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// The sedan: a big, boxy late-70s four door. The working car in the parking lot and the wrecks share the body - real
    /// wheel arches with wheel wells, a greenhouse of pillars and see-through glass, chrome bumpers, trim and lamps, and an
    /// interior (dashboard with its gauges, steering wheel, bench seats, door cards, headliner) for the riders to sit in.
    /// Car-local space: front at +Z, right at +X, the wheels on the ground at y = 0; 4.7 x 1.8 m.
    /// </summary>
    internal static partial class Props
    {
        const float CarHL = 2.35f, CarHW = 0.9f, CarY0 = 0.32f, CarY1 = 0.95f, CarY2 = 1.42f;
        const float CabF = 0.95f, CabR = -1.35f, RoofF = 0.35f, RoofR = -1.0f, CabW = 0.8f, RoofW = 0.7f;
        const float WheelZ = 1.45f, WheelR = 0.33f, WheelX = 0.82f, WheelW = 0.22f, ArchR = 0.42f, CarFloorY = 0.36f;
        const float BPillarF = -0.08f, BPillarR = -0.17f;

        /// <summary>Hood hinge (car-local): the separate hood of the working car hangs here.</summary>
        public static readonly Vector3 CarHoodPivot = new Vector3(0f, 0.955f, CabF);
        /// <summary>Headlight lens centres on the front face (car-local).</summary>
        public static readonly Vector3[] CarHeadlights = { new Vector3(-0.648f, 0.66f, CarHL), new Vector3(0.648f, 0.66f, CarHL) };
        /// <summary>Eye points of the four seats (car-local): driver, front passenger, rear left, rear right.</summary>
        public static readonly Vector3[] CarSeatEyes =
        {
            new Vector3(-0.38f, 1.2f, -0.12f), new Vector3(0.38f, 1.2f, -0.12f), new Vector3(-0.38f, 1.16f, -1.0f), new Vector3(0.38f, 1.16f, -1.0f),
        };

        /// <summary>Car-local centre of the fuel filler: on the left rear quarter panel, between the rear wheel and the tail light.</summary>
        public static readonly Vector3 FuelFillerLocal = new Vector3(-0.9f, 0.76f, -2.0f);
        /// <summary>Car-local position of the screw cap (outside the filler neck).</summary>
        public static Vector3 FuelCapLocal => FuelFillerLocal + new Vector3(-0.045f, 0f, 0f);

        // ------------------------------------------------------------------ materials / helpers

        static Material CarPaint(bool wreck) => Mat.Lit(wreck ? Tex.CarWreck : Tex.CarBody);
        static Material CarChrome(bool wreck) => Mat.Lit(Tex.Galvanized, wreck ? new Color(0.5f, 0.4f, 0.33f) : new Color(0.86f, 0.86f, 0.83f));
        static Material CarUnder() => Mat.Lit(Tex.MetalDark, new Color(0.26f, 0.24f, 0.22f));
        static Material CarRubber() => Mat.Lit(Tex.Plastic, new Color(0.13f, 0.13f, 0.12f));
        static Material CarTrim(bool wreck) => Mat.Lit(Tex.Plastic, wreck ? new Color(0.25f, 0.2f, 0.15f) : new Color(0.46f, 0.36f, 0.27f));
        static Material CarGlassMat(bool wreck) => wreck
            ? Mat.Transparent(Tex.CarGlassBroken, new Color(0.85f, 0.85f, 0.8f, 0.85f))
            : Mat.Transparent(Tex.CarGlass, new Color(0.9f, 0.95f, 1f, 0.68f));

        /// <summary>Quad a, b, c, d (in order round its edge, either winding) with a UV per corner, wound to face <paramref name="want"/>.</summary>
        static void CarQuad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 want)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, want) >= 0f) mb.AddQuad(a, b, c, d, ua, ub, uc, ud);
            else mb.AddQuad(d, c, b, a, ud, uc, ub, ua);
        }

        /// <summary>The same with UVs projected from the position (tiling materials: paint, trim, carpet ...).</summary>
        static void CarQuad(MeshBuilder mb, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 want, float tile = 0.8f)
            => CarQuad(mb, a, b, c, d, PlanarUv(a, want, tile), PlanarUv(b, want, tile), PlanarUv(c, want, tile), PlanarUv(d, want, tile), want);

        static Vector2 PlanarUv(Vector3 p, Vector3 n, float tile)
        {
            float ax = Mathf.Abs(n.x), ay = Mathf.Abs(n.y), az = Mathf.Abs(n.z);
            Vector2 uv = ax >= ay && ax >= az ? new Vector2(p.z, p.y) : ay >= az ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y);
            return uv / Mathf.Max(0.05f, tile);
        }

        /// <summary>Box whose length runs from a to b (w across, h up).</summary>
        static void CarBar(MeshBuilder mb, Vector3 a, Vector3 b, float w, float h, float tile = 0.4f)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
            mb.Push((a + b) * 0.5f, Quaternion.LookRotation(d / len, up));
            mb.AddBox(Vector3.zero, new Vector3(w, h, len), BoxUV.Local, tile);
            mb.Pop();
        }

        /// <summary>Elliptic disc (rx across, ry up) standing out of p along <paramref name="normal"/> by <paramref name="thick"/>.</summary>
        static void CarDisc(MeshBuilder mb, Vector3 p, Vector3 normal, float rx, float ry, float thick, int sides = 12)
        {
            normal.Normalize();
            var look = Quaternion.LookRotation(normal, Mathf.Abs(normal.y) > 0.95f ? Vector3.forward : Vector3.up);
            mb.Push(p, look * Quaternion.Euler(90f, 0f, 0f), new Vector3(rx, 1f, ry));
            mb.AddCylinder(Vector3.zero, 1f, 1f, thick, sides, true, false, null, false);
            mb.Pop();
        }

        /// <summary>Rectangular frame of four bars (lamp bezels), w x h, bars t wide, standing <paramref name="depth"/> deep.</summary>
        static void CarFrame(MeshBuilder mb, Vector3 c, float w, float h, float t, float depth)
        {
            mb.AddBox(c + new Vector3(0f, (h - t) * 0.5f, 0f), new Vector3(w, t, depth), BoxUV.Local, 0.3f);
            mb.AddBox(c - new Vector3(0f, (h - t) * 0.5f, 0f), new Vector3(w, t, depth), BoxUV.Local, 0.3f);
            mb.AddBox(c + new Vector3((w - t) * 0.5f, 0f, 0f), new Vector3(t, h - 2f * t, depth), BoxUV.Local, 0.3f);
            mb.AddBox(c - new Vector3((w - t) * 0.5f, 0f, 0f), new Vector3(t, h - 2f * t, depth), BoxUV.Local, 0.3f);
        }

        /// <summary>Height of the bottom of the body side at z: the rocker, or the wheel arch over a wheel.</summary>
        static float ArchTop(float z)
        {
            float y = CarY0;
            for (int k = -1; k <= 1; k += 2)
            {
                float dz = z - k * WheelZ;
                if (Mathf.Abs(dz) < ArchR) y = Mathf.Max(y, WheelR + Mathf.Sqrt(ArchR * ArchR - dz * dz));
            }
            return y;
        }

        /// <summary>Stations along the body (z) the side panels are cut at: evenly spaced, dense round the arches.</summary>
        static List<float> CarStations()
        {
            var zs = new List<float>();
            for (int i = 0; i <= 20; i++) zs.Add(-CarHL + 2f * CarHL * i / 20f);
            for (int k = -1; k <= 1; k += 2)
                for (int i = 0; i <= 12; i++) zs.Add(k * WheelZ + ArchR * Mathf.Cos(i * Mathf.PI / 12f));
            zs.Sort();
            var o = new List<float>();
            foreach (float z in zs) if (o.Count == 0 || z - o[o.Count - 1] > 0.012f) o.Add(z);
            return o;
        }

        // ------------------------------------------------------------------ the body

        /// <summary>
        /// Sedan in car-local space (front at +Z, wheels on the ground at y = 0), 4.7 x 1.8 m. The hood is left off when
        /// <paramref name="hood"/> is false (the working car's hood is a separate mesh on <see cref="CarHoodPivot"/>): the
        /// engine bay is built instead. Wrecks are rusty, some glass and wheels missing.
        /// </summary>
        public static void CarShell(MeshBuilder mb, bool wreck, bool hood, int seed)
        {
            var rng = new DeterministicRandom(seed, 67);
            var zs = CarStations();
            Gray(mb, 0.82f);
            CarBodySides(mb, Mat.Lit(Tex.CarSide, wreck ? new Color(0.65f, 0.5f, 0.42f) : Color.white), zs);
            CarWheelWells(mb, wreck);
            CarEnds(mb, wreck);
            CarDecks(mb, wreck, hood);
            Gray(mb, 0.82f);
            CarGreenhouse(mb, wreck, rng);
            CarSideTrim(mb, wreck, zs);
            CarUnderbody(mb, wreck, hood);
            Gray(mb, 0.85f);
            CarInterior(mb, wreck);
            // wheels (the working car has chrome hubcaps, the wrecks show their rusty rims)
            Gray(mb, 0.75f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    if (wreck && rng.Chance(0.35f)) continue;
                    var c = new Vector3(sx * WheelX, WheelR, sz * WheelZ);
                    Wheel(mb, c, WheelR, WheelW, 14);
                    if (wreck) continue;
                    var n = new Vector3(sx, 0f, 0f);
                    mb.Material = CarChrome(false);
                    CarDisc(mb, c + n * (WheelW * 0.5f), n, 0.16f, 0.16f, 0.02f, 12);
                    mb.Material = CarUnder();
                    CarDisc(mb, c + n * (WheelW * 0.5f + 0.02f), n, 0.045f, 0.045f, 0.012f, 8);
                }
            Gray(mb, 1f);
        }

        static Vector2 SideUv(float z, float y) => new Vector2((CarHL - z) / (2f * CarHL), (y - CarY0) / (CarY1 - CarY0));

        /// <summary>Body sides from the rocker to the beltline, cut round the wheel arches (car_side: u from the front).</summary>
        static void CarBodySides(MeshBuilder mb, Material side, List<float> zs)
        {
            mb.Material = side;
            float ym = (CarY0 + CarY1) * 0.5f;
            for (int si = -1; si <= 1; si += 2)
            {
                float x = si * CarHW;
                var want = new Vector3(si, 0f, 0f);
                for (int i = 0; i + 1 < zs.Count; i++)
                {
                    float zA = zs[i], zB = zs[i + 1], bA = ArchTop(zA), bB = ArchTop(zB);
                    float mA = Mathf.Max(bA, ym), mB = Mathf.Max(bB, ym);
                    if (mA - bA > 1e-4f || mB - bB > 1e-4f)
                        CarQuad(mb, new Vector3(x, bA, zA), new Vector3(x, mA, zA), new Vector3(x, mB, zB), new Vector3(x, bB, zB),
                            SideUv(zA, bA), SideUv(zA, mA), SideUv(zB, mB), SideUv(zB, bB), want);
                    CarQuad(mb, new Vector3(x, mA, zA), new Vector3(x, CarY1, zA), new Vector3(x, CarY1, zB), new Vector3(x, mB, zB),
                        SideUv(zA, mA), SideUv(zA, CarY1), SideUv(zB, CarY1), SideUv(zB, mB), want);
                }
            }
        }

        /// <summary>Inside of each arch (the dark wheel well up to an inner wall) and the chrome moulding round its edge.</summary>
        static void CarWheelWells(MeshBuilder mb, bool wreck)
        {
            var well = Mat.Lit(Tex.MetalDark, new Color(0.17f, 0.15f, 0.13f));
            var lip = CarChrome(wreck);
            const int N = 12;
            for (int si = -1; si <= 1; si += 2)
                for (int k = -1; k <= 1; k += 2)
                {
                    float zw = k * WheelZ, xo = si * (CarHW - 0.004f), xi = si * (CarHW - 0.3f), xl = si * (CarHW + 0.006f), r2 = ArchR + 0.035f;
                    for (int i = 0; i < N; i++)
                    {
                        float a0 = Mathf.PI * i / N, a1 = Mathf.PI * (i + 1) / N;
                        float z0 = zw + ArchR * Mathf.Cos(a0), ya = WheelR + ArchR * Mathf.Sin(a0);
                        float z1 = zw + ArchR * Mathf.Cos(a1), yb = WheelR + ArchR * Mathf.Sin(a1);
                        var toCentre = new Vector3(0f, WheelR - (ya + yb) * 0.5f, zw - (z0 + z1) * 0.5f);
                        mb.Material = well;
                        CarQuad(mb, new Vector3(xo, ya, z0), new Vector3(xi, ya, z0), new Vector3(xi, yb, z1), new Vector3(xo, yb, z1), toCentre, 0.5f);
                        CarQuad(mb, new Vector3(xi, CarY0 - 0.05f, z0), new Vector3(xi, ya, z0), new Vector3(xi, yb, z1), new Vector3(xi, CarY0 - 0.05f, z1),
                            new Vector3(si, 0f, 0f), 0.5f);
                        mb.Material = lip;
                        CarQuad(mb, new Vector3(xl, ya, z0), new Vector3(xl, WheelR + r2 * Mathf.Sin(a0), zw + r2 * Mathf.Cos(a0)),
                            new Vector3(xl, WheelR + r2 * Mathf.Sin(a1), zw + r2 * Mathf.Cos(a1)), new Vector3(xl, yb, z1), new Vector3(si, 0f, 0f), 0.3f);
                    }
                }
        }

        /// <summary>Front (grille, lamps) and rear (tail lights, trunk, plate) faces with the 3D lamps, bumpers and plate.</summary>
        static void CarEnds(MeshBuilder mb, bool wreck)
        {
            var tint = wreck ? new Color(0.6f, 0.5f, 0.45f) : Color.white;
            var frontMat = Mat.Lit(Tex.CarFront, tint);
            mb.Material = frontMat;
            mb.AddQuad(new Vector3(CarHW, CarY0, CarHL), new Vector3(CarHW, CarY1, CarHL), new Vector3(-CarHW, CarY1, CarHL), new Vector3(-CarHW, CarY0, CarHL));
            mb.Material = Mat.Lit(Tex.CarRear, tint);
            mb.AddQuad(new Vector3(-CarHW, CarY0, -CarHL), new Vector3(-CarHW, CarY1, -CarHL), new Vector3(CarHW, CarY1, -CarHL), new Vector3(CarHW, CarY0, -CarHL));
            var chrome = CarChrome(wreck);
            // chrome bezels round the headlights
            mb.Material = chrome;
            foreach (var h in CarHeadlights) CarFrame(mb, h + new Vector3(0f, 0f, 0.012f), 0.42f, 0.23f, 0.022f, 0.024f);
            // tail lights: red lenses over the white reversing lamps, in chrome surrounds
            for (int k = -1; k <= 1; k += 2)
            {
                var c = new Vector3(k * 0.666f, 0.667f, -CarHL);
                mb.Material = chrome;
                CarFrame(mb, c + new Vector3(0f, 0f, -0.012f), 0.42f, 0.21f, 0.018f, 0.024f);
                mb.Material = Mat.Lit(Tex.Plastic, wreck ? new Color(0.4f, 0.06f, 0.05f) : new Color(0.72f, 0.1f, 0.07f));
                mb.AddBox(c + new Vector3(0f, 0.025f, -0.012f), new Vector3(0.38f, 0.13f, 0.024f), BoxUV.Local, 0.3f);
                mb.Material = Mat.Lit(Tex.Plastic, new Color(0.75f, 0.72f, 0.66f));
                mb.AddBox(c + new Vector3(0f, -0.065f, -0.012f), new Vector3(0.38f, 0.045f, 0.024f), BoxUV.Local, 0.3f);
            }
            CarBumper(mb, 1f, wreck);
            CarBumper(mb, -1f, wreck);
            // front plate bolted to the bumper (the plate drawn on car_front, which the bumper now covers)
            mb.Material = frontMat;
            float pz = CarHL + 0.08f + 0.049f;
            CarQuad(mb, new Vector3(0.17f, 0.4f, pz), new Vector3(0.17f, 0.52f, pz), new Vector3(-0.17f, 0.52f, pz), new Vector3(-0.17f, 0.4f, pz),
                new Vector2(0.4f, 0.14f), new Vector2(0.4f, 0.32f), new Vector2(0.6f, 0.32f), new Vector2(0.6f, 0.14f), Vector3.forward);
            // amber turn signals in the front bumper
            mb.Material = Mat.Lit(Tex.Plastic, wreck ? new Color(0.45f, 0.3f, 0.1f) : new Color(0.92f, 0.55f, 0.15f));
            for (int k = -1; k <= 1; k += 2) mb.AddBox(new Vector3(k * 0.58f, 0.46f, CarHL + 0.08f + 0.048f), new Vector3(0.14f, 0.05f, 0.008f), BoxUV.Local, 0.2f);
        }

        /// <summary>Chrome bumper wrapping round the corners, rubber guards, the dark valance under it. s = +1 front, -1 rear.</summary>
        static void CarBumper(MeshBuilder mb, float s, bool wreck)
        {
            const float y = 0.46f, h = 0.13f, d = 0.09f;
            float zf = s * (CarHL + 0.08f);
            mb.Material = CarChrome(wreck);
            mb.AddBox(new Vector3(0f, y, zf), new Vector3(1.66f, h, d), BoxUV.Local, 0.5f);
            for (int k = -1; k <= 1; k += 2)
                CarBar(mb, new Vector3(k * 0.82f, y, zf), new Vector3(k * 0.95f, y, s * (CarHL - 0.22f)), d, h, 0.5f);
            mb.Material = CarRubber();
            for (int k = -1; k <= 1; k += 2)
                mb.AddBox(new Vector3(k * 0.36f, y - 0.005f, zf + s * 0.055f), new Vector3(0.07f, h + 0.03f, 0.03f), BoxUV.Local, 0.3f);
            mb.Material = CarUnder();
            mb.AddBox(new Vector3(0f, 0.36f, s * (CarHL + 0.03f)), new Vector3(1.7f, 0.07f, 0.06f), BoxUV.Local, 0.5f);
            for (int k = -1; k <= 1; k += 2)
                mb.AddBox(new Vector3(k * 0.55f, y - 0.03f, s * (CarHL + 0.02f)), new Vector3(0.05f, 0.05f, 0.08f), BoxUV.Local, 0.3f);
        }

        /// <summary>Trunk lid, the decks beside the cabin, and the hood (or the engine bay when the hood is a separate mesh).</summary>
        static void CarDecks(MeshBuilder mb, bool wreck, bool hood)
        {
            mb.Material = CarPaint(wreck);
            CarQuad(mb, new Vector3(-CarHW, CarY1, -CarHL), new Vector3(-CarHW, CarY1, CabR), new Vector3(CarHW, CarY1, CabR), new Vector3(CarHW, CarY1, -CarHL), Vector3.up, 1.2f);
            for (int si = -1; si <= 1; si += 2)
                CarQuad(mb, new Vector3(si * CabW, CarY1, CabR), new Vector3(si * CabW, CarY1, CabF), new Vector3(si * CarHW, CarY1, CabF),
                    new Vector3(si * CarHW, CarY1, CabR), Vector3.up, 1.2f);
            // the trunk lid's seams
            mb.Material = CarUnder();
            float ys = CarY1 + 0.002f, x0 = 0.84f, zr = -CarHL + 0.05f, zf = CabR - 0.07f, w = 0.012f;
            CarQuad(mb, new Vector3(-x0, ys, zr), new Vector3(-x0, ys, zr + w), new Vector3(x0, ys, zr + w), new Vector3(x0, ys, zr), Vector3.up, 0.5f);
            CarQuad(mb, new Vector3(-x0, ys, zf - w), new Vector3(-x0, ys, zf), new Vector3(x0, ys, zf), new Vector3(x0, ys, zf - w), Vector3.up, 0.5f);
            for (int si = -1; si <= 1; si += 2)
                CarQuad(mb, new Vector3(si * x0 - w * 0.5f, ys, zr), new Vector3(si * x0 - w * 0.5f, ys, zf), new Vector3(si * x0 + w * 0.5f, ys, zf),
                    new Vector3(si * x0 + w * 0.5f, ys, zr), Vector3.up, 0.5f);
            if (hood)
            {
                mb.Push(CarHoodPivot, Quaternion.identity);
                CarHood(mb, wreck);
                mb.Pop();
            }
            else CarEngineBay(mb);
        }

        /// <summary>Hood panel in hood-pivot space (pivot at the windshield base, the hood runs forward along +Z).</summary>
        public static void CarHood(MeshBuilder mb, bool wreck)
        {
            Gray(mb, 0.82f);
            mb.Material = CarPaint(wreck);
            mb.AddBox(new Vector3(0f, -0.015f, 0.7f), new Vector3(1.78f, 0.03f, 1.4f), BoxUV.Local, 1.2f, 0f, BoxFaces.All & ~BoxFaces.NegY);
            // the raised centre section running forward to the grille
            mb.AddBox(new Vector3(0f, 0.007f, 0.72f), new Vector3(0.6f, 0.014f, 1.3f), BoxUV.Local, 1.2f, 0f, BoxFaces.All & ~BoxFaces.NegY);
            // underside: bare metal and the insulation pad
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.36f, 0.34f, 0.3f));
            mb.AddBox(new Vector3(0f, -0.015f, 0.7f), new Vector3(1.78f, 0.03f, 1.4f), BoxUV.Local, 0.8f, 0f, BoxFaces.NegY);
            mb.Material = Mat.Lit(Tex.Cloth, new Color(0.24f, 0.22f, 0.19f));
            CarQuad(mb, new Vector3(-0.7f, -0.032f, 0.15f), new Vector3(-0.7f, -0.032f, 1.25f), new Vector3(0.7f, -0.032f, 1.25f), new Vector3(0.7f, -0.032f, 0.15f), Vector3.down, 0.5f);
            // hood ornament
            mb.Material = CarChrome(wreck);
            mb.AddBox(new Vector3(0f, 0.03f, 1.34f), new Vector3(0.018f, 0.03f, 0.07f), BoxUV.Local, 0.2f);
            mb.AddBox(new Vector3(0f, 0.016f, 1.32f), new Vector3(0.05f, 0.006f, 0.1f), BoxUV.Local, 0.2f);
            Gray(mb, 1f);
        }

        /// <summary>Engine bay under the working car's hood: inner fenders, radiator, firewall, the engine, battery, hoses.</summary>
        static void CarEngineBay(MeshBuilder mb)
        {
            float yb = CarY1 - 0.27f, xb = CarHW - 0.015f, zb = CarHL - 0.02f;
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.3f, 0.28f, 0.26f));
            CarQuad(mb, new Vector3(-xb, yb, CabF), new Vector3(-xb, yb, zb), new Vector3(xb, yb, zb), new Vector3(xb, yb, CabF), Vector3.up, 0.6f);
            for (int si = -1; si <= 1; si += 2)
                CarQuad(mb, new Vector3(si * xb, yb, CabF), new Vector3(si * xb, CarY1, CabF), new Vector3(si * xb, CarY1, zb), new Vector3(si * xb, yb, zb),
                    new Vector3(-si, 0f, 0f), 0.6f);
            CarQuad(mb, new Vector3(-xb, yb, CabF + 0.004f), new Vector3(-xb, CarY1, CabF + 0.004f), new Vector3(xb, CarY1, CabF + 0.004f), new Vector3(xb, yb, CabF + 0.004f),
                Vector3.forward, 0.6f);
            mb.AddBox(new Vector3(0f, CarY1 - 0.025f, CarHL - 0.06f), new Vector3(1.76f, 0.04f, 0.07f), BoxUV.Local, 0.4f);
            // radiator across the front
            mb.Material = Mat.Lit(Tex.Radiator, new Color(0.38f, 0.36f, 0.32f));
            CarQuad(mb, new Vector3(-0.7f, yb, CarHL - 0.03f), new Vector3(-0.7f, CarY1 - 0.045f, CarHL - 0.03f), new Vector3(0.7f, CarY1 - 0.045f, CarHL - 0.03f),
                new Vector3(0.7f, yb, CarHL - 0.03f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.back);
            // the engine (valve cover / air cleaner on top) and the battery, low enough to clear the closed hood
            var engine = Mat.Lit(Tex.CarEngine);
            mb.Material = engine;
            var blockSides = new Rect(0.01f, 0.02f, 0.06f, 0.12f);
            mb.AddBox(new Vector3(0.1f, yb + 0.11f, 1.6f), new Vector3(0.7f, 0.22f, 0.5f),
                new BoxUVRects { PosY = new Rect(0f, 0f, 0.5f, 1f), PosX = blockSides, NegX = blockSides, PosZ = blockSides, NegZ = blockSides, NegY = blockSides });
            var batSide = new Rect(0.5f, 0f, 0.5f, 0.5f);
            mb.AddBox(new Vector3(-0.55f, yb + 0.1f, 1.2f), new Vector3(0.3f, 0.2f, 0.22f),
                new BoxUVRects { PosY = new Rect(0.5f, 0.5f, 0.5f, 0.5f), PosX = batSide, NegX = batSide, PosZ = batSide, NegZ = batSide, NegY = batSide });
            // hoses, the wiring from the battery, the coolant bottle, the brake master cylinder
            mb.Material = CarRubber();
            CarBar(mb, new Vector3(0.1f, yb + 0.17f, 1.86f), new Vector3(0.1f, yb + 0.17f, CarHL - 0.04f), 0.05f, 0.05f);
            CarBar(mb, new Vector3(-0.2f, yb + 0.07f, 1.86f), new Vector3(-0.28f, yb + 0.07f, CarHL - 0.04f), 0.05f, 0.05f);
            CarBar(mb, new Vector3(-0.55f, yb + 0.21f, 1.25f), new Vector3(-0.22f, yb + 0.2f, 1.45f), 0.016f, 0.016f);
            CarBar(mb, new Vector3(-0.62f, yb + 0.21f, 1.2f), new Vector3(-0.8f, yb + 0.12f, 1.05f), 0.016f, 0.016f);
            mb.Material = Mat.Lit(Tex.Plastic, new Color(0.85f, 0.82f, 0.68f));
            mb.AddBox(new Vector3(0.66f, yb + 0.09f, 2.0f), new Vector3(0.12f, 0.16f, 0.18f), BoxUV.Local, 0.3f);
            mb.Material = CarUnder();
            mb.AddBox(new Vector3(-0.5f, yb + 0.09f, 1.02f), new Vector3(0.12f, 0.08f, 0.1f), BoxUV.Local, 0.3f);
            mb.Material = Mat.Lit(Tex.Plastic, new Color(0.7f, 0.66f, 0.55f));
            mb.AddBox(new Vector3(-0.5f, yb + 0.15f, 1.02f), new Vector3(0.08f, 0.05f, 0.06f), BoxUV.Local, 0.3f);
        }

        static Vector3 CabPt(float s, float z, float t) => new Vector3(s * Mathf.Lerp(CabW, RoofW, t), Mathf.Lerp(CarY1, CarY2, t), z);
        static float CabFz(float t) => Mathf.Lerp(CabF, RoofF, t);
        static float CabRz(float t) => Mathf.Lerp(CabR, RoofR, t);

        /// <summary>Panel of a cabin side between two edges (each a z that may slant with the height t), rows t0..t1.</summary>
        static void CabPanel(MeshBuilder mb, float s, System.Func<float, float> zA, System.Func<float, float> zB, float t0, float t1, Vector3 want, bool glassUv)
        {
            Vector3 a = CabPt(s, zA(t0), t0), b = CabPt(s, zA(t1), t1), c = CabPt(s, zB(t1), t1), d = CabPt(s, zB(t0), t0);
            if (glassUv) CarQuad(mb, a, b, c, d, new Vector2(a.z, t0), new Vector2(b.z, t1), new Vector2(c.z, t1), new Vector2(d.z, t0), want);
            else CarQuad(mb, a, b, c, d, want, 0.8f);
        }

        /// <summary>A point on the windshield (front) or back window plane: u across (0 = left), t up.</summary>
        static Vector3 ScreenPt(bool front, float u, float t)
        {
            float half = Mathf.Lerp(CabW, RoofW, t);
            return new Vector3(Mathf.Lerp(-half, half, u), Mathf.Lerp(CarY1, CarY2, t), front ? CabFz(t) : CabRz(t));
        }

        static void ScreenPanel(MeshBuilder mb, bool front, float u0, float u1, float t0, float t1, Vector3 want, bool glassUv)
        {
            Vector3 a = ScreenPt(front, u0, t0), b = ScreenPt(front, u0, t1), c = ScreenPt(front, u1, t1), d = ScreenPt(front, u1, t0);
            if (glassUv) CarQuad(mb, a, b, c, d, new Vector2(u0 * 1.6f, t0), new Vector2(u0 * 1.6f, t1), new Vector2(u1 * 1.6f, t1), new Vector2(u1 * 1.6f, t0), want);
            else CarQuad(mb, a, b, c, d, want, 0.8f);
        }

        /// <summary>
        /// The greenhouse: chrome belt line, A / B / C pillars and the roof rail on the sides with the door windows between
        /// them, windshield and back window in painted frames with a rubber seal, wipers, the roof with its drip rails. Every
        /// painted piece has an interior trim face inside; the glass is see-through from both sides.
        /// </summary>
        static void CarGreenhouse(MeshBuilder mb, bool wreck, DeterministicRandom rng)
        {
            var paint = CarPaint(wreck);
            var chrome = CarChrome(wreck);
            var rubber = CarRubber();
            var trim = CarTrim(wreck);
            var glass = CarGlassMat(wreck);
            System.Func<float, float> rear = CabRz, front = CabFz;
            System.Func<float, float> cEdge = t => CabRz(t) + Mathf.Lerp(0.34f, 0.2f, t);
            System.Func<float, float> aEdge = t => CabFz(t) - 0.07f;
            System.Func<float, float> bR = t => BPillarR, bF = t => BPillarF;
            const float tw0 = 0.06f, tw1 = 0.9f;
            for (int si = -1; si <= 1; si += 2)
            {
                float s = si;
                Vector3 outN = new Vector3(s * (CarY2 - CarY1), CabW - RoofW, 0f).normalized;
                void Both(Material outside, System.Func<float, float> zA, System.Func<float, float> zB, float t0, float t1)
                {
                    mb.Material = outside;
                    CabPanel(mb, s, zA, zB, t0, t1, outN, false);
                    mb.Material = trim;
                    mb.Push(-outN * 0.012f);
                    CabPanel(mb, s, zA, zB, t0, t1, -outN, false);
                    mb.Pop();
                }
                Both(chrome, rear, front, 0f, tw0);     // chrome belt line under the windows
                Both(paint, rear, cEdge, tw0, tw1);     // C pillar
                Both(paint, bR, bF, tw0, tw1);          // B pillar
                Both(paint, aEdge, front, tw0, tw1);    // A pillar
                Both(paint, rear, front, tw1, 1f);      // roof rail
                mb.Material = glass;
                if (!wreck || !rng.Chance(0.4f)) CabPanel(mb, s, cEdge, bR, tw0, tw1, outN, true);   // rear door window
                if (!wreck || !rng.Chance(0.4f)) CabPanel(mb, s, bF, aEdge, tw0, tw1, outN, true);   // front door window
            }
            // windshield / back window: glass in a painted frame, rubber seal along the bottom
            for (int f = 0; f < 2; f++)
            {
                bool fr = f == 0;
                Vector3 n = fr ? new Vector3(0f, CabF - RoofF, CarY2 - CarY1).normalized : new Vector3(0f, RoofR - CabR, -(CarY2 - CarY1)).normalized;
                void Frame(Material m, float u0, float u1, float t0, float t1)
                {
                    mb.Material = m;
                    ScreenPanel(mb, fr, u0, u1, t0, t1, n, false);
                    mb.Material = trim;
                    mb.Push(-n * 0.012f);
                    ScreenPanel(mb, fr, u0, u1, t0, t1, -n, false);
                    mb.Pop();
                }
                Frame(paint, 0f, 0.05f, 0f, 1f);
                Frame(paint, 0.95f, 1f, 0f, 1f);
                Frame(paint, 0.05f, 0.95f, 0.94f, 1f);
                Frame(rubber, 0.05f, 0.95f, 0f, 0.035f);
                mb.Material = glass;
                if (!wreck || !rng.Chance(0.3f)) ScreenPanel(mb, fr, 0.05f, 0.95f, 0.035f, 0.94f, n, true);
            }
            // wipers parked on the windshield
            mb.Material = rubber;
            Vector3 wn = new Vector3(0f, CabF - RoofF, CarY2 - CarY1).normalized * 0.014f;
            CarBar(mb, ScreenPt(true, 0.14f, 0.07f) + wn, ScreenPt(true, 0.47f, 0.05f) + wn, 0.018f, 0.012f);
            CarBar(mb, ScreenPt(true, 0.53f, 0.07f) + wn, ScreenPt(true, 0.86f, 0.05f) + wn, 0.018f, 0.012f);
            // roof and its drip rails
            mb.Material = paint;
            CarQuad(mb, new Vector3(-RoofW, CarY2, RoofR), new Vector3(-RoofW, CarY2, RoofF), new Vector3(RoofW, CarY2, RoofF), new Vector3(RoofW, CarY2, RoofR), Vector3.up, 1.2f);
            mb.Material = chrome;
            for (int si = -1; si <= 1; si += 2)
                CarBar(mb, new Vector3(si * RoofW, CarY2 + 0.004f, RoofR + 0.02f), new Vector3(si * RoofW, CarY2 + 0.004f, RoofF - 0.02f), 0.016f, 0.014f);
        }

        /// <summary>Chrome side mouldings, rocker strips, door handles, the driver's mirror, the antenna, marker lamps, mud flaps.</summary>
        static void CarSideTrim(MeshBuilder mb, bool wreck, List<float> zs)
        {
            var chrome = CarChrome(wreck);
            mb.Material = chrome;
            float zArch = WheelZ - ArchR;
            for (int si = -1; si <= 1; si += 2)
            {
                float x = si * (CarHW + 0.006f);
                var want = new Vector3(si, 0f, 0f);
                for (int i = 0; i + 1 < zs.Count; i++)
                {
                    float zA = zs[i], zB = zs[i + 1];
                    if (ArchTop(zA) < 0.6f && ArchTop(zB) < 0.6f)
                        CarQuad(mb, new Vector3(x, 0.6f, zA), new Vector3(x, 0.625f, zA), new Vector3(x, 0.625f, zB), new Vector3(x, 0.6f, zB), want, 0.3f);
                }
                CarQuad(mb, new Vector3(x, 0.335f, -zArch), new Vector3(x, 0.365f, -zArch), new Vector3(x, 0.365f, zArch), new Vector3(x, 0.335f, zArch), want, 0.3f);
                foreach (float zh in new[] { 0.02f, -1.08f })
                    mb.AddBox(new Vector3(si * (CarHW + 0.012f), 0.86f, zh), new Vector3(0.02f, 0.022f, 0.11f), BoxUV.Local, 0.2f);
            }
            // the driver's door mirror
            CarBar(mb, new Vector3(-0.85f, 0.96f, 0.8f), new Vector3(-0.98f, 1.0f, 0.78f), 0.02f, 0.02f);
            mb.AddBox(new Vector3(-1.0f, 1.03f, 0.76f), new Vector3(0.13f, 0.085f, 0.05f), BoxUV.Local, 0.2f);
            mb.Material = Mat.Lit(Tex.Mirror);
            CarQuad(mb, new Vector3(-1.055f, 0.995f, 0.734f), new Vector3(-1.055f, 1.065f, 0.734f), new Vector3(-0.945f, 1.065f, 0.734f), new Vector3(-0.945f, 0.995f, 0.734f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.back);
            // the radio antenna on the right front fender
            mb.Material = chrome;
            mb.AddBox(new Vector3(0.74f, CarY1 + 0.01f, 1.95f), new Vector3(0.03f, 0.02f, 0.03f), BoxUV.Local, 0.2f);
            CarBar(mb, new Vector3(0.74f, CarY1, 1.95f), new Vector3(0.7f, 1.75f, 1.86f), 0.006f, 0.006f);
            // side marker lamps (amber at the front corners, red at the back)
            for (int si = -1; si <= 1; si += 2)
            {
                float x = si * (CarHW + 0.004f);
                var want = new Vector3(si, 0f, 0f);
                mb.Material = Mat.Lit(Tex.Plastic, wreck ? new Color(0.45f, 0.3f, 0.1f) : new Color(0.92f, 0.55f, 0.15f));
                CarQuad(mb, new Vector3(x, 0.6f, 2.07f), new Vector3(x, 0.645f, 2.07f), new Vector3(x, 0.645f, 2.17f), new Vector3(x, 0.6f, 2.17f), want, 0.2f);
                mb.Material = Mat.Lit(Tex.Plastic, wreck ? new Color(0.4f, 0.06f, 0.05f) : new Color(0.72f, 0.1f, 0.07f));
                CarQuad(mb, new Vector3(x, 0.6f, -2.2f), new Vector3(x, 0.645f, -2.2f), new Vector3(x, 0.645f, -2.1f), new Vector3(x, 0.6f, -2.1f), want, 0.2f);
                // mud flap behind the rear wheel
                mb.Material = CarRubber();
                mb.AddBox(new Vector3(si * WheelX, 0.22f, -(WheelZ + ArchR + 0.02f)), new Vector3(0.22f, 0.27f, 0.012f), BoxUV.Local, 0.3f);
            }
        }

        /// <summary>Underside, exhaust (pipe, muffler, tailpipe), fuel tank, and the hidden cores that keep the vertex-snapping
        /// seams from showing the sky (kept out of the wheel wells).</summary>
        static void CarUnderbody(MeshBuilder mb, bool wreck, bool hood)
        {
            var under = CarUnder();
            mb.Material = under;
            CarQuad(mb, new Vector3(-CarHW, CarY0, CarHL), new Vector3(-CarHW, CarY0, -CarHL), new Vector3(CarHW, CarY0, -CarHL), new Vector3(CarHW, CarY0, CarHL), Vector3.down, 0.8f);
            CarBar(mb, new Vector3(0.35f, 0.22f, 1.2f), new Vector3(0.35f, 0.22f, -1.9f), 0.06f, 0.06f);
            mb.AddBox(new Vector3(0.35f, 0.22f, -0.7f), new Vector3(0.22f, 0.14f, 0.6f), BoxUV.Local, 0.4f);
            mb.AddBox(new Vector3(0f, 0.24f, -1.95f), new Vector3(1.0f, 0.16f, 0.5f), BoxUV.Local, 0.4f);
            mb.Material = Mat.Lit(Tex.MetalRusty, new Color(0.6f, 0.55f, 0.5f));
            CarBar(mb, new Vector3(0.35f, 0.22f, -1.9f), new Vector3(0.5f, 0.25f, -2.47f), 0.055f, 0.055f);
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.06f, 0.05f, 0.05f));
            CarDisc(mb, new Vector3(0.5f, 0.25f, -2.47f), Vector3.back, 0.022f, 0.022f, 0.003f, 8);
            // cores
            mb.Material = CarPaint(wreck);
            float zA = WheelZ - ArchR, zB = WheelZ + ArchR, wide = CarHW - 0.02f, narrow = CarHW - 0.31f;
            float topF = hood ? CarY1 - 0.02f : CarY1 - 0.275f, topR = CarY1 - 0.02f, topC = CarFloorY - 0.005f;
            void Core(float z0, float z1, float top, float halfX)
            {
                if (z1 - z0 < 0.01f || top - CarY0 < 0.01f) return;
                mb.AddBox(new Vector3(0f, (CarY0 + top) * 0.5f, (z0 + z1) * 0.5f), new Vector3(2f * halfX, top - CarY0 - 0.005f, z1 - z0), BoxUV.Local, 1.2f);
            }
            Core(-CarHL + 0.02f, -zB, topR, wide);          // trunk, behind the rear wheels
            Core(-zB, CabR, topR, narrow);                  // between the rear wheel wells, under the trunk
            Core(CabR, -zA, topC, narrow);                  // between the rear wheel wells, under the cabin floor
            Core(-zA, CabF, topC, wide);                    // under the cabin floor
            Core(CabF, zA, topF, wide);                     // front of the cabin to the front wheels
            Core(zA, zB, topF, narrow);                     // between the front wheel wells
            Core(zB, CarHL - 0.02f, topF, wide);            // ahead of the front wheels
        }

        /// <summary>
        /// The interior: carpet and transmission tunnel, door cards with arm rests, dashboard (gauges, radio, glove box) under
        /// a padded top, the steering wheel on its column, front and rear bench seats, parcel shelf, headliner, sun visors
        /// and the rear-view mirror. Wrecks get the same, dirtier.
        /// </summary>
        static void CarInterior(MeshBuilder mb, bool wreck)
        {
            float k = wreck ? 0.55f : 1f;
            var trim = CarTrim(wreck);
            var dash = Mat.Lit(Tex.Plastic, new Color(0.2f * k, 0.17f * k, 0.15f * k));
            var seat = Mat.Lit(Tex.Cloth, new Color(0.62f * k, 0.44f * k, 0.32f * k));   // worn brown upholstery
            var carpet = Mat.Lit(Tex.Carpet, new Color(0.3f * k, 0.25f * k, 0.21f * k));
            var chrome = CarChrome(wreck);
            float xi = CarHW - 0.03f;
            // floor and the transmission tunnel
            mb.Material = carpet;
            CarQuad(mb, new Vector3(-xi, CarFloorY, CabR), new Vector3(-xi, CarFloorY, CabF), new Vector3(xi, CarFloorY, CabF), new Vector3(xi, CarFloorY, CabR), Vector3.up, 0.6f);
            mb.AddBox(new Vector3(0f, CarFloorY + 0.05f, 0.15f), new Vector3(0.26f, 0.1f, 1.4f), BoxUV.Local, 0.6f, 0f, BoxFaces.All & ~BoxFaces.NegY);
            // door cards, arm rests, window cranks
            for (int si = -1; si <= 1; si += 2)
            {
                mb.Material = trim;
                CarQuad(mb, new Vector3(si * xi, CarFloorY, CabR), new Vector3(si * xi, CarY1, CabR), new Vector3(si * xi, CarY1, CabF), new Vector3(si * xi, CarFloorY, CabF),
                    new Vector3(-si, 0f, 0f), 0.6f);
                mb.AddBox(new Vector3(si * (xi - 0.035f), 0.7f, -0.05f), new Vector3(0.07f, 0.05f, 0.5f), BoxUV.Local, 0.4f);
                mb.AddBox(new Vector3(si * (xi - 0.035f), 0.7f, -1.0f), new Vector3(0.07f, 0.05f, 0.4f), BoxUV.Local, 0.4f);
                mb.Material = chrome;
                mb.AddBox(new Vector3(si * (xi - 0.02f), 0.8f, 0.15f), new Vector3(0.02f, 0.06f, 0.02f), BoxUV.Local, 0.2f);
                mb.AddBox(new Vector3(si * (xi - 0.02f), 0.8f, -0.85f), new Vector3(0.02f, 0.06f, 0.02f), BoxUV.Local, 0.2f);
            }
            // footwell firewall, the bulkhead behind the rear seat, the parcel shelf
            mb.Material = trim;
            CarQuad(mb, new Vector3(-xi, CarFloorY, CabF - 0.005f), new Vector3(-xi, CarY1, CabF - 0.005f), new Vector3(xi, CarY1, CabF - 0.005f), new Vector3(xi, CarFloorY, CabF - 0.005f),
                Vector3.back, 0.6f);
            CarQuad(mb, new Vector3(-xi, CarFloorY, CabR + 0.005f), new Vector3(-xi, CarY1, CabR + 0.005f), new Vector3(xi, CarY1, CabR + 0.005f), new Vector3(xi, CarFloorY, CabR + 0.005f),
                Vector3.forward, 0.6f);
            mb.Material = dash;
            mb.AddBox(new Vector3(0f, 0.962f, -1.29f), new Vector3(2f * xi, 0.02f, 0.12f), BoxUV.Local, 0.5f);
            // dashboard: the face with the gauges, radio and glove box towards the seats, a padded top under the windshield
            const float dz0 = 0.6f, dz1 = 0.92f, dy0 = 0.745f, dy1 = 0.965f;
            mb.AddBox(new Vector3(0f, (dy0 + dy1) * 0.5f, (dz0 + dz1) * 0.5f), new Vector3(2f * xi, dy1 - dy0, dz1 - dz0), BoxUV.Local, 0.5f, 0f, BoxFaces.All & ~BoxFaces.NegZ);
            mb.Material = Mat.Lit(Tex.CarDash, wreck ? new Color(0.6f, 0.55f, 0.5f) : Color.white);
            CarQuad(mb, new Vector3(-xi, dy0, dz0 - 0.001f), new Vector3(-xi, dy1, dz0 - 0.001f), new Vector3(xi, dy1, dz0 - 0.001f), new Vector3(xi, dy0, dz0 - 0.001f),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.back);
            mb.Material = dash;
            mb.AddBox(new Vector3(-0.39f, dy1 + 0.02f, dz0 + 0.07f), new Vector3(0.46f, 0.04f, 0.16f), BoxUV.Local, 0.4f);   // hood over the gauges
            // steering wheel, tipped back towards the driver, on its column
            var c = new Vector3(-0.38f, 0.93f, 0.47f);
            var right = Vector3.right;
            var up = new Vector3(0f, Mathf.Cos(25f * Mathf.Deg2Rad), -Mathf.Sin(25f * Mathf.Deg2Rad));
            var n = Vector3.Cross(right, up);
            const int ring = 14;
            const float r = 0.19f;
            mb.Material = CarRubber();
            for (int i = 0; i < ring; i++)
            {
                float a0 = 2f * Mathf.PI * i / ring, a1 = 2f * Mathf.PI * (i + 1) / ring;
                CarBar(mb, c + (right * Mathf.Cos(a0) + up * Mathf.Sin(a0)) * r, c + (right * Mathf.Cos(a1) + up * Mathf.Sin(a1)) * r, 0.028f, 0.028f);
            }
            CarBar(mb, c - right * r, c + right * r, 0.03f, 0.016f);
            CarBar(mb, c, c - up * r, 0.03f, 0.016f);
            mb.Material = dash;
            CarBar(mb, c - n * 0.015f, c + n * 0.035f, 0.08f, 0.08f);
            CarBar(mb, c + n * 0.035f, new Vector3(-0.38f, 0.83f, 0.66f), 0.05f, 0.05f);
            mb.Material = chrome;
            CarBar(mb, c + n * 0.07f + right * 0.03f, c + n * 0.08f + right * 0.17f - up * 0.02f, 0.012f, 0.012f);   // column shifter
            // bench seats: cushions and backrests leaning back
            mb.Material = seat;
            mb.AddBox(new Vector3(0f, 0.5f, -0.17f), new Vector3(1.5f, 0.16f, 0.52f), BoxUV.Local, 0.5f);
            mb.Push(new Vector3(0f, 0.84f, -0.47f), Quaternion.Euler(-12f, 0f, 0f));
            mb.AddBox(Vector3.zero, new Vector3(1.5f, 0.52f, 0.12f), BoxUV.Local, 0.5f);
            mb.Pop();
            mb.AddBox(new Vector3(0f, 0.47f, -0.92f), new Vector3(1.56f, 0.14f, 0.42f), BoxUV.Local, 0.5f);
            mb.Push(new Vector3(0f, 0.8f, -1.14f), Quaternion.Euler(-12f, 0f, 0f));
            mb.AddBox(Vector3.zero, new Vector3(1.56f, 0.42f, 0.12f), BoxUV.Local, 0.5f);
            mb.Pop();
            // headliner, sun visors, rear-view mirror
            mb.Material = Mat.Lit(Tex.Cloth, new Color(0.58f * k, 0.55f * k, 0.48f * k));
            float yh = CarY2 - 0.025f;
            CarQuad(mb, new Vector3(-RoofW + 0.02f, yh, RoofR), new Vector3(-RoofW + 0.02f, yh, RoofF), new Vector3(RoofW - 0.02f, yh, RoofF), new Vector3(RoofW - 0.02f, yh, RoofR),
                Vector3.down, 0.6f);
            for (int si = -1; si <= 1; si += 2)
                mb.AddBox(new Vector3(si * 0.36f, CarY2 - 0.045f, RoofF - 0.08f), new Vector3(0.36f, 0.015f, 0.16f), BoxUV.Local, 0.4f);
            mb.Material = dash;
            CarBar(mb, new Vector3(0f, yh, RoofF - 0.03f), new Vector3(0f, CarY2 - 0.09f, RoofF - 0.03f), 0.02f, 0.02f);
            mb.AddBox(new Vector3(0f, CarY2 - 0.11f, RoofF - 0.03f), new Vector3(0.22f, 0.06f, 0.025f), BoxUV.Local, 0.3f);
            mb.Material = Mat.Lit(Tex.Mirror);
            float mz = RoofF - 0.044f;
            CarQuad(mb, new Vector3(-0.1f, CarY2 - 0.135f, mz), new Vector3(-0.1f, CarY2 - 0.085f, mz), new Vector3(0.1f, CarY2 - 0.085f, mz), new Vector3(0.1f, CarY2 - 0.135f, mz),
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector3.back);
        }

        /// <summary>The lit lamps (emissive): headlights and tail lights, on their own mesh so the game can switch them on with the engine.</summary>
        public static void CarLampGlow(MeshBuilder mb)
        {
            mb.Color = new Color32(255, 255, 255, 255);
            mb.Material = Mat.Glow(1f, 0.93f, 0.72f);
            foreach (var h in CarHeadlights) CarDisc(mb, h + new Vector3(0f, 0f, 0.004f), Vector3.forward, 0.165f, 0.1f, 0.003f, 12);
            mb.Material = Mat.Glow(0.9f, 0.1f, 0.06f);
            for (int k = -1; k <= 1; k += 2)
                mb.AddBox(new Vector3(k * 0.666f, 0.692f, -CarHL - 0.0255f), new Vector3(0.36f, 0.115f, 0.002f), BoxUV.Local, 1f);
        }

        // ------------------------------------------------------------------ fuel filler / wheels

        /// <summary>
        /// The fuel filler on the left rear quarter panel (car-local, front at +Z): a recessed housing with a chrome rim, the
        /// filler neck, the painted fuel door hanging open on its hinge and a fuel stain run down the paint under it. The
        /// screw cap (with its grip bar) goes into <paramref name="capMb"/>, pivot at the cap's centre, so the game can take
        /// it off once the tank is filled.
        /// </summary>
        public static void FuelFiller(MeshBuilder mb, MeshBuilder capMb, bool wreck)
        {
            const float W = 0.17f, H = 0.15f;
            // local frame on the panel: +Z out of the car (car -X), +X towards the car's front, +Y up
            mb.Push(FuelFillerLocal, Quaternion.LookRotation(Vector3.left, Vector3.up));
            Gray(mb, 0.8f);
            // the dark housing seen through the opening, with a lip of bare metal round it
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.22f, 0.2f, 0.18f));
            mb.AddQuad(new Vector3(W * 0.5f, -H * 0.5f, 0.003f), new Vector3(W * 0.5f, H * 0.5f, 0.003f), new Vector3(-W * 0.5f, H * 0.5f, 0.003f), new Vector3(-W * 0.5f, -H * 0.5f, 0.003f));
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.62f, 0.6f, 0.56f));
            float lip = 0.012f, lz = 0.006f;
            mb.AddBox(new Vector3(0f, H * 0.5f + lip * 0.5f, lz * 0.5f), new Vector3(W + lip * 2f, lip, lz));
            mb.AddBox(new Vector3(0f, -H * 0.5f - lip * 0.5f, lz * 0.5f), new Vector3(W + lip * 2f, lip, lz));
            mb.AddBox(new Vector3(W * 0.5f + lip * 0.5f, 0f, lz * 0.5f), new Vector3(lip, H, lz));
            mb.AddBox(new Vector3(-W * 0.5f - lip * 0.5f, 0f, lz * 0.5f), new Vector3(lip, H, lz));
            // filler neck (a short steel pipe sticking out of the housing, a little upward)
            mb.Push(new Vector3(0f, -0.005f, 0.003f), Quaternion.Euler(80f, 0f, 0f));
            mb.AddCylinder(Vector3.zero, 0.034f, 0.03f, 0.04f, 10, false, false, null, true);
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.08f, 0.07f, 0.06f));   // the dark mouth of the pipe
            mb.AddCylinder(new Vector3(0f, 0.039f, 0f), 0.026f, 0.026f, 0.001f, 10, true, false, null, false);
            mb.Pop();
            // the fuel door: painted outside, bare primer inside, hinged on its front edge and swung open
            mb.Push(new Vector3(W * 0.5f + lip, 0f, 0.004f), Quaternion.Euler(0f, 100f, 0f));
            mb.Material = CarPaint(wreck);
            mb.AddBox(new Vector3(-(W + lip) * 0.5f, 0f, 0.004f), new Vector3(W + lip, H + lip, 0.006f), BoxUV.Local, 0.6f, 0f, BoxFaces.All & ~BoxFaces.NegZ);
            mb.Material = Mat.Lit(Tex.MetalRusty, new Color(0.55f, 0.52f, 0.48f));
            mb.AddBox(new Vector3(-(W + lip) * 0.5f, 0f, 0.004f), new Vector3(W + lip, H + lip, 0.006f), BoxUV.Local, 0.3f, 0f, BoxFaces.NegZ);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBox(new Vector3(-0.012f, 0f, 0.0f), new Vector3(0.02f, 0.05f, 0.012f), BoxUV.Local, 0.2f);   // hinge arm
            mb.Pop();
            Gray(mb, 1f);
            mb.Pop();
            // fuel that ran down the paint the last time someone filled it
            Arch.Decal(mb, Mat.Decal("grime", new Color(0.5f, 0.42f, 0.3f, 0.8f)), FuelFillerLocal + new Vector3(-0.004f, -0.17f, 0.01f), Vector3.left, 0.1f, 0.24f, 4f);

            if (capMb == null) return;
            // the screw cap: a ribbed black disc with a grip bar across it (pivot = cap centre, built facing car -X)
            capMb.Push(Vector3.zero, Quaternion.Euler(0f, 0f, 90f));   // cylinder axis +Y -> car -X
            Gray(capMb, 0.8f);
            capMb.Material = Mat.Lit(Tex.Plastic, new Color(0.16f, 0.15f, 0.14f));
            capMb.AddCylinder(new Vector3(0f, -0.01f, 0f), 0.044f, 0.042f, 0.02f, 12, true, true, null, false);
            capMb.AddBox(new Vector3(0f, 0.016f, 0f), new Vector3(0.012f, 0.014f, 0.07f), BoxUV.Local, 0.2f);
            Gray(capMb, 1f);
            capMb.Pop();
        }

        /// <summary>Tyre on its rim standing on the tread, axle along local X: tread round it, sidewall + rim on both faces.</summary>
        public static void Wheel(MeshBuilder mb, Vector3 center, float radius, float width, int sides = 12)
        {
            mb.Push(center, Quaternion.Euler(0, 0, 90f));
            mb.Material = Mat.Lit(Tex.CarTire);
            mb.AddCylinder(new Vector3(0, -width * 0.5f, 0), radius, radius, width, sides, true, true, Tex.TireTreadUv, true, 1, Tex.TireSideUv);
            mb.Pop();
        }
    }
}
