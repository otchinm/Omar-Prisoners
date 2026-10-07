using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Reusable low-poly props written into a combined MeshBuilder. Convention: origin = center of the footprint on the
    /// floor, FRONT faces local -Z (MeshBuilder convention), placed with a yaw in degrees. When a MapContext is given,
    /// world colliders (Layers.World) are added for the big parts. Surface heights (for item spawns) are public constants.
    /// </summary>
    internal static partial class Props
    {
        public const float BedTop = 0.56f, CotTop = 0.44f, DresserTop = 0.95f, TableTop = 0.78f, DeskTop = 0.78f,
            CounterTop = 0.92f, CoffeeTop = 0.45f, TvStandTop = 0.5f, SideboardTop = 0.9f, WorkbenchTop = 0.92f,
            NightstandTop = 0.6f, FridgeTop = 1.7f, ButcherTop = 0.86f, SofaSeat = 0.45f;

        // ------------------------------------------------------------------ helpers

        static readonly Rect Full = new Rect(0, 0, 1, 1);

        static void Begin(MeshBuilder mb, Vector3 pos, float yaw) => mb.Push(pos, MapMath.Yaw(yaw));
        static void End(MeshBuilder mb) => mb.Pop();

        static void Box(MeshBuilder mb, Material m, Vector3 c, Vector3 s, float tile = 0.5f, BoxFaces f = BoxFaces.All)
        {
            mb.Material = m;
            mb.AddBox(c, s, BoxUV.Local, tile, 0f, f);
        }

        static void BoxSeg(MeshBuilder mb, Material m, Vector3 c, Vector3 s, float tile, float seg, BoxFaces f = BoxFaces.All)
        {
            mb.Material = m;
            mb.AddBox(c, s, BoxUV.Local, tile, seg, f);
        }

        /// <summary>Box whose front (-Z) face shows the full <paramref name="front"/> texture, other faces <paramref name="other"/>.</summary>
        static void FrontBox(MeshBuilder mb, Material front, Material other, Vector3 c, Vector3 s, float tile = 0.5f, Rect? frontRect = null)
        {
            mb.Material = front;
            mb.AddBox(c, s, BoxUVRects.All(frontRect ?? Full), BoxFaces.NegZ);
            mb.Material = other;
            mb.AddBox(c, s, BoxUV.Local, tile, 0f, BoxFaces.All & ~BoxFaces.NegZ & ~BoxFaces.NegY);
        }

        static void Leg(MeshBuilder mb, Material m, float x, float z, float h, float t)
        {
            mb.Material = m;
            mb.AddBox(new Vector3(x, h * 0.5f, z), new Vector3(t, h, t), BoxUV.Local, 0.4f, 0f, BoxFaces.Sides);
        }

        static void Col(MapContext ctx, Vector3 pos, float yaw, Vector3 localCenter, Vector3 size, SurfaceType s = SurfaceType.Wood, string name = "Prop")
        {
            if (ctx != null) ctx.SolidLocal(pos, yaw, localCenter, size, s, name);
        }

        static void Gray(MeshBuilder mb, float v) => mb.Color = Shade.Gray(v);

        // ------------------------------------------------------------------ beds

        /// <summary>Metal bed (head at local +Z), 1.0 x 2.0, mattress top at BedTop.</summary>
        public static void BedMetal(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, bool pillow = true, bool bloody = false)
        {
            Begin(mb, pos, yaw);
            var frame = Mat.Lit(Tex.BedMetal);
            var mattress = Mat.Lit(Tex.Mattress, bloody ? new Color(0.85f, 0.7f, 0.65f) : Color.white);
            Gray(mb, 0.7f);
            float hw = 0.5f, hl = 1.0f;
            // posts
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Leg(mb, frame, sx * (hw - 0.025f), hl - 0.025f, 1.05f, 0.045f);
                Leg(mb, frame, sx * (hw - 0.025f), -hl + 0.025f, 0.8f, 0.045f);
                Box(mb, frame, new Vector3(sx * (hw - 0.025f), 0.36f, 0), new Vector3(0.04f, 0.06f, 2f * hl), 0.5f);
            }
            // head + foot rails / bars
            Box(mb, frame, new Vector3(0, 1.0f, hl - 0.025f), new Vector3(2f * hw, 0.05f, 0.04f));
            Box(mb, frame, new Vector3(0, 0.5f, hl - 0.025f), new Vector3(2f * hw, 0.04f, 0.04f));
            Box(mb, frame, new Vector3(0, 0.75f, -hl + 0.025f), new Vector3(2f * hw, 0.05f, 0.04f));
            for (int i = 1; i < 6; i++)
            {
                float x = -hw + 2f * hw * i / 6f;
                Box(mb, frame, new Vector3(x, 0.75f, hl - 0.025f), new Vector3(0.025f, 0.5f, 0.025f));
                Box(mb, frame, new Vector3(x, 0.56f, -hl + 0.025f), new Vector3(0.025f, 0.38f, 0.025f));
            }
            Box(mb, frame, new Vector3(0, 0.36f, 0), new Vector3(2f * hw - 0.06f, 0.03f, 2f * hl - 0.06f), 0.6f, BoxFaces.NegY);
            Gray(mb, 0.85f);
            mb.Material = mattress;
            mb.AddBox(new Vector3(0, 0.47f, 0), new Vector3(0.94f, 0.18f, 1.92f), BoxUV.Local, 0.8f, 0.7f);
            if (pillow)
            {
                Gray(mb, 0.8f);
                Box(mb, Mat.Lit(Tex.Cloth, new Color(0.8f, 0.78f, 0.7f)), new Vector3(0.05f, 0.6f, hl - 0.28f), new Vector3(0.6f, 0.1f, 0.35f));
            }
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.3f, 0), new Vector3(1.0f, 0.6f, 2.0f), SurfaceType.Metal, "Bed");
        }

        /// <summary>Folding army cot (green canvas), 0.8 x 1.9, top at CotTop.</summary>
        public static void Cot(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            var metal = Mat.Lit(Tex.MetalGreen);
            var canvas = Mat.Lit(Tex.Cloth, new Color(0.45f, 0.5f, 0.35f));
            Gray(mb, 0.7f);
            for (int sx = -1; sx <= 1; sx += 2)
                Box(mb, metal, new Vector3(sx * 0.38f, 0.4f, 0), new Vector3(0.035f, 0.035f, 1.9f));
            for (int sz = -1; sz <= 1; sz += 2)
            {
                mb.Material = metal;
                mb.AddBeam(new Vector3(-0.38f, 0.0f, sz * 0.8f), new Vector3(0.38f, 0.4f, sz * 0.8f), 0.03f);
                mb.AddBeam(new Vector3(0.38f, 0.0f, sz * 0.8f), new Vector3(-0.38f, 0.4f, sz * 0.8f), 0.03f);
            }
            Gray(mb, 0.85f);
            Box(mb, canvas, new Vector3(0, 0.42f, 0), new Vector3(0.74f, 0.03f, 1.86f), 0.8f);
            Box(mb, Mat.Lit(Tex.Cloth, new Color(0.5f, 0.45f, 0.35f)), new Vector3(0.1f, 0.47f, 0.2f), new Vector3(0.6f, 0.07f, 0.9f), 0.8f);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.23f, 0), new Vector3(0.8f, 0.46f, 1.9f), SurfaceType.Metal, "Cot");
        }

        // ------------------------------------------------------------------ drawers
        // dresser_front.png (128 px): four painted drawers, x 8..120 px, rows (top..bottom px) 12-37, 40-65, 68-94, 97-122
        static readonly float[] DrawerRowTop = { 12f, 40f, 68f, 97f }, DrawerRowBottom = { 37f, 65f, 94f, 122f };
        const float DrawerColL = 8f, DrawerColR = 120f;

        /// <summary>
        /// Box (centre <paramref name="c"/>, size <paramref name="s"/>, prop local space) whose painted drawers really slide
        /// out: the frame around them stays in <paramref name="mb"/>, each drawer becomes its own moving object
        /// (MapData.Drawers) with a small-item spot inside. Without a context (menu scene) it is the plain painted front.
        /// </summary>
        static void DrawerFront(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, Material front, Material wood, Vector3 c, Vector3 s, Rect frontRect)
        {
            mb.Material = wood;
            mb.AddBox(c, s, BoxUV.Local, 0.5f, 0f, BoxFaces.All & ~BoxFaces.NegZ & ~BoxFaces.NegY);
            if (ctx == null)
            {
                mb.Material = front;
                mb.AddBox(c, s, BoxUVRects.All(frontRect), BoxFaces.NegZ);
                return;
            }
            float x0 = c.x - s.x * 0.5f, x1 = c.x + s.x * 0.5f, y0 = c.y - s.y * 0.5f, y1 = c.y + s.y * 0.5f, zf = c.z - s.z * 0.5f;
            float X(float u) => x0 + (u - frontRect.xMin) / frontRect.width * (x1 - x0);
            float Y(float v) => y0 + (v - frontRect.yMin) / frontRect.height * (y1 - y0);
            float U(float x) => frontRect.xMin + (x - x0) / (x1 - x0) * frontRect.width;
            float V(float y) => frontRect.yMin + (y - y0) / (y1 - y0) * frontRect.height;
            var ops = new List<Rect>();   // the drawer openings (local x / y)
            for (int t = Mathf.FloorToInt(frontRect.xMin + 0.001f); t < Mathf.CeilToInt(frontRect.xMax - 0.001f); t++)
                for (int r = 0; r < DrawerRowTop.Length; r++)
                {
                    float u0 = t + DrawerColL / 128f, u1 = t + DrawerColR / 128f;
                    float v0 = 1f - DrawerRowBottom[r] / 128f, v1 = 1f - DrawerRowTop[r] / 128f;
                    if (u0 < frontRect.xMin - 0.001f || u1 > frontRect.xMax + 0.001f || v0 < frontRect.yMin - 0.02f || v1 > frontRect.yMax + 0.02f) continue;
                    v0 = Mathf.Max(v0, frontRect.yMin + 0.004f); v1 = Mathf.Min(v1, frontRect.yMax - 0.004f);
                    ops.Add(Rect.MinMaxRect(X(u0), Y(v0), X(u1), Y(v1)));
                }
            // the frame: horizontal strips between the opening edges, minus the openings crossing each strip
            var ys = new List<float> { y0, y1 };
            foreach (var o in ops) { ys.Add(o.yMin); ys.Add(o.yMax); }
            ys.Sort();
            mb.Material = front;
            for (int i = 0; i + 1 < ys.Count; i++)
            {
                float ya = ys[i], yb = ys[i + 1];
                if (yb - ya < 0.0005f) continue;
                var cross = ops.FindAll(o => o.yMin <= ya + 0.0005f && o.yMax >= yb - 0.0005f);
                cross.Sort((a, b) => a.xMin.CompareTo(b.xMin));
                float cur = x0;
                for (int k = 0; k <= cross.Count; k++)
                {
                    float end = k < cross.Count ? cross[k].xMin : x1;
                    if (end - cur > 0.0005f)
                        mb.AddQuad(new Vector3(cur, ya, zf), new Vector3(cur, yb, zf), new Vector3(end, yb, zf), new Vector3(end, ya, zf), Rect.MinMaxRect(U(cur), V(ya), U(end), V(yb)));
                    if (k < cross.Count) cur = Mathf.Max(cur, cross[k].xMax);
                }
            }
            // dark cavities behind the openings, then the drawers
            var dark = Mat.Lit(Tex.WoodFurniture, new Color(0.13f, 0.11f, 0.09f));
            var rot = MapMath.Yaw(yaw);
            float depth = s.z - 0.04f, zb = zf + depth;
            var uv = new Rect(0, 0, 1, 1);
            foreach (var o in ops)
            {
                mb.Material = dark;
                mb.AddQuad(new Vector3(o.xMin, o.yMin, zb), new Vector3(o.xMin, o.yMax, zb), new Vector3(o.xMax, o.yMax, zb), new Vector3(o.xMax, o.yMin, zb), uv);
                mb.AddQuad(new Vector3(o.xMin, o.yMin, zf), new Vector3(o.xMin, o.yMax, zf), new Vector3(o.xMin, o.yMax, zb), new Vector3(o.xMin, o.yMin, zb), uv);
                mb.AddQuad(new Vector3(o.xMax, o.yMin, zb), new Vector3(o.xMax, o.yMax, zb), new Vector3(o.xMax, o.yMax, zf), new Vector3(o.xMax, o.yMin, zf), uv);
                mb.AddQuad(new Vector3(o.xMax, o.yMax, zf), new Vector3(o.xMax, o.yMax, zb), new Vector3(o.xMin, o.yMax, zb), new Vector3(o.xMin, o.yMax, zf), uv);
                mb.AddQuad(new Vector3(o.xMin, o.yMin, zf), new Vector3(o.xMin, o.yMin, zb), new Vector3(o.xMax, o.yMin, zb), new Vector3(o.xMax, o.yMin, zf), uv);
                BuildDrawer(ctx, pos, rot, front, wood, o, zf, depth, Rect.MinMaxRect(U(o.xMin), V(o.yMin), U(o.xMax), V(o.yMax)));
            }
        }

        static void BuildDrawer(MapContext ctx, Vector3 pos, Quaternion rot, Material front, Material wood, Rect o, float zf, float depth, Rect uv)
        {
            int index = ctx.Data.Drawers.Count;
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Drawer_" + index, pos, rot, Layers.World);
            var dmb = new MeshBuilder();
            const float g = 0.004f;
            float w = o.width - 2f * g, h = o.height - 2f * g;
            var fc = new Vector3(o.center.x, o.center.y, zf + 0.008f);
            // the painted face (a hair proud of the frame), then the box: bottom, two sides, back (open top)
            dmb.Color = Shade.Gray(0.85f);
            dmb.Material = front;
            dmb.AddBox(fc, new Vector3(w, h, 0.02f), BoxUVRects.All(uv), BoxFaces.NegZ);
            dmb.Material = wood;
            dmb.AddBox(fc, new Vector3(w, h, 0.02f), BoxUV.Local, 0.4f, 0f, BoxFaces.All & ~BoxFaces.NegZ);
            float bd = depth - 0.05f, bh = Mathf.Max(0.04f, h - 0.035f), bw = w - 0.03f;
            float zc = zf + 0.018f + bd * 0.5f, yb = o.yMin + g + 0.006f;
            dmb.Color = Shade.Gray(0.5f);
            dmb.AddBox(new Vector3(o.center.x, yb, zc), new Vector3(bw, 0.012f, bd), BoxUV.Local, 0.4f);
            dmb.AddBox(new Vector3(o.center.x - bw * 0.5f + 0.006f, yb + bh * 0.5f, zc), new Vector3(0.012f, bh, bd), BoxUV.Local, 0.4f);
            dmb.AddBox(new Vector3(o.center.x + bw * 0.5f - 0.006f, yb + bh * 0.5f, zc), new Vector3(0.012f, bh, bd), BoxUV.Local, 0.4f);
            dmb.AddBox(new Vector3(o.center.x, yb + bh * 0.5f, zc + bd * 0.5f - 0.006f), new Vector3(bw, bh, 0.012f), BoxUV.Local, 0.4f);
            dmb.Build("Mesh", root, Layers.World);
            ctx.CountRenderer(dmb);
            var interact = GeoUtil.AddBox(root, fc, new Vector3(w, h, 0.06f), Quaternion.identity, Layers.Interactable, SurfaceType.Default, true, "DrawerInteract");
            var info = new DrawerInfo
            {
                Drawer = root,
                OpenOffset = rot * new Vector3(0f, 0f, -Mathf.Min(0.62f * depth, depth - 0.08f)),
                Interact = interact,
                InsideLocal = new Bounds(new Vector3(o.center.x, yb + bh * 0.5f + 0.01f, zc), new Vector3(bw, bh + 0.04f, bd)),
                ItemPoint = pos + rot * new Vector3(o.center.x, yb + 0.006f, zf + 0.13f),
            };
            ctx.Data.Drawers.Add(info);
            if (bh >= 0.07f) ctx.Item(ctx.Data.AreaAt(info.ItemPoint), info.ItemPoint, ItemSpawnTier.Common, float.NaN, true);
        }

        // ------------------------------------------------------------------ storage furniture

        public static void Dresser(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w = 1.0f)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.85f);
            var wood = Mat.Lit(Tex.WoodFurniture);
            DrawerFront(ctx, mb, pos, yaw, Mat.Lit(Tex.DresserFront), wood, new Vector3(0, 0.48f, 0), new Vector3(w, 0.86f, 0.48f), Full);
            Box(mb, wood, new Vector3(0, 0.93f, -0.01f), new Vector3(w + 0.05f, 0.04f, 0.52f));
            Leg(mb, wood, -w * 0.45f, -0.2f, 0.05f, 0.06f); Leg(mb, wood, w * 0.45f, -0.2f, 0.05f, 0.06f);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.475f, 0), new Vector3(w, 0.95f, 0.5f), SurfaceType.Wood, "Dresser");
        }

        public static void Nightstand(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.8f);
            var wood = Mat.Lit(Tex.WoodFurniture);
            DrawerFront(ctx, mb, pos, yaw, Mat.Lit(Tex.DresserFront), wood, new Vector3(0, 0.32f, 0), new Vector3(0.45f, 0.56f, 0.4f), new Rect(0, 0.5f, 1, 0.5f));
            Box(mb, wood, new Vector3(0, 0.58f, 0), new Vector3(0.48f, 0.04f, 0.43f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.3f, 0), new Vector3(0.45f, 0.6f, 0.4f), SurfaceType.Wood, "Nightstand");
        }

        /// <summary>Plain (non hiding) wardrobe body with closed doors.</summary>
        public static void WardrobeStatic(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w = 1.2f, float h = 2.0f)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.8f);
            FrontBox(mb, Mat.Lit(Tex.WardrobeFront), Mat.Lit(Tex.WoodFurniture), new Vector3(0, h * 0.5f, 0), new Vector3(w, h, 0.6f));
            Box(mb, Mat.Lit(Tex.WoodFurniture), new Vector3(0, h + 0.03f, -0.01f), new Vector3(w + 0.08f, 0.06f, 0.64f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, 0.6f), SurfaceType.Wood, "Wardrobe");
        }

        public static void Bookshelf(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w = 1.0f, float h = 2.0f, int seed = 0)
        {
            Begin(mb, pos, yaw);
            var wood = Mat.Lit(Tex.WoodFurniture);
            var books = Mat.Lit(Tex.BookSpines);
            Gray(mb, 0.75f);
            float d = 0.32f;
            Box(mb, wood, new Vector3(-w * 0.5f + 0.02f, h * 0.5f, 0), new Vector3(0.04f, h, d));
            Box(mb, wood, new Vector3(w * 0.5f - 0.02f, h * 0.5f, 0), new Vector3(0.04f, h, d));
            Box(mb, wood, new Vector3(0, h * 0.5f, d * 0.5f - 0.01f), new Vector3(w, h, 0.02f));
            int levels = 4;
            for (int i = 0; i <= levels; i++)
            {
                float y = 0.04f + (h - 0.08f) * i / levels;
                Box(mb, wood, new Vector3(0, y, 0), new Vector3(w - 0.04f, 0.03f, d));
                if (i < levels)
                {
                    float fill = 0.45f + 0.5f * Shade.Hash(pos.x + i, pos.z, seed, 31);
                    float bw = (w - 0.1f) * fill;
                    float x0 = -w * 0.5f + 0.05f + (Shade.Hash(pos.z, i, seed, 37) > 0.5f ? (w - 0.1f - bw) : 0f);
                    mb.Color = Shade.Gray(0.7f + 0.2f * Shade.Hash(i, seed, pos.x, 41));
                    mb.Material = books;
                    mb.AddBox(new Vector3(x0 + bw * 0.5f, y + 0.015f + 0.13f, 0.02f), new Vector3(bw, 0.26f, 0.22f),
                        BoxUVRects.Front(new Rect(0, 0, fill, 1), new Rect(0, 0, 0.1f, 0.1f)), BoxFaces.NegZ | BoxFaces.PosY | BoxFaces.PosX | BoxFaces.NegX);
                    Gray(mb, 0.75f);
                }
            }
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, h * 0.5f, d * 0.25f), new Vector3(w, h, d * 0.5f), SurfaceType.Wood, "Bookshelf");
            for (int i = 1; i <= levels; i++)
                Col(ctx, pos, yaw, new Vector3(0, BookshelfLevel(i, h) - 0.02f, -d * 0.25f), new Vector3(w, 0.04f, d * 0.5f), SurfaceType.Wood, "ShelfBoard");
        }

        /// <summary>Shelf top heights of <see cref="Bookshelf"/> (level i).</summary>
        public static float BookshelfLevel(int i, float h = 2.0f) => 0.055f + (h - 0.08f) * i / 4f;

        /// <summary>Black metal storage shelf (4 levels) loaded with white buckets, black barrels and boxes (like DefO6).</summary>
        public static void MetalShelf(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w, int seed, float fill = 0.85f, bool barrelsBottom = true)
        {
            Begin(mb, pos, yaw);
            var metal = Mat.Lit(Tex.ShelfMetal);
            float d = 0.5f, h = 2.0f;
            Gray(mb, 0.6f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Leg(mb, metal, sx * (w * 0.5f - 0.02f), sz * (d * 0.5f - 0.02f), h, 0.035f);
            for (int i = 0; i < 4; i++)
            {
                float y = ShelfLevel(i);
                Box(mb, metal, new Vector3(0, y - 0.015f, 0), new Vector3(w, 0.03f, d), 0.5f);
            }
            var rng = new DeterministicRandom(seed, 7);
            var bucket = Mat.Lit(Tex.BucketFood);
            var white = Mat.Lit(Tex.Plastic, new Color(0.95f, 0.95f, 0.92f));
            var black = Mat.Lit(Tex.BarrelWater, new Color(0.35f, 0.35f, 0.35f));
            var box = Mat.Lit(Tex.Cardboard);
            for (int i = 0; i < 4; i++)
            {
                float y = ShelfLevel(i);
                // level 1 keeps its left 0.5 m free (item spawn spot at local x = -w/2 + 0.28)
                float x = -w * 0.5f + 0.06f + (i == 1 ? 0.5f : 0f);
                while (x < w * 0.5f - 0.2f)
                {
                    float r = rng.NextFloat();
                    if (r > fill) { x += 0.3f; continue; }
                    if (i == 0 && barrelsBottom && r < 0.45f && x + 0.42f < w * 0.5f)
                    {
                        Gray(mb, 0.8f);
                        mb.Material = black;
                        mb.AddCylinder(new Vector3(x + 0.2f, y, 0), 0.19f, 0.19f, 0.5f, 8, true, false, new Rect(0, 0, 1, 1), true);
                        x += 0.44f;
                    }
                    else if (r < 0.6f)
                    {
                        Gray(mb, 0.85f);
                        mb.Material = rng.Chance(0.4f) ? bucket : white;
                        mb.AddCylinder(new Vector3(x + 0.15f, y, 0.02f), 0.13f, 0.15f, 0.36f, 8, true, false, null, true);
                        x += 0.33f;
                    }
                    else
                    {
                        Gray(mb, 0.75f);
                        float bw = rng.Range(0.25f, 0.4f);
                        if (x + bw > w * 0.5f - 0.05f) break;
                        mb.Material = box;
                        mb.AddBox(new Vector3(x + bw * 0.5f, y + 0.14f, 0.0f), new Vector3(bw, 0.28f, 0.38f), BoxUV.PerFace);
                        x += bw + 0.04f;
                    }
                }
            }
            Gray(mb, 1f);
            End(mb);
            // collider on the back 0.2 m only: items in front of the uprights stay reachable by the interaction ray
            Col(ctx, pos, yaw, new Vector3(0, h * 0.5f, d * 0.5f - 0.1f), new Vector3(w, h, 0.2f), SurfaceType.Metal, "Shelf");
            for (int i = 0; i < 4; i++)
                Col(ctx, pos, yaw, new Vector3(0, ShelfLevel(i) - 0.02f, -0.1f), new Vector3(w, 0.04f, d - 0.2f), SurfaceType.Metal, "ShelfBoard");
        }

        public static float ShelfLevel(int i) => 0.12f + 0.56f * i;

        public static void ChinaCabinet(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            var wood = Mat.Lit(Tex.WoodFurniture);
            Gray(mb, 0.75f);
            FrontBox(mb, Mat.Lit(Tex.DresserFront), wood, new Vector3(0, 0.45f, 0), new Vector3(1.4f, 0.9f, 0.5f), 0.5f, new Rect(0, 0, 1, 0.5f));
            Box(mb, wood, new Vector3(0, 1.45f, 0.06f), new Vector3(1.4f, 1.1f, 0.38f), 0.5f, BoxFaces.All & ~BoxFaces.NegZ);
            Box(mb, wood, new Vector3(0, 2.03f, 0.04f), new Vector3(1.5f, 0.06f, 0.46f));
            // shelves with plates behind dark glass
            for (int i = 0; i < 2; i++)
            {
                float y = 1.05f + i * 0.45f;
                Box(mb, wood, new Vector3(0, y, 0.06f), new Vector3(1.36f, 0.02f, 0.36f));
                mb.Material = Mat.Lit(Tex.Porcelain, new Color(0.92f, 0.9f, 0.84f));
                Gray(mb, 0.9f);
                for (int p = 0; p < 5; p++)
                {
                    mb.Push(new Vector3(-0.5f + p * 0.25f, y + 0.12f, 0.15f), Quaternion.Euler(-80f, 0, 0));
                    mb.AddCylinder(Vector3.zero, 0.1f, 0.1f, 0.015f, 8, true, false, null, false);
                    mb.Pop();
                }
                Gray(mb, 0.75f);
            }
            mb.Material = Mat.Transparent(null, new Color(0.25f, 0.3f, 0.32f, 0.45f));
            mb.AddQuad(new Vector3(-0.68f, 0.92f, -0.135f), new Vector3(-0.68f, 1.98f, -0.135f), new Vector3(0.68f, 1.98f, -0.135f), new Vector3(0.68f, 0.92f, -0.135f));
            Box(mb, wood, new Vector3(0, 1.45f, -0.13f), new Vector3(0.04f, 1.1f, 0.03f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 1.03f, 0), new Vector3(1.4f, 2.06f, 0.5f), SurfaceType.Wood, "Cabinet");
        }

        public static void Sideboard(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w = 1.8f)
        {
            Begin(mb, pos, yaw);
            var wood = Mat.Lit(Tex.WoodFurniture);
            Gray(mb, 0.78f);
            DrawerFront(ctx, mb, pos, yaw, Mat.Lit(Tex.DresserFront), wood, new Vector3(0, 0.45f, 0), new Vector3(w, 0.82f, 0.48f), new Rect(0, 0, 2f, 0.5f));
            Box(mb, wood, new Vector3(0, 0.88f, 0), new Vector3(w + 0.04f, 0.04f, 0.52f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.45f, 0), new Vector3(w, 0.9f, 0.5f), SurfaceType.Wood, "Sideboard");
        }

        public static void Locker(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, bool green = true, float w = 0.6f)
        {
            Begin(mb, pos, yaw);
            var m = Mat.Lit(green ? Tex.MetalGreen : Tex.MetalDark);
            Gray(mb, 0.7f);
            Box(mb, m, new Vector3(0, 0.95f, 0), new Vector3(w, 1.9f, 0.5f), 0.6f);
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.15f, 0.15f, 0.15f));
            for (int i = 0; i < 4; i++)
                mb.AddBox(new Vector3(0, 1.55f + i * 0.05f, -0.252f), new Vector3(w * 0.5f, 0.015f, 0.005f), BoxUV.Local, 1f, 0f, BoxFaces.NegZ);
            Box(mb, Mat.Lit(Tex.MetalDark), new Vector3(w * 0.3f, 1.0f, -0.26f), new Vector3(0.03f, 0.15f, 0.03f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.95f, 0), new Vector3(w, 1.9f, 0.5f), SurfaceType.Metal, "Locker");
        }

        // ------------------------------------------------------------------ tables / seats

        public static void Table(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w, float d, float h = TableTop, string topTex = Tex.TableWood, float tint = 1f)
        {
            Begin(mb, pos, yaw);
            var top = Mat.Lit(topTex, new Color(tint, tint * 0.95f, tint * 0.9f));
            var wood = Mat.Lit(Tex.WoodFurniture);
            Gray(mb, 0.85f);
            mb.Material = top;
            mb.AddBox(new Vector3(0, h - 0.025f, 0), new Vector3(w, 0.05f, d), BoxUV.PerFace, 1f, 0.9f, BoxFaces.PosY);
            Box(mb, wood, new Vector3(0, h - 0.025f, 0), new Vector3(w, 0.05f, d), 0.5f, BoxFaces.Sides | BoxFaces.NegY);
            Gray(mb, 0.7f);
            Box(mb, wood, new Vector3(0, h - 0.1f, 0), new Vector3(w - 0.12f, 0.1f, d - 0.12f), 0.5f, BoxFaces.Sides);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Leg(mb, wood, sx * (w * 0.5f - 0.07f), sz * (d * 0.5f - 0.07f), h - 0.05f, 0.07f);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), SurfaceType.Wood, "Table");
        }

        public static void Desk(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w = 1.4f, float d = 0.7f)
        {
            Begin(mb, pos, yaw);
            var wood = Mat.Lit(Tex.WoodFurniture);
            Gray(mb, 0.85f);
            mb.Material = Mat.Lit(Tex.TableWood, new Color(0.6f, 0.45f, 0.35f));
            mb.AddBox(new Vector3(0, DeskTop - 0.02f, 0), new Vector3(w, 0.04f, d), BoxUV.PerFace, 1f, 0.8f, BoxFaces.PosY);
            Box(mb, wood, new Vector3(0, DeskTop - 0.02f, 0), new Vector3(w, 0.04f, d), 0.5f, BoxFaces.Sides | BoxFaces.NegY);
            Gray(mb, 0.72f);
            DrawerFront(ctx, mb, pos, yaw, Mat.Lit(Tex.DresserFront), wood, new Vector3(w * 0.5f - 0.24f, (DeskTop - 0.04f) * 0.5f, 0), new Vector3(0.44f, DeskTop - 0.04f, d - 0.04f), Full);
            Box(mb, wood, new Vector3(-w * 0.5f + 0.03f, (DeskTop - 0.04f) * 0.5f, 0), new Vector3(0.04f, DeskTop - 0.04f, d - 0.04f));
            Box(mb, wood, new Vector3(-0.1f, 0.45f, d * 0.5f - 0.03f), new Vector3(w - 0.5f, 0.5f, 0.02f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, DeskTop * 0.5f, 0), new Vector3(w, DeskTop, d), SurfaceType.Wood, "Desk");
        }

        public static void Chair(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, bool broken = false, bool collider = true)
        {
            Begin(mb, pos, yaw);
            var wood = Mat.Lit(Tex.WoodFurniture);
            Gray(mb, 0.75f);
            if (broken)
            {
                // tipped over on its back
                mb.Push(new Vector3(0, 0.22f, 0), Quaternion.Euler(-80f, 0, 12f));
            }
            Box(mb, wood, new Vector3(0, 0.45f, 0), new Vector3(0.44f, 0.04f, 0.42f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Leg(mb, wood, sx * 0.19f, sz * 0.18f, 0.44f, 0.035f);
            Box(mb, wood, new Vector3(-0.19f, 0.72f, 0.19f), new Vector3(0.035f, 0.52f, 0.035f));
            Box(mb, wood, new Vector3(0.19f, 0.72f, 0.19f), new Vector3(0.035f, 0.52f, 0.035f));
            Box(mb, wood, new Vector3(0, 0.88f, 0.19f), new Vector3(0.42f, 0.12f, 0.03f));
            Box(mb, wood, new Vector3(0, 0.68f, 0.19f), new Vector3(0.42f, 0.05f, 0.03f));
            if (broken) mb.Pop();
            Gray(mb, 1f);
            End(mb);
            if (collider && !broken) Col(ctx, pos, yaw, new Vector3(0, 0.23f, 0), new Vector3(0.44f, 0.46f, 0.42f), SurfaceType.Wood, "Chair");
        }

        public static void Sofa(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w = 2.0f)
        {
            Begin(mb, pos, yaw);
            var fab = Mat.Lit(Tex.Sofa);
            Gray(mb, 0.8f);
            BoxSeg(mb, fab, new Vector3(0, 0.22f, 0.02f), new Vector3(w, 0.3f, 0.85f), 0.6f, 0.7f);
            Gray(mb, 0.88f);
            BoxSeg(mb, fab, new Vector3(0, 0.42f, -0.03f), new Vector3(w - 0.4f, 0.12f, 0.72f), 0.6f, 0.7f);
            BoxSeg(mb, fab, new Vector3(0, 0.6f, 0.34f), new Vector3(w, 0.55f, 0.22f), 0.6f, 0.7f);
            for (int s = -1; s <= 1; s += 2)
                BoxSeg(mb, fab, new Vector3(s * (w * 0.5f - 0.11f), 0.45f, 0.02f), new Vector3(0.22f, 0.3f, 0.85f), 0.6f, 0f);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.22f, 0.02f), new Vector3(w, 0.44f, 0.85f), SurfaceType.Carpet, "Sofa");
            Col(ctx, pos, yaw, new Vector3(0, 0.6f, 0.34f), new Vector3(w, 0.6f, 0.22f), SurfaceType.Carpet, "SofaBack");
        }

        public static void Armchair(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw) => Sofa(ctx, mb, pos, yaw, 0.95f);

        public static void CoffeeTable(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw) => Table(ctx, mb, pos, yaw, 1.1f, 0.6f, CoffeeTop, Tex.TableWood, 0.8f);

        public static void TvStand(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.75f);
            FrontBox(mb, Mat.Lit(Tex.DresserFront), Mat.Lit(Tex.WoodFurniture), new Vector3(0, 0.25f, 0), new Vector3(1.0f, 0.5f, 0.48f), 0.5f, new Rect(0, 0, 1, 0.5f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.25f, 0), new Vector3(1.0f, 0.5f, 0.48f), SurfaceType.Wood, "TvStand");
        }

        /// <summary>CRT TV body (screen is a separate quad: see <see cref="TvScreenLocal"/>). Origin on its support surface.</summary>
        public static void CrtTv(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.8f);
            FrontBox(mb, Mat.Lit(Tex.TvBody), Mat.Lit(Tex.WoodFurniture, new Color(0.5f, 0.4f, 0.3f)), new Vector3(0, 0.25f, 0.02f), new Vector3(0.62f, 0.48f, 0.46f));
            Box(mb, Mat.Lit(Tex.Plastic, new Color(0.24f, 0.21f, 0.18f)), new Vector3(0, 0.25f, 0.3f), new Vector3(0.42f, 0.36f, 0.14f));
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBeam(new Vector3(0.05f, 0.49f, 0.1f), new Vector3(-0.2f, 0.85f, 0.15f), 0.012f);
            mb.AddBeam(new Vector3(0.08f, 0.49f, 0.1f), new Vector3(0.3f, 0.8f, 0.12f), 0.012f);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.25f, 0.05f), new Vector3(0.62f, 0.5f, 0.55f), SurfaceType.Wood, "Tv");
        }

        /// <summary>(iteration 3) VCR on top of a <see cref="CrtTv"/> (same origin and yaw as the TV), on a lace doily, its
        /// clock blinking 12:00 like every VCR nobody ever set (dark when the power is cut). Returns the world pose of a tape
        /// pushed half into its slot.</summary>
        // vcr.png 128x64: front (0,0,128,28) with the tape door at (13,7,64,10), top (0,28,64,36), sides / back (64,28,64,36)
        public static Pose Vcr(MapContext ctx, MeshBuilder mb, Vector3 tvPos, float yaw)
        {
            Rect P(float x, float y, float w, float h) => new Rect(x / 128f, 1f - (y + h) / 64f, w / 128f, h / 64f);
            Begin(mb, tvPos, yaw);
            Gray(mb, 0.85f);
            Box(mb, Mat.Lit(Tex.Cloth, new Color(0.86f, 0.82f, 0.72f)), new Vector3(0f, 0.491f, -0.04f), new Vector3(0.46f, 0.002f, 0.3f));   // her doily
            var black = Mat.Lit(Tex.Plastic, new Color(0.07f, 0.07f, 0.08f));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = 0; sz < 2; sz++)
                    Box(mb, black, new Vector3(sx * 0.16f, 0.495f, sz == 0 ? -0.15f : 0.05f), new Vector3(0.03f, 0.006f, 0.03f));
            mb.Material = Mat.Lit(Tex.Vcr);
            Rect side = P(64, 28, 64, 36);
            mb.AddBox(new Vector3(0f, 0.5405f, -0.05f), new Vector3(0.4f, 0.085f, 0.26f),
                new BoxUVRects { NegZ = P(0, 0, 128, 28), PosY = P(0, 28, 64, 36), PosX = side, NegX = side, PosZ = side, NegY = side });
            mb.AddBox(new Vector3(-0.06f, 0.545f, -0.1815f), new Vector3(0.2f, 0.03f, 0.003f), BoxUVRects.All(P(13, 7, 64, 10)));   // tape door
            // piano keys on the black strip: STOP REW PLAY FF PAUSE and a red REC
            for (int i = 0; i < 6; i++)
                Box(mb, i == 5 ? Mat.Lit(Tex.Plastic, new Color(0.7f, 0.12f, 0.08f)) : Mat.Lit(Tex.MetalDark, new Color(0.62f, 0.62f, 0.6f)),
                    new Vector3(0.065f + i * 0.024f, 0.512f, -0.183f), new Vector3(0.022f, 0.011f, 0.008f));
            // the power cord out of the back, down the side of the TV and the stand to the wall socket
            mb.Material = black;
            Vector3[] cord = { new Vector3(0.15f, 0.52f, 0.08f), new Vector3(0.27f, 0.495f, 0.1f), new Vector3(0.315f, 0.44f, 0.12f),
                               new Vector3(0.32f, 0.005f, 0.14f), new Vector3(0.49f, 0.005f, 0.16f), new Vector3(0.5f, -0.5f, 0.18f) };
            for (int i = 0; i + 1 < cord.Length; i++) mb.AddBeam(cord[i], cord[i + 1], 0.005f);
            Gray(mb, 1f);
            End(mb);
            var r = MapMath.Yaw(yaw);
            // the green clock display and the two lamps glow with the house power
            var g = ctx.GlowBuilder(HouseBuilder.GlowBulbs);
            g.Push(tvPos, r);
            g.Material = ctx.GlowMat(Tex.VcrClock, new Color(0.35f, 1f, 0.65f), new Color(0.03f, 0.05f, 0.04f));
            g.AddBox(new Vector3(0.12f, 0.556f, -0.1812f), new Vector3(0.07f, 0.018f, 0.002f), BoxUVRects.All(new Rect(0, 0, 1, 1)), BoxFaces.NegZ);
            g.Material = ctx.GlowColor(new Color(1f, 0.15f, 0.08f));
            g.AddBox(new Vector3(0.175f, 0.556f, -0.1812f), new Vector3(0.006f, 0.006f, 0.003f), BoxUV.Local, 1f);
            g.Material = ctx.GlowColor(new Color(0.3f, 1f, 0.3f));
            g.AddBox(new Vector3(0.19f, 0.556f, -0.1812f), new Vector3(0.006f, 0.006f, 0.003f), BoxUV.Local, 1f);
            g.Pop();
            return new Pose(tvPos + r * new Vector3(-0.06f, 0.545f, -0.159f), r);
        }

        /// <summary>Screen center (local to the TV origin) and its size.</summary>
        public static readonly Vector3 TvScreenLocal = new Vector3(-0.07f, 0.27f, -0.215f);
        public const float TvScreenW = 0.38f, TvScreenH = 0.3f;

        // ------------------------------------------------------------------ kitchen / bath

        public static void Fridge(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, bool bloody = false)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.82f);
            var body = Mat.Lit(Tex.Enamel, new Color(0.92f, 0.9f, 0.85f));
            FrontBox(mb, Mat.Lit(Tex.FridgeFront), body, new Vector3(0, 0.86f, 0), new Vector3(0.75f, 1.68f, 0.7f));
            Box(mb, Mat.Lit(Tex.MetalDark), new Vector3(0.3f, 1.25f, -0.37f), new Vector3(0.04f, 0.35f, 0.04f));
            Gray(mb, 1f);
            End(mb);
            if (bloody)
            {
                Quaternion r = MapMath.Yaw(yaw);
                Arch.Decal(mb, Mat.Decal("blood_splatter_2"), pos + r * new Vector3(-0.1f, 1.1f, -0.351f), r * Vector3.back, 0.6f, 0.6f, 20f);
                Arch.Decal(mb, Mat.Decal("blood_handprint"), pos + r * new Vector3(0.18f, 1.3f, -0.352f), r * Vector3.back, 0.2f, 0.2f, -10f);
            }
            Col(ctx, pos, yaw, new Vector3(0, 0.85f, 0), new Vector3(0.75f, 1.7f, 0.7f), SurfaceType.Metal, "Fridge");
        }

        public static void Counter(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w, bool sink = false)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.75f);
            var cab = Mat.Lit(Tex.WoodWhite, new Color(0.62f, 0.6f, 0.52f));
            FrontBox(mb, Mat.Lit(Tex.DresserFront, new Color(0.75f, 0.72f, 0.62f)), cab, new Vector3(0, 0.44f, 0.02f), new Vector3(w, 0.84f, 0.56f), 0.5f, new Rect(0, 0, Mathf.Max(1f, w), 0.5f));
            Gray(mb, 0.85f);
            mb.Material = Mat.Lit(Tex.TileWhiteDirty, new Color(0.8f, 0.78f, 0.7f));
            mb.AddBox(new Vector3(0, 0.9f, 0), new Vector3(w + 0.02f, 0.04f, 0.62f), BoxUV.Local, 0.4f, 1.0f, BoxFaces.All & ~BoxFaces.NegY);
            if (sink)
            {
                mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.42f, 0.42f, 0.4f));   // the steel basin
                mb.AddBox(new Vector3(0, 0.922f, 0), new Vector3(0.5f, 0.005f, 0.38f), BoxUV.Local, 0.5f, 0f, BoxFaces.PosY);
                mb.Material = Mat.Lit(Tex.Galvanized);
                mb.AddBeam(new Vector3(0, 0.92f, 0.26f), new Vector3(0, 1.15f, 0.26f), 0.03f);
                mb.AddBeam(new Vector3(0, 1.15f, 0.26f), new Vector3(0, 1.12f, 0.08f), 0.025f);
            }
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.46f, 0), new Vector3(w, 0.92f, 0.62f), SurfaceType.Wood, "Counter");
        }

        public static void UpperCabinet(MeshBuilder mb, Vector3 pos, float yaw, float w)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.7f);
            FrontBox(mb, Mat.Lit(Tex.DresserFront, new Color(0.75f, 0.72f, 0.62f)), Mat.Lit(Tex.WoodWhite, new Color(0.62f, 0.6f, 0.52f)),
                new Vector3(0, 0.35f, 0), new Vector3(w, 0.7f, 0.34f), 0.5f, new Rect(0, 0.5f, Mathf.Max(1f, w), 0.5f));
            Gray(mb, 1f);
            End(mb);
        }

        public static void Stove(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.75f);
            var enamel = Mat.Lit(Tex.Enamel, new Color(0.9f, 0.88f, 0.8f));
            // knobs, oven door with its window, drawer on the front; burner grates on top
            FrontBox(mb, Mat.Lit(Tex.StoveFront), enamel, new Vector3(0, 0.46f, 0), new Vector3(0.76f, 0.92f, 0.64f));
            Box(mb, enamel, new Vector3(0, 0.46f, 0), new Vector3(0.76f, 0.92f, 0.64f), 0.5f, BoxFaces.NegY);
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.3f, 0.28f, 0.26f));
            for (int i = 0; i < 4; i++)
                mb.AddCylinder(new Vector3((i % 2 == 0 ? -0.18f : 0.18f), 0.92f, (i < 2 ? -0.14f : 0.14f)), 0.09f, 0.09f, 0.015f, 8, true, false, null, false);
            Box(mb, enamel, new Vector3(0, 1.02f, 0.29f), new Vector3(0.76f, 0.2f, 0.06f));
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.46f, 0), new Vector3(0.76f, 0.92f, 0.64f), SurfaceType.Metal, "Stove");
        }

        public static void PedestalSink(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            var por = Mat.Lit(Tex.Porcelain);
            Gray(mb, 0.8f);
            mb.Material = por;
            mb.AddCylinder(Vector3.zero, 0.12f, 0.09f, 0.72f, 8, false, false, null, true);
            Box(mb, por, new Vector3(0, 0.8f, 0.02f), new Vector3(0.56f, 0.16f, 0.44f));
            Box(mb, Mat.Lit(Tex.Porcelain, new Color(0.45f, 0.43f, 0.38f)), new Vector3(0, 0.885f, -0.01f), new Vector3(0.4f, 0.005f, 0.28f), 0.4f, BoxFaces.PosY);
            mb.Material = Mat.Lit(Tex.Galvanized);
            mb.AddBeam(new Vector3(0, 0.88f, 0.18f), new Vector3(0, 1.0f, 0.18f), 0.025f);
            mb.AddBeam(new Vector3(0, 1.0f, 0.18f), new Vector3(0, 0.97f, 0.06f), 0.02f);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.44f, 0.02f), new Vector3(0.56f, 0.88f, 0.44f), SurfaceType.Tile, "Sink");
        }

        public const float SinkTop = 0.88f;

        public static void Toilet(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            var por = Mat.Lit(Tex.Porcelain, new Color(0.95f, 0.93f, 0.86f));
            Gray(mb, 0.78f);
            mb.Material = por;
            mb.AddCylinder(new Vector3(0, 0, -0.05f), 0.16f, 0.2f, 0.4f, 8, true, false, null, true);
            Box(mb, por, new Vector3(0, 0.6f, 0.2f), new Vector3(0.42f, 0.4f, 0.18f));
            Box(mb, por, new Vector3(0, 0.81f, 0.2f), new Vector3(0.45f, 0.03f, 0.2f));
            mb.Material = Mat.Lit(Tex.WoodFurniture, new Color(0.75f, 0.62f, 0.45f));   // the wooden seat
            mb.AddCylinder(new Vector3(0, 0.4f, -0.05f), 0.2f, 0.2f, 0.025f, 8, true, false, null, false);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.4f, 0.05f), new Vector3(0.45f, 0.8f, 0.6f), SurfaceType.Tile, "Toilet");
        }

        public const float ToiletTankTop = 0.825f;

        /// <summary>Clawfoot tub, long axis along local X (1.7 x 0.75).</summary>
        public static void Tub(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, bool bloody = true)
        {
            Begin(mb, pos, yaw);
            var en = Mat.Lit(Tex.Enamel, new Color(0.95f, 0.93f, 0.88f));
            Gray(mb, 0.78f);
            float L = 1.7f, W = 0.75f, H = 0.58f, t = 0.06f;
            Box(mb, en, new Vector3(0, 0.12f, 0), new Vector3(L - 0.1f, 0.06f, W - 0.1f));
            Box(mb, en, new Vector3(0, 0.1f + H * 0.5f, -W * 0.5f + t * 0.5f), new Vector3(L, H, t));
            Box(mb, en, new Vector3(0, 0.1f + H * 0.5f, W * 0.5f - t * 0.5f), new Vector3(L, H, t));
            Box(mb, en, new Vector3(-L * 0.5f + t * 0.5f, 0.1f + H * 0.5f, 0), new Vector3(t, H, W - 2 * t));
            Box(mb, en, new Vector3(L * 0.5f - t * 0.5f, 0.1f + H * 0.5f, 0), new Vector3(t, H, W - 2 * t));
            mb.Material = Mat.Lit(Tex.MetalRusty);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    mb.AddBox(new Vector3(sx * (L * 0.5f - 0.15f), 0.05f, sz * (W * 0.5f - 0.12f)), new Vector3(0.08f, 0.1f, 0.08f), BoxUV.Local, 0.3f);
            Gray(mb, 1f);
            End(mb);
            if (bloody)
            {
                Arch.FloorDecal(mb, Mat.Decal("blood_pool"), pos + Vector3.up * 0.155f, 1.1f, 0.55f, yaw);
                Quaternion r = MapMath.Yaw(yaw);
                Arch.Decal(mb, Mat.Decal("blood_smear"), pos + r * new Vector3(0.2f, 0.45f, -W * 0.5f + t + 0.002f), r * Vector3.forward, 0.7f, 0.35f, 0f);
            }
            Col(ctx, pos, yaw, new Vector3(0, (0.1f + H) * 0.5f, 0), new Vector3(L, 0.1f + H, W), SurfaceType.Tile, "Tub");
        }

        public const float TubRim = 0.68f;

        /// <summary>Dark mirror (or framed picture) on a wall at wallPoint facing outward.</summary>
        public static void WallPicture(MeshBuilder mb, Vector3 wallPoint, Vector3 outward, float w, float h, Material picture, Material frame, float frameW = 0.06f)
        {
            float yaw = MapMath.PropYawFacing(outward);
            Begin(mb, wallPoint, yaw);
            Gray(mb, 0.8f);
            mb.Material = frame;
            mb.AddBox(new Vector3(0, h * 0.5f + frameW * 0.5f, -0.025f), new Vector3(w + 2 * frameW, frameW, 0.05f), BoxUV.Local, 0.3f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
            mb.AddBox(new Vector3(0, -h * 0.5f - frameW * 0.5f, -0.025f), new Vector3(w + 2 * frameW, frameW, 0.05f), BoxUV.Local, 0.3f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
            mb.AddBox(new Vector3(-w * 0.5f - frameW * 0.5f, 0, -0.025f), new Vector3(frameW, h, 0.05f), BoxUV.Local, 0.3f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
            mb.AddBox(new Vector3(w * 0.5f + frameW * 0.5f, 0, -0.025f), new Vector3(frameW, h, 0.05f), BoxUV.Local, 0.3f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
            mb.Color = Shade.Gray(1f);
            mb.Material = picture;
            mb.AddQuad(new Vector3(-w * 0.5f, -h * 0.5f, -0.015f), new Vector3(-w * 0.5f, h * 0.5f, -0.015f), new Vector3(w * 0.5f, h * 0.5f, -0.015f), new Vector3(w * 0.5f, -h * 0.5f, -0.015f));
            End(mb);
        }

        public static void Mirror(MeshBuilder mb, Vector3 wallPoint, Vector3 outward, float w = 0.5f, float h = 0.7f)
            => WallPicture(mb, wallPoint, outward, w, h, Mat.Lit(Tex.Mirror), Mat.Lit(Tex.MetalDark), 0.03f);

        // ------------------------------------------------------------------ technical

        public static void RadioSet(MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.85f);
            FrontBox(mb, Mat.Lit(Tex.RadioSet), Mat.Lit(Tex.MetalGreen), new Vector3(0, 0.17f, 0), new Vector3(0.62f, 0.32f, 0.36f));
            Box(mb, Mat.Lit(Tex.MetalGreen), new Vector3(0.38f, 0.1f, 0.02f), new Vector3(0.16f, 0.2f, 0.3f));
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBeam(new Vector3(-0.25f, 0.32f, 0.12f), new Vector3(-0.3f, 1.2f, 0.18f), 0.012f);
            // microphone
            mb.AddBox(new Vector3(-0.42f, 0.04f, -0.08f), new Vector3(0.1f, 0.08f, 0.1f), BoxUV.Local, 0.2f);
            mb.AddBeam(new Vector3(-0.42f, 0.08f, -0.08f), new Vector3(-0.44f, 0.25f, -0.12f), 0.015f);
            mb.AddBox(new Vector3(-0.44f, 0.28f, -0.12f), new Vector3(0.05f, 0.08f, 0.05f), BoxUV.Local, 0.2f);
            Gray(mb, 1f);
            End(mb);
        }

        public static void FuseBox(MeshBuilder mb, Vector3 wallPoint, Vector3 outward, float ceilingY)
        {
            float yaw = MapMath.PropYawFacing(outward);
            Begin(mb, wallPoint, yaw);
            Gray(mb, 0.85f);
            FrontBox(mb, Mat.Lit(Tex.FuseBox), Mat.Lit(Tex.MetalDark, new Color(0.6f, 0.6f, 0.6f)), new Vector3(0, 0, -0.08f), new Vector3(0.45f, 0.6f, 0.16f));
            mb.Material = Mat.Lit(Tex.Galvanized);
            mb.AddBeam(new Vector3(0.12f, 0.3f, -0.05f), new Vector3(0.12f, ceilingY - wallPoint.y, -0.05f), 0.04f);
            mb.AddBeam(new Vector3(-0.12f, 0.3f, -0.05f), new Vector3(-0.12f, ceilingY - wallPoint.y, -0.05f), 0.03f);
            Gray(mb, 1f);
            End(mb);
        }

        public static void GeneratorProp(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float ceilingY)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.75f);
            var green = Mat.Lit(Tex.MetalGreen);
            var dark = Mat.Lit(Tex.MetalDark);
            Box(mb, dark, new Vector3(0, 0.08f, 0), new Vector3(1.5f, 0.1f, 0.85f));
            mb.Material = Mat.Lit(Tex.Generator);
            mb.AddBox(new Vector3(-0.15f, 0.5f, 0), new Vector3(0.95f, 0.75f, 0.7f), BoxUV.PerFace);
            Box(mb, green, new Vector3(0.5f, 0.42f, 0), new Vector3(0.38f, 0.6f, 0.6f));
            mb.Material = Mat.Lit(Tex.MetalRusty, new Color(0.7f, 0.25f, 0.2f));
            mb.Push(new Vector3(-0.15f, 1.05f, 0), Quaternion.Euler(0, 0, 90f));
            mb.AddCylinder(new Vector3(0, -0.4f, 0), 0.2f, 0.2f, 0.8f, 8, true, true, null, true);
            mb.Pop();
            mb.Material = dark;
            mb.AddBeam(new Vector3(0.5f, 0.72f, 0.2f), new Vector3(0.5f, ceilingY - pos.y - 0.05f, 0.35f), 0.08f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    mb.AddCylinder(new Vector3(sx * 0.65f, 0f, sz * 0.38f), 0.06f, 0.06f, 0.06f, 6, true, false, null, false);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 0.6f, 0), new Vector3(1.5f, 1.2f, 0.9f), SurfaceType.Metal, "Generator");
        }

        /// <summary>Big coal furnace, 2.2 wide (local X) x 1.6 deep, grate on the front. Returns the grate center (local).</summary>
        public static Vector3 Furnace(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float ceilingY)
        {
            Begin(mb, pos, yaw);
            var iron = Mat.Lit(Tex.MetalDark, new Color(0.55f, 0.5f, 0.45f));
            var rust = Mat.Lit(Tex.MetalRusty);
            Gray(mb, 0.7f);
            BoxSeg(mb, iron, new Vector3(0, 0.95f, 0.1f), new Vector3(2.2f, 1.8f, 1.6f), 0.8f, 0.8f);
            Box(mb, rust, new Vector3(0, 1.95f, 0.1f), new Vector3(1.9f, 0.2f, 1.4f));
            // fire box glow + grate bars
            mb.Material = Mat.Emissive(Tex.Fire0, new Color(1f, 0.55f, 0.25f));
            mb.Color = Shade.Gray(1f);
            mb.AddQuad(new Vector3(-0.45f, 0.35f, -0.69f), new Vector3(-0.45f, 0.95f, -0.69f), new Vector3(0.45f, 0.95f, -0.69f), new Vector3(0.45f, 0.35f, -0.69f));
            Gray(mb, 0.6f);
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.26f, 0.22f, 0.2f));
            for (int i = 0; i < 6; i++)
                mb.AddBox(new Vector3(-0.4f + i * 0.16f, 0.65f, -0.71f), new Vector3(0.04f, 0.62f, 0.04f), BoxUV.Local, 1f);
            Box(mb, rust, new Vector3(0, 0.3f, -0.72f), new Vector3(1.0f, 0.06f, 0.06f));
            Box(mb, rust, new Vector3(0, 1.0f, -0.72f), new Vector3(1.0f, 0.06f, 0.06f));
            // ducts to the ceiling (octopus furnace)
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.6f, 0.6f, 0.55f));
            float top = ceilingY - pos.y;
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var dir = Quaternion.Euler(0, a, 0) * Vector3.forward;
                Vector3 p0 = new Vector3(0, 2.0f, 0.1f) + dir * 0.4f;
                Vector3 p1 = new Vector3(0, top - 0.25f, 0.1f) + dir * 1.3f;
                mb.AddBeam(p0, p1, 0.22f);
                mb.AddBeam(p1, p1 + dir * 1.2f, 0.2f);
            }
            mb.AddCylinder(new Vector3(0, 2.0f, 0.1f), 0.3f, 0.3f, top - 2.0f, 8, false, false, null, true);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, 1.0f, 0.1f), new Vector3(2.2f, 2.0f, 1.6f), SurfaceType.Metal, "Furnace");
            return new Vector3(0, 0.65f, -0.75f);
        }

        public static void Workbench(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float w = 2.0f, bool pegboard = true, int seed = 0)
        {
            Begin(mb, pos, yaw);
            var wood = Mat.Lit(Tex.WoodRaw, new Color(0.7f, 0.62f, 0.52f));
            Gray(mb, 0.78f);
            mb.Material = wood;
            mb.AddBox(new Vector3(0, WorkbenchTop - 0.04f, 0), new Vector3(w, 0.08f, 0.7f), BoxUV.Local, 0.8f, 1.0f);
            Gray(mb, 0.6f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Leg(mb, wood, sx * (w * 0.5f - 0.06f), sz * 0.28f, WorkbenchTop - 0.08f, 0.08f);
            Box(mb, wood, new Vector3(0, 0.18f, 0), new Vector3(w - 0.1f, 0.03f, 0.6f), 0.8f);
            if (pegboard)
            {
                Gray(mb, 0.7f);
                Box(mb, Mat.Lit(Tex.WoodRaw, new Color(0.5f, 0.42f, 0.32f)), new Vector3(0, 1.45f, 0.33f), new Vector3(w, 0.9f, 0.02f), 0.6f);
                mb.Material = Mat.Lit(Tex.MetalDark);
                var rng = new DeterministicRandom(seed, 3);
                for (int i = 0; i < 7; i++)
                {
                    float x = -w * 0.5f + 0.2f + (w - 0.4f) * i / 6f;
                    float len = rng.Range(0.18f, 0.4f);
                    mb.AddBox(new Vector3(x, 1.55f - len * 0.5f, 0.3f), new Vector3(0.04f, len, 0.02f), BoxUV.Local, 0.3f);
                    mb.AddBox(new Vector3(x, 1.55f - len - 0.03f, 0.29f), new Vector3(0.1f, 0.06f, 0.03f), BoxUV.Local, 0.3f);
                }
            }
            // vise + toolbox
            Gray(mb, 0.7f);
            Box(mb, Mat.Lit(Tex.MetalGreen), new Vector3(w * 0.5f - 0.2f, WorkbenchTop + 0.08f, -0.2f), new Vector3(0.15f, 0.16f, 0.22f), 0.3f);
            Box(mb, Mat.Lit(Tex.MetalRusty, new Color(0.8f, 0.2f, 0.15f)), new Vector3(-w * 0.5f + 0.3f, WorkbenchTop + 0.1f, 0.1f), new Vector3(0.45f, 0.2f, 0.22f), 0.3f);
            Gray(mb, 1f);
            End(mb);
            Col(ctx, pos, yaw, new Vector3(0, WorkbenchTop * 0.5f, 0), new Vector3(w, WorkbenchTop, 0.7f), SurfaceType.Wood, "Workbench");
        }

        // ------------------------------------------------------------------ containers

        public static void Barrel(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, string tex, float tint = 1f, bool collider = true, float scale = 1f)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.85f);
            float r = 0.29f * scale, h = 0.88f * scale;
            mb.Material = Mat.Lit(tex, new Color(tint, tint, tint));
            mb.AddCylinder(Vector3.zero, r, r, h, 10, false, false, null, true);
            mb.Material = Mat.Lit(Tex.MetalDark, new Color(0.5f, 0.5f, 0.5f));
            mb.AddCylinder(new Vector3(0, h - 0.005f, 0), r * 0.98f, r * 0.98f, 0.01f, 10, true, false, null, false);
            mb.AddCylinder(new Vector3(0, h * 0.33f, 0), r + 0.008f, r + 0.008f, 0.025f, 10, false, false, null, true);
            mb.AddCylinder(new Vector3(0, h * 0.66f, 0), r + 0.008f, r + 0.008f, 0.025f, 10, false, false, null, true);
            Gray(mb, 1f);
            End(mb);
            if (collider) Col(ctx, pos, yaw, new Vector3(0, h * 0.5f, 0), new Vector3(r * 1.8f, h, r * 1.8f), SurfaceType.Metal, "Barrel");
        }

        public const float BarrelTop = 0.88f;

        public static void Bucket(MeshBuilder mb, Vector3 pos, float yaw, bool food, float tint = 1f, bool lid = true)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.85f);
            // plain pails are dented galvanised metal (they used to be untextured flat grey)
            mb.Material = food ? Mat.Lit(Tex.BucketFood, new Color(tint, tint, tint)) : Mat.Lit(Tex.Galvanized, new Color(0.85f * tint, 0.82f * tint, 0.76f * tint));
            mb.AddCylinder(Vector3.zero, 0.13f, 0.155f, 0.38f, 9, false, false, null, true);
            if (!lid)
            {
                // murky contents just below the rim (single-sided walls would otherwise show the floor through it)
                mb.Material = Mat.Lit(Tex.Dirt, new Color(0.3f * tint, 0.26f * tint, 0.18f * tint));
                mb.AddCylinder(new Vector3(0, 0.36f, 0), 0.152f, 0.152f, 0.002f, 9, true, false, null, false);
            }
            if (lid)
            {
                mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.85f * tint, 0.85f * tint, 0.8f * tint));
                mb.AddCylinder(new Vector3(0, 0.38f, 0), 0.165f, 0.16f, 0.03f, 9, true, false, null, false);
            }
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBeam(new Vector3(-0.15f, 0.33f, 0), new Vector3(0, 0.46f, 0), 0.01f);
            mb.AddBeam(new Vector3(0, 0.46f, 0), new Vector3(0.15f, 0.33f, 0), 0.01f);
            Gray(mb, 1f);
            End(mb);
        }

        public static void Crate(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, float size = 0.6f, bool cardboard = false, bool collider = true)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.8f);
            mb.Material = Mat.Lit(cardboard ? Tex.Cardboard : Tex.Crate);
            mb.AddBox(new Vector3(0, size * 0.5f * (cardboard ? 0.8f : 1f), 0), new Vector3(size, size * (cardboard ? 0.8f : 1f), size * (cardboard ? 0.75f : 1f)), BoxUV.PerFace);
            Gray(mb, 1f);
            End(mb);
            float ch = cardboard ? size * 0.8f : size, cd = cardboard ? size * 0.75f : size;
            if (collider && size >= 0.4f) Col(ctx, pos, yaw, new Vector3(0, ch * 0.5f, 0), new Vector3(size, ch, cd), SurfaceType.Wood, "Crate");
        }

        public static void Tire(MeshBuilder mb, Vector3 pos, float yaw, bool lying = true)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.7f);
            if (!lying) mb.Push(new Vector3(0, 0.33f, 0), Quaternion.Euler(0, 0, 90f));
            else mb.Push(Vector3.zero, Quaternion.identity);
            Vector3 b = lying ? Vector3.zero : new Vector3(0, -0.11f, 0);
            // tread round it, the sidewall and rusty rim on both faces
            mb.Material = Mat.Lit(Tex.CarTire);
            mb.AddCylinder(b, 0.33f, 0.33f, 0.22f, 12, true, true, Tex.TireTreadUv, true, 1, Tex.TireSideUv);
            mb.Pop();
            Gray(mb, 1f);
            End(mb);
        }

        public static void HayBale(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, bool collider = true)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.8f);
            mb.Material = Mat.Lit(Tex.HayBale);
            mb.AddBox(new Vector3(0, 0.23f, 0), new Vector3(1.0f, 0.46f, 0.5f), BoxUV.PerFace);
            Gray(mb, 1f);
            End(mb);
            if (collider) Col(ctx, pos, yaw, new Vector3(0, 0.23f, 0), new Vector3(1.0f, 0.46f, 0.5f), SurfaceType.Dirt, "Hay");
        }

        public const float HayH = 0.46f;

        public static void Pallet(MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.7f);
            var w = Mat.Lit(Tex.WoodRaw, new Color(0.6f, 0.55f, 0.45f));
            for (int i = 0; i < 3; i++) Box(mb, w, new Vector3(-0.5f + i * 0.5f, 0.05f, 0), new Vector3(0.09f, 0.09f, 1.2f), 0.6f);
            for (int i = 0; i < 6; i++) Box(mb, w, new Vector3(0, 0.115f, -0.5f + i * 0.2f), new Vector3(1.1f, 0.025f, 0.1f), 0.6f);
            Gray(mb, 1f);
            End(mb);
        }

        public const float PalletTop = 0.13f;

        // ------------------------------------------------------------------ butchery

        public static void ButcherTable(MapContext ctx, MeshBuilder mb, Vector3 pos, float yaw, int seed)
        {
            Begin(mb, pos, yaw);
            var top = Mat.Lit(Tex.TableWood, new Color(0.65f, 0.38f, 0.32f));
            var wood = Mat.Lit(Tex.WoodRaw, new Color(0.5f, 0.38f, 0.3f));
            Gray(mb, 0.8f);
            mb.Material = top;
            mb.AddBox(new Vector3(0, ButcherTop - 0.06f, 0), new Vector3(2.0f, 0.12f, 1.0f), BoxUV.PerFace, 1f, 0.9f);
            Gray(mb, 0.6f);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    Leg(mb, wood, sx * 0.88f, sz * 0.4f, ButcherTop - 0.12f, 0.1f);
            Box(mb, wood, new Vector3(0, 0.2f, 0), new Vector3(1.8f, 0.04f, 0.85f));
            // meat slabs (like the video)
            var rng = new DeterministicRandom(seed, 13);
            var meat = Mat.Lit(Tex.Meat);
            var slab = Mat.Lit(Tex.MeatSlab);
            Gray(mb, 0.95f);
            mb.Material = meat;
            mb.AddSphere(new Vector3(-0.45f, ButcherTop + 0.12f, 0.05f), new Vector3(0.45f, 0.18f, 0.35f), 7, 4);
            mb.AddSphere(new Vector3(0.35f, ButcherTop + 0.08f, 0.15f), new Vector3(0.3f, 0.12f, 0.25f), 6, 4);
            mb.Material = slab;
            for (int i = 0; i < 3; i++)
            {
                mb.Push(new Vector3(rng.Range(0.1f, 0.8f), ButcherTop + 0.03f, rng.Range(-0.35f, 0.0f)), Quaternion.Euler(0, rng.Range(0f, 180f), 0));
                mb.AddBox(Vector3.zero, new Vector3(0.32f, 0.05f, 0.18f), BoxUV.PerFace);
                mb.Pop();
            }
            // cleaver stuck in the block
            mb.Material = Mat.Lit(Tex.Galvanized);
            mb.Push(new Vector3(0.75f, ButcherTop + 0.07f, 0.3f), Quaternion.Euler(0, 30f, 8f));
            mb.AddBox(Vector3.zero, new Vector3(0.22f, 0.12f, 0.008f), BoxUV.Local, 0.3f);
            mb.Material = Mat.Lit(Tex.WoodFurniture);
            mb.AddBox(new Vector3(0.18f, 0.06f, 0), new Vector3(0.14f, 0.035f, 0.025f), BoxUV.Local, 0.3f);
            mb.Pop();
            Gray(mb, 1f);
            End(mb);
            Quaternion r = MapMath.Yaw(yaw);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), pos + Vector3.up * (ButcherTop + 0.002f) + r * new Vector3(0.1f, 0, -0.1f), 1.6f, 0.8f, yaw + 5f);
            Arch.FloorDecal(mb, Mat.Decal("blood_pool"), pos + r * new Vector3(0.2f, 0, -0.6f), 1.8f, 1.4f, yaw + 40f);
            Arch.Decal(mb, Mat.Decal("blood_smear"), pos + r * new Vector3(0, ButcherTop - 0.06f, -0.505f), r * Vector3.back, 1.6f, 0.12f, 0f);
            Col(ctx, pos, yaw, new Vector3(0, ButcherTop * 0.5f, 0), new Vector3(2.0f, ButcherTop, 1.0f), SurfaceType.Wood, "ButcherTable");
        }

        /// <summary>Ceiling rail with meat hooks and hanging chunks along local X.</summary>
        public static void MeatHooks(MeshBuilder mb, Vector3 ceilingCenter, float yaw, float length, int hooks, int seed)
        {
            Begin(mb, ceilingCenter, yaw);
            var rng = new DeterministicRandom(seed, 17);
            Gray(mb, 0.7f);
            Box(mb, Mat.Lit(Tex.MetalRusty), new Vector3(0, -0.06f, 0), new Vector3(length, 0.06f, 0.06f));
            mb.Material = Mat.Lit(Tex.MetalRusty);
            for (int i = 0; i < hooks; i++)
            {
                float x = -length * 0.5f + length * (i + 0.5f) / hooks;
                float drop = rng.Range(0.35f, 0.7f);
                mb.AddBeam(new Vector3(x, -0.09f, 0), new Vector3(x, -0.09f - drop, 0), 0.02f);
                if (rng.Chance(0.7f))
                {
                    mb.Material = Mat.Lit(Tex.Meat);
                    Gray(mb, 0.9f);
                    mb.AddSphere(new Vector3(x, -0.09f - drop - 0.32f, 0), new Vector3(rng.Range(0.14f, 0.22f), rng.Range(0.3f, 0.42f), rng.Range(0.1f, 0.16f)), 6, 5);
                    mb.Material = Mat.Lit(Tex.MetalRusty);
                    Gray(mb, 0.7f);
                }
            }
            Gray(mb, 1f);
            End(mb);
        }

        public static void SeveredHand(MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            var skin = Mat.Lit(Tex.SkinDead);
            Gray(mb, 0.9f);
            mb.Material = skin;
            mb.AddBox(new Vector3(0, 0.025f, 0), new Vector3(0.09f, 0.04f, 0.1f), BoxUV.Local, 0.2f);
            for (int i = 0; i < 4; i++) mb.AddBeam(new Vector3(-0.033f + i * 0.022f, 0.025f, 0.05f), new Vector3(-0.04f + i * 0.026f, 0.02f, 0.12f), 0.018f);
            mb.AddBeam(new Vector3(0.045f, 0.025f, 0.0f), new Vector3(0.08f, 0.02f, 0.05f), 0.02f);
            mb.Material = Mat.Lit(Tex.Meat);
            mb.AddBox(new Vector3(0, 0.025f, -0.06f), new Vector3(0.07f, 0.05f, 0.03f), BoxUV.Local, 0.2f);
            Gray(mb, 1f);
            End(mb);
            Arch.FloorDecal(mb, Mat.Decal("blood_splatter_1"), pos, 0.5f, 0.5f, yaw);
        }

        public static void Bones(MeshBuilder mb, Vector3 pos, int seed, int count = 6, bool skull = true)
        {
            var rng = new DeterministicRandom(seed, 19);
            var bone = Mat.Lit(Tex.Bone);
            mb.Material = bone;
            Gray(mb, 0.85f);
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector3(rng.Range(-0.5f, 0.5f), 0.025f, rng.Range(-0.5f, 0.5f));
                float a = rng.Range(0f, 360f);
                var d = Quaternion.Euler(0, a, 0) * Vector3.forward * rng.Range(0.12f, 0.25f);
                mb.AddBeam(p - d, p + d, 0.03f);
                mb.AddSphere(p + d, new Vector3(0.03f, 0.025f, 0.03f), 5, 3);
            }
            if (skull)
            {
                var p = pos + new Vector3(rng.Range(-0.3f, 0.3f), 0.09f, rng.Range(-0.3f, 0.3f));
                mb.AddSphere(p, new Vector3(0.09f, 0.09f, 0.11f), 6, 5);
                mb.Material = Mat.Lit(Tex.Bone, new Color(0.12f, 0.1f, 0.08f));   // eye sockets
                mb.AddBox(p + new Vector3(-0.03f, 0.0f, -0.1f), new Vector3(0.025f, 0.025f, 0.01f), BoxUV.Local, 1f);
                mb.AddBox(p + new Vector3(0.03f, 0.0f, -0.1f), new Vector3(0.025f, 0.025f, 0.01f), BoxUV.Local, 1f);
            }
            Gray(mb, 1f);
        }

        public static void CoalPile(MeshBuilder mb, Vector3 pos, float w, float d, int seed)
        {
            var rng = new DeterministicRandom(seed, 23);
            mb.Material = Mat.Lit(Tex.Coal);
            Gray(mb, 1f);
            mb.AddSphere(pos, new Vector3(w * 0.5f, 0.45f, d * 0.5f), 8, 4, new Rect(0, 0, Mathf.Max(1f, w * 1.5f), 1f));
            for (int i = 0; i < 14; i++)
            {
                var p = pos + new Vector3(rng.Range(-w * 0.6f, w * 0.6f), 0.04f, rng.Range(-d * 0.6f, d * 0.6f));
                mb.AddBox(p, Vector3.one * rng.Range(0.06f, 0.14f), BoxUV.Local, 1f);
            }
        }

        // ------------------------------------------------------------------ clutter

        public static void Rug(MeshBuilder mb, Vector3 pos, float yaw, float w, float d, Color tint)
        {
            Begin(mb, pos, yaw);
            mb.Color = Shade.Gray(0.85f);
            mb.Material = Mat.Lit(Tex.Carpet, tint);
            // thick enough that floor decals (+1.2 cm) and contact blobs stay underneath instead of z-fighting with it
            mb.AddBox(new Vector3(0, 0.013f, 0), new Vector3(w, 0.026f, d), BoxUV.PerFace, 1f, 1.0f, BoxFaces.All & ~BoxFaces.NegY);
            Gray(mb, 1f);
            End(mb);
        }

        public static void Papers(MeshBuilder mb, Vector3 pos, int seed, int count = 5, float spread = 0.6f)
        {
            var rng = new DeterministicRandom(seed, 29);
            var paper = Mat.TwoSided(Tex.PaperNote);
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector3(rng.Range(-spread, spread), 0.004f + i * 0.0015f, rng.Range(-spread, spread));
                mb.Color = Shade.Gray(rng.Range(0.6f, 0.9f));
                Arch.Quad(mb, paper, p, Vector3.up, Vector3.forward, 0.21f, 0.28f, Full, rng.Range(0f, 360f));
            }
            Gray(mb, 1f);
        }

        public static void Bottles(MeshBuilder mb, Vector3 pos, int seed, int count = 4, float spread = 0.25f)
        {
            var rng = new DeterministicRandom(seed, 43);
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector3(rng.Range(-spread, spread), 0, rng.Range(-spread, spread));
                float t = rng.NextFloat();
                // green or brown glass, a peeling label round the body, the neck above it
                mb.Material = Mat.Lit(Tex.Bottle, t < 0.5f ? new Color(0.42f, 0.7f, 0.36f) : new Color(0.8f, 0.52f, 0.26f));
                Gray(mb, 0.9f);
                Rect body = new Rect(0, 0, 1, 0.7f), neck = new Rect(0, 0.7f, 1, 0.3f);
                if (rng.Chance(0.3f))
                {
                    mb.Push(p + Vector3.up * 0.04f, Quaternion.Euler(90f, rng.Range(0f, 360f), 0));
                    mb.AddCylinder(new Vector3(0, -0.12f, 0), 0.035f, 0.035f, 0.18f, 6, true, false, body, true);
                    mb.AddCylinder(new Vector3(0, 0.06f, 0), 0.035f, 0.013f, 0.08f, 6, true, false, neck, true);
                    mb.Pop();
                }
                else
                {
                    mb.AddCylinder(p, 0.035f, 0.035f, 0.18f, 6, false, false, body, true);
                    mb.AddCylinder(p + Vector3.up * 0.18f, 0.035f, 0.013f, 0.08f, 6, true, false, neck, true);
                }
            }
            Gray(mb, 1f);
        }

        public static void Cans(MeshBuilder mb, Vector3 pos, int seed, int count = 3, float spread = 0.2f)
        {
            var rng = new DeterministicRandom(seed, 47);
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector3(rng.Range(-spread, spread), 0, rng.Range(-spread, spread));
                mb.Material = Mat.Lit(Tex.MetalRusty, new Color(rng.Range(0.5f, 1f), rng.Range(0.4f, 0.8f), 0.5f));
                Gray(mb, 0.9f);
                mb.AddCylinder(p, 0.035f, 0.035f, 0.11f, 6, true, false, null, true);
            }
            Gray(mb, 1f);
        }

        public static void Plates(MeshBuilder mb, Vector3 pos, int seed, int count = 3, float spread = 0.5f)
        {
            var rng = new DeterministicRandom(seed, 53);
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector3(rng.Range(-spread, spread), 0, rng.Range(-spread * 0.3f, spread * 0.3f));
                mb.Material = Mat.Lit(Tex.Porcelain, new Color(0.92f, 0.9f, 0.84f));
                Gray(mb, 0.9f);
                mb.AddCylinder(p, 0.1f, 0.12f, 0.02f, 8, true, false, null, false);
                if (rng.Chance(0.5f))
                {
                    mb.Material = Mat.Lit(Tex.Meat);
                    mb.AddSphere(p + Vector3.up * 0.04f, new Vector3(0.06f, 0.03f, 0.05f), 5, 3);
                }
            }
            Gray(mb, 1f);
        }

        public static void TrashBags(MeshBuilder mb, Vector3 pos, int seed, int count = 3)
        {
            var rng = new DeterministicRandom(seed, 59);
            mb.Material = Mat.Lit(Tex.TrashBag);
            for (int i = 0; i < count; i++)
            {
                var p = pos + new Vector3(rng.Range(-0.4f, 0.4f), 0.22f, rng.Range(-0.4f, 0.4f));
                Gray(mb, 0.9f);
                mb.AddSphere(p, new Vector3(rng.Range(0.25f, 0.33f), rng.Range(0.2f, 0.26f), rng.Range(0.22f, 0.3f)), 6, 4);
                mb.AddCylinder(p + Vector3.up * 0.18f, 0.05f, 0.02f, 0.12f, 5, true, false, null, false);
            }
            Gray(mb, 1f);
        }

        public static void FloorMattress(MeshBuilder mb, Vector3 pos, float yaw, bool bloody)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.75f);
            mb.Material = Mat.Lit(Tex.Mattress, bloody ? new Color(0.8f, 0.65f, 0.6f) : new Color(0.75f, 0.72f, 0.65f));
            mb.AddBox(new Vector3(0, 0.07f, 0), new Vector3(0.9f, 0.14f, 1.9f), BoxUV.Local, 0.8f, 0.8f);
            Gray(mb, 1f);
            End(mb);
            if (bloody) Arch.FloorDecal(mb, Mat.Decal("blood_splatter_3"), pos + Vector3.up * 0.142f, 0.7f, 0.7f, yaw + 30f);
        }

        public static void Chains(MeshBuilder mb, Vector3 top, float length)
        {
            mb.Material = Mat.Lit(Tex.MetalRusty, new Color(0.6f, 0.55f, 0.5f));
            Gray(mb, 0.75f);
            int links = Mathf.Max(2, Mathf.RoundToInt(length / 0.08f));
            for (int i = 0; i < links; i++)
            {
                var c = top - Vector3.up * (i * 0.08f + 0.04f);
                mb.Push(c, Quaternion.Euler(0, (i % 2) * 90f, 0));
                mb.AddBox(Vector3.zero, new Vector3(0.05f, 0.09f, 0.012f), BoxUV.Local, 0.2f, 0f, BoxFaces.Sides);
                mb.Pop();
            }
            mb.AddBeam(top - Vector3.up * (length + 0.02f), top - Vector3.up * (length + 0.2f), 0.02f);
            Gray(mb, 1f);
        }

        public static void WallPhone(MeshBuilder mb, Vector3 wallPoint, Vector3 outward)
        {
            float yaw = MapMath.PropYawFacing(outward);
            Begin(mb, wallPoint, yaw);
            Gray(mb, 0.8f);
            // yellowed beige plastic, coiled black cord
            Box(mb, Mat.Lit(Tex.Plastic, new Color(0.78f, 0.7f, 0.52f)), new Vector3(0, 0, -0.04f), new Vector3(0.18f, 0.26f, 0.08f), 0.3f);
            Box(mb, Mat.Lit(Tex.Plastic, new Color(0.72f, 0.64f, 0.46f)), new Vector3(-0.1f, 0.0f, -0.09f), new Vector3(0.05f, 0.24f, 0.05f), 0.3f);
            mb.Material = Mat.Lit(Tex.Plastic, new Color(0.13f, 0.13f, 0.12f));
            mb.AddBeam(new Vector3(-0.1f, -0.12f, -0.09f), new Vector3(-0.05f, -0.45f, -0.06f), 0.012f);
            mb.AddBeam(new Vector3(-0.05f, -0.45f, -0.06f), new Vector3(0.0f, -0.12f, -0.05f), 0.012f);
            Gray(mb, 1f);
            End(mb);
        }

        public static void FloorLampProp(MeshBuilder mb, Vector3 pos, float h = 1.55f)
        {
            Gray(mb, 0.75f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddCylinder(pos, 0.16f, 0.14f, 0.04f, 8, true, false, null, false);
            mb.AddBeam(pos, pos + Vector3.up * h, 0.03f);
            mb.Material = Mat.TwoSided(Tex.Cloth, new Color(0.75f, 0.55f, 0.35f));
            Gray(mb, 0.9f);
            mb.AddCylinder(pos + Vector3.up * (h - 0.12f), 0.24f, 0.15f, 0.3f, 8, false, false, null, false);
            Gray(mb, 1f);
        }

        public static void CoatRack(MeshBuilder mb, Vector3 pos)
        {
            Gray(mb, 0.7f);
            mb.Material = Mat.Lit(Tex.WoodFurniture);
            mb.AddBeam(pos, pos + Vector3.up * 1.8f, 0.05f);
            for (int i = 0; i < 4; i++)
            {
                var d = Quaternion.Euler(0, i * 90f, 0) * Vector3.forward;
                mb.AddBeam(pos + Vector3.up * 0.05f, pos + d * 0.3f, 0.04f);
                mb.AddBeam(pos + Vector3.up * 1.7f, pos + Vector3.up * 1.78f + d * 0.18f, 0.03f);
            }
            mb.Material = Mat.TwoSided(Tex.Cloth, new Color(0.3f, 0.28f, 0.22f));
            mb.AddBox(pos + new Vector3(0.12f, 1.3f, 0), new Vector3(0.12f, 0.75f, 0.4f), BoxUV.Local, 0.5f);
            Gray(mb, 1f);
        }

        public static void Toolbox(MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.8f);
            Box(mb, Mat.Lit(Tex.MetalRusty, new Color(0.8f, 0.25f, 0.18f)), new Vector3(0, 0.1f, 0), new Vector3(0.45f, 0.2f, 0.22f), 0.3f);
            Box(mb, Mat.Lit(Tex.MetalDark), new Vector3(0, 0.23f, 0), new Vector3(0.25f, 0.03f, 0.03f), 0.3f);
            Gray(mb, 1f);
            End(mb);
        }

        public static void GasCanProp(MeshBuilder mb, Vector3 pos, float yaw)
        {
            Begin(mb, pos, yaw);
            Gray(mb, 0.85f);
            Box(mb, Mat.Lit(Tex.MetalRusty, new Color(0.85f, 0.2f, 0.12f)), new Vector3(0, 0.17f, 0), new Vector3(0.3f, 0.34f, 0.17f), 0.3f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBeam(new Vector3(0.1f, 0.34f, 0), new Vector3(0.16f, 0.44f, 0), 0.03f);
            Gray(mb, 1f);
            End(mb);
        }

        public static void Candle(MeshBuilder mb, MeshBuilder glow, Vector3 pos)
        {
            mb.Material = Mat.Lit(Tex.Wax);
            Gray(mb, 0.9f);
            mb.AddCylinder(pos, 0.025f, 0.025f, 0.12f, 6, true, false, new Rect(0, 0, 0.5f, 1f), true);
            Gray(mb, 1f);
            if (glow != null)
            {
                glow.Material = Mat.Glow(1f, 0.75f, 0.35f);
                glow.AddSphere(pos + Vector3.up * 0.145f, new Vector3(0.012f, 0.025f, 0.012f), 4, 3);
            }
        }
    }
}
