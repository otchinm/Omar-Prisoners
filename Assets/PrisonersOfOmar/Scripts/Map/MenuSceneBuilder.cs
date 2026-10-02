using System;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Small dark room shown behind the main menu (the player sits on the floor in it holding a lit lighter):
    /// dark wooden planks, a bed, a wardrobe, a moonlit window, a candle. Built far from the map (around 1000,0,1000).
    /// Sets its own (very dark) environment; MapBuilder.Build sets the match environment later.
    /// </summary>
    public static class MenuSceneBuilder
    {
        static readonly Vector3 O = new Vector3(1000f, 0f, 1000f);
        static Vector3 V(float x, float y, float z) => O + new Vector3(x, y, z);

        /// <summary>Builds the menu room under <paramref name="parent"/> and returns the camera pose to use.</summary>
        public static Transform Build(Transform parent, out Pose cameraPose)
        {
            var root = new GameObject("MenuRoom").transform;
            root.SetParent(parent, false);
            // mesh / lights are built in root-local space; the returned pose is in world space
            var eye = V(-1.25f, 0.78f, -1.85f);
            var look = Quaternion.LookRotation((V(0.9f, 0.85f, 2.4f) - eye).normalized, Vector3.up);
            cameraPose = new Pose(root.TransformPoint(eye), root.rotation * look);
            try
            {
                PsxEnvironment.Set(new Color(0.03f, 0.03f, 0.045f), new Color(0.01f, 0.012f, 0.018f), 2.5f, 16f);
                BuildRoom(root);
            }
            catch (Exception e) { Debug.LogError("[MenuSceneBuilder] failed: " + e); }
            return root;
        }

        static void BuildRoom(Transform root)
        {
            var mb = new MeshBuilder();
            float x0 = -2f, x1 = 2f, z0 = -2.5f, z1 = 2.5f, H = 2.6f;
            var planks = WallSkin.Of(Mat.Lit(Tex.WoodPlanksWall, new Color(0.55f, 0.5f, 0.45f)), null, 0f, null, 1.4f);
            planks.AoFloor = 0.4f; planks.AoCorner = 0.6f;
            Vector2 P(float x, float z) => new Vector2(O.x + x, O.z + z);
            // walls (faces on the right side of a->b point into the room)
            Arch.WallFace(mb, P(x1, z0), P(x0, z0), 0f, H, null, planks);
            Arch.WallFace(mb, P(x0, z1), P(x1, z1), 0f, H, null, planks);
            Arch.WallFace(mb, P(x0, z0), P(x0, z1), 0f, H, null, planks);
            Arch.WallFace(mb, P(x1, z1), P(x1, z0), 0f, H, null, planks);
            Arch.Flat(mb, O.x + x0, O.z + z0, O.x + x1, O.z + z1, 0f, Mat.Lit(Tex.FloorPlanks, new Color(0.6f, 0.55f, 0.5f)), 1.2f, null, true, Color.white, 0.45f, 0.8f);
            Arch.Flat(mb, O.x + x0, O.z + z0, O.x + x1, O.z + z1, H, Mat.Lit(Tex.CeilingWood, new Color(0.45f, 0.42f, 0.4f)), 1.2f, null, false, Color.white, 0.5f, 0.8f);
            // beams
            mb.Material = Mat.Lit(Tex.WoodRaw, new Color(0.35f, 0.3f, 0.27f));
            mb.Color = Shade.Gray(0.5f);
            for (float z = z0 + 0.8f; z < z1; z += 1.4f) mb.AddBox(V(0, H - 0.08f, z), new Vector3(x1 - x0, 0.16f, 0.14f), BoxUV.Local, 0.8f, 1.2f);
            mb.Color = Shade.Gray(1f);
            // bed (left), wardrobe (back), rug, junk
            Props.BedMetal(null, mb, V(-1.42f, 0, 1.35f), 0f, true, false);
            Arch.Blob(mb, V(-1.42f, 0, 1.35f), 1.4f, 2.3f);
            Props.WardrobeStatic(null, mb, V(0.7f, 0, 2.18f), 0f, 1.2f, 2.0f);
            Props.Nightstand(null, mb, V(-1.55f, 0, -0.05f), -90f);
            Props.Rug(mb, V(0.2f, 0, 0.3f), 10f, 2.0f, 1.4f, new Color(0.35f, 0.25f, 0.22f));
            Props.Papers(mb, V(0.4f, 0, -0.6f), 7, 4, 0.5f);
            Props.Bucket(mb, V(1.6f, 0, -2.1f), 0f, false, 0.55f, false);
            Props.Chair(null, mb, V(1.45f, 0, 0.9f), -110f, true);
            Props.Bottles(mb, V(1.3f, 0, -1.5f), 9, 3, 0.2f);
            Arch.Decal(mb, Mat.Decal("graffiti_scrawl_2"), V(-1.99f, 1.5f, -0.9f), Vector3.right, 0.9f, 0.9f, 0f);
            Arch.Decal(mb, Mat.Decal("water_stain"), V(0.5f, H - 0.01f, 0.5f), Vector3.down, 1.5f, 1.5f, 30f);
            // moonlit window on the east wall
            var win = V(1.99f, 1.5f, 0.6f);
            mb.Push(win, MapMath.Yaw(MapMath.PropYawFacing(Vector3.left)));
            mb.Color = Shade.Gray(1f);
            mb.Material = PsxMaterials.Get(Tex.WindowDark, PsxSurface.Emissive, new Color(0.32f, 0.38f, 0.55f));
            mb.AddQuad(new Vector3(-0.45f, -0.55f, -0.01f), new Vector3(-0.45f, 0.55f, -0.01f), new Vector3(0.45f, 0.55f, -0.01f), new Vector3(0.45f, -0.55f, -0.01f));
            mb.Material = Mat.Lit(Tex.WoodRaw, new Color(0.4f, 0.36f, 0.32f));
            mb.Color = Shade.Gray(0.7f);
            mb.AddBox(new Vector3(0, 0, -0.03f), new Vector3(0.05f, 1.1f, 0.05f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(0, 0.1f, -0.03f), new Vector3(0.9f, 0.05f, 0.05f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(0, -0.6f, -0.06f), new Vector3(1.05f, 0.06f, 0.12f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(0, 0.6f, -0.03f), new Vector3(1.05f, 0.06f, 0.06f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(-0.5f, 0, -0.03f), new Vector3(0.06f, 1.2f, 0.06f), BoxUV.Local, 0.5f);
            mb.AddBox(new Vector3(0.5f, 0, -0.03f), new Vector3(0.06f, 1.2f, 0.06f), BoxUV.Local, 0.5f);
            mb.Pop();
            // candle on the floor next to the player
            var glow = new MeshBuilder();
            Props.Candle(mb, glow, V(-0.55f, 0, -1.55f));
            mb.Build("MenuRoom_Mesh", root, Layers.World);
            if (!glow.IsEmpty) glow.Build("MenuRoom_Glow", root, Layers.World);
            var moon = PsxLight.Create(root, V(1.4f, 1.6f, 0.6f), new Color(0.35f, 0.42f, 0.65f), 0.7f, 5.5f, PsxFlicker.None, "MoonWindow");
            var candle = PsxLight.Create(root, V(-0.55f, 0.25f, -1.55f), new Color(1f, 0.68f, 0.38f), 0.55f, 3.2f, PsxFlicker.Candle, "Candle");
            candle.FlickerAmount = 0.25f;
        }
    }
}
