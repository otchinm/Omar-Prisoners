using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>Rectangular outbuilding description (walls on the centerline rect, thickness T).</summary>
    internal sealed class BuildingSpec
    {
        public string Name, Area;
        public float X0, Z0, X1, Z1;
        public float WallH = 2.6f, T = 0.18f, FloorY = 0.02f;
        public WallSkin Outside, Inside;
        public Material Floor, Ceiling, Roof, Soffit, Trim;
        public SurfaceType FloorSurf = SurfaceType.Concrete, WallSurf = SurfaceType.Wood;
        /// <summary>Openings: side 'S','N','E','W', center along the wall (world coordinate), width, height.</summary>
        public readonly List<(char side, float along, float w, float h)> Openings = new List<(char, float, float, float)>();
        public bool Gable = true;
        /// <summary>Gable ridge along X (gables on W/E walls) or along Z (gables on S/N walls).</summary>
        public bool RidgeAlongX = true;
        public float RoofRise = 1.2f, Overhang = 0.4f, FloorTile = 1.5f;
        public bool FloorOverlay = true;
    }

    internal static class Buildings
    {
        static List<Hole> SideHoles(BuildingSpec b, char side)
        {
            var list = new List<Hole>();
            foreach (var o in b.Openings)
                if (o.side == side) list.Add(new Hole(o.along - o.w * 0.5f, o.along + o.w * 0.5f, 0f, o.h));
            return list;
        }

        public static void Build(MapContext ctx, MeshBuilder mb, BuildingSpec b)
        {
            float t = b.T * 0.5f, H = b.WallH;
            float ox0 = b.X0 - t, ox1 = b.X1 + t, oz0 = b.Z0 - t, oz1 = b.Z1 + t;
            float ix0 = b.X0 + t, ix1 = b.X1 - t, iz0 = b.Z0 + t, iz1 = b.Z1 - t;
            var hs = SideHoles(b, 'S'); var hn = SideHoles(b, 'N'); var hw = SideHoles(b, 'W'); var he = SideHoles(b, 'E');
            // outside faces (from the ground)
            Arch.FaceN(mb, true, oz0, ox0, ox1, -1, 0f, H, hs, b.Outside, 0f, H);
            Arch.FaceN(mb, true, oz1, ox0, ox1, +1, 0f, H, hn, b.Outside, 0f, H);
            Arch.FaceN(mb, false, ox0, oz0, oz1, -1, 0f, H, hw, b.Outside, 0f, H);
            Arch.FaceN(mb, false, ox1, oz0, oz1, +1, 0f, H, he, b.Outside, 0f, H);
            // inside faces
            Arch.FaceN(mb, true, iz0, ix0, ix1, +1, b.FloorY, H, hs, b.Inside, b.FloorY, H);
            Arch.FaceN(mb, true, iz1, ix0, ix1, -1, b.FloorY, H, hn, b.Inside, b.FloorY, H);
            Arch.FaceN(mb, false, ix0, iz0, iz1, +1, b.FloorY, H, hw, b.Inside, b.FloorY, H);
            Arch.FaceN(mb, false, ix1, iz0, iz1, -1, b.FloorY, H, he, b.Inside, b.FloorY, H);
            // opening reveals (thickness faces) so holes look solid
            if (b.Trim != null)
                foreach (var o in b.Openings)
                {
                    bool alongX = o.side == 'S' || o.side == 'N';
                    float c = o.side == 'S' ? b.Z0 : o.side == 'N' ? b.Z1 : o.side == 'W' ? b.X0 : b.X1;
                    Vector3 center = alongX ? new Vector3(o.along, b.FloorY, c) : new Vector3(c, b.FloorY, o.along); // threshold at floor level
                    Arch.DoorFrame(mb, center, alongX, o.w, o.h, b.T, b.Trim);
                }
            // floor
            if (b.FloorOverlay)
            {
                Arch.Flat(mb, ix0, iz0, ix1, iz1, b.FloorY, b.Floor, b.FloorTile, null, true, Color.white, 0.5f, 0.7f);
                Arch.SlabCollider(ctx, Rect.MinMaxRect(b.X0, b.Z0, b.X1, b.Z1), b.FloorY, 0.15f, null, b.FloorSurf, b.Name + "_Floor");
            }
            // wall colliders (full height incl. gables)
            float top = b.Gable ? H + b.RoofRise : H + 0.2f;
            Arch.WallColliderN(ctx, true, b.Z0, ox0, ox1, 0f, b.Gable && !b.RidgeAlongX ? top : H, b.T, hs, b.WallSurf, b.Name + "_Wall");
            Arch.WallColliderN(ctx, true, b.Z1, ox0, ox1, 0f, b.Gable && !b.RidgeAlongX ? top : H, b.T, hn, b.WallSurf, b.Name + "_Wall");
            Arch.WallColliderN(ctx, false, b.X0, iz0, iz1, 0f, b.Gable && b.RidgeAlongX ? top : H, b.T, hw, b.WallSurf, b.Name + "_Wall");
            Arch.WallColliderN(ctx, false, b.X1, iz0, iz1, 0f, b.Gable && b.RidgeAlongX ? top : H, b.T, he, b.WallSurf, b.Name + "_Wall");
            if (b.Gable) GableRoof(ctx, mb, b, ox0, ox1, oz0, oz1, ix0, ix1, iz0, iz1);
            else FlatRoof(ctx, mb, b, ox0, ox1, oz0, oz1, ix0, ix1, iz0, iz1);
        }

        static void GableRoof(MapContext ctx, MeshBuilder mb, BuildingSpec b, float ox0, float ox1, float oz0, float oz1, float ix0, float ix1, float iz0, float iz1)
        {
            float H = b.WallH, R = H + b.RoofRise, o = b.Overhang;
            var wallMat = b.Outside.Upper;
            var inMat = b.Inside.Upper;
            if (b.RidgeAlongX)
            {
                float zc = (b.Z0 + b.Z1) * 0.5f, half = (oz1 - oz0) * 0.5f, slope = b.RoofRise / half;
                Arch.Gable(mb, new Vector2(ox0, oz1), new Vector2(ox0, oz0), H, R, wallMat, 1.5f, new Color(0.8f, 0.8f, 0.8f));
                Arch.Gable(mb, new Vector2(ox1, oz0), new Vector2(ox1, oz1), H, R, wallMat, 1.5f, new Color(0.8f, 0.8f, 0.8f));
                Arch.Gable(mb, new Vector2(ix0, iz0), new Vector2(ix0, iz1), H, R - 0.05f, inMat, 1.5f, new Color(0.55f, 0.55f, 0.55f));
                Arch.Gable(mb, new Vector2(ix1, iz1), new Vector2(ix1, iz0), H, R - 0.05f, inMat, 1.5f, new Color(0.55f, 0.55f, 0.55f));
                float yE = H - o * slope, zE0 = oz0 - o, zE1 = oz1 + o, xa = ox0 - o * 0.6f, xb = ox1 + o * 0.6f;
                mb.Material = b.Roof;
                mb.Color = Shade.Gray(0.8f);
                mb.AddPlane(new Vector3(xa, yE, zE0), new Vector3(xb - xa, 0, 0), new Vector3(0, R - yE, zc - zE0), 2f, 2f);
                mb.AddPlane(new Vector3(xb, yE, zE1), new Vector3(xa - xb, 0, 0), new Vector3(0, R - yE, zc - zE1), 2f, 2f);
                mb.Material = b.Soffit ?? b.Roof;
                mb.Color = Shade.Gray(0.45f);
                mb.AddPlane(new Vector3(xb, yE - 0.06f, zE0), new Vector3(xa - xb, 0, 0), new Vector3(0, R - yE, zc - zE0), 2f, 2f);
                mb.AddPlane(new Vector3(xa, yE - 0.06f, zE1), new Vector3(xb - xa, 0, 0), new Vector3(0, R - yE, zc - zE1), 2f, 2f);
                mb.Color = Shade.Gray(1f);
                RoofCollider(ctx, new Vector3((xa + xb) * 0.5f, (yE + R) * 0.5f, (zE0 + zc) * 0.5f), new Vector3(0, R - yE, zc - zE0), xb - xa, b.Name);
                RoofCollider(ctx, new Vector3((xa + xb) * 0.5f, (yE + R) * 0.5f, (zE1 + zc) * 0.5f), new Vector3(0, R - yE, zc - zE1), xb - xa, b.Name);
            }
            else
            {
                float xc = (b.X0 + b.X1) * 0.5f, half = (ox1 - ox0) * 0.5f, slope = b.RoofRise / half;
                Arch.Gable(mb, new Vector2(ox0, oz0), new Vector2(ox1, oz0), H, R, wallMat, 1.5f, new Color(0.8f, 0.8f, 0.8f));
                Arch.Gable(mb, new Vector2(ox1, oz1), new Vector2(ox0, oz1), H, R, wallMat, 1.5f, new Color(0.8f, 0.8f, 0.8f));
                Arch.Gable(mb, new Vector2(ix1, iz0), new Vector2(ix0, iz0), H, R - 0.05f, inMat, 1.5f, new Color(0.55f, 0.55f, 0.55f));
                Arch.Gable(mb, new Vector2(ix0, iz1), new Vector2(ix1, iz1), H, R - 0.05f, inMat, 1.5f, new Color(0.55f, 0.55f, 0.55f));
                float yE = H - o * slope, xE0 = ox0 - o, xE1 = ox1 + o, za = oz0 - o * 0.6f, zb = oz1 + o * 0.6f;
                mb.Material = b.Roof;
                mb.Color = Shade.Gray(0.8f);
                mb.AddPlane(new Vector3(xE0, yE, zb), new Vector3(0, 0, za - zb), new Vector3(xc - xE0, R - yE, 0), 2f, 2f);
                mb.AddPlane(new Vector3(xE1, yE, za), new Vector3(0, 0, zb - za), new Vector3(xc - xE1, R - yE, 0), 2f, 2f);
                mb.Material = b.Soffit ?? b.Roof;
                mb.Color = Shade.Gray(0.45f);
                mb.AddPlane(new Vector3(xE0, yE - 0.06f, za), new Vector3(0, 0, zb - za), new Vector3(xc - xE0, R - yE, 0), 2f, 2f);
                mb.AddPlane(new Vector3(xE1, yE - 0.06f, zb), new Vector3(0, 0, za - zb), new Vector3(xc - xE1, R - yE, 0), 2f, 2f);
                mb.Color = Shade.Gray(1f);
                RoofCollider(ctx, new Vector3((xE0 + xc) * 0.5f, (yE + R) * 0.5f, (za + zb) * 0.5f), new Vector3(xc - xE0, R - yE, 0), zb - za, b.Name);
                RoofCollider(ctx, new Vector3((xE1 + xc) * 0.5f, (yE + R) * 0.5f, (za + zb) * 0.5f), new Vector3(xc - xE1, R - yE, 0), zb - za, b.Name);
            }
        }

        static void RoofCollider(MapContext ctx, Vector3 center, Vector3 slopeDir, float width, string name)
        {
            var rot = Quaternion.LookRotation(slopeDir.normalized, Vector3.up);
            ctx.Solid(center - rot * Vector3.up * 0.1f, new Vector3(width, 0.2f, slopeDir.magnitude), rot, SurfaceType.Metal, name + "_Roof");
        }

        static void FlatRoof(MapContext ctx, MeshBuilder mb, BuildingSpec b, float ox0, float ox1, float oz0, float oz1, float ix0, float ix1, float iz0, float iz1)
        {
            float H = b.WallH, o = b.Overhang;
            Arch.Flat(mb, ix0, iz0, ix1, iz1, H, b.Ceiling, 1.6f, null, false, Color.white, 0.6f, 0.6f);
            mb.Material = b.Roof;
            mb.Color = Shade.Gray(0.7f);
            mb.AddBox(new Vector3((ox0 + ox1) * 0.5f, H + 0.15f, (oz0 + oz1) * 0.5f), new Vector3(ox1 - ox0 + 2 * o, 0.3f, oz1 - oz0 + 2 * o), BoxUV.Local, 1.5f, 2f);
            mb.Color = Shade.Gray(1f);
            ctx.Solid(new Vector3((ox0 + ox1) * 0.5f, H + 0.15f, (oz0 + oz1) * 0.5f), new Vector3(ox1 - ox0 + 2 * o, 0.3f, oz1 - oz0 + 2 * o), SurfaceType.Concrete, b.Name + "_Roof");
        }
    }
}
