using System;
using PrisonersOfOmar.Characters;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>Doorway description used by <see cref="Dyn.Door"/>.</summary>
    internal sealed class DoorSpec
    {
        public string Name;
        public DoorKind Kind;
        /// <summary>Center of the doorway at floor height (on the wall centerline).</summary>
        public Vector3 Center;
        /// <summary>true = the wall runs along X (constant Z).</summary>
        public bool AlongX;
        /// <summary>World direction (perpendicular to the wall) into the room the leaf swings into.</summary>
        public Vector3 Swing;
        /// <summary>Extent of the swing room along the wall axis (used to put the hinge near the closer corner).</summary>
        public float RoomMin = -1000f, RoomMax = 1000f;
        public bool Locked;
        public ItemType Key = ItemType.None;
        public float WallT = 0.14f, HoleW = 1.0f, HoleH = 2.2f;
        public string AreaSwing = "", AreaOther = "";
        /// <summary>0 = automatic, -1 = hinge on the min side of the axis, +1 = on the max side.</summary>
        public int HingeSide;
        /// <summary>Static builder that receives the frame (jambs + casings); null = no frame.</summary>
        public MeshBuilder FrameMb;
        public Material FrameMat;
        public bool Nav = true;
        public float NavOffset = 0.75f;
        public float OpenDegrees = 90f;
    }

    /// <summary>Builders for the dynamic (separately transformable) parts of the map.</summary>
    internal static class Dyn
    {
        static readonly Rect Full = new Rect(0, 0, 1, 1);
        static readonly Rect Mirrored = new Rect(1, 0, -1, 1);

        // ================================================================== doors

        public static string DoorTexture(DoorKind k)
        {
            switch (k)
            {
                case DoorKind.WoodDirty: case DoorKind.Boarded: return Tex.DoorWoodDirty;
                case DoorKind.Metal: case DoorKind.Restroom: case DoorKind.Silo: return Tex.DoorMetal;
                case DoorKind.Front: return Tex.DoorFront;
                case DoorKind.Shed: return Tex.DoorPlanks;
                case DoorKind.Shelter: return Tex.DoorShelter;
                default: return Tex.DoorWood;
            }
        }

        static bool IsMetal(DoorKind k) => k == DoorKind.Metal || k == DoorKind.Restroom || k == DoorKind.Silo || k == DoorKind.Shelter;

        /// <summary>Yaw that maps local +X to <paramref name="dir"/> (horizontal).</summary>
        public static float YawForX(Vector3 dir) => Mathf.Atan2(-dir.z, dir.x) * Mathf.Rad2Deg;

        /// <summary>
        /// Creates a hinged leaf: Pivot (at the hinge, floor level, local +X = from hinge to latch) with the leaf mesh
        /// child (+ BoxCollider on <paramref name="layer"/>). Returns the pivot; outputs the collider and the signed
        /// open angle that swings the leaf towards <paramref name="swing"/>.
        /// </summary>
        public static Transform HingedLeaf(MapContext ctx, Transform parent, string name, Vector3 hinge, Vector3 leafDir, Vector3 swing,
            float w, float h, float t, string tex, Material edge, int layer, float openDegrees, out BoxCollider leafCol, out float openAngle,
            float floorGap = 0.02f, bool knobs = true, Color? tint = null)
        {
            leafDir.y = 0; leafDir.Normalize();
            var rot = MapMath.Yaw(YawForX(leafDir));
            var pivot = GeoUtil.CreateChild(parent, name, Vector3.zero, Quaternion.identity, layer);
            pivot.position = hinge;
            pivot.rotation = rot;
            Vector3 nl = Quaternion.Inverse(rot) * swing;
            openAngle = nl.z < 0f ? openDegrees : -openDegrees;

            var mb = new MeshBuilder();
            var face = Mat.Lit(tex, tint);
            var c = new Vector3(w * 0.5f, floorGap + h * 0.5f, 0f);
            var size = new Vector3(w, h, t);
            mb.Color = Shade.Gray(0.85f);
            mb.Material = face;
            mb.AddBox(c, size, new BoxUVRects { NegZ = Full, PosZ = Mirrored }, BoxFaces.NegZ | BoxFaces.PosZ);
            mb.Material = edge;
            mb.AddBox(c, size, BoxUV.Local, 0.5f, 0f, BoxFaces.PosX | BoxFaces.NegX | BoxFaces.PosY | BoxFaces.NegY);
            if (knobs)
            {
                mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.75f, 0.6f, 0.3f));
                for (int s = -1; s <= 1; s += 2)
                    mb.AddBox(new Vector3(w - 0.09f, floorGap + 1.0f, s * (t * 0.5f + 0.03f)), new Vector3(0.05f, 0.05f, 0.06f), BoxUV.Local, 0.2f);
            }
            var go = mb.Build("Leaf", pivot, layer);
            ctx.CountRenderer(mb);
            leafCol = go.AddComponent<BoxCollider>();
            leafCol.center = c;
            leafCol.size = size + new Vector3(0f, 0f, 0.01f);
            return pivot;
        }

        public static DoorInfo Door(MapContext ctx, DoorSpec s)
        {
            Vector3 axis = s.AlongX ? Vector3.right : Vector3.forward;
            Vector3 swing = s.Swing; swing.y = 0; swing.Normalize();
            float c = s.AlongX ? s.Center.x : s.Center.z;
            bool hingeAtMin = s.HingeSide < 0 || (s.HingeSide == 0 && (c - s.RoomMin) <= (s.RoomMax - c));
            float leafW = s.HoleW - 0.1f, leafH = s.HoleH - 0.1f;
            Vector3 hinge = s.Center + (hingeAtMin ? -axis : axis) * (leafW * 0.5f);
            Vector3 leafDir = hingeAtMin ? axis : -axis;
            bool metal = IsMetal(s.Kind);
            float t = metal ? 0.06f : 0.045f;
            Material edge = metal ? Mat.Lit(Tex.MetalDark) : (s.Kind == DoorKind.Shed ? Mat.Lit(Tex.WoodRaw) : Mat.Lit(Tex.WoodFurniture));
            Color? tint = s.Kind == DoorKind.Silo ? new Color(0.75f, 0.55f, 0.45f) : (Color?)null;

            var pivot = HingedLeaf(ctx, ctx.Dynamic, "Door_" + s.Name, hinge, leafDir, swing, leafW, leafH, t, DoorTexture(s.Kind), edge,
                Layers.Door, s.OpenDegrees, out var leafCol, out float angle, 0.02f, true, tint);

            if (s.FrameMb != null)
            {
                var fm = s.FrameMat ?? (metal ? Mat.Lit(Tex.MetalDark) : Mat.Lit(Tex.WoodWhite, new Color(0.62f, 0.6f, 0.55f)));
                Arch.DoorFrame(s.FrameMb, s.Center, s.AlongX, s.HoleW, s.HoleH, s.WallT, fm);
            }

            GameObject boards = null;
            if (s.Kind == DoorKind.Boarded) boards = Boards(ctx, s.Name, s.Center, axis, -swing, s.WallT, s.HoleW, s.HoleH);

            var info = new DoorInfo
            {
                Name = s.Name,
                Kind = s.Kind,
                Pivot = pivot,
                OpenAngle = angle,
                Leaf = leafCol,
                StartsOpen = false,
                StartsLocked = s.Locked || s.Kind == DoorKind.Boarded,
                KeyItem = s.Kind == DoorKind.Boarded ? ItemType.Crowbar : s.Key,
                Boards = boards,
                Center = s.Center,
                SwingDirection = swing,
                AreaSwingSide = s.AreaSwing,
                AreaOtherSide = s.AreaOther,
            };
            int index = ctx.Data.Doors.Count;
            ctx.Data.Doors.Add(info);
            if (s.Nav)
            {
                int a = ctx.Nav.Add(s.Center + swing * s.NavOffset, s.AreaSwing, true);
                int b = ctx.Nav.Add(s.Center - swing * s.NavOffset, s.AreaOther, true);
                ctx.Nav.Link(a, b, index);
            }
            return info;
        }

        /// <summary>Planks nailed across a doorway on the approach side (separate object so gameplay can remove it).</summary>
        static GameObject Boards(MapContext ctx, string name, Vector3 center, Vector3 axis, Vector3 approach, float wallT, float w, float h)
        {
            var mb = new MeshBuilder();
            var plank = Mat.Lit(Tex.WoodRaw, new Color(0.55f, 0.48f, 0.4f));
            Vector3 face = center + approach.normalized * (wallT * 0.5f + 0.06f);
            float yaw = MapMath.PropYawFacing(approach);
            float[] heights = { 0.45f, 1.05f, 1.65f, 1.15f };
            float[] tilt = { 4f, -6f, 3f, 32f };
            for (int i = 0; i < heights.Length; i++)
            {
                mb.Push(face + Vector3.up * heights[i], MapMath.Yaw(yaw) * Quaternion.Euler(0, 0, tilt[i]));
                mb.Color = Shade.Gray(0.7f + 0.1f * i % 0.3f);
                mb.Material = plank;
                mb.AddBox(new Vector3(0, 0, -0.02f * (i == 3 ? 2 : 1)), new Vector3(w + (i == 3 ? 0.45f : 0.35f), 0.17f, 0.035f), BoxUV.Local, 0.7f);
                mb.Material = Mat.Lit(Tex.MetalDark);
                for (int s = -1; s <= 1; s += 2)
                    mb.AddBox(new Vector3(s * (w * 0.5f + 0.08f), 0, -0.045f * (i == 3 ? 1.6f : 1f)), new Vector3(0.025f, 0.025f, 0.02f), BoxUV.Local, 0.1f);
                mb.Pop();
            }
            var go = mb.Build("Boards_" + name, ctx.Dynamic, Layers.World);
            ctx.CountRenderer(mb);
            return go;
        }

        // ================================================================== hiding spots

        public static HidingSpotInfo Wardrobe(MapContext ctx, string name, Vector3 pos, float yaw, float w = 1.2f, float h = 2.05f, float d = 0.62f)
        {
            var rot = MapMath.Yaw(yaw);
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Hide_" + name, pos, rot, Layers.World);
            var wood = Mat.Lit(Tex.WoodFurniture);
            var mb = new MeshBuilder();
            mb.Color = Shade.Gray(0.75f);
            mb.Material = wood;
            mb.AddBox(new Vector3(0, h * 0.5f, d * 0.5f - 0.015f), new Vector3(w, h, 0.03f), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(-w * 0.5f + 0.015f, h * 0.5f, 0), new Vector3(0.03f, h, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(w * 0.5f - 0.015f, h * 0.5f, 0), new Vector3(0.03f, h, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(0, h - 0.015f, 0), new Vector3(w, 0.03f, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(0, h + 0.03f, -0.01f), new Vector3(w + 0.07f, 0.06f, d + 0.05f), BoxUV.Local, 0.6f);
            // hanging coats at the sides
            mb.Material = Mat.Lit(Tex.MetalDark);
            mb.AddBeam(new Vector3(-w * 0.5f + 0.03f, h - 0.15f, 0.08f), new Vector3(w * 0.5f - 0.03f, h - 0.15f, 0.08f), 0.02f);
            mb.Material = Mat.TwoSided(Tex.Cloth, new Color(0.3f, 0.27f, 0.22f));
            mb.Color = Shade.Gray(0.6f);
            mb.AddBox(new Vector3(-w * 0.5f + 0.16f, h - 0.6f, 0.08f), new Vector3(0.2f, 0.85f, 0.4f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(w * 0.5f - 0.14f, h - 0.65f, 0.1f), new Vector3(0.16f, 0.95f, 0.38f), BoxUV.Local, 0.5f);
            mb.Build("Body", root, Layers.World);
            ctx.CountRenderer(mb);

            float wl = w * 0.5f - 0.035f;
            var left = WardrobeDoor(ctx, root, new Vector3(-w * 0.5f + 0.03f, 0.1f, -d * 0.5f + 0.015f), 0f, wl, h - 0.13f);
            var right = WardrobeDoor(ctx, root, new Vector3(w * 0.5f - 0.03f, 0.1f, -d * 0.5f + 0.015f), 180f, wl, h - 0.13f);

            GeoUtil.AddBox(root, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), Quaternion.identity, Layers.World, SurfaceType.Wood, false, "Body");
            var interact = GeoUtil.AddBox(root, new Vector3(0, h * 0.5f, -d * 0.5f - 0.09f), new Vector3(w * 0.9f, h * 0.9f, 0.2f), Quaternion.identity,
                Layers.Interactable, SurfaceType.Default, true, "Interact");
            Vector3 front = rot * Vector3.back;
            var info = new HidingSpotInfo
            {
                Name = name,
                Kind = HidingKind.Wardrobe,
                Root = root,
                Interact = interact,
                HiddenView = new Pose(pos + rot * new Vector3(0, 1.55f, 0.02f), Quaternion.LookRotation(front, Vector3.up)),
                ExitPose = MapMath.FacingPose(pos + front * (d * 0.5f + 0.6f), front),
                Doors = new[] { left, right },
                DoorOpenAngles = new[] { 100f, -100f },
            };
            ctx.Data.HidingSpots.Add(info);
            Arch.Blob(StaticShadow(ctx), pos, w + 0.4f, d + 0.4f, yaw);
            return info;
        }

        /// <summary>Shared static builder for contact shadows of dynamic props.</summary>
        static MeshBuilder StaticShadow(MapContext ctx) => ctx.SharedBuilder("PropShadows");

        /// <summary>Louvered wardrobe door (pivot at the hinge, leaf extends along local +X).</summary>
        static Transform WardrobeDoor(MapContext ctx, Transform root, Vector3 hingeLocal, float localYaw, float wl, float h)
        {
            var pivot = GeoUtil.CreateChild(root, "Door", hingeLocal, MapMath.Yaw(localYaw), Layers.World);
            var mb = new MeshBuilder();
            var wood = Mat.Lit(Tex.WoodFurniture, new Color(0.9f, 0.85f, 0.8f));
            var panel = Mat.Lit(Tex.WardrobeFront);
            mb.Color = Shade.Gray(0.8f);
            mb.Material = wood;
            float t = 0.025f;
            mb.AddBox(new Vector3(0.03f, h * 0.5f, 0), new Vector3(0.06f, h, t), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(wl - 0.03f, h * 0.5f, 0), new Vector3(0.06f, h, t), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(wl * 0.5f, 0.04f, 0), new Vector3(wl, 0.08f, t), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(wl * 0.5f, 0.92f, 0), new Vector3(wl, 0.07f, t), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(wl * 0.5f, h - 0.04f, 0), new Vector3(wl, 0.08f, t), BoxUV.Local, 0.5f);
            mb.Material = panel;
            float u0 = localYaw > 90f ? 0.5f : 0f;
            mb.AddBox(new Vector3(wl * 0.5f, 0.48f, 0), new Vector3(wl - 0.1f, 0.8f, 0.015f),
                BoxUVRects.All(new Rect(u0, 0.05f, 0.5f, 0.4f)), BoxFaces.NegZ | BoxFaces.PosZ);
            // louvers (gaps between them = the view out while hidden)
            mb.Material = wood;
            for (float y = 1.0f; y < h - 0.12f; y += 0.075f)
            {
                mb.Push(new Vector3(wl * 0.5f, y, 0), Quaternion.Euler(38f, 0, 0));
                mb.AddBox(Vector3.zero, new Vector3(wl - 0.1f, 0.04f, 0.012f), BoxUV.Local, 0.5f);
                mb.Pop();
            }
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.7f, 0.55f, 0.3f));
            mb.AddBox(new Vector3(wl - 0.07f, 1.05f, -0.025f), new Vector3(0.025f, 0.08f, 0.025f), BoxUV.Local, 0.2f);
            mb.Build("Leaf", pivot, Layers.World);
            ctx.CountRenderer(mb);
            return pivot;
        }

        public static HidingSpotInfo Locker(MapContext ctx, string name, Vector3 pos, float yaw, bool green = true)
        {
            var rot = MapMath.Yaw(yaw);
            float w = 0.6f, h = 1.9f, d = 0.5f;
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Hide_" + name, pos, rot, Layers.World);
            var metal = Mat.Lit(green ? Tex.MetalGreen : Tex.MetalDark);
            var mb = new MeshBuilder();
            mb.Color = Shade.Gray(0.7f);
            mb.Material = metal;
            mb.AddBox(new Vector3(0, h * 0.5f, d * 0.5f - 0.01f), new Vector3(w, h, 0.02f), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(-w * 0.5f + 0.01f, h * 0.5f, 0), new Vector3(0.02f, h, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(w * 0.5f - 0.01f, h * 0.5f, 0), new Vector3(0.02f, h, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(0, h - 0.01f, 0), new Vector3(w, 0.02f, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(0, 0.04f, 0), new Vector3(w, 0.08f, d), BoxUV.Local, 0.6f);
            mb.AddBox(new Vector3(0, 1.62f, 0.03f), new Vector3(w - 0.04f, 0.02f, d - 0.1f), BoxUV.Local, 0.6f);
            mb.Build("Body", root, Layers.World);
            ctx.CountRenderer(mb);

            var pivot = GeoUtil.CreateChild(root, "Door", new Vector3(-w * 0.5f + 0.01f, 0.08f, -d * 0.5f + 0.01f), Quaternion.identity, Layers.World);
            var dm = new MeshBuilder();
            dm.Color = Shade.Gray(0.75f);
            dm.Material = metal;
            float dw = w - 0.02f, dh = h - 0.1f, t = 0.02f;
            dm.AddBox(new Vector3(dw * 0.5f, 0.7f, 0), new Vector3(dw, 1.4f, t), BoxUV.Local, 0.6f);
            dm.AddBox(new Vector3(dw * 0.5f, dh - 0.12f, 0), new Vector3(dw, 0.24f, t), BoxUV.Local, 0.6f);
            dm.AddBox(new Vector3(0.05f, 1.52f, 0), new Vector3(0.1f, 0.24f, t), BoxUV.Local, 0.6f);
            dm.AddBox(new Vector3(dw - 0.05f, 1.52f, 0), new Vector3(0.1f, 0.24f, t), BoxUV.Local, 0.6f);
            for (int i = 0; i < 4; i++)
                dm.AddBox(new Vector3(dw * 0.5f, 1.43f + i * 0.055f, 0), new Vector3(dw - 0.2f, 0.022f, t), BoxUV.Local, 0.6f);
            dm.Material = Mat.Lit(Tex.MetalDark);
            dm.AddBox(new Vector3(dw - 0.08f, 1.0f, -0.025f), new Vector3(0.03f, 0.14f, 0.03f), BoxUV.Local, 0.2f);
            dm.Build("Leaf", pivot, Layers.World);
            ctx.CountRenderer(dm);

            GeoUtil.AddBox(root, new Vector3(0, h * 0.5f, 0), new Vector3(w, h, d), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "Body");
            var interact = GeoUtil.AddBox(root, new Vector3(0, h * 0.5f, -d * 0.5f - 0.09f), new Vector3(w * 0.95f, h * 0.9f, 0.2f), Quaternion.identity,
                Layers.Interactable, SurfaceType.Default, true, "Interact");
            Vector3 front = rot * Vector3.back;
            var info = new HidingSpotInfo
            {
                Name = name,
                Kind = HidingKind.Locker,
                Root = root,
                Interact = interact,
                HiddenView = new Pose(pos + rot * new Vector3(0, 1.52f, 0.02f), Quaternion.LookRotation(front, Vector3.up)),
                ExitPose = MapMath.FacingPose(pos + front * (d * 0.5f + 0.55f), front),
                Doors = new[] { pivot },
                DoorOpenAngles = new[] { 105f },
            };
            ctx.Data.HidingSpots.Add(info);
            Arch.Blob(StaticShadow(ctx), pos, w + 0.3f, d + 0.3f, yaw);
            return info;
        }

        /// <summary>
        /// A bed / cot Omar can tip over: built under a LiftPivot hinged along the long edge away from
        /// <paramref name="openSide"/> (floor level), so lifting the open side rotates it up and away from him.
        /// Pass the returned pivot to <see cref="UnderBed"/>. Things lying on the bed should be parented to it.
        /// </summary>
        public static Transform LiftableBed(MapContext ctx, string name, Vector3 bedPos, float yaw, Vector3 openSide, bool cot,
            bool pillow = true, bool bloody = false, bool bloodOnMattress = false)
        {
            var rot = MapMath.Yaw(yaw);
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Bed_" + name, bedPos, rot, Layers.World);
            openSide.y = 0; openSide.Normalize();
            float sx = (Quaternion.Inverse(rot) * openSide).x >= 0 ? 1f : -1f;
            float hw = cot ? 0.4f : 0.5f;
            var pivot = GeoUtil.CreateChild(root, "LiftPivot", new Vector3(-sx * hw, 0f, 0f), Quaternion.identity, Layers.World);
            var c = new Vector3(sx * hw, 0f, 0f);
            var mb = new MeshBuilder();
            if (cot) Props.Cot(null, mb, c, 0f);
            else Props.BedMetal(null, mb, c, 0f, pillow, bloody);
            if (bloodOnMattress && !cot)
                Arch.Decal(mb, Mat.Decal("blood_splatter_2"), c + new Vector3(0.08f, Props.BedTop + 0.012f, -0.25f), Vector3.up, 0.9f, 0.7f, 0f);
            mb.Build("Frame", pivot, Layers.World);
            ctx.CountRenderer(mb);
            if (cot) GeoUtil.AddBox(pivot, c + new Vector3(0, 0.23f, 0), new Vector3(0.8f, 0.46f, 1.9f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "Cot");
            else GeoUtil.AddBox(pivot, c + new Vector3(0, 0.3f, 0), new Vector3(1.0f, 0.6f, 2.0f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "Bed");
            return pivot;
        }

        /// <summary>Under-bed hiding spot for a bed/cot at bedPos (long axis along local Z of bedYaw).
        /// <paramref name="liftPivot"/> = the bed's <see cref="LiftableBed"/> pivot (Omar tips it up when he searches).</summary>
        public static HidingSpotInfo UnderBed(MapContext ctx, string name, Vector3 bedPos, float bedYaw, Vector3 openSide, float halfWidth, float length, float clearance,
            Transform liftPivot = null)
        {
            var rot = MapMath.Yaw(bedYaw);
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Hide_" + name, bedPos, rot, Layers.World);
            openSide.y = 0; openSide.Normalize();
            Vector3 local = Quaternion.Inverse(rot) * openSide;
            float sx = local.x >= 0 ? 1f : -1f;
            var interact = GeoUtil.AddBox(root, new Vector3(sx * (halfWidth + 0.1f), clearance * 0.5f + 0.1f, 0), new Vector3(0.22f, clearance + 0.2f, length * 0.9f),
                Quaternion.identity, Layers.Interactable, SurfaceType.Default, true, "Interact");
            Vector3 viewPos = bedPos + Vector3.up * 0.2f + openSide * (halfWidth * 0.25f);
            var info = new HidingSpotInfo
            {
                Name = name,
                Kind = HidingKind.UnderBed,
                Root = root,
                Interact = interact,
                HiddenView = new Pose(viewPos, Quaternion.LookRotation((openSide + Vector3.down * 0.08f).normalized, Vector3.up)),
                ExitPose = MapMath.FacingPose(bedPos + openSide * (halfWidth + 0.6f), openSide),
                CrawlStart = MapMath.FacingPose(bedPos + openSide * (halfWidth + 0.45f), -openSide),
                LifterPose = MapMath.FacingPose(bedPos + openSide * (halfWidth + 0.55f), -openSide),
                LiftPivot = liftPivot,
                LiftAxis = Vector3.forward,
                LiftAngle = sx * 62f,
            };
            ctx.Data.HidingSpots.Add(info);
            return info;
        }

        // ================================================================== cages

        /// <summary>2x2x2 m cage with its door on the local -Z face. Static bars go to <paramref name="mb"/>.</summary>
        public static CageInfo Cage(MapContext ctx, MeshBuilder mb, int index, Vector3 center, float yaw)
        {
            var rot = MapMath.Yaw(yaw);
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Cage_" + index, center, rot, Layers.World);
            var bars = Mat.Cutout(Tex.CageBars);
            var frame = Mat.Lit(Tex.MetalRusty);
            float s = 1.0f, H = 2.0f, dw = 0.47f;
            mb.Push(center, rot);
            mb.Color = Shade.Gray(0.8f);
            mb.Material = frame;
            for (int ix = -1; ix <= 1; ix += 2)
                for (int iz = -1; iz <= 1; iz += 2)
                    mb.AddBox(new Vector3(ix * s, H * 0.5f, iz * s), new Vector3(0.05f, H, 0.05f), BoxUV.Local, 0.5f);
            for (int iy = 0; iy <= 1; iy++)
            {
                float y = iy == 0 ? 0.03f : H;
                mb.AddBeam(new Vector3(-s, y, -s), new Vector3(s, y, -s), 0.045f);
                mb.AddBeam(new Vector3(-s, y, s), new Vector3(s, y, s), 0.045f);
                mb.AddBeam(new Vector3(-s, y, -s), new Vector3(-s, y, s), 0.045f);
                mb.AddBeam(new Vector3(s, y, -s), new Vector3(s, y, s), 0.045f);
            }
            mb.AddBox(new Vector3(-dw - 0.025f, H * 0.5f, -s), new Vector3(0.05f, H, 0.05f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(dw + 0.025f, H * 0.5f, -s), new Vector3(0.05f, H, 0.05f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(0, 1.94f, -s), new Vector3(2 * dw, 0.05f, 0.05f), BoxUV.Local, 0.5f);
            mb.Material = Mat.Lit(Tex.FloorPlanks, new Color(0.55f, 0.5f, 0.42f));
            mb.Color = Shade.Gray(0.6f);
            mb.AddBox(new Vector3(0, 0.015f, 0), new Vector3(2 * s, 0.03f, 2 * s), BoxUV.Local, 1f, 0f, BoxFaces.PosY);
            mb.Color = Shade.Gray(0.9f);
            mb.Material = bars;
            float uvs = 0.5f;
            mb.AddQuad(new Vector3(s, 0, s), new Vector3(s, H, s), new Vector3(-s, H, s), new Vector3(-s, 0, s), new Rect(0, 0, 2 * s / uvs, H / uvs));
            mb.AddQuad(new Vector3(-s, 0, s), new Vector3(-s, H, s), new Vector3(-s, H, -s), new Vector3(-s, 0, -s), new Rect(0, 0, 2 * s / uvs, H / uvs));
            mb.AddQuad(new Vector3(s, 0, -s), new Vector3(s, H, -s), new Vector3(s, H, s), new Vector3(s, 0, s), new Rect(0, 0, 2 * s / uvs, H / uvs));
            mb.AddQuad(new Vector3(-s, H, -s), new Vector3(-s, H, s), new Vector3(s, H, s), new Vector3(s, H, -s), new Rect(0, 0, 2 * s / uvs, 2 * s / uvs));
            mb.AddQuad(new Vector3(-s, 0, -s), new Vector3(-s, H, -s), new Vector3(-dw, H, -s), new Vector3(-dw, 0, -s), new Rect(0, 0, (s - dw) / uvs, H / uvs));
            mb.AddQuad(new Vector3(dw, 0, -s), new Vector3(dw, H, -s), new Vector3(s, H, -s), new Vector3(s, 0, -s), new Rect(0, 0, (s - dw) / uvs, H / uvs));
            mb.AddQuad(new Vector3(-dw, 1.92f, -s), new Vector3(-dw, H, -s), new Vector3(dw, H, -s), new Vector3(dw, 1.92f, -s), new Rect(0, 0, 2 * dw / uvs, 0.16f));
            mb.Color = Shade.Gray(1f);
            mb.Pop();

            // door (hinge on the local -X side, swings outward = local -Z)
            var pivot = GeoUtil.CreateChild(root, "CageDoorPivot", new Vector3(-dw + 0.02f, 0.02f, -s), Quaternion.identity, Layers.Door);
            var dmb = new MeshBuilder();
            float leafW = 2 * dw - 0.04f, leafH = 1.88f;
            dmb.Color = Shade.Gray(0.8f);
            dmb.Material = frame;
            dmb.AddBox(new Vector3(0.02f, leafH * 0.5f, 0), new Vector3(0.04f, leafH, 0.04f), BoxUV.Local, 0.5f);
            dmb.AddBox(new Vector3(leafW - 0.02f, leafH * 0.5f, 0), new Vector3(0.04f, leafH, 0.04f), BoxUV.Local, 0.5f);
            dmb.AddBox(new Vector3(leafW * 0.5f, 0.02f, 0), new Vector3(leafW, 0.04f, 0.04f), BoxUV.Local, 0.5f);
            dmb.AddBox(new Vector3(leafW * 0.5f, leafH - 0.02f, 0), new Vector3(leafW, 0.04f, 0.04f), BoxUV.Local, 0.5f);
            dmb.AddBox(new Vector3(leafW * 0.5f, leafH * 0.5f, 0), new Vector3(leafW, 0.035f, 0.03f), BoxUV.Local, 0.5f);
            dmb.Material = Mat.Lit(Tex.Galvanized, new Color(0.6f, 0.55f, 0.45f));
            dmb.AddBox(new Vector3(leafW - 0.02f, 1.05f, -0.07f), new Vector3(0.07f, 0.09f, 0.035f), BoxUV.Local, 0.2f);
            dmb.Color = Shade.Gray(0.9f);
            dmb.Material = bars;
            dmb.AddQuad(new Vector3(0, 0, 0), new Vector3(0, leafH, 0), new Vector3(leafW, leafH, 0), new Vector3(leafW, 0, 0), new Rect(0, 0, leafW / uvs, leafH / uvs));
            var leafGo = dmb.Build("CageDoor", pivot, Layers.Door);
            ctx.CountRenderer(dmb);
            var doorCol = leafGo.AddComponent<BoxCollider>();
            doorCol.center = new Vector3(leafW * 0.5f, leafH * 0.5f, 0);
            doorCol.size = new Vector3(leafW, leafH, 0.07f);

            // solid sides
            GeoUtil.AddBox(root, new Vector3(0, H * 0.5f, s), new Vector3(2 * s, H, 0.06f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CageBack");
            GeoUtil.AddBox(root, new Vector3(-s, H * 0.5f, 0), new Vector3(0.06f, H, 2 * s), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CageSide");
            GeoUtil.AddBox(root, new Vector3(s, H * 0.5f, 0), new Vector3(0.06f, H, 2 * s), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CageSide");
            GeoUtil.AddBox(root, new Vector3(0, H, 0), new Vector3(2 * s, 0.06f, 2 * s), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CageRoof");
            GeoUtil.AddBox(root, new Vector3(-(s + dw) * 0.5f, H * 0.5f, -s), new Vector3(s - dw, H, 0.06f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CageFront");
            GeoUtil.AddBox(root, new Vector3((s + dw) * 0.5f, H * 0.5f, -s), new Vector3(s - dw, H, 0.06f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CageFront");
            GeoUtil.AddBox(root, new Vector3(0, 1.96f, -s), new Vector3(2 * dw, 0.08f, 0.06f), Quaternion.identity, Layers.World, SurfaceType.Metal, false, "CageFrontTop");
            var interact = GeoUtil.AddBox(root, new Vector3(0, 0.95f, -s - 0.12f), new Vector3(1.0f, 1.9f, 0.5f), Quaternion.identity,
                Layers.Interactable, SurfaceType.Default, true, "Interact");

            Vector3 front = rot * Vector3.back;
            var info = new CageInfo
            {
                Root = root,
                DoorPivot = pivot,
                OpenAngle = 100f,
                DoorCollider = doorCol,
                Interact = interact,
                Inside = new Pose(center + rot * new Vector3(0, 0, 0.2f), Quaternion.LookRotation(front, Vector3.up)),
                Outside = MapMath.FacingPose(center + front * (s + 0.75f), front),
            };
            ctx.Data.Cages.Add(info);
            return info;
        }

        // ================================================================== gates

        /// <summary>Chain-link gate leaf: pivot at the hinge post (floor), leaf along local +X, collider on Layers.Door.</summary>
        public static Transform GateLeaf(MapContext ctx, string name, Vector3 hinge, float yaw, float length, float height, out BoxCollider col)
        {
            var pivot = GeoUtil.CreateChild(ctx.Dynamic, name, hinge, MapMath.Yaw(yaw), Layers.Door);
            var mb = new MeshBuilder();
            var pipe = Mat.Lit(Tex.Galvanized);
            mb.Color = Shade.Gray(0.75f);
            mb.Material = pipe;
            float L = length - 0.05f, H = height, y0 = 0.08f;
            mb.AddBeam(new Vector3(0.05f, y0, 0), new Vector3(0.05f, y0 + H, 0), 0.06f);
            mb.AddBeam(new Vector3(L, y0, 0), new Vector3(L, y0 + H, 0), 0.06f);
            mb.AddBeam(new Vector3(0.05f, y0, 0), new Vector3(L, y0, 0), 0.05f);
            mb.AddBeam(new Vector3(0.05f, y0 + H, 0), new Vector3(L, y0 + H, 0), 0.05f);
            mb.AddBeam(new Vector3(0.05f, y0 + H * 0.5f, 0), new Vector3(L, y0 + H * 0.5f, 0), 0.04f);
            mb.AddBeam(new Vector3(0.05f, y0, 0), new Vector3(L, y0 + H, 0), 0.035f);
            for (int i = 0; i < 2; i++)
                mb.AddBox(new Vector3(0.0f, 0.4f + i * (H - 0.5f), 0), new Vector3(0.1f, 0.08f, 0.1f), BoxUV.Local, 0.2f);
            mb.Color = Shade.Gray(0.9f);
            mb.Material = Mat.Cutout(Tex.Chainlink);
            mb.AddQuad(new Vector3(0.05f, y0, 0), new Vector3(0.05f, y0 + H, 0), new Vector3(L, y0 + H, 0), new Vector3(L, y0, 0), new Rect(0, 0, L, H));
            mb.Build("Leaf", pivot, Layers.Door);
            ctx.CountRenderer(mb);
            col = GeoUtil.AddBox(pivot, new Vector3(L * 0.5f, y0 + H * 0.5f, 0), new Vector3(L, H + 0.1f, 0.1f), Quaternion.identity, Layers.Door, SurfaceType.Metal, false, "Blocker");
            return pivot;
        }

        /// <summary>Chain wrapped between two gate leaf ends + padlock (separate object, origin at x/z of the gate center).</summary>
        public static GameObject ChainAndPadlock(MapContext ctx, string name, Vector3 center, float spanX, bool padlock)
        {
            var go = GeoUtil.CreateChild(ctx.Dynamic, name, center, Quaternion.identity, Layers.World).gameObject;
            var mb = new MeshBuilder();
            mb.Color = Shade.Gray(0.8f);
            mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.65f, 0.6f, 0.55f));
            for (int i = 0; i < 9; i++)
            {
                float t = i / 8f;
                float x = Mathf.Lerp(-spanX, spanX, t);
                float y = 1.15f - Mathf.Sin(t * Mathf.PI) * 0.12f + (i % 2) * 0.04f;
                float z = (i % 2 == 0 ? -0.05f : 0.05f);
                mb.Push(new Vector3(x, y, z), Quaternion.Euler(0, 0, i * 37f));
                mb.AddBox(Vector3.zero, new Vector3(0.07f, 0.035f, 0.035f), BoxUV.Local, 0.1f);
                mb.Pop();
            }
            if (padlock)
            {
                mb.Material = Mat.Lit(Tex.Galvanized, new Color(0.7f, 0.6f, 0.35f));
                mb.AddBox(new Vector3(0.02f, 0.93f, -0.07f), new Vector3(0.09f, 0.11f, 0.035f), BoxUV.Local, 0.1f);
                mb.Material = Mat.Lit(Tex.Galvanized);
                mb.AddBeam(new Vector3(-0.01f, 0.98f, -0.07f), new Vector3(-0.01f, 1.06f, -0.06f), 0.012f);
                mb.AddBeam(new Vector3(0.05f, 0.98f, -0.07f), new Vector3(0.05f, 1.06f, -0.06f), 0.012f);
            }
            mb.Build("Mesh", go.transform, Layers.World);
            ctx.CountRenderer(mb);
            return go;
        }

        // ================================================================== mannequins / figures

        public static GameObject Figure(MapContext ctx, Transform parent, FigureKind kind, FigurePose pose, Vector3 pos, Quaternion rot, int seed)
        {
            try
            {
                var go = HumanoidFactory.BuildFigure(kind, pose, parent != null ? parent : ctx.Dynamic, seed);
                if (go != null)
                {
                    go.transform.position = pos;
                    go.transform.rotation = rot;
                }
                return go;
            }
            catch (Exception e)
            {
                Debug.LogError("[MapBuilder] BuildFigure(" + kind + ") failed: " + e);
                return null;
            }
        }

        public static MannequinInfo Mannequin(MapContext ctx, int index, Pose pose, Pose[] alts, int seed)
        {
            var root = GeoUtil.CreateChild(ctx.Dynamic, "Mannequin_" + index, pose.position, pose.rotation, Layers.Corpse);
            Figure(ctx, root, FigureKind.BurntMannequin, FigurePose.StandingHandsCrossed, pose.position, pose.rotation, seed);
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.size = new Vector3(0.5f, 1.8f, 0.35f);
            var info = new MannequinInfo { Root = root, AltPoses = alts ?? new Pose[0] };
            ctx.Data.Mannequins.Add(info);
            return info;
        }
    }
}
