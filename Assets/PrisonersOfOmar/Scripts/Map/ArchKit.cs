using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>Rectangular opening in a wall face: U along the wall (meters from its start), V = absolute height.</summary>
    internal struct Hole
    {
        public float U0, U1, V0, V1;
        public Hole(float u0, float u1, float v0, float v1) { U0 = u0; U1 = u1; V0 = v0; V1 = v1; }
    }

    /// <summary>How a wall face is dressed (optional lower band = wainscot / tiles / foundation).</summary>
    internal sealed class WallSkin
    {
        public Material Upper, Lower, Trim;
        public float LowerH;
        public float TileU = 1.5f, TileV = 1.5f, LowerTileU = 1.0f;
        public Color Tint = Color.white;
        public float AoFloor = 0.5f, AoCeil = 0.78f, AoCorner = 0.72f, Grime = 0.16f;
        public bool CornerAo = true;

        public static WallSkin Of(Material upper, Material lower = null, float lowerH = 0f, Material trim = null, float tile = 1.5f)
            => new WallSkin { Upper = upper, Lower = lower, LowerH = lowerH, Trim = trim, TileU = tile, TileV = tile };

        public WallSkin WithTint(Color c) { Tint = c; return this; }
    }

    internal enum WindowKind { Red = 0, Dark, Boarded }

    /// <summary>Low level architecture emitters. All coordinates are world space; the MeshBuilder matrix must be identity.</summary>
    internal static class Arch
    {
        static readonly List<float> _u = new List<float>(64), _v = new List<float>(64);

        // ------------------------------------------------------------------ breakpoints

        static void Breaks(List<float> list, float min, float max, IList<float> hard, float soft1, float soft2, float step)
        {
            list.Clear();
            list.Add(min); list.Add(max);
            if (hard != null)
                for (int i = 0; i < hard.Count; i++)
                {
                    float h = Mathf.Clamp(hard[i], min, max);
                    if (!Near(list, h, 0.01f)) list.Add(h);
                }
            if (soft1 > min && soft1 < max && !Near(list, soft1, 0.15f)) list.Add(soft1);
            if (soft2 > min && soft2 < max && !Near(list, soft2, 0.15f)) list.Add(soft2);
            int n = Mathf.Max(1, Mathf.CeilToInt((max - min) / step));
            for (int k = 1; k < n; k++)
            {
                float s = min + (max - min) * k / n;
                if (!Near(list, s, 0.3f)) list.Add(s);
            }
            list.Sort();
        }

        static bool Near(List<float> list, float v, float eps)
        {
            for (int i = 0; i < list.Count; i++) if (Mathf.Abs(list[i] - v) < eps) return true;
            return false;
        }

        static readonly List<float> _hardU = new List<float>(16), _hardV = new List<float>(16);

        // ------------------------------------------------------------------ walls

        /// <summary>
        /// Vertical wall face from a to b (XZ), visible on the RIGHT side of a→b, between y0 and y1 (the room's
        /// floor and ceiling heights are floorY / ceilY for the ambient-occlusion gradient). Holes are cut out.
        /// </summary>
        public static void WallFace(MeshBuilder mb, Vector2 a, Vector2 b, float y0, float y1, IList<Hole> holes, WallSkin skin,
            float floorY = float.NaN, float ceilY = float.NaN)
        {
            if (float.IsNaN(floorY)) floorY = y0;
            if (float.IsNaN(ceilY)) ceilY = y1;
            if (skin.Lower != null && skin.LowerH > 0.01f && y0 + skin.LowerH < y1)
            {
                Band(mb, a, b, y0, y0 + skin.LowerH, holes, skin.Lower, skin.LowerTileU, skin.LowerH, y0, skin, floorY, ceilY);
                Band(mb, a, b, y0 + skin.LowerH, y1, holes, skin.Upper, skin.TileU, skin.TileV, y0 + skin.LowerH, skin, floorY, ceilY);
                if (skin.Trim != null) TrimLine(mb, a, b, y0 + skin.LowerH, holes, skin.Trim);
            }
            else Band(mb, a, b, y0, y1, holes, skin.Upper, skin.TileU, skin.TileV, y0, skin, floorY, ceilY);
        }

        static void Band(MeshBuilder mb, Vector2 a, Vector2 b, float y0, float y1, IList<Hole> holes, Material mat,
            float tileU, float tileV, float vBase, WallSkin skin, float floorY, float ceilY)
        {
            Vector2 d2 = b - a;
            float L = d2.magnitude;
            if (L < 0.01f || y1 - y0 < 0.01f || mat == null) return;
            Vector2 dir = d2 / L;
            Vector3 n = new Vector3(dir.y, 0f, -dir.x);
            _hardU.Clear(); _hardV.Clear();
            if (holes != null)
                for (int i = 0; i < holes.Count; i++)
                {
                    var h = holes[i];
                    if (h.V1 <= y0 || h.V0 >= y1) continue;
                    _hardU.Add(h.U0); _hardU.Add(h.U1);
                    _hardV.Add(h.V0); _hardV.Add(h.V1);
                }
            Breaks(_u, 0f, L, _hardU, 0.4f, L - 0.4f, 1.3f);
            Breaks(_v, y0, y1, _hardV, floorY + 0.25f, ceilY - 0.3f, 1.25f);
            int nu = _u.Count, nv = _v.Count;
            var idx = new int[nu * nv];
            float uOff = Mathf.Abs(a.x * 0.37f + a.y * 0.61f);
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    float u = _u[i], v = _v[j];
                    var p = new Vector3(a.x + dir.x * u, v, a.y + dir.y * u);
                    float f = Shade.Edge(v - floorY, skin.AoFloor, 0.75f) * Shade.Edge(ceilY - v, skin.AoCeil, 0.5f);
                    if (skin.CornerAo) f *= Shade.Edge(u, skin.AoCorner, 0.6f) * Shade.Edge(L - u, skin.AoCorner, 0.6f);
                    f *= 1f - skin.Grime * Shade.Hash(p, 3);
                    var uv = new Vector2((uOff + u) / tileU, (v - vBase) / tileV);
                    idx[j * nu + i] = mb.AddVertex(p, n, uv, Shade.Tinted(skin.Tint, f));
                }
            mb.Material = mat;
            for (int j = 0; j < nv - 1; j++)
                for (int i = 0; i < nu - 1; i++)
                {
                    float uc = (_u[i] + _u[i + 1]) * 0.5f, vc = (_v[j] + _v[j + 1]) * 0.5f;
                    if (InHole(holes, uc, vc)) continue;
                    int ia = idx[j * nu + i], ib = idx[(j + 1) * nu + i], ic = idx[(j + 1) * nu + i + 1], id = idx[j * nu + i + 1];
                    mb.AddTriangle(ia, ib, ic);
                    mb.AddTriangle(ia, ic, id);
                }
        }

        static bool InHole(IList<Hole> holes, float u, float v)
        {
            if (holes == null) return false;
            for (int i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                if (u > h.U0 && u < h.U1 && v > h.V0 && v < h.V1) return true;
            }
            return false;
        }

        static void TrimLine(MeshBuilder mb, Vector2 a, Vector2 b, float y, IList<Hole> holes, Material trim)
        {
            Vector2 d2 = b - a;
            float L = d2.magnitude;
            if (L < 0.05f) return;
            Vector2 dir = d2 / L;
            Vector3 n = new Vector3(dir.y, 0f, -dir.x);
            var cuts = new List<Vector2>();
            if (holes != null)
                for (int i = 0; i < holes.Count; i++)
                    if (holes[i].V0 < y + 0.03f && holes[i].V1 > y - 0.03f) cuts.Add(new Vector2(holes[i].U0, holes[i].U1));
            cuts.Sort((p, q) => p.x.CompareTo(q.x));
            float cur = 0f;
            mb.Material = trim;
            var col = mb.Color;
            mb.Color = Shade.Gray(0.8f);
            for (int i = 0; i <= cuts.Count; i++)
            {
                float end = i < cuts.Count ? cuts[i].x : L;
                if (end - cur > 0.05f)
                {
                    Vector3 p0 = new Vector3(a.x + dir.x * cur, y, a.y + dir.y * cur) + n * 0.02f;
                    Vector3 p1 = new Vector3(a.x + dir.x * end, y, a.y + dir.y * end) + n * 0.02f;
                    Vector3 c = (p0 + p1) * 0.5f;
                    mb.Push(c, Quaternion.LookRotation(n, Vector3.up));
                    mb.AddBox(Vector3.zero, new Vector3(end - cur, 0.06f, 0.04f), BoxUV.Local, 0.5f);
                    mb.Pop();
                }
                if (i < cuts.Count) cur = Mathf.Max(cur, cuts[i].y);
            }
            mb.Color = col;
        }

        /// <summary>Gable triangle (apex above the middle of a→b), visible on the right side of a→b.</summary>
        public static void Gable(MeshBuilder mb, Vector2 a, Vector2 b, float yBase, float yApex, Material m, float tile, Color tint)
        {
            Vector2 d2 = b - a;
            float L = d2.magnitude;
            if (L < 0.01f) return;
            Vector2 dir = d2 / L;
            Vector3 n = new Vector3(dir.y, 0f, -dir.x);
            mb.Material = m;
            int rows = Mathf.Max(1, Mathf.CeilToInt((yApex - yBase) / 1.0f));
            for (int r = 0; r < rows; r++)
            {
                float t0 = (float)r / rows, t1 = (float)(r + 1) / rows;
                float y0 = Mathf.Lerp(yBase, yApex, t0), y1 = Mathf.Lerp(yBase, yApex, t1);
                float h0 = L * 0.5f * (1f - t0), h1 = L * 0.5f * (1f - t1);
                int cols = Mathf.Max(1, Mathf.CeilToInt(2f * h0 / 1.5f));
                for (int c = 0; c < cols; c++)
                {
                    float f0 = (float)c / cols, f1 = (float)(c + 1) / cols;
                    float ub0 = L * 0.5f - h0 + 2f * h0 * f0, ub1 = L * 0.5f - h0 + 2f * h0 * f1;
                    float ut0 = L * 0.5f - h1 + 2f * h1 * f0, ut1 = L * 0.5f - h1 + 2f * h1 * f1;
                    Vector3 pa = P(a, dir, ub0, y0), pb = P(a, dir, ut0, y1), pc = P(a, dir, ut1, y1), pd = P(a, dir, ub1, y0);
                    float sh = 0.85f + 0.15f * t0;
                    var col = Shade.Tinted(tint, sh * (1f - 0.12f * Shade.Hash(pa, 5)));
                    int i0 = mb.AddVertex(pa, n, new Vector2(ub0 / tile, (y0 - yBase) / tile), col);
                    int i1 = mb.AddVertex(pb, n, new Vector2(ut0 / tile, (y1 - yBase) / tile), col);
                    int i2 = mb.AddVertex(pc, n, new Vector2(ut1 / tile, (y1 - yBase) / tile), col);
                    int i3 = mb.AddVertex(pd, n, new Vector2(ub1 / tile, (y0 - yBase) / tile), col);
                    mb.AddTriangle(i0, i1, i2);
                    mb.AddTriangle(i0, i2, i3);
                }
            }
        }

        static Vector3 P(Vector2 a, Vector2 dir, float u, float y) => new Vector3(a.x + dir.x * u, y, a.y + dir.y * u);

        // ------------------------------------------------------------------ floors / ceilings

        /// <summary>Horizontal subdivided surface over [x0,x1]x[z0,z1] (holes skipped). Up = floor, else ceiling (faces down).</summary>
        public static void Flat(MeshBuilder mb, float x0, float z0, float x1, float z1, float y, Material m, float tile,
            IList<Rect> holes, bool up, Color tint, float aoMin = 0.6f, float aoRange = 0.9f, float grime = 0.15f, float step = 1.3f)
        {
            if (x1 - x0 < 0.01f || z1 - z0 < 0.01f || m == null) return;
            _hardU.Clear(); _hardV.Clear();
            if (holes != null)
                for (int i = 0; i < holes.Count; i++)
                {
                    _hardU.Add(holes[i].xMin); _hardU.Add(holes[i].xMax);
                    _hardV.Add(holes[i].yMin); _hardV.Add(holes[i].yMax);
                }
            Breaks(_u, x0, x1, _hardU, x0 + 0.4f, x1 - 0.4f, step);
            Breaks(_v, z0, z1, _hardV, z0 + 0.4f, z1 - 0.4f, step);
            int nu = _u.Count, nv = _v.Count;
            var idx = new int[nu * nv];
            Vector3 n = up ? Vector3.up : Vector3.down;
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    float x = _u[i], z = _v[j];
                    float d = Mathf.Min(Mathf.Min(x - x0, x1 - x), Mathf.Min(z - z0, z1 - z));
                    var p = new Vector3(x, y, z);
                    float f = Shade.Edge(d, aoMin, aoRange) * (1f - grime * Shade.Hash(p, up ? 7 : 9));
                    idx[j * nu + i] = mb.AddVertex(p, n, new Vector2(x / tile, z / tile), Shade.Tinted(tint, f));
                }
            mb.Material = m;
            for (int j = 0; j < nv - 1; j++)
                for (int i = 0; i < nu - 1; i++)
                {
                    float xc = (_u[i] + _u[i + 1]) * 0.5f, zc = (_v[j] + _v[j + 1]) * 0.5f;
                    if (InRects(holes, xc, zc)) continue;
                    int ia = idx[j * nu + i], ib = idx[(j + 1) * nu + i], ic = idx[(j + 1) * nu + i + 1], id = idx[j * nu + i + 1];
                    if (up) { mb.AddTriangle(ia, ib, ic); mb.AddTriangle(ia, ic, id); }
                    else { mb.AddTriangle(ia, ic, ib); mb.AddTriangle(ia, id, ic); }
                }
        }

        public static bool InRects(IList<Rect> rects, float x, float z)
        {
            if (rects == null) return false;
            for (int i = 0; i < rects.Count; i++)
                if (x > rects[i].xMin && x < rects[i].xMax && z > rects[i].yMin && z < rects[i].yMax) return true;
            return false;
        }

        /// <summary>Rect on the XZ plane from min/max corners (Rect.x = X, Rect.y = Z).</summary>
        public static Rect R(float x0, float z0, float x1, float z1) => Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1));

        // ------------------------------------------------------------------ colliders

        /// <summary>Wall collider pieces along a→b (thickness t, centered on the line) leaving the holes open.</summary>
        public static void WallCollider(MapContext ctx, Vector2 a, Vector2 b, float y0, float y1, float t, IList<Hole> holes, SurfaceType s, string name = "Wall")
        {
            Vector2 d2 = b - a;
            float L = d2.magnitude;
            if (L < 0.01f) return;
            Vector2 dir = d2 / L;
            var list = new List<Hole>();
            if (holes != null) list.AddRange(holes);
            list.Sort((p, q) => p.U0.CompareTo(q.U0));
            float cur = 0f;
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i];
                if (h.V1 <= y0 || h.V0 >= y1) continue;
                if (h.U0 > cur + 0.01f) Piece(ctx, a, dir, cur, h.U0, y0, y1, t, s, name);
                if (h.V1 < y1 - 0.01f) Piece(ctx, a, dir, h.U0, h.U1, h.V1, y1, t, s, name + "_Lintel");
                if (h.V0 > y0 + 0.01f) Piece(ctx, a, dir, h.U0, h.U1, y0, h.V0, t, s, name + "_Sill");
                cur = Mathf.Max(cur, h.U1);
            }
            if (L > cur + 0.01f) Piece(ctx, a, dir, cur, L, y0, y1, t, s, name);
        }

        static void Piece(MapContext ctx, Vector2 a, Vector2 dir, float u0, float u1, float y0, float y1, float t, SurfaceType s, string name)
        {
            float uc = (u0 + u1) * 0.5f;
            var c = new Vector3(a.x + dir.x * uc, (y0 + y1) * 0.5f, a.y + dir.y * uc);
            var rot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.y), Vector3.up);
            ctx.Solid(c, new Vector3(t, y1 - y0, u1 - u0), rot, s, name);
        }

        /// <summary>Slab collider (top at yTop) covering a rect minus holes.</summary>
        public static void SlabCollider(MapContext ctx, Rect r, float yTop, float thick, IList<Rect> holes, SurfaceType s, string name = "Floor")
        {
            var pieces = new List<Rect> { r };
            if (holes != null)
                for (int h = 0; h < holes.Count; h++)
                {
                    var next = new List<Rect>();
                    for (int i = 0; i < pieces.Count; i++) Subtract(pieces[i], holes[h], next);
                    pieces = next;
                }
            for (int i = 0; i < pieces.Count; i++)
            {
                var p = pieces[i];
                if (p.width < 0.02f || p.height < 0.02f) continue;
                ctx.Solid(new Vector3(p.center.x, yTop - thick * 0.5f, p.center.y), new Vector3(p.width, thick, p.height), s, name);
            }
        }

        static void Subtract(Rect r, Rect h, List<Rect> outList)
        {
            if (!r.Overlaps(h)) { outList.Add(r); return; }
            float x0 = r.xMin, x1 = r.xMax, z0 = r.yMin, z1 = r.yMax;
            float hx0 = Mathf.Max(h.xMin, x0), hx1 = Mathf.Min(h.xMax, x1), hz0 = Mathf.Max(h.yMin, z0), hz1 = Mathf.Min(h.yMax, z1);
            if (hx0 > x0) outList.Add(Rect.MinMaxRect(x0, z0, hx0, z1));
            if (hx1 < x1) outList.Add(Rect.MinMaxRect(hx1, z0, x1, z1));
            if (hz0 > z0) outList.Add(Rect.MinMaxRect(hx0, z0, hx1, hz0));
            if (hz1 < z1) outList.Add(Rect.MinMaxRect(hx0, hz1, hx1, z1));
        }

        // ------------------------------------------------------------------ axis aligned helpers (world-coordinate openings)

        /// <summary>
        /// Axis aligned wall face on the line (alongX ? z = c : x = c) from 'from' to 'to' (world coordinates along the
        /// axis), facing +axis-normal when normalSign &gt; 0. Openings are given with U0/U1 in WORLD along-coordinates.
        /// </summary>
        public static void FaceN(MeshBuilder mb, bool alongX, float c, float from, float to, float normalSign, float y0, float y1,
            IList<Hole> worldHoles, WallSkin skin, float floorY = float.NaN, float ceilY = float.NaN)
        {
            float lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
            Vector2 a, b;
            float aAlong;
            if (alongX)
            {
                if (normalSign > 0) { a = new Vector2(hi, c); b = new Vector2(lo, c); aAlong = hi; }
                else { a = new Vector2(lo, c); b = new Vector2(hi, c); aAlong = lo; }
            }
            else
            {
                if (normalSign > 0) { a = new Vector2(c, lo); b = new Vector2(c, hi); aAlong = lo; }
                else { a = new Vector2(c, hi); b = new Vector2(c, lo); aAlong = hi; }
            }
            WallFace(mb, a, b, y0, y1, ToLocal(worldHoles, aAlong, lo, hi), skin, floorY, ceilY);
        }

        static List<Hole> ToLocal(IList<Hole> worldHoles, float aAlong, float lo, float hi)
        {
            var list = new List<Hole>();
            if (worldHoles == null) return list;
            for (int i = 0; i < worldHoles.Count; i++)
            {
                var h = worldHoles[i];
                if (h.U1 <= lo || h.U0 >= hi) continue;
                float u0 = Mathf.Abs(Mathf.Clamp(h.U0, lo, hi) - aAlong), u1 = Mathf.Abs(Mathf.Clamp(h.U1, lo, hi) - aAlong);
                list.Add(new Hole(Mathf.Min(u0, u1), Mathf.Max(u0, u1), h.V0, h.V1));
            }
            return list;
        }

        /// <summary>Collider version of <see cref="FaceN"/> (thickness t centered on the line).</summary>
        public static void WallColliderN(MapContext ctx, bool alongX, float c, float from, float to, float y0, float y1, float t,
            IList<Hole> worldHoles, SurfaceType s, string name = "Wall")
        {
            float lo = Mathf.Min(from, to), hi = Mathf.Max(from, to);
            Vector2 a = alongX ? new Vector2(lo, c) : new Vector2(c, lo);
            Vector2 b = alongX ? new Vector2(hi, c) : new Vector2(c, hi);
            WallCollider(ctx, a, b, y0, y1, t, ToLocal(worldHoles, lo, lo, hi), s, name);
        }

        // ------------------------------------------------------------------ generic quads / decals

        /// <summary>Quad centered at c facing n (front = n), with an up hint. Optionally rotated around n.</summary>
        public static void Quad(MeshBuilder mb, Material m, Vector3 c, Vector3 n, Vector3 upHint, float w, float h, Rect uv, float rotDeg = 0f)
        {
            n = n.normalized;
            if (Mathf.Abs(Vector3.Dot(n, upHint.normalized)) > 0.98f) upHint = Mathf.Abs(n.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 r = Vector3.Cross(n, upHint).normalized;
            Vector3 u = Vector3.Cross(r, n).normalized;
            if (Mathf.Abs(rotDeg) > 0.01f)
            {
                var q = Quaternion.AngleAxis(rotDeg, n);
                r = q * r; u = q * u;
            }
            Vector3 a = c - r * (w * 0.5f) - u * (h * 0.5f);
            mb.Material = m;
            mb.AddQuad(a, a + u * h, a + u * h + r * w, a + r * w, uv);
        }

        static readonly Rect Full = new Rect(0, 0, 1, 1);

        /// <summary>Decal (blood, graffiti, grime) slightly off a surface.</summary>
        public static void Decal(MeshBuilder mb, Material m, Vector3 surfacePoint, Vector3 normal, float w, float h, float rotDeg = 0f, Vector3? upHint = null)
        {
            var col = mb.Color;
            mb.Color = new Color32(255, 255, 255, 255);
            Quad(mb, m, surfacePoint + normal.normalized * 0.012f, normal, upHint ?? (Mathf.Abs(normal.y) > 0.7f ? Vector3.forward : Vector3.up), w, h, Full, rotDeg);
            mb.Color = col;
        }

        public static void FloorDecal(MeshBuilder mb, Material m, Vector3 p, float w, float h, float rotDeg)
            => Decal(mb, m, p, Vector3.up, w, h, rotDeg, Vector3.forward);

        /// <summary>Soft dark blob under furniture (fake contact shadow).</summary>
        public static void Blob(MeshBuilder mb, Vector3 floorPoint, float w, float d, float yaw = 0f)
            => Decal(mb, Mat.Shadow(), floorPoint + Vector3.up * 0.004f, Vector3.up, w, d, -yaw, Vector3.forward);

        // ------------------------------------------------------------------ stairs / railings

        /// <summary>
        /// Straight stair: solid stacked steps from baseY, starting at bottomCenter (center of the first riser at floor level)
        /// climbing along dir. Adds the ramp + fill colliders. Returns the top center.
        /// </summary>
        public static Vector3 Stair(MapContext ctx, MeshBuilder mb, Vector3 bottomCenter, Vector3 dir, float width, float rise, float run,
            int steps, float baseY, Material tread, Material side, SurfaceType surf, string name)
        {
            dir.y = 0; dir.Normalize();
            var rot = Quaternion.LookRotation(dir, Vector3.up);
            float dr = rise / steps, dl = run / steps;
            var col = mb.Color;
            for (int i = 0; i < steps; i++)
            {
                float top = bottomCenter.y + (i + 1) * dr;
                float s0 = i * dl, s1 = (i + 1) * dl;
                float h = top - baseY;
                Vector3 c = bottomCenter + dir * ((s0 + s1) * 0.5f);
                c.y = baseY + h * 0.5f;
                mb.Push(c, rot);
                mb.Color = Shade.Gray(0.75f + 0.2f * Shade.Hash(c, 21));
                mb.Material = tread;
                mb.AddBox(Vector3.zero, new Vector3(width, h, dl), BoxUV.PerFace, 1f, 0f, BoxFaces.PosY);
                mb.Material = side;
                mb.Color = Shade.Gray(0.55f);
                mb.AddBox(Vector3.zero, new Vector3(width, h, dl), BoxUV.WorldAligned, 1.2f, 1.0f, BoxFaces.NegZ | BoxFaces.PosX | BoxFaces.NegX);
                mb.Pop();
            }
            mb.Color = col;
            // ramp collider through the nosings
            Vector3 p0 = bottomCenter, p1 = bottomCenter + dir * run + Vector3.up * rise;
            Vector3 s = p1 - p0;
            var rampRot = Quaternion.LookRotation(s.normalized, Vector3.up);
            Vector3 localUp = rampRot * Vector3.up;
            ctx.Solid((p0 + p1) * 0.5f - localUp * 0.15f, new Vector3(width, 0.3f, s.magnitude + 0.25f), rampRot, surf, name + "_Ramp");
            // fill under the ramp so nobody walks into the stair block
            int k = 5;
            for (int j = 1; j < k; j++)
            {
                float hgt = bottomCenter.y + rise * j / k - 0.05f;
                if (hgt - baseY < 0.15f) continue;
                Vector3 c = bottomCenter + dir * (run * (j + 0.5f) / k);
                c.y = (baseY + hgt) * 0.5f;
                ctx.Solid(c, new Vector3(width, hgt - baseY, run / k), rot, surf, name + "_Fill");
            }
            return p1;
        }

        /// <summary>Railing from base points a to b (may slope). Posts, handrail, balusters; optional collider.</summary>
        public static void Railing(MapContext ctx, MeshBuilder mb, Vector3 a, Vector3 b, float h, Material m, bool collider, float spacing = 0.28f, string name = "Railing")
        {
            mb.Material = m;
            var col = mb.Color;
            mb.Color = Shade.Gray(0.82f);
            Post(mb, a, h + 0.08f, 0.08f);
            Post(mb, b, h + 0.08f, 0.08f);
            mb.AddBeam(a + Vector3.up * h, b + Vector3.up * h, 0.065f, BoxUV.Local, 0.5f);
            mb.AddBeam(a + Vector3.up * 0.09f, b + Vector3.up * 0.09f, 0.045f, BoxUV.Local, 0.5f);
            float L = Vector3.Distance(a, b);
            int n = Mathf.Max(1, Mathf.RoundToInt(L / spacing));
            for (int i = 1; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / n);
                mb.AddBeam(p + Vector3.up * 0.09f, p + Vector3.up * h, 0.035f, BoxUV.Local, 0.5f);
            }
            mb.Color = col;
            if (collider && L > 0.05f)
            {
                Vector3 d = b - a;
                var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
                Vector3 c = (a + b) * 0.5f + Vector3.up * ((h + 0.1f) * 0.5f);
                ctx.Solid(c, new Vector3(0.1f, h + 0.1f, L), rot, SurfaceType.Wood, name);
            }
        }

        static void Post(MeshBuilder mb, Vector3 basePoint, float h, float t)
        {
            mb.AddBox(basePoint + Vector3.up * (h * 0.5f), new Vector3(t, h, t), BoxUV.Local, 0.5f);
        }

        // ------------------------------------------------------------------ doors / windows

        /// <summary>Jambs, head and casings for a door opening centered at floorCenter (wall along X if alongX).</summary>
        public static void DoorFrame(MeshBuilder mb, Vector3 floorCenter, bool alongX, float holeW, float holeH, float wallT, Material m)
        {
            mb.Push(floorCenter, alongX ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f));
            mb.Material = m;
            var col = mb.Color;
            mb.Color = Shade.Gray(0.78f);
            float hw = holeW * 0.5f, d = wallT + 0.03f;
            mb.AddBox(new Vector3(-hw + 0.02f, holeH * 0.5f, 0), new Vector3(0.04f, holeH, d), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(hw - 0.02f, holeH * 0.5f, 0), new Vector3(0.04f, holeH, d), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(0, holeH - 0.02f, 0), new Vector3(holeW, 0.04f, d), BoxUV.Local, 0.5f);
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * (wallT * 0.5f + 0.012f);
                mb.AddBox(new Vector3(-hw - 0.035f, (holeH + 0.07f) * 0.5f, z), new Vector3(0.07f, holeH + 0.07f, 0.024f), BoxUV.Local, 0.5f);
                mb.AddBox(new Vector3(hw + 0.035f, (holeH + 0.07f) * 0.5f, z), new Vector3(0.07f, holeH + 0.07f, 0.024f), BoxUV.Local, 0.5f);
                mb.AddBox(new Vector3(0, holeH + 0.035f, z), new Vector3(holeW + 0.14f, 0.07f, 0.024f), BoxUV.Local, 0.5f);
            }
            mb.Color = col;
            mb.Pop();
        }

        /// <summary>
        /// Window dressing on a wall face (no hole is cut): pane + frame + sill. center = point on the face,
        /// outward = face normal. Red windows put their pane in the glow builder.
        /// </summary>
        public static void Window(MapContext ctx, MeshBuilder mb, MeshBuilder glow, Vector3 center, Vector3 outward, float w, float h,
            WindowKind kind, bool exterior)
        {
            float yaw = MapMath.PropYawFacing(outward);
            var col = mb.Color;
            var frame = Mat.Lit(Tex.WoodWhite, exterior ? new Color(0.85f, 0.85f, 0.8f) : new Color(0.7f, 0.68f, 0.62f));
            Material pane;
            MeshBuilder target = mb;
            switch (kind)
            {
                case WindowKind.Red:
                    // drawn curtains lit from behind: dusty gray cloth (no more red)
                    if (exterior) { pane = ctx.GlowMat(Tex.Cloth, new Color(0.5f, 0.49f, 0.46f), new Color(0.16f, 0.16f, 0.15f)); }
                    else pane = ctx.GlowMat(Tex.Cloth, new Color(0.46f, 0.45f, 0.42f), new Color(0.14f, 0.14f, 0.13f));
                    target = glow ?? mb;
                    break;
                case WindowKind.Boarded:
                    pane = Mat.Lit(Tex.WindowBoarded);
                    break;
                default:
                    pane = Mat.Lit(Tex.WindowDark, exterior ? Color.white : new Color(0.6f, 0.65f, 0.75f));
                    break;
            }
            // pane
            float hw = w * 0.5f, hh = h * 0.5f, z = -0.012f;
            var tcol = target.Color;
            target.Push(center, MapMath.Yaw(yaw));
            target.Color = new Color32(255, 255, 255, 255);
            target.Material = pane;
            target.AddQuad(new Vector3(-hw, -hh, z), new Vector3(-hw, hh, z), new Vector3(hw, hh, z), new Vector3(hw, -hh, z));
            target.Pop();
            target.Color = tcol;
            // frame + sill + muntins
            mb.Push(center, MapMath.Yaw(yaw));
            mb.Material = frame;
            mb.Color = Shade.Gray(0.8f);
            mb.AddBox(new Vector3(-hw - 0.03f, 0, -0.03f), new Vector3(0.07f, h + 0.12f, 0.06f), BoxUV.Local, 0.5f, 0f, BoxFaces.NoBottom & ~BoxFaces.PosZ);
            mb.AddBox(new Vector3(hw + 0.03f, 0, -0.03f), new Vector3(0.07f, h + 0.12f, 0.06f), BoxUV.Local, 0.5f, 0f, BoxFaces.NoBottom & ~BoxFaces.PosZ);
            mb.AddBox(new Vector3(0, hh + 0.03f, -0.03f), new Vector3(w + 0.12f, 0.07f, 0.06f), BoxUV.Local, 0.5f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
            mb.AddBox(new Vector3(0, -hh - 0.04f, -0.06f), new Vector3(w + 0.2f, 0.05f, 0.12f), BoxUV.Local, 0.5f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
            if (kind != WindowKind.Boarded)
            {
                mb.AddBox(new Vector3(0, 0, -0.02f), new Vector3(0.035f, h, 0.03f), BoxUV.Local, 0.5f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
                mb.AddBox(new Vector3(0, h * 0.08f, -0.02f), new Vector3(w, 0.035f, 0.03f), BoxUV.Local, 0.5f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
            }
            else
            {
                var plank = Mat.Lit(Tex.WoodRaw, new Color(0.55f, 0.5f, 0.45f));
                mb.Material = plank;
                mb.Color = Shade.Gray(0.75f);
                for (int i = 0; i < 3; i++)
                {
                    float py = -hh + h * (0.2f + 0.3f * i);
                    mb.Push(new Vector3(0, py, -0.05f), Quaternion.Euler(0, 0, (i - 1) * 6f + 3f));
                    mb.AddBox(Vector3.zero, new Vector3(w + 0.25f, 0.16f, 0.03f), BoxUV.Local, 0.6f, 0f, BoxFaces.All & ~BoxFaces.PosZ);
                    mb.Pop();
                }
            }
            if (!exterior && kind == WindowKind.Red)
            {
                // heavy curtain folds + rod in front of the glowing cloth
                var cloth = Mat.Lit(Tex.Cloth, new Color(0.4f, 0.39f, 0.36f));
                mb.Material = cloth;
                mb.Color = Shade.Gray(0.85f);
                mb.AddBox(new Vector3(-hw + 0.08f, -0.05f, -0.09f), new Vector3(0.22f, h + 0.25f, 0.05f), BoxUV.Local, 0.6f);
                mb.AddBox(new Vector3(hw - 0.08f, -0.05f, -0.09f), new Vector3(0.22f, h + 0.25f, 0.05f), BoxUV.Local, 0.6f);
                mb.Material = Mat.Lit(Tex.MetalDark);
                mb.AddBeam(new Vector3(-hw - 0.2f, hh + 0.12f, -0.1f), new Vector3(hw + 0.2f, hh + 0.12f, -0.1f), 0.025f);
            }
            mb.Color = col;
            mb.Pop();
        }

        // ------------------------------------------------------------------ lights with fixtures

        /// <summary>Bare hanging bulb (cord + socket [+ shade]) with its PsxLight.</summary>
        public static PsxLight Bulb(MapContext ctx, MeshBuilder mb, string glowGroup, Vector3 ceilingPoint, float drop, Color color,
            float intensity, float range, PsxFlicker flicker, LightGroup group, string name, bool shade = false, float flickerAmount = 0.35f, bool broken = false)
        {
            Vector3 bulb = ceilingPoint - Vector3.up * drop;
            var col = mb.Color;
            mb.Color = Shade.Gray(0.7f);
            mb.Material = Mat.Flat(0.08f, 0.08f, 0.08f);
            mb.AddBeam(ceilingPoint, bulb + Vector3.up * 0.08f, 0.012f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBox(bulb + Vector3.up * 0.07f, new Vector3(0.05f, 0.06f, 0.05f), BoxUV.Local, 0.2f);
            if (shade)
            {
                mb.Material = Mat.Lit(Tex.MetalGreen, new Color(0.6f, 0.65f, 0.55f));
                mb.AddCylinder(bulb + Vector3.up * 0.0f, 0.24f, 0.05f, 0.14f, 8, false, false, null, false);
            }
            mb.Color = col;
            if (broken)
            {
                mb.Material = Mat.Flat(0.2f, 0.2f, 0.18f);
                mb.AddSphere(bulb + Vector3.up * 0.02f, new Vector3(0.03f, 0.025f, 0.03f), 5, 3);
                return null;
            }
            var light = ctx.Light(bulb - Vector3.up * 0.05f, color, intensity, range, flicker, group, name, flickerAmount, 9f, glowGroup);
            var g = ctx.GlowBuilder(glowGroup);
            g.Color = new Color32(255, 255, 255, 255);
            g.Material = ctx.GlowColor(new Color(Mathf.Min(1f, color.r * 1.1f + 0.1f), Mathf.Min(1f, color.g * 1.1f + 0.1f), Mathf.Min(1f, color.b + 0.1f)));
            g.AddSphere(bulb, new Vector3(0.05f, 0.065f, 0.05f), 6, 4);
            return light;
        }

        /// <summary>Wall lamp (bracket + glass) facing outward with its light.</summary>
        public static PsxLight WallLamp(MapContext ctx, MeshBuilder mb, string glowGroup, Vector3 wallPoint, Vector3 outward, Color color,
            float intensity, float range, PsxFlicker flicker, LightGroup group, string name, float flickerAmount = 0.3f)
        {
            Vector3 lampPos = wallPoint + outward.normalized * 0.18f;
            var col = mb.Color;
            mb.Color = Shade.Gray(0.7f);
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBeam(wallPoint, lampPos + Vector3.up * 0.05f, 0.03f);
            mb.AddBox(lampPos + Vector3.up * 0.11f, new Vector3(0.16f, 0.03f, 0.16f), BoxUV.Local, 0.3f);
            mb.Color = col;
            var light = ctx.Light(lampPos + outward.normalized * 0.1f, color, intensity, range, flicker, group, name, flickerAmount, 8f, glowGroup);
            var g = ctx.GlowBuilder(glowGroup);
            g.Color = new Color32(255, 255, 255, 255);
            g.Material = ctx.GlowColor(new Color(Mathf.Min(1f, color.r + 0.1f), Mathf.Min(1f, color.g + 0.1f), Mathf.Min(1f, color.b + 0.1f)));
            g.AddCylinder(lampPos - Vector3.up * 0.06f, 0.06f, 0.07f, 0.16f, 6, true, true, null, false);
            return light;
        }
    }
}
