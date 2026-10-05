using UnityEngine;

namespace PrisonersOfOmar.Map
{
    internal static partial class Props
    {
        // ------------------------------------------------------------------ yard

        /// <summary>Pink plastic lawn flamingo (body + S-neck + head + beak + thin legs), facing local +Z.</summary>
        public static void Flamingo(MeshBuilder mb, Vector3 pos, float yaw, float scale, bool fallen = false)
        {
            mb.Push(pos, MapMath.Yaw(yaw) * (fallen ? Quaternion.Euler(0, 0, 80f) : Quaternion.identity), Vector3.one * scale);
            if (fallen) mb.Push(new Vector3(0.0f, -0.02f, 0), Quaternion.identity);
            var pink = Mat.Lit(Tex.Flamingo);
            var leg = Mat.Lit(Tex.MetalDark, new Color(0.45f, 0.45f, 0.42f));
            Gray(mb, 0.95f);
            mb.Material = leg;
            mb.AddBeam(new Vector3(-0.03f, 0f, 0f), new Vector3(-0.03f, 0.5f, 0.0f), 0.012f);
            mb.AddBeam(new Vector3(0.03f, 0f, 0.02f), new Vector3(0.02f, 0.5f, 0.0f), 0.012f);
            mb.Material = pink;
            mb.AddSphere(new Vector3(0, 0.6f, -0.02f), new Vector3(0.11f, 0.12f, 0.2f), 7, 4);
            mb.AddSphere(new Vector3(0, 0.66f, -0.2f), new Vector3(0.07f, 0.05f, 0.08f), 5, 3);
            Vector3 n0 = new Vector3(0, 0.68f, 0.12f), n1 = new Vector3(0, 0.8f, 0.2f), n2 = new Vector3(0, 0.9f, 0.13f), n3 = new Vector3(0, 0.98f, 0.17f);
            mb.AddBeam(n0, n1, 0.045f);
            mb.AddBeam(n1, n2, 0.04f);
            mb.AddBeam(n2, n3, 0.04f);
            mb.AddSphere(n3 + new Vector3(0, 0.02f, 0.02f), new Vector3(0.045f, 0.045f, 0.06f), 5, 3);
            mb.Material = Mat.Lit(Tex.Plastic, new Color(0.14f, 0.12f, 0.12f));
            mb.AddBeam(n3 + new Vector3(0, 0.01f, 0.06f), n3 + new Vector3(0, -0.04f, 0.13f), 0.02f);
            if (fallen) mb.Pop();
            Gray(mb, 1f);
            mb.Pop();
        }

