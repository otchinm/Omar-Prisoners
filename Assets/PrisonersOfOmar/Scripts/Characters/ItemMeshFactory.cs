using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// Low-poly models for items (pickups, held items, inventory preview) and Omar's tools.
    /// Model conventions: pivot = grip point / bottom center when lying on a surface is NOT required;
    /// use <see cref="RestOffset"/> to place it on the ground. Forward = +Z, Up = +Y, real-world scale in meters.
    /// Optional named children: "Anchor_Flame" (lighter flame position), "Anchor_Light" (flashlight lens, forward = beam).
    /// Also: "Grip_L" (where the left hand goes on two handed items), "Needle" (sound meter needle: rotate its local Z,
    /// 0 = needle straight up, see <see cref="SoundMeterNeedleAngle"/>), "Anchor_Wire" (tripwire stake hook),
    /// "Anchor_Spout" (gas can / lighter fuel nozzle tip).
    /// Textures: Resources/Textures/Items/&lt;name&gt;.png (Tools/AssetPipeline/characters/item_textures.py).
    /// </summary>
    public static class ItemMeshFactory
    {
        const string Tex = "Textures/Items/";

        public static GameObject Build(ItemType type)
        {
            var go = new GameObject("Item_" + type);
            go.layer = Layers.Item;
            var mb = new MeshBuilder();
            switch (type)
            {
                case ItemType.Lighter: Lighter(mb, go.transform); break;
                case ItemType.LighterFuel: LighterFuel(mb, go.transform); break;
                case ItemType.Bandages: Bandages(mb); break;
                case ItemType.Flashlight: Flashlight(mb, go.transform); break;
                case ItemType.Batteries: Batteries(mb); break;
                case ItemType.SoundMeter: SoundMeter(mb, go.transform); break;
                case ItemType.BoltCutters: BoltCutters(mb, go.transform); break;
                case ItemType.CarKeys: CarKeys(mb); break;
                case ItemType.GasCan: GasCan(mb, go.transform); break;
                case ItemType.CarBattery: CarBattery(mb, go.transform); break;
                case ItemType.Fuse: Fuse(mb); break;
                case ItemType.CageKey: CageKey(mb); break;
                case ItemType.Lockpick: Lockpick(mb); break;
                case ItemType.Crowbar: Crowbar(mb, go.transform); break;
                case ItemType.Bottle: Bottle(mb); break;
                case ItemType.Pills: Pills(mb); break;
                default:
                    mb.SetMaterial(PsxMaterials.GetColor(new Color(0.5f, 0.5f, 0.5f)));
                    mb.AddBox(Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), BoxUV.PerFace);
                    break;
            }
            if (!mb.IsEmpty) mb.Build("Mesh", go.transform, Layers.Item);
            GeoUtil.SetLayerRecursive(go, Layers.Item);
            return go;
        }

        /// <summary>How characters hold an item (third person animator / first person arms).</summary>
        public static HoldPose HoldPoseFor(ItemType type)
        {
            switch (type)
            {
                case ItemType.None: return HoldPose.None;
                case ItemType.Lighter: return HoldPose.Lighter;
                case ItemType.Flashlight: return HoldPose.Flashlight;
                case ItemType.GasCan:
                case ItemType.CarBattery:
                case ItemType.BoltCutters:
                case ItemType.Crowbar: return HoldPose.TwoHanded;
                case ItemType.Bottle: return HoldPose.Bottle;
                default: return HoldPose.OneHandSmall;
            }
        }

        /// <summary>Local offset to apply so the model rests on a surface (model placed at surface point + RestOffset, RestRotation).</summary>
        public static Vector3 RestOffset(ItemType type)
        {
            switch (type)
            {
                case ItemType.Lighter: return new Vector3(0, 0.0185f, 0);
                case ItemType.LighterFuel: return new Vector3(0, 0.059f, 0);
                case ItemType.Bandages: return new Vector3(0, 0.050f, 0);
                case ItemType.Flashlight: return new Vector3(0, 0.0221f, 0f);
                case ItemType.Batteries: return new Vector3(0, 0.017f, 0);
                case ItemType.SoundMeter: return new Vector3(0, 0.0225f, -0.035f);
                case ItemType.BoltCutters: return new Vector3(0.035f, 0.013f, -0.245f);
                case ItemType.CarKeys: return new Vector3(-0.004f, 0.009f, -0.004f);
                case ItemType.GasCan: return new Vector3(0, 0.335f, 0);
                case ItemType.CarBattery: return new Vector3(0.12f, 0.095f, 0);
                case ItemType.Fuse: return new Vector3(0, 0.0115f, 0);
                case ItemType.CageKey: return new Vector3(0, 0.006f, -0.05f);
                case ItemType.Lockpick: return new Vector3(0, 0.005f, -0.028f);
                case ItemType.Crowbar: return new Vector3(0.043f, 0.013f, -0.093f);
                case ItemType.Bottle: return new Vector3(0, 0.245f, 0);
                case ItemType.Pills: return new Vector3(0, 0.0445f, 0);
                default: return new Vector3(0, 0.03f, 0);
            }
        }

        public static Quaternion RestRotation(ItemType type)
        {
            switch (type)
            {
                case ItemType.Flashlight: return Quaternion.Euler(-3.2f, 0f, 0f);  // head is thicker than the body
                case ItemType.SoundMeter: return Quaternion.Euler(90f, 0f, 0f);    // lying on its back, dial up
                case ItemType.Crowbar: return Quaternion.Euler(0f, 0f, 90f);       // hook lies flat
                default: return Quaternion.identity;
            }
        }

        /// <summary>Local Z rotation of the "Needle" child for a level 0..1 (left stop .. right stop).</summary>
        public static float SoundMeterNeedleAngle(float level01) => Mathf.Lerp(48f, -48f, Mathf.Clamp01(level01));

        // ============================================================================================ helpers
        /// <summary>UV rect of a pixel rectangle (origin top-left) in a W x H texture, inset by half a texel.</summary>
        static Rect R(float x, float y, float w, float h, float W, float H)
            => new Rect((x + 0.5f) / W, 1f - (y + h - 0.5f) / H, (w - 1f) / W, (h - 1f) / H);

        static Material Mat(string name, PsxSurface surface = PsxSurface.Lit) => PsxMaterials.Get(Tex + name, surface);

        static Transform Child(Transform parent, string name, Vector3 pos, Quaternion rot)
            => GeoUtil.CreateChild(parent, name, pos, rot, Layers.Item);

        /// <summary>Cylinder from a to b (side uses rect side: u around, v from a to b), optional caps.</summary>
        static void Cyl(MeshBuilder mb, Vector3 a, Vector3 b, float ra, float rb, int sides, Rect side, Rect? capA = null, Rect? capB = null, bool smooth = true)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            mb.Push(a, Quaternion.FromToRotation(Vector3.up, d / len));
            mb.AddCylinder(Vector3.zero, ra, rb, len, sides, false, false, side, smooth);
            if (capA.HasValue) Disc(mb, Vector3.zero, ra, sides, capA.Value, false);
            if (capB.HasValue) Disc(mb, new Vector3(0, len, 0), rb, sides, capB.Value, true);
            mb.Pop();
        }

        /// <summary>Flat disc in the local XZ plane facing +Y (up = true) or -Y.</summary>
        static void Disc(MeshBuilder mb, Vector3 c, float r, int sides, Rect uv, bool up)
        {
            Vector3 n = up ? Vector3.up : Vector3.down;
            int ci = mb.AddVertex(c, n, uv.center);
            int first = mb.VertexCount;
            for (int i = 0; i <= sides; i++)
            {
                float a = (float)i / sides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                mb.AddVertex(c + dir * r, n, new Vector2(uv.center.x + dir.x * uv.width * 0.5f, uv.center.y + dir.z * uv.height * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                if (up) mb.AddTriangle(ci, first + i + 1, first + i);
                else mb.AddTriangle(ci, first + i, first + i + 1);
            }
        }

        static void Box(MeshBuilder mb, Vector3 c, Vector3 size, Rect all) => mb.AddBox(c, size, BoxUVRects.All(all));

        static void Box(MeshBuilder mb, Vector3 c, Quaternion rot, Vector3 size, BoxUVRects uv)
        {
            mb.Push(c, rot);
            mb.AddBox(Vector3.zero, size, uv);
            mb.Pop();
        }

        /// <summary>Beam (square bar) from a to b with one UV rect.</summary>
        static void Bar(MeshBuilder mb, Vector3 a, Vector3 b, float w, float h, Rect uv)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            mb.Push((a + b) * 0.5f, Quaternion.LookRotation(d / len, Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up));
            mb.AddBox(Vector3.zero, new Vector3(w, h, len), BoxUVRects.All(uv));
            mb.Pop();
        }

        // ============================================================================================ items
        // lighter.png 64x64: body front (0,0,32,32) side (32,0,16,32) lid (0,32,32,16) chimney (32,32,16,16)
        //                    top (48,0,16,16) insert (48,16,16,16) wick (48,32,16,16)
        static void Lighter(MeshBuilder mb, Transform root)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("lighter"));
            Rect front = R(0, 0, 32, 32, W, H), side = R(32, 0, 16, 32, W, H), lid = R(0, 32, 32, 16, W, H);
            Rect chim = R(32, 32, 16, 16, W, H), top = R(48, 0, 16, 16, W, H), insert = R(48, 16, 16, 16, W, H), wick = R(48, 32, 16, 16, W, H);
            // body (pivot = grip, middle of the case)
            mb.AddBox(new Vector3(0, 0, 0), new Vector3(0.038f, 0.037f, 0.013f),
                new BoxUVRects { PosZ = front, NegZ = front, PosX = side, NegX = side, PosY = insert, NegY = top });
            // chimney with holes
            mb.AddBox(new Vector3(-0.002f, 0.0265f, 0), new Vector3(0.017f, 0.016f, 0.011f), BoxUVRects.All(chim));
            // flint wheel + wick
            Cyl(mb, new Vector3(0.0085f, 0.031f, -0.004f), new Vector3(0.0085f, 0.031f, 0.004f), 0.0035f, 0.0035f, 6, wick, wick, wick);
            mb.AddBox(new Vector3(-0.002f, 0.0355f, 0), new Vector3(0.003f, 0.003f, 0.003f), BoxUVRects.All(wick));
            // open lid hinged on the right edge, swung open ~125 degrees
            mb.Push(new Vector3(0.019f, 0.0185f, 0), Quaternion.Euler(0, 0, -125f));
            mb.AddBox(new Vector3(-0.019f, 0.0105f, 0), new Vector3(0.038f, 0.021f, 0.013f),
                new BoxUVRects { PosZ = lid, NegZ = lid, PosX = side, NegX = side, PosY = top, NegY = insert });
            mb.Pop();
            Child(root, "Anchor_Flame", new Vector3(-0.002f, 0.039f, 0), Quaternion.identity);
        }

        // lighterfuel.png 128x64: front (0,0,64,64) back (64,0,32,64) side (96,0,16,64) top (112,0,16,16) nozzle (112,16,16,16) neck (112,32,16,16)
        static void LighterFuel(MeshBuilder mb, Transform root)
        {
            const float W = 128, H = 64;
            mb.SetMaterial(Mat("lighterfuel"));
            Rect front = R(0, 0, 64, 64, W, H), back = R(64, 0, 32, 64, W, H), side = R(96, 0, 16, 64, W, H);
            Rect top = R(112, 0, 16, 16, W, H), nozzle = R(112, 16, 16, 16, W, H), neck = R(112, 32, 16, 16, W, H);
            mb.AddBox(Vector3.zero, new Vector3(0.064f, 0.118f, 0.026f),
                new BoxUVRects { PosZ = back, NegZ = front, PosX = side, NegX = side, PosY = top, NegY = top });
            // slightly raised top seam
            mb.AddBox(new Vector3(0, 0.0605f, 0), new Vector3(0.060f, 0.003f, 0.022f), BoxUVRects.All(top));
            Cyl(mb, new Vector3(0.012f, 0.062f, 0), new Vector3(0.012f, 0.068f, 0), 0.0055f, 0.005f, 6, neck, null, neck);
            Cyl(mb, new Vector3(0.012f, 0.068f, 0), new Vector3(0.012f, 0.094f, 0), 0.0045f, 0.0012f, 6, nozzle, nozzle);
            Child(root, "Anchor_Spout", new Vector3(0.012f, 0.094f, 0), Quaternion.identity);
        }

        // bandages.png 64x64: front (0,0,32,48) back (32,0,32,48) side (0,48,32,16) top (32,48,32,16)
        static void Bandages(MeshBuilder mb)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("bandages"));
            Rect front = R(0, 0, 32, 48, W, H), back = R(32, 0, 32, 48, W, H), side = R(0, 48, 32, 16, W, H), top = R(32, 48, 32, 16, W, H);
            // side texture is drawn horizontally: rotate the box so the side strips read along the height
            mb.AddBox(Vector3.zero, new Vector3(0.072f, 0.10f, 0.030f),
                new BoxUVRects { NegZ = front, PosZ = back, PosX = side, NegX = side, PosY = top, NegY = top });
        }

        // flashlight.png 64x64: body (0,0,64,32) head (0,32,32,16) lens (32,32,16,16) tail (48,32,16,16) switch (0,48,16,16)
        static void Flashlight(MeshBuilder mb, Transform root)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("flashlight"));
            Rect body = R(0, 0, 64, 32, W, H), head = R(0, 32, 32, 16, W, H), lens = R(32, 32, 16, 16, W, H);
            Rect tail = R(48, 32, 16, 16, W, H), sw = R(0, 48, 16, 16, W, H);
            const int S = 8;
            Cyl(mb, new Vector3(0, 0, -0.10f), new Vector3(0, 0, 0.055f), 0.0165f, 0.0165f, S, body, tail);
            Cyl(mb, new Vector3(0, 0, 0.055f), new Vector3(0, 0, 0.085f), 0.0165f, 0.0255f, S, head);
            Cyl(mb, new Vector3(0, 0, 0.085f), new Vector3(0, 0, 0.102f), 0.0255f, 0.0255f, S, head, null, lens);
            mb.AddBox(new Vector3(0, 0.0175f, 0.025f), new Vector3(0.009f, 0.004f, 0.016f), BoxUVRects.All(sw));
            Child(root, "Anchor_Light", new Vector3(0, 0, 0.104f), Quaternion.identity);
        }

        // batteries.png 64x32: label (0,0,48,32) plus end (48,0,16,16) minus end (48,16,16,16)
        static void Batteries(MeshBuilder mb)
        {
            const float W = 64, H = 32;
            mb.SetMaterial(Mat("batteries"));
            Rect label = R(0, 0, 48, 32, W, H), plus = R(48, 0, 16, 16, W, H), minus = R(48, 16, 16, 16, W, H);
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.0172f : 0.0172f;
                float z = i == 0 ? 0.004f : -0.004f;
                Cyl(mb, new Vector3(x, 0, -0.0305f + z), new Vector3(x, 0, 0.0285f + z), 0.017f, 0.017f, 8, label, minus);
                Cyl(mb, new Vector3(x, 0, 0.0285f + z), new Vector3(x, 0, 0.0305f + z), 0.006f, 0.006f, 6, plus, null, plus);
            }
        }

        // soundmeter.png 128x128: front (0,0,64,128) back (64,0,32,128) side (96,0,16,128) top (112,0,16,32) needle (112,32,16,32)
        static void SoundMeter(MeshBuilder mb, Transform root)
        {
            const float W = 128, H = 128;
            mb.SetMaterial(Mat("soundmeter"));
            Rect front = R(0, 0, 64, 128, W, H), back = R(64, 0, 32, 128, W, H), side = R(96, 0, 16, 128, W, H), top = R(112, 0, 16, 32, W, H);
            // pivot = grip in the lower third; the dial faces the holder (-Z)
            Vector3 c = new Vector3(0, 0.035f, 0);
            Vector3 size = new Vector3(0.075f, 0.20f, 0.045f);
            mb.AddBox(c, size, new BoxUVRects { NegZ = front, PosZ = back, PosX = side, NegX = side, PosY = top, NegY = top });
            // knob bumps
            Cyl(mb, new Vector3(-0.018f, c.y - 0.01f, -0.0225f), new Vector3(-0.018f, c.y - 0.01f, -0.03f), 0.007f, 0.006f, 6, back, null, top);
            Cyl(mb, new Vector3(0.018f, c.y - 0.01f, -0.0225f), new Vector3(0.018f, c.y - 0.01f, -0.03f), 0.007f, 0.006f, 6, back, null, top);
            // needle: pivot at the bottom centre of the dial window (dial occupies the top quarter of the face)
            var needle = Child(root, "Needle", new Vector3(0, c.y + size.y * 0.5f - 0.052f, -0.0235f), Quaternion.identity);
            var nb = new MeshBuilder();
            nb.SetMaterial(Mat("soundmeter"));
            Rect nr = R(112, 32, 16, 32, W, H);
            nb.AddQuad(new Vector3(0.0012f, 0, 0), new Vector3(0.0006f, 0.03f, 0), new Vector3(-0.0006f, 0.03f, 0), new Vector3(-0.0012f, 0, 0), nr);
            nb.Build("NeedleMesh", needle, Layers.Item);
            needle.localRotation = Quaternion.Euler(0, 0, SoundMeterNeedleAngle(0.15f));
        }

        // boltcutters.png 64x64: steel (0,0,32,64) grip (32,0,32,32) jaw (32,32,32,32)
        static void BoltCutters(MeshBuilder mb, Transform root)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("boltcutters"));
            Rect steel = R(0, 0, 32, 64, W, H), grip = R(32, 0, 32, 32, W, H), jaw = R(32, 32, 32, 32, W, H);
            // pivot = right handle grip; handles run forward (+Z) and converge to the head
            float gx = -0.035f; // centre line offset from the right grip
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? 1f : -1f;
                Vector3 end = new Vector3(gx + sx * 0.035f, 0, -0.07f);
                Vector3 mid = new Vector3(gx + sx * 0.03f, 0, 0.09f);
                Vector3 joint = new Vector3(gx + sx * 0.012f, 0, 0.43f);
                Bar(mb, end, mid, 0.022f, 0.02f, grip);
                Bar(mb, mid, joint, 0.016f, 0.014f, steel);
                // jaw
                Vector3 tip = new Vector3(gx + sx * 0.004f, 0, 0.56f);
                Bar(mb, joint, tip, 0.016f, 0.024f, jaw);
            }
            mb.AddBox(new Vector3(gx, 0, 0.43f), new Vector3(0.05f, 0.026f, 0.05f), BoxUVRects.All(jaw));
            Child(root, "Grip_L", new Vector3(-0.07f, 0, 0f), Quaternion.identity);
        }

        // carkeys.png 64x64: brass key (0,0,32,16) silver key (0,16,32,16) fob (32,0,32,32) ring (0,32,16,16) fob side (16,32,16,16)
        static void CarKeys(MeshBuilder mb)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("carkeys"));
            Rect k1 = R(0, 0, 32, 16, W, H), k2 = R(0, 16, 32, 16, W, H), fob = R(32, 0, 32, 32, W, H), ring = R(0, 32, 16, 16, W, H), fobSide = R(16, 32, 16, 16, W, H);
            // ring (octagonal torus approximated by 8 bars) around the pivot
            const int N = 8;
            for (int i = 0; i < N; i++)
            {
                float a0 = (float)i / N * Mathf.PI * 2f, a1 = (float)(i + 1) / N * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.014f, p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.014f;
                Bar(mb, p0, p1, 0.0025f, 0.0025f, ring);
            }
            // two keys hanging from the ring, flat, blades towards +Z
            for (int k = 0; k < 2; k++)
            {
                Rect r = k == 0 ? k1 : k2;
                Quaternion rot = Quaternion.Euler(0, k == 0 ? -12f : 18f, 0);
                mb.Push(new Vector3(0, k * 0.003f, 0.014f), rot);
                mb.AddBox(new Vector3(0, 0, 0.012f), new Vector3(0.022f, 0.0022f, 0.022f), BoxUVRects.All(r));
                mb.AddBox(new Vector3(0.002f, 0, 0.040f), new Vector3(0.008f, 0.0018f, 0.034f), BoxUVRects.All(r));
                mb.Pop();
            }
            // fob
            mb.Push(new Vector3(-0.004f, -0.004f, -0.016f), Quaternion.Euler(0, 160f, 0));
            mb.AddBox(new Vector3(0, 0, 0.022f), new Vector3(0.032f, 0.010f, 0.044f),
                new BoxUVRects { PosY = fob, NegY = fob, PosX = fobSide, NegX = fobSide, PosZ = fobSide, NegZ = fobSide });
            mb.Pop();
        }

        // gascan.png 64x64: side (0,0,64,32) end (0,32,32,32) top (32,32,16,16) spout (48,32,16,16) cap (32,48,16,16)
        static void GasCan(MeshBuilder mb, Transform root)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("gascan"));
            Rect side = R(0, 0, 64, 32, W, H), end = R(0, 32, 32, 32, W, H), top = R(32, 32, 16, 16, W, H);
            Rect spout = R(48, 32, 16, 16, W, H), cap = R(32, 48, 16, 16, W, H);
            // pivot = handle on top; body hangs below
            Vector3 c = new Vector3(0, -0.185f, 0);
            Vector3 size = new Vector3(0.15f, 0.30f, 0.27f);
            mb.AddBox(c, size, new BoxUVRects { PosX = side, NegX = side, PosZ = end, NegZ = end, PosY = top, NegY = top });
            // bevelled top edge
            mb.AddBox(c + new Vector3(0, size.y * 0.5f + 0.008f, -0.02f), new Vector3(0.13f, 0.016f, 0.20f), BoxUVRects.All(top));
            // handle bar + posts
            Bar(mb, new Vector3(0, 0, -0.07f), new Vector3(0, 0, 0.05f), 0.022f, 0.022f, top);
            Bar(mb, new Vector3(0, 0, -0.07f), new Vector3(0, -0.03f, -0.085f), 0.02f, 0.02f, top);
            Bar(mb, new Vector3(0, 0, 0.05f), new Vector3(0, -0.03f, 0.065f), 0.02f, 0.02f, top);
            // spout at the front-top corner, angled forward/up
            Vector3 sBase = c + new Vector3(0, size.y * 0.5f, size.z * 0.5f - 0.03f);
            Vector3 sTip = sBase + new Vector3(0, 0.07f, 0.06f);
            Cyl(mb, sBase, sTip, 0.016f, 0.011f, 6, spout, null, cap);
            // filler cap at the back
            Cyl(mb, c + new Vector3(0, size.y * 0.5f + 0.012f, -size.z * 0.5f + 0.04f), c + new Vector3(0, size.y * 0.5f + 0.03f, -size.z * 0.5f + 0.04f), 0.02f, 0.02f, 6, cap, null, cap);
            Child(root, "Anchor_Spout", sTip, Quaternion.LookRotation(sTip - sBase));
            Child(root, "Grip_L", new Vector3(-0.085f, -0.24f, 0.06f), Quaternion.identity);
        }

        // carbattery.png 64x64: side (0,0,64,32) end (0,32,32,24) top (32,32,32,32) red (0,56,8,8) black (8,56,8,8) handle (16,56,16,8)
        static void CarBattery(MeshBuilder mb, Transform root)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("carbattery"));
            Rect side = R(0, 0, 64, 32, W, H), end = R(0, 32, 32, 24, W, H), top = R(32, 32, 32, 32, W, H);
            Rect red = R(0, 56, 8, 8, W, H), blk = R(8, 56, 8, 8, W, H), handle = R(16, 56, 16, 8, W, H);
            // long axis along X, pivot = right hand on the right end (+X); the left hand on the other end
            Vector3 c = new Vector3(-0.12f, 0, 0);
            Vector3 size = new Vector3(0.24f, 0.19f, 0.175f);
            mb.AddBox(c, size, new BoxUVRects { PosZ = side, NegZ = side, PosX = end, NegX = end, PosY = top, NegY = end });
            float ty = size.y * 0.5f;
            Cyl(mb, c + new Vector3(0.08f, ty, 0.05f), c + new Vector3(0.08f, ty + 0.02f, 0.05f), 0.009f, 0.008f, 6, red, null, red);
            Cyl(mb, c + new Vector3(-0.08f, ty, 0.05f), c + new Vector3(-0.08f, ty + 0.02f, 0.05f), 0.009f, 0.008f, 6, blk, null, blk);
            // strap handle
            Bar(mb, c + new Vector3(-0.06f, ty + 0.05f, 0f), c + new Vector3(0.06f, ty + 0.05f, 0f), 0.025f, 0.006f, handle);
            Bar(mb, c + new Vector3(-0.06f, ty + 0.05f, 0f), c + new Vector3(-0.1f, ty, 0f), 0.025f, 0.006f, handle);
            Bar(mb, c + new Vector3(0.06f, ty + 0.05f, 0f), c + new Vector3(0.1f, ty, 0f), 0.025f, 0.006f, handle);
            Child(root, "Grip_L", new Vector3(-0.24f, 0, 0), Quaternion.identity);
        }

        // fuse.png 64x32: body (0,0,48,32) ferrule (48,0,16,16) end (48,16,16,16)
        static void Fuse(MeshBuilder mb)
        {
            const float W = 64, H = 32;
            mb.SetMaterial(Mat("fuse"));
            Rect body = R(0, 0, 48, 32, W, H), fer = R(48, 0, 16, 16, W, H), end = R(48, 16, 16, 16, W, H);
            Cyl(mb, new Vector3(-0.026f, 0, 0), new Vector3(0.026f, 0, 0), 0.0105f, 0.0105f, 8, body);
            Cyl(mb, new Vector3(-0.038f, 0, 0), new Vector3(-0.024f, 0, 0), 0.0115f, 0.0115f, 8, fer, end);
            Cyl(mb, new Vector3(0.024f, 0, 0), new Vector3(0.038f, 0, 0), 0.0115f, 0.0115f, 8, fer, null, end);
        }

        // cagekey.png 32x32: iron
        static void CageKey(MeshBuilder mb)
        {
            mb.SetMaterial(Mat("cagekey"));
            Rect iron = R(0, 0, 32, 32, 32, 32);
            // bow (ring) around the pivot, flat in XZ
            const int N = 8;
            for (int i = 0; i < N; i++)
            {
                float a0 = (float)i / N * Mathf.PI * 2f, a1 = (float)(i + 1) / N * Mathf.PI * 2f;
                Vector3 p0 = new Vector3(Mathf.Cos(a0) * 0.018f, 0, Mathf.Sin(a0) * 0.022f);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * 0.018f, 0, Mathf.Sin(a1) * 0.022f);
                Bar(mb, p0, p1, 0.007f, 0.006f, iron);
            }
            // shaft + collar + bit
            Cyl(mb, new Vector3(0, 0, 0.022f), new Vector3(0, 0, 0.125f), 0.0045f, 0.0042f, 6, iron, iron, iron);
            mb.AddBox(new Vector3(0, 0, 0.032f), new Vector3(0.012f, 0.012f, 0.006f), BoxUVRects.All(iron));
            mb.AddBox(new Vector3(0.011f, 0, 0.112f), new Vector3(0.018f, 0.004f, 0.016f), BoxUVRects.All(iron));
            mb.AddBox(new Vector3(0.017f, 0, 0.106f), new Vector3(0.006f, 0.004f, 0.006f), BoxUVRects.All(iron));
        }

        // lockpick.png 64x32: leather (0,0,48,32) steel (48,0,16,32)
        static void Lockpick(MeshBuilder mb)
        {
            const float W = 64, H = 32;
            mb.SetMaterial(Mat("lockpick"));
            Rect leather = R(0, 0, 48, 32, W, H), steel = R(48, 0, 16, 32, W, H);
            mb.AddBox(Vector3.zero, new Vector3(0.056f, 0.010f, 0.115f), BoxUVRects.All(leather));
            // flap
            mb.Push(new Vector3(0, 0.005f, 0.05f), Quaternion.Euler(-8f, 0, 0));
            mb.AddBox(new Vector3(0, 0.002f, -0.03f), new Vector3(0.054f, 0.003f, 0.06f), BoxUVRects.All(leather));
            mb.Pop();
            // picks sticking out of the end
            float[] xs = { -0.018f, -0.006f, 0.006f, 0.018f };
            float[] ls = { 0.04f, 0.055f, 0.045f, 0.035f };
            for (int i = 0; i < 4; i++)
                mb.AddBox(new Vector3(xs[i], 0.001f, 0.0575f + ls[i] * 0.5f), new Vector3(0.0025f, 0.0012f, ls[i]), BoxUVRects.All(steel));
            mb.AddBox(new Vector3(0.006f, 0.001f, 0.0575f + 0.055f), new Vector3(0.007f, 0.0012f, 0.003f), BoxUVRects.All(steel));
        }

        // crowbar.png 32x64: paint (0,0,16,64) bare steel (16,0,16,64)
        static void Crowbar(MeshBuilder mb, Transform root)
        {
            const float W = 32, H = 64;
            mb.SetMaterial(Mat("crowbar"));
            Rect paint = R(0, 0, 16, 64, W, H), bare = R(16, 0, 16, 64, W, H);
            const float t = 0.02f;
            // straight part along +Z, pivot = lower grip
            Vector3 a = new Vector3(0, 0, -0.17f), b = new Vector3(0, 0, 0.38f);
            Bar(mb, a, b, t, t, paint);
            // flat pry end, slightly bent
            Vector3 p1 = a + new Vector3(0, 0.012f, -0.06f);
            Bar(mb, a, p1, 0.026f, 0.008f, bare);
            // hook (goose neck) at the far end, bending up and back
            Vector3 prev = b;
            float ang = 0f;
            for (int i = 0; i < 4; i++)
            {
                ang += 42f;
                Vector3 dir = new Vector3(0, Mathf.Sin(ang * Mathf.Deg2Rad), Mathf.Cos(ang * Mathf.Deg2Rad));
                Vector3 next = prev + dir * 0.032f;
                Bar(mb, prev, next, t, t, i < 3 ? paint : bare);
                prev = next;
            }
            Bar(mb, prev, prev + new Vector3(0, -0.012f, -0.025f), 0.026f, 0.008f, bare);
            Child(root, "Grip_L", new Vector3(0, 0, 0.2f), Quaternion.identity);
        }

        // bottle.png 64x64: glass (0,0,48,48) neck (48,0,16,48) base (0,48,16,16) lip (16,48,16,16)
        static void Bottle(MeshBuilder mb)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("bottle"));
            Rect glass = R(0, 0, 48, 48, W, H), neck = R(48, 0, 16, 48, W, H), bas = R(0, 48, 16, 16, W, H), lip = R(16, 48, 16, 16, W, H);
            // pivot = neck grip; body below
            const int S = 8;
            float y0 = -0.245f;
            Cyl(mb, new Vector3(0, y0, 0), new Vector3(0, y0 + 0.165f, 0), 0.034f, 0.034f, S, glass, bas);
            Cyl(mb, new Vector3(0, y0 + 0.165f, 0), new Vector3(0, y0 + 0.215f, 0), 0.034f, 0.013f, S, neck);
            Cyl(mb, new Vector3(0, y0 + 0.215f, 0), new Vector3(0, y0 + 0.272f, 0), 0.013f, 0.012f, S, neck);
            Cyl(mb, new Vector3(0, y0 + 0.272f, 0), new Vector3(0, y0 + 0.282f, 0), 0.0145f, 0.0145f, S, lip, null, lip);
        }

        // pills.png 64x32: body (0,0,48,32) cap top (48,0,16,16) cap side (48,16,16,16)
        static void Pills(MeshBuilder mb)
        {
            const float W = 64, H = 32;
            mb.SetMaterial(Mat("pills"));
            Rect body = R(0, 0, 48, 32, W, H), capTop = R(48, 0, 16, 16, W, H), capSide = R(48, 16, 16, 16, W, H);
            Cyl(mb, new Vector3(0, -0.0445f, 0), new Vector3(0, 0.026f, 0), 0.0165f, 0.0165f, 8, body, capTop);
            Cyl(mb, new Vector3(0, 0.024f, 0), new Vector3(0, 0.0445f, 0), 0.0185f, 0.0185f, 8, capSide, null, capTop);
        }

        // ============================================================================================ Omar's tools
        /// <summary>Omar's meat cleaver (pivot = handle grip). Blade extends forward (+Z), cutting edge down (-Y).</summary>
        // cleaver.png 64x64: blade (0,0,64,32) handle (0,32,32,16) edge (32,32,32,8) wood (0,48,32,16)
        public static GameObject BuildCleaver()
        {
            var go = new GameObject("Cleaver");
            go.layer = Layers.Item;
            const float W = 64, H = 64;
            var mb = new MeshBuilder();
            mb.SetMaterial(Mat("cleaver"));
            Rect blade = R(0, 0, 64, 32, W, H), handle = R(0, 32, 32, 16, W, H), edge = R(32, 32, 32, 8, W, H), wood = R(0, 48, 32, 16, W, H);
            // handle: z -0.07 .. 0.045
            mb.AddBox(new Vector3(0, -0.002f, -0.012f), new Vector3(0.017f, 0.026f, 0.115f),
                new BoxUVRects { PosX = handle, NegX = handle, PosY = wood, NegY = wood, PosZ = wood, NegZ = wood });
            // blade: z 0.045 .. 0.245, spine at y +0.016, edge at y -0.072 (slightly curved belly via 2 segments)
            float z0 = 0.045f, z1 = 0.245f, ySpine = 0.016f, yEdge0 = -0.062f, yEdge1 = -0.076f;
            float th = 0.0035f;
            Vector3 a = new Vector3(0, yEdge0, z0), b = new Vector3(0, ySpine, z0), c = new Vector3(0, ySpine + 0.004f, z1), d = new Vector3(0, yEdge1, z1);
            // +X side (seen from +X: right = +Z)
            mb.AddQuad(a + Vector3.right * th, b + Vector3.right * th, c + Vector3.right * th, d + Vector3.right * th,
                new Vector2(blade.xMin, blade.yMin), new Vector2(blade.xMin, blade.yMax), new Vector2(blade.xMax, blade.yMax), new Vector2(blade.xMax, blade.yMin));
            // -X side (seen from -X: right = -Z)
            mb.AddQuad(d - Vector3.right * th, c - Vector3.right * th, b - Vector3.right * th, a - Vector3.right * th,
                new Vector2(blade.xMax, blade.yMin), new Vector2(blade.xMax, blade.yMax), new Vector2(blade.xMin, blade.yMax), new Vector2(blade.xMin, blade.yMin));
            // spine, edge, tip strips
            mb.AddQuad(b - Vector3.right * th, b + Vector3.right * th, c + Vector3.right * th, c - Vector3.right * th, edge); // top (seen from above: right = +X... approximate)
            mb.AddQuad(d - Vector3.right * th, d + Vector3.right * th, a + Vector3.right * th, a - Vector3.right * th, edge);
            mb.AddQuad(d + Vector3.right * th, c + Vector3.right * th, c - Vector3.right * th, d - Vector3.right * th, edge);
            // rivets
            for (int i = 0; i < 3; i++)
            {
                float z = -0.05f + i * 0.033f;
                mb.AddBox(new Vector3(0, -0.002f, z), new Vector3(0.019f, 0.006f, 0.006f), BoxUVRects.All(edge));
            }
            mb.Build("Mesh", go.transform, Layers.Item);
            return go;
        }

        /// <summary>Omar's tripwire kit / placed tripwire posts (two small stakes; wire drawn by gameplay between anchors).</summary>
        /// <remarks>Pivot at ground level; the stake goes 10 cm into the ground. Child "Anchor_Wire" = hook where the wire attaches.</remarks>
        public static GameObject BuildTripwireStake()
        {
            var go = new GameObject("TripwireStake");
            go.layer = Layers.Item;
            var mb = new MeshBuilder();
            mb.SetMaterial(Mat("tripwire_stake"));
            Rect steel = R(0, 0, 32, 24, 32, 32), wire = R(0, 24, 32, 8, 32, 32);
            Bar(mb, new Vector3(0, -0.10f, 0), new Vector3(0, 0.12f, 0), 0.009f, 0.009f, steel);
            Cyl(mb, new Vector3(0, -0.12f, 0), new Vector3(0, -0.10f, 0), 0.0005f, 0.0055f, 4, steel);
            // hook / eye at the top
            const int N = 6;
            Vector3 eyeC = new Vector3(0, 0.135f, 0);
            for (int i = 0; i < N; i++)
            {
                float a0 = (float)i / N * Mathf.PI * 2f, a1 = (float)(i + 1) / N * Mathf.PI * 2f;
                Bar(mb, eyeC + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0), 0) * 0.014f, eyeC + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1), 0) * 0.014f, 0.004f, 0.004f, steel);
            }
            // a few turns of wire wrapped around the stake
            mb.AddBox(new Vector3(0, 0.09f, 0), new Vector3(0.014f, 0.012f, 0.014f), BoxUVRects.All(wire));
            mb.Build("Mesh", go.transform, Layers.Item);
            GeoUtil.CreateChild(go.transform, "Anchor_Wire", eyeC + new Vector3(0, 0.012f, 0), Quaternion.identity, Layers.Item);
            return go;
        }

        /// <summary>Open bear trap (pivot at ground center). Child "Jaw_L" / "Jaw_R" pivots rotate to snap shut.</summary>
        /// <remarks>Jaw_L / Jaw_R: localRotation = Quaternion.Euler(a, 0, 0) with a = 0 open (flat on the ground) .. 88 closed
        /// (both jaws vertical, teeth meeting in the middle). The pan in the middle is the trigger.</remarks>
        public static GameObject BuildBearTrap()
        {
            var go = new GameObject("BearTrap");
            go.layer = Layers.Item;
            const float W = 64, H = 64;
            Rect steel = R(0, 0, 32, 64, W, H), teeth = R(32, 0, 32, 32, W, H), pan = R(32, 32, 32, 32, W, H);
            var mat = Mat("beartrap");
            var mb = new MeshBuilder();
            mb.SetMaterial(mat);
            const float r = 0.15f, hy = 0.02f;
            // base bar along the hinge axis (Z) + springs extending beyond
            Bar(mb, new Vector3(0, 0.008f, -0.2f), new Vector3(0, 0.008f, 0.2f), 0.03f, 0.012f, steel);
            for (int s = 0; s < 2; s++)
            {
                float sz = s == 0 ? 1f : -1f;
                Vector3 p0 = new Vector3(0, 0.012f, sz * 0.19f), p1 = new Vector3(0.0f, 0.035f, sz * 0.32f), p2 = new Vector3(0, 0.012f, sz * 0.42f);
                Bar(mb, p0, p1, 0.026f, 0.006f, steel);
                Bar(mb, p1, p2, 0.026f, 0.006f, steel);
                Bar(mb, new Vector3(-0.012f, hy, sz * r), new Vector3(0.012f, hy, sz * r), 0.016f, 0.016f, steel); // hinge posts
            }
            // pan (trigger plate)
            mb.AddBox(new Vector3(0, 0.022f, 0), new Vector3(0.075f, 0.006f, 0.075f), new BoxUVRects { PosY = pan, NegY = pan, PosX = steel, NegX = steel, PosZ = steel, NegZ = steel });
            Bar(mb, new Vector3(0, 0.014f, 0), new Vector3(0, 0.02f, 0), 0.02f, 0.02f, steel);
            mb.Build("Mesh", go.transform, Layers.Item);

            for (int side = 0; side < 2; side++)
            {
                var hinge = GeoUtil.CreateChild(go.transform, side == 0 ? "JawHinge_L" : "JawHinge_R", new Vector3(0, hy, 0),
                    Quaternion.Euler(0, side == 0 ? 90f : -90f, 0), Layers.Item);
                var jaw = GeoUtil.CreateChild(hinge, side == 0 ? "Jaw_L" : "Jaw_R", Vector3.zero, Quaternion.identity, Layers.Item);
                var jb = new MeshBuilder();
                jb.SetMaterial(mat);
                // semicircle in the jaw's local XZ plane on the -Z side, hinge axis = local X (ends at x = +-r)
                const int N = 8;
                for (int i = 0; i < N; i++)
                {
                    float a0 = (float)i / N * Mathf.PI, a1 = (float)(i + 1) / N * Mathf.PI;
                    Vector3 q0 = new Vector3(Mathf.Cos(a0) * r, 0, -Mathf.Sin(a0) * r);
                    Vector3 q1 = new Vector3(Mathf.Cos(a1) * r, 0, -Mathf.Sin(a1) * r);
                    Bar(jb, q0, q1, 0.006f, 0.022f, steel);
                    // tooth pointing towards the centre line (becomes "up" when closed)
                    Vector3 mid = (q0 + q1) * 0.5f;
                    Vector3 inward = -mid.normalized;
                    Vector3 tb0 = q0 + inward * 0.004f, tb1 = q1 + inward * 0.004f, tip = mid + inward * 0.026f;
                    jb.AddTriangle(tb0 + Vector3.up * 0.004f, tip, tb1 + Vector3.up * 0.004f,
                        new Vector2(teeth.xMin, teeth.yMin), new Vector2(teeth.center.x, teeth.yMax), new Vector2(teeth.xMax, teeth.yMin));
                    jb.AddTriangle(tb1 - Vector3.up * 0.004f, tip, tb0 - Vector3.up * 0.004f,
                        new Vector2(teeth.xMin, teeth.yMin), new Vector2(teeth.center.x, teeth.yMax), new Vector2(teeth.xMax, teeth.yMin));
                }
                jb.Build("JawMesh", jaw, Layers.Item);
            }
            return go;
        }
    }
}