        /// <summary>Wooden utility pole (creosote) with a crossarm; returns the 3 wire attachment points (world).</summary>
        public static Vector3[] UtilityPole(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float height = 8.5f, float lean = 0f)
        {
            var rot = MapMath.Yaw(yaw) * Quaternion.Euler(lean, 0, lean * 0.5f);
            mb.Push(pos, rot);
            Gray(mb, 0.75f);
            mb.Material = Mat.Lit(Tex.UtilityPole);
            mb.AddCylinder(Vector3.zero, 0.15f, 0.12f, height, 6, true, false, new Rect(0, 0, 1, height / 4f), true, 4);
            mb.Material = Mat.Lit(Tex.WoodRaw, new Color(0.4f, 0.35f, 0.3f));
            mb.AddBox(new Vector3(0, height - 0.6f, 0), new Vector3(2.0f, 0.12f, 0.12f), BoxUV.Local, 0.8f);
            mb.AddBeam(new Vector3(0, height - 1.3f, 0), new Vector3(-0.7f, height - 0.66f, 0), 0.05f);
            mb.AddBeam(new Vector3(0, height - 1.3f, 0), new Vector3(0.7f, height - 0.66f, 0), 0.05f);
            mb.Material = Mat.Lit(Tex.Porcelain, new Color(0.42f, 0.55f, 0.48f));   // green glass insulators
            var pts = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                float x = -0.85f + i * 0.85f;
                var p = new Vector3(x, height - 0.48f, 0);
                mb.AddCylinder(p - Vector3.up * 0.06f, 0.04f, 0.03f, 0.14f, 5, true, false, null, true);
                pts[i] = mb.TransformPoint(p + Vector3.up * 0.08f);
            }
            // transformer can on some poles
            if (Shade.Hash(pos, 61) > 0.6f)
            {
                mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.5f, 0.5f, 0.48f));
                mb.AddCylinder(new Vector3(0, height - 2.2f, 0.25f), 0.22f, 0.22f, 0.6f, 8, true, true, null, true);
            }
            Gray(mb, 1f);
            mb.Pop();
            if (ctx != null) ctx.Solid(pos + Vector3.up * (height * 0.5f), new Vector3(0.3f, height, 0.3f), SurfaceType.Wood, "Pole");
            return pts;
        }

        /// <summary>Sagging wire between two points (two crossed thin quads per segment).</summary>
        public static void Wire(MeshBuilder mb, Vector3 a, Vector3 b, float sag, int segments = 6)
        {
            mb.Material = Mat.TwoSided(null, new Color(0.03f, 0.03f, 0.03f));
            mb.Color = Shade.Gray(0.6f);
            Vector3 prev = a;
            for (int i = 1; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 p = Vector3.Lerp(a, b, t) - Vector3.up * (sag * 4f * t * (1f - t));
                Vector3 d = (p - prev);
                Vector3 side = Vector3.Cross(d.normalized, Vector3.up).normalized * 0.015f;
                Vector3 up = Vector3.up * 0.015f;
                mb.AddQuad(prev - side, p - side, p + side, prev + side);
                mb.AddQuad(prev - up, p - up, p + up, prev + up);
                prev = p;
            }
            Gray(mb, 1f);
        }

        /// <summary>Tall lamp post (pole + arm + lamp head pointing along local +Z). Returns the lamp head world position.</summary>
        public static Vector3 LampPost(MapContext ctx, MeshBuilder mb, MeshBuilder glow, Material glowMat, Vector3 pos, float yaw, float height, float arm)
        {
            mb.Push(pos, MapMath.Yaw(yaw));
            Gray(mb, 0.7f);
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.6f, 0.6f, 0.58f));
            mb.AddCylinder(Vector3.zero, 0.14f, 0.09f, height, 6, true, false, null, true, 3);
            mb.AddBeam(new Vector3(0, height - 0.1f, 0), new Vector3(0, height + 0.15f, arm), 0.07f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBox(new Vector3(0, height + 0.1f, arm + 0.25f), new Vector3(0.35f, 0.15f, 0.65f), BoxUV.Local, 0.4f);
            Vector3 head = mb.TransformPoint(new Vector3(0, height - 0.0f, arm + 0.25f));
            mb.Material = Mat.Lit(Tex.Concrete);
            mb.AddCylinder(new Vector3(0, -0.05f, 0), 0.35f, 0.3f, 0.35f, 8, true, false, null, true);
            Gray(mb, 1f);
            mb.Pop();
            if (glow != null)
            {
                glow.Push(pos, MapMath.Yaw(yaw));
                glow.Color = new Color32(255, 255, 255, 255);
                glow.Material = glowMat;
                glow.AddBox(new Vector3(0, height + 0.01f, arm + 0.25f), new Vector3(0.28f, 0.04f, 0.55f), BoxUV.Local, 1f, 0f, BoxFaces.NegY);
                glow.Pop();
            }
            if (ctx != null) ctx.Solid(pos + Vector3.up * (height * 0.5f), new Vector3(0.3f, height, 0.3f), SurfaceType.Metal, "LampPost");
            return head;
        }

        /// <summary>Two-post road sign (sign faces local -Z).</summary>
        public static void RoadSign(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, string tex, float w = 1.6f, float h = 0.8f, float postH = 2.3f)
        {
            mb.Push(pos, MapMath.Yaw(yaw));
            Gray(mb, 0.75f);
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.75f, 0.75f, 0.72f));
            mb.AddBox(new Vector3(-w * 0.35f, postH * 0.5f, 0.03f), new Vector3(0.05f, postH, 0.05f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(w * 0.35f, postH * 0.5f, 0.03f), new Vector3(0.05f, postH, 0.05f), BoxUV.Local, 0.5f);
            Gray(mb, 0.9f);
            mb.Material = Mat.Lit(tex);
            mb.AddBox(new Vector3(0, postH - h * 0.5f, 0), new Vector3(w, h, 0.03f), BoxUVRects.Front(new Rect(0, 0, 1, 1), new Rect(0, 0, 0.05f, 0.05f)));
            Gray(mb, 1f);
            mb.Pop();
            if (ctx != null)
            {
                ctx.SolidLocal(pos, yaw, new Vector3(-w * 0.35f, postH * 0.5f, 0.03f), new Vector3(0.08f, postH, 0.08f), SurfaceType.Metal, "SignPost");
                ctx.SolidLocal(pos, yaw, new Vector3(w * 0.35f, postH * 0.5f, 0.03f), new Vector3(0.08f, postH, 0.08f), SurfaceType.Metal, "SignPost");
            }
        }

        /// <summary>Flat sign plate (texture faces outward) mounted on a fence / wall.</summary>
        public static void SignPlate(MeshBuilder mb, Vector3 center, Vector3 outward, string tex, float w, float h)
        {
            float yaw = MapMath.PropYawFacing(outward);
            mb.Push(center, MapMath.Yaw(yaw));
            Gray(mb, 0.9f);
            mb.Material = Mat.Lit(tex);
            mb.AddBox(Vector3.zero, new Vector3(w, h, 0.02f), BoxUVRects.Front(new Rect(0, 0, 1, 1), new Rect(0, 0, 0.05f, 0.05f)));
            Gray(mb, 1f);
            mb.Pop();
        }

        // ------------------------------------------------------------------ vehicles (the sedan itself: PropLibraryCar.cs)

        /// <summary>Static rusty sedan wreck (front at local +Z), with collider.</summary>
        public static void CarWreck(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, int seed)
        {
            var rng = new DeterministicRandom(seed, 71);
            float sink = rng.Range(0.05f, 0.2f);
            mb.Push(pos - Vector3.up * sink, MapMath.Yaw(yaw) * Quaternion.Euler(rng.Range(-3f, 3f), 0, rng.Range(-4f, 4f)));
            CarShell(mb, true, true, seed);
            mb.Pop();
            if (ctx != null) ctx.SolidLocal(pos, yaw, new Vector3(0, 0.7f, 0), new Vector3(1.8f, 1.4f, 4.7f), SurfaceType.Metal, "Wreck");
            Arch.Blob(mb, pos, 2.6f, 5.4f, yaw);
        }

        /// <summary>Rusty pickup wreck (front at local +Z): cab + open bed, no wheels at the back.</summary>
        public static void PickupWreck(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, int seed)
        {
            mb.Push(pos, MapMath.Yaw(yaw) * Quaternion.Euler(-2.5f, 0, 3f));
            var rust = Mat.Lit(Tex.CarWreck);
            var dark = Mat.Lit(Tex.CarGlassBroken);
            Gray(mb, 0.78f);
            mb.Material = rust;
            mb.AddBox(new Vector3(0, 0.62f, 1.55f), new Vector3(1.85f, 0.6f, 1.6f), BoxUV.Local, 1.2f);
            mb.AddBox(new Vector3(0, 1.0f, 0.25f), new Vector3(1.85f, 1.35f, 1.1f), BoxUV.Local, 1.2f);
            mb.Material = Mat.Lit(Tex.CarFront, new Color(0.55f, 0.45f, 0.4f));
            mb.AddQuad(new Vector3(0.925f, 0.32f, 2.36f), new Vector3(0.925f, 0.92f, 2.36f), new Vector3(-0.925f, 0.92f, 2.36f), new Vector3(-0.925f, 0.32f, 2.36f));
            mb.Material = dark;
            mb.AddQuad(new Vector3(0.8f, 1.15f, 0.81f), new Vector3(0.75f, 1.6f, 0.81f), new Vector3(-0.75f, 1.6f, 0.81f), new Vector3(-0.8f, 1.15f, 0.81f));   // faces forward
            mb.AddQuad(new Vector3(0.93f, 1.15f, -0.15f), new Vector3(0.93f, 1.6f, -0.15f), new Vector3(0.93f, 1.6f, 0.65f), new Vector3(0.93f, 1.15f, 0.65f));
            mb.AddQuad(new Vector3(-0.93f, 1.15f, 0.65f), new Vector3(-0.93f, 1.6f, 0.65f), new Vector3(-0.93f, 1.6f, -0.15f), new Vector3(-0.93f, 1.15f, -0.15f));
            mb.Material = rust;
            mb.AddBox(new Vector3(0, 0.45f, -1.45f), new Vector3(1.85f, 0.08f, 2.3f), BoxUV.Local, 1.2f);
            mb.AddBox(new Vector3(0.9f, 0.75f, -1.45f), new Vector3(0.06f, 0.55f, 2.3f), BoxUV.Local, 1.2f);
            mb.AddBox(new Vector3(-0.9f, 0.75f, -1.45f), new Vector3(0.06f, 0.55f, 2.3f), BoxUV.Local, 1.2f);
            mb.AddBox(new Vector3(0, 0.75f, -2.57f), new Vector3(1.85f, 0.55f, 0.06f), BoxUV.Local, 1.2f);
            for (int sx = -1; sx <= 1; sx += 2) Wheel(mb, new Vector3(sx * 0.82f, 0.33f, 1.55f), 0.33f, 0.22f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBox(new Vector3(0.5f, 0.2f, -1.6f), new Vector3(0.2f, 0.4f, 0.2f), BoxUV.Local, 0.4f);
            mb.AddBox(new Vector3(-0.5f, 0.2f, -1.2f), new Vector3(0.2f, 0.4f, 0.2f), BoxUV.Local, 0.4f);
            Gray(mb, 1f);
            mb.Pop();
            Tire(mb, pos + MapMath.Yaw(yaw) * new Vector3(1.6f, 0, -2.2f), 0f);
            if (ctx != null) ctx.SolidLocal(pos, yaw, new Vector3(0, 0.85f, 0), new Vector3(1.9f, 1.7f, 4.9f), SurfaceType.Metal, "Pickup");
            Arch.Blob(mb, pos, 2.6f, 5.4f, yaw);
        }

        // ------------------------------------------------------------------ foliage

        /// <summary>
        /// Vertical cutout billboard planes (no collision). Normals point up so per-vertex lighting is the same from
        /// every side; vertex colors darken the base.
        /// </summary>
        public static void Billboard(MeshBuilder mb, Material m, Vector3 basePos, float width, float height, int planes, float rotDeg,
            float baseShade = 0.45f, float topShade = 1f, Rect? uv = null, float sink = 0.05f)
        {
            Rect r = uv ?? new Rect(0, 0, 1, 1);
            mb.Material = m;
            var c0 = Shade.Gray(baseShade);
            var c1 = Shade.Gray(topShade);
            var mid = Shade.Gray((baseShade + topShade) * 0.5f);
            int rows = height > 4f ? 2 : 1;
            for (int i = 0; i < planes; i++)
            {
                float ang = (rotDeg + 180f * i / planes) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (width * 0.5f);
                Vector3 a = basePos - dir - Vector3.up * sink, d = basePos + dir - Vector3.up * sink;
                int start = -1;
                for (int y = 0; y <= rows; y++)
                {
                    float t = (float)y / rows;
                    var col = y == 0 ? c0 : (y == rows ? c1 : mid);
                    int i0 = mb.AddVertex(a + Vector3.up * (height * t), Vector3.up, new Vector2(r.xMin, Mathf.Lerp(r.yMin, r.yMax, t)), col);
                    mb.AddVertex(d + Vector3.up * (height * t), Vector3.up, new Vector2(r.xMax, Mathf.Lerp(r.yMin, r.yMax, t)), col);
                    if (start < 0) start = i0;
                }
                for (int y = 0; y < rows; y++)
                {
                    int bl = start + y * 2, br = bl + 1, tl = bl + 2, tr = bl + 3;
                    mb.AddTriangle(bl, tl, tr);
                    mb.AddTriangle(bl, tr, br);
                }
            }
        }

        /// <summary>Tree billboard with optional trunk collider (inside the fence only).</summary>
        public static void Tree(MapContext ctx, MeshBuilder mb, string tex, Vector3 pos, float height, float width, float rot, bool trunkCollider, int planes = 2)
        {
            Billboard(mb, Mat.Cutout(tex), pos, width, height, planes, rot, 0.35f, 0.9f);
            if (trunkCollider && ctx != null)
                ctx.Solid(pos + Vector3.up * 1.5f, new Vector3(0.45f, 3f, 0.45f), SurfaceType.Wood, "Trunk");
        }

        public static void GrassClump(MeshBuilder mb, Vector3 pos, float size, float rot, int variant)
        {
            Billboard(mb, Mat.Cutout(variant % 2 == 0 ? Tex.GrassTall1 : Tex.GrassTall2), pos, size * 1.3f, size, 2, rot, 0.4f, 0.85f, null, 0.02f);
        }

        // ------------------------------------------------------------------ misc outdoor

        public static void Dumpster(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            mb.Push(pos, MapMath.Yaw(yaw));
            Gray(mb, 0.75f);
            mb.Material = Mat.Lit(Tex.MetalGreen, new Color(0.55f, 0.6f, 0.5f));
            mb.AddBox(new Vector3(0, 0.7f, 0), new Vector3(1.9f, 1.2f, 1.2f), BoxUV.Local, 1f, 1f);
            mb.Material = Mat.Lit(Tex.Plastic, new Color(0.17f, 0.18f, 0.17f));
            mb.Push(new Vector3(0, 1.32f, 0.55f), Quaternion.Euler(-25f, 0, 0));
            mb.AddBox(new Vector3(0, 0, -0.6f), new Vector3(1.9f, 0.04f, 1.2f), BoxUV.Local, 1f);
            mb.Pop();
            Gray(mb, 1f);
            mb.Pop();
            if (ctx != null) ctx.SolidLocal(pos, yaw, new Vector3(0, 0.7f, 0), new Vector3(1.9f, 1.4f, 1.2f), SurfaceType.Metal, "Dumpster");
        }

        public static void JerseyBarrier(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float len = 2.0f)
        {
            mb.Push(pos, MapMath.Yaw(yaw));
            Gray(mb, 0.75f);
            mb.Material = Mat.Lit(Tex.Concrete, new Color(0.75f, 0.74f, 0.7f));
            mb.AddBox(new Vector3(0, 0.15f, 0), new Vector3(len, 0.3f, 0.6f), BoxUV.Local, 1f);
            mb.AddBox(new Vector3(0, 0.55f, 0), new Vector3(len, 0.5f, 0.25f), BoxUV.Local, 1f);
            mb.AddBox(new Vector3(0, 0.36f, 0), new Vector3(len, 0.12f, 0.42f), BoxUV.Local, 1f);
            Gray(mb, 1f);
            mb.Pop();
            if (ctx != null) ctx.SolidLocal(pos, yaw, new Vector3(0, 0.4f, 0), new Vector3(len, 0.8f, 0.6f), SurfaceType.Concrete, "Barrier");
        }

        public static void PorchCouch(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Sofa(ctx, mb, pos, yaw, 1.8f);
            Arch.FloorDecal(mb, Mat.Decal("grime"), pos + Vector3.up * 0.46f, 1.2f, 0.6f, yaw);
        }

        public static void Cooler(MeshBuilder mb, Vector3 pos, float yaw)
        {
            mb.Push(pos, MapMath.Yaw(yaw));
            Gray(mb, 0.8f);
            mb.Material = Mat.Lit(Tex.Plastic, new Color(0.85f, 0.22f, 0.17f));
            mb.AddBox(new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.4f, 0.38f), BoxUV.Local, 1f);
            mb.Material = Mat.Lit(Tex.Plastic, new Color(0.92f, 0.92f, 0.88f));
            mb.AddBox(new Vector3(0, 0.42f, 0), new Vector3(0.62f, 0.05f, 0.4f), BoxUV.Local, 1f);
            Gray(mb, 1f);
            mb.Pop();
        }

        public const float CoolerTop = 0.445f;

        public static void Lawnmower(MeshBuilder mb, Vector3 pos, float yaw)
        {
            mb.Push(pos, MapMath.Yaw(yaw));
            Gray(mb, 0.75f);
            mb.Material = Mat.Lit(Tex.MetalRusty, new Color(0.8f, 0.3f, 0.2f));
            mb.AddBox(new Vector3(0, 0.2f, 0), new Vector3(0.55f, 0.15f, 0.6f), BoxUV.Local, 0.5f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBox(new Vector3(0, 0.35f, 0.05f), new Vector3(0.25f, 0.2f, 0.25f), BoxUV.Local, 0.5f);
            mb.AddBeam(new Vector3(-0.2f, 0.25f, -0.25f), new Vector3(-0.22f, 1.0f, -0.75f), 0.025f);
            mb.AddBeam(new Vector3(0.2f, 0.25f, -0.25f), new Vector3(0.22f, 1.0f, -0.75f), 0.025f);
            mb.AddBeam(new Vector3(-0.22f, 1.0f, -0.75f), new Vector3(0.22f, 1.0f, -0.75f), 0.025f);
            Gray(mb, 1f);
            mb.Pop();
        }

        public static void HayStack(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, int seed, int count)
        {
            var rng = new DeterministicRandom(seed, 73);
            var r = MapMath.Yaw(yaw);
            int perRow = 3;
            for (int i = 0; i < count; i++)
            {
                int layer = i / (perRow * 2), inLayer = i % (perRow * 2);
                int row = inLayer / perRow, col = inLayer % perRow;
                if (layer > 0 && col == perRow - 1) continue;
                Vector3 local = new Vector3(-1.0f + col * 1.02f + layer * 0.5f, layer * HayH, -0.26f + row * 0.52f);
                HayBale(null, mb, pos + r * local, yaw + rng.Range(-4f, 4f), false);
            }
            int layers = Mathf.Max(1, (count + perRow * 2 - 1) / (perRow * 2));
            if (ctx != null) ctx.SolidLocal(pos, yaw, new Vector3(0, layers * HayH * 0.5f, 0), new Vector3(3.1f, layers * HayH, 1.05f), SurfaceType.Dirt, "HayStack");
        }

        /// <summary>Rusted tractor wreck (front at local +Z).</summary>
        public static void TractorWreck(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            mb.Push(pos, MapMath.Yaw(yaw) * Quaternion.Euler(0, 0, 2.5f));
            Gray(mb, 0.72f);
            var rust = Mat.Lit(Tex.MetalRusty);
            var paint = Mat.Lit(Tex.MetalRusty, new Color(0.75f, 0.35f, 0.2f));
            mb.Material = paint;
            mb.AddBox(new Vector3(0, 1.0f, 0.7f), new Vector3(0.8f, 0.7f, 1.8f), BoxUV.Local, 0.8f);
            mb.Material = rust;
            mb.AddBox(new Vector3(0, 1.0f, -0.6f), new Vector3(1.0f, 0.9f, 0.9f), BoxUV.Local, 0.8f);
            mb.AddBox(new Vector3(0, 1.55f, -0.75f), new Vector3(0.5f, 0.06f, 0.45f), BoxUV.Local, 0.8f);
            mb.AddBeam(new Vector3(0, 1.4f, 0.3f), new Vector3(0, 2.3f, 0.3f), 0.08f);
            mb.AddBeam(new Vector3(0, 1.45f, -0.3f), new Vector3(0.0f, 1.75f, -0.1f), 0.04f);
            mb.AddCylinder(new Vector3(0, 1.75f, -0.1f), 0.18f, 0.18f, 0.04f, 8, true, true, null, false);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Wheel(mb, new Vector3(sx * 0.75f, 0.75f, -0.6f), 0.75f, 0.4f, 14);
                Wheel(mb, new Vector3(sx * 0.6f, 0.42f, 1.35f), 0.42f, 0.24f, 10);
            }
            Gray(mb, 1f);
            mb.Pop();
            if (ctx != null) ctx.SolidLocal(pos, yaw, new Vector3(0, 0.9f, 0.2f), new Vector3(2.0f, 1.8f, 3.4f), SurfaceType.Metal, "Tractor");
        }

        /// <summary>Tall stick + crossarm scarecrow-like stake (also used as tripwire anchor in open ground).</summary>
        public static void Stake(MeshBuilder mb, Vector3 pos, float h = 0.6f)
        {
            mb.Material = Mat.Lit(Tex.WoodRaw, new Color(0.5f, 0.45f, 0.38f));
            Gray(mb, 0.7f);
            mb.AddBox(pos + Vector3.up * (h * 0.5f - 0.1f), new Vector3(0.05f, h + 0.2f, 0.05f), BoxUV.Local, 0.5f);
            Gray(mb, 1f);
        }
    }
}
