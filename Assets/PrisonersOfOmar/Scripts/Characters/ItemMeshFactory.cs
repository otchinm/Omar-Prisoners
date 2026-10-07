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
                case ItemType.Revolver: Revolver(mb, go.transform); break;
                case ItemType.Screwdriver: Screwdriver(mb); break;
                case ItemType.Backpack: Backpack(mb); break;
                case ItemType.SmallKey: SmallKey(mb); break;
                case ItemType.VhsTape: VhsTape(mb); break;
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
                case ItemType.Revolver: return HoldPose.Pistol;
                default: return HoldPose.OneHandSmall;
            }
        }

        /// <summary>Local offset to apply so the model rests on a surface (model placed at surface point + RestOffset, RestRotation).</summary>
        public static Vector3 RestOffset(ItemType type)
        {
            switch (type)
            {
                case ItemType.Lighter: return new Vector3(0, 0.0236f, 0);
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
                case ItemType.Revolver: return new Vector3(0f, 0.0175f, -0.05f);
                case ItemType.Screwdriver: return new Vector3(0f, 0.0145f, -0.04f);
                case ItemType.Backpack: return new Vector3(0f, BackpackH * 0.5f, 0f);
                case ItemType.SmallKey: return new Vector3(0.004f, 0.0015f, 0.009f);
                case ItemType.VhsTape: return new Vector3(0f, 0.0125f, 0f);
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
                case ItemType.Revolver: return Quaternion.Euler(0f, 0f, 90f);      // lying on its side
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
        // lighter.png 64x64 (Zippo): case front (0,0,32,32) case side (32,0,16,32) lid (0,32,32,16) chimney (32,32,16,16)
        //                   case top / insert top (48,0,16,16) insert (48,16,16,16) flint wheel (48,32,16,16) lid inside (0,48,32,16)
        /// <summary>Lid hinge child of the Zippo model (rotate local Z: 0 = shut, <see cref="LighterLidOpen"/> = flipped open).</summary>
        public const float LighterLidOpen = -128f;

        static void Lighter(MeshBuilder mb, Transform root)
        {
            const float W = 64, H = 64;
            var mat = Mat("lighter");
            mb.SetMaterial(mat);
            Rect front = R(0, 0, 32, 32, W, H), side = R(32, 0, 16, 32, W, H), lid = R(0, 32, 32, 16, W, H);
            Rect chim = R(32, 32, 16, 16, W, H), top = R(48, 0, 16, 16, W, H), insert = R(48, 16, 16, 16, W, H), wheel = R(48, 32, 16, 16, W, H);
            Rect lidIn = R(0, 48, 32, 16, W, H);
            // slim and upright (narrower and taller than a stock Zippo, about the same overall size)
            const float w = 0.031f, d = 0.013f, caseH = 0.047f, lidH = 0.025f;
            const float top0 = caseH * 0.5f, chimX = -0.0036f, wheelX = 0.0087f;
            // lower case (pivot = grip, middle of the case)
            mb.AddBox(new Vector3(0, 0, 0), new Vector3(w, caseH, d),
                new BoxUVRects { PosZ = front, NegZ = front, PosX = side, NegX = side, PosY = top, NegY = top });
            // insert rim sticking out of the case
            mb.AddBox(new Vector3(0, top0 + 0.002f, 0), new Vector3(w - 0.003f, 0.004f, d - 0.002f), BoxUVRects.All(insert));
            // perforated chimney (wind guard) on the left, flint wheel + cam on the right
            mb.AddBox(new Vector3(chimX, top0 + 0.012f, 0), new Vector3(0.017f, 0.016f, 0.0115f),
                new BoxUVRects { PosZ = chim, NegZ = chim, PosX = chim, NegX = chim, PosY = insert, NegY = insert });
            Cyl(mb, new Vector3(wheelX, top0 + 0.0125f, -0.0042f), new Vector3(wheelX, top0 + 0.0125f, 0.0042f), 0.0038f, 0.0038f, 8, wheel, wheel, wheel);
            mb.AddBox(new Vector3(wheelX + 0.0009f, top0 + 0.006f, 0), new Vector3(0.005f, 0.008f, 0.0016f), BoxUVRects.All(insert));
            // wick tip inside the chimney
            mb.AddBox(new Vector3(chimX, top0 + 0.019f, 0), new Vector3(0.0028f, 0.0025f, 0.0028f), BoxUVRects.All(wheel));

            // hinged lid as its own child so it can flip open / shut (hinge on the right edge of the case top)
            var hinge = Child(root, "Lid", new Vector3(w * 0.5f, caseH * 0.5f, 0), Quaternion.identity);
            var lm = new MeshBuilder();
            lm.SetMaterial(mat);
            lm.AddBox(new Vector3(-w * 0.5f, lidH * 0.5f, 0), new Vector3(w, lidH, d),
                new BoxUVRects { PosZ = lid, NegZ = lid, PosX = side, NegX = side, PosY = top, NegY = lidIn });
            lm.AddBox(new Vector3(0.0008f, 0.002f, 0), new Vector3(0.0022f, 0.006f, 0.006f), BoxUVRects.All(insert)); // hinge barrel
            lm.Build("LidMesh", hinge, Layers.Item);

            // the tongue starts just inside the chimney so its round base is half hidden, like the reference
            Child(root, "Anchor_Flame", new Vector3(chimX, top0 + 0.0175f, 0), Quaternion.identity);
        }

        /// <summary>Open the Zippo lid of a lighter model (0 = shut, 1 = open). Safe on any model.</summary>
        public static void SetLighterLid(GameObject model, float open01)
        {
            if (model == null) return;
            var lid = model.transform.Find("Lid");
            if (lid == null) return;
            float k = Mathf.Clamp01(open01);
            lid.localRotation = Quaternion.Euler(0f, 0f, LighterLidOpen * k);
        }

        // revolver.png 64x64: blued steel (0,0,32,32) worn steel (32,0,32,32) wooden grip (0,32,32,32) cylinder (32,32,16,16)
        //                     bore / dark (48,32,16,16) brass rims (32,48,16,16)
        static void Revolver(MeshBuilder mb, Transform root)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("revolver"));
            Rect steel = R(0, 0, 32, 32, W, H), worn = R(32, 0, 32, 32, W, H), wood = R(0, 32, 32, 32, W, H);
            Rect cyl = R(32, 32, 16, 16, W, H), dark = R(48, 32, 16, 16, W, H), brass = R(32, 48, 16, 16, W, H);
            // grip (pivot = where the hand closes), raked back
            Box(mb, new Vector3(0f, -0.035f, -0.012f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.026f, 0.075f, 0.032f), BoxUVRects.All(wood));
            Box(mb, new Vector3(0f, -0.072f, -0.024f), Quaternion.Euler(18f, 0f, 0f), new Vector3(0.028f, 0.008f, 0.036f), BoxUVRects.All(steel)); // butt cap
            // frame
            Box(mb, new Vector3(0f, 0.012f, 0.022f), Quaternion.identity, new Vector3(0.02f, 0.03f, 0.075f), BoxUVRects.All(worn));
            // cylinder with chambers (brass rims at the back)
            Cyl(mb, new Vector3(0f, 0.014f, 0.004f), new Vector3(0f, 0.014f, 0.046f), 0.0175f, 0.0175f, 8, cyl, brass, dark);
            // barrel + ejector rod
            Cyl(mb, new Vector3(0f, 0.022f, 0.046f), new Vector3(0f, 0.022f, 0.17f), 0.0072f, 0.0068f, 7, steel, null, dark);
            Cyl(mb, new Vector3(0f, 0.008f, 0.05f), new Vector3(0f, 0.008f, 0.13f), 0.0032f, 0.0032f, 5, worn, null, worn);
            Box(mb, new Vector3(0f, 0.031f, 0.163f), Quaternion.identity, new Vector3(0.003f, 0.006f, 0.008f), BoxUVRects.All(steel)); // front sight
            // top strap + hammer
            Box(mb, new Vector3(0f, 0.034f, 0.02f), Quaternion.identity, new Vector3(0.012f, 0.006f, 0.06f), BoxUVRects.All(steel));
            Box(mb, new Vector3(0f, 0.032f, -0.018f), Quaternion.Euler(-35f, 0f, 0f), new Vector3(0.007f, 0.016f, 0.008f), BoxUVRects.All(worn));
            // trigger guard + trigger
            Bar(mb, new Vector3(0f, -0.004f, 0.026f), new Vector3(0f, -0.022f, 0.02f), 0.004f, 0.004f, steel);
            Bar(mb, new Vector3(0f, -0.022f, 0.02f), new Vector3(0f, -0.02f, -0.002f), 0.004f, 0.004f, steel);
            Bar(mb, new Vector3(0f, -0.002f, 0.012f), new Vector3(0f, -0.014f, 0.008f), 0.0035f, 0.003f, worn);
            Child(root, "Anchor_Muzzle", new Vector3(0f, 0.022f, 0.172f), Quaternion.identity);
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

        // screwdriver.png 32x32: handle (0,0,16,32) shaft (16,0,16,32). Pivot = middle of the handle, tip towards +Z.
        static void Screwdriver(MeshBuilder mb)
        {
            const float W = 32, H = 32;
            mb.SetMaterial(Mat("screwdriver"));
            Rect handle = R(0, 0, 16, 32, W, H), steel = R(16, 0, 16, 32, W, H);
            // fluted handle, a bit fatter towards the back, rounded butt
            Cyl(mb, new Vector3(0, 0, -0.052f), new Vector3(0, 0, 0.038f), 0.0135f, 0.0125f, 8, handle, handle, null, false);
            Cyl(mb, new Vector3(0, 0, 0.038f), new Vector3(0, 0, 0.05f), 0.0125f, 0.007f, 8, handle, null, null, false);
            Cyl(mb, new Vector3(0, 0, -0.058f), new Vector3(0, 0, -0.052f), 0.011f, 0.0135f, 8, handle, handle, null, false);
            // ferrule + shaft + flat blade
            Cyl(mb, new Vector3(0, 0, 0.05f), new Vector3(0, 0, 0.058f), 0.0055f, 0.0045f, 6, steel, steel, null);
            Cyl(mb, new Vector3(0, 0, 0.058f), new Vector3(0, 0, 0.152f), 0.0032f, 0.0032f, 6, steel, null, null);
            mb.AddBox(new Vector3(0, 0, 0.158f), new Vector3(0.0075f, 0.0016f, 0.014f), BoxUVRects.All(steel));
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

        // backpack.png 64x64: canvas (0,0,32,32) flap (32,0,32,16) pocket (32,16,32,16) strap (0,32,32,8) leather (0,40,32,8)
        //                   bottom (0,48,32,16) buckle (32,32,16,16) side pocket (32,48,16,16) side (48,32,16,32, portrait)
        const float BackpackW = 0.30f, BackpackH = 0.40f, BackpackD = 0.15f;

        /// <summary>(iteration 3) Old canvas rucksack, pivot = centre of the bag, the shoulder straps on the +Z side
        /// (against the wearer's back, see <see cref="WearOnBack"/>), flap and pocket on -Z.</summary>
        static void Backpack(MeshBuilder mb)
        {
            const float W = 64, H = 64;
            mb.SetMaterial(Mat("backpack"));
            Rect canvas = R(0, 0, 32, 32, W, H), flap = R(32, 0, 32, 16, W, H), pocket = R(32, 16, 32, 16, W, H);
            Rect strap = R(0, 32, 32, 8, W, H), leather = R(0, 40, 32, 8, W, H), bottom = R(0, 48, 32, 16, W, H);
            Rect buckle = R(32, 32, 16, 16, W, H), sidePocket = R(32, 48, 16, 16, W, H), side = R(48, 32, 16, 32, W, H);
            const float w = BackpackW, d = BackpackD;
            // two bodies: a full, sagging bottom half and a slacker top half under the lid (bottom at y = -0.2)
            mb.AddBox(new Vector3(0f, -0.095f, 0f), new Vector3(w + 0.01f, 0.21f, d + 0.02f),
                new BoxUVRects { PosZ = canvas, NegZ = canvas, PosX = side, NegX = side, PosY = canvas, NegY = bottom });
            mb.AddBox(new Vector3(0f, 0.095f, 0.002f), new Vector3(w - 0.012f, 0.17f, d - 0.008f),
                new BoxUVRects { PosZ = canvas, NegZ = canvas, PosX = side, NegX = side, PosY = canvas, NegY = bottom });
            // the lid on top and its flap hanging down the front (wider at the bottom where the bag bulges)
            mb.AddBox(new Vector3(0f, 0.185f, -0.004f), new Vector3(w, 0.022f, d + 0.004f),
                new BoxUVRects { PosY = R(32, 0, 32, 8, W, H), NegY = bottom, PosZ = strap, NegZ = strap, PosX = strap, NegX = strap });
            Box(mb, new Vector3(0f, 0.10f, -0.088f), Quaternion.Euler(4f, 0f, 0f), new Vector3(w * 0.86f, 0.19f, 0.008f),
                new BoxUVRects { NegZ = flap, PosZ = flap, PosX = strap, NegX = strap, PosY = strap, NegY = strap });
            // the front pocket under the flap, the side pockets set forward (they barely show from the front when worn)
            mb.AddBox(new Vector3(0f, -0.105f, -0.105f), new Vector3(w * 0.72f, 0.13f, 0.04f),
                new BoxUVRects { PosZ = pocket, NegZ = pocket, PosX = sidePocket, NegX = sidePocket, PosY = strap, NegY = bottom });
            for (int sx = -1; sx <= 1; sx += 2)
            {
                mb.AddBox(new Vector3(sx * 0.17f, -0.11f, -0.045f), new Vector3(0.03f, 0.12f, 0.07f),
                    new BoxUVRects { PosZ = sidePocket, NegZ = sidePocket, PosX = sidePocket, NegX = sidePocket, PosY = strap, NegY = bottom });
                // flap straps lying on the lid and the flap, down into buckles sewn on between the flap and the pocket
                float x = sx * 0.075f;
                Bar(mb, new Vector3(x, 0.1985f, -0.055f), new Vector3(x, 0.1985f, -0.084f), 0.022f, 0.005f, leather);
                Bar(mb, new Vector3(x, 0.1985f, -0.084f), new Vector3(x, 0.19f, -0.0885f), 0.022f, 0.005f, leather);
                Bar(mb, new Vector3(x, 0.19f, -0.0885f), new Vector3(x, 0.006f, -0.1011f), 0.022f, 0.005f, leather);
                Bar(mb, new Vector3(x, 0.006f, -0.1011f), new Vector3(x, -0.012f, -0.0945f), 0.022f, 0.005f, leather);
                mb.AddBox(new Vector3(x, -0.016f, -0.0895f), new Vector3(0.032f, 0.026f, 0.009f), BoxUVRects.All(buckle));
                // shoulder straps flat on the back side, folding over the top edge into the lid
                Bar(mb, new Vector3(sx * 0.085f, 0.165f, 0.08f), new Vector3(sx * 0.1f, -0.17f, 0.091f), 0.048f, 0.01f, strap);
                Bar(mb, new Vector3(sx * 0.085f, 0.165f, 0.08f), new Vector3(sx * 0.085f, 0.2f, 0.066f), 0.048f, 0.01f, strap);
                mb.AddBox(new Vector3(sx * 0.1f, -0.16f, 0.094f), new Vector3(0.04f, 0.03f, 0.008f), BoxUVRects.All(buckle));
            }
            // flat grab loop on top at the back
            Bar(mb, new Vector3(-0.025f, 0.196f, 0.055f), new Vector3(-0.02f, 0.221f, 0.055f), 0.016f, 0.005f, strap);
            Bar(mb, new Vector3(-0.02f, 0.221f, 0.055f), new Vector3(0.02f, 0.221f, 0.055f), 0.016f, 0.005f, strap);
            Bar(mb, new Vector3(0.02f, 0.221f, 0.055f), new Vector3(0.025f, 0.196f, 0.055f), 0.016f, 0.005f, strap);
        }

        // smallkey.png 32x32: brass (0,0,16,32) tag (16,0,16,32)
        /// <summary>(iteration 3) A small brass cabinet key on a string with a cardboard tag, lying flat (pivot = the bow).</summary>
        static void SmallKey(MeshBuilder mb)
        {
            mb.SetMaterial(Mat("smallkey"));
            Rect brass = R(0, 0, 16, 32, 32, 32), tag = R(16, 0, 16, 32, 32, 32);
            const int N = 7;
            for (int i = 0; i < N; i++)
            {
                float a0 = (float)i / N * Mathf.PI * 2f, a1 = (float)(i + 1) / N * Mathf.PI * 2f;
                Bar(mb, new Vector3(Mathf.Cos(a0) * 0.009f, 0, Mathf.Sin(a0) * 0.009f), new Vector3(Mathf.Cos(a1) * 0.009f, 0, Mathf.Sin(a1) * 0.009f), 0.004f, 0.003f, brass);
            }
            Bar(mb, new Vector3(0, 0, 0.009f), new Vector3(0, 0, 0.046f), 0.0035f, 0.0025f, brass);
            mb.AddBox(new Vector3(0.0045f, 0, 0.04f), new Vector3(0.006f, 0.0025f, 0.009f), BoxUVRects.All(brass));
            // the string and the tag
            Bar(mb, new Vector3(0, 0, -0.009f), new Vector3(-0.006f, 0, -0.03f), 0.0012f, 0.0012f, tag);
            mb.AddBox(new Vector3(-0.008f, 0, -0.046f), new Vector3(0.022f, 0.001f, 0.034f), BoxUVRects.All(tag));
        }

        // vhstape.png 64x32: label (0,0,48,16) shell (0,16,48,16) spine label (48,0,16,16) window + reels (48,16,16,16)
        /// <summary>(iteration 3) A VHS cassette lying flat, label up (pivot = its centre; the spine faces -Z).</summary>
        static void VhsTape(MeshBuilder mb)
        {
            const float W = 64, H = 32;
            mb.SetMaterial(Mat("vhstape"));
            Rect label = R(0, 0, 48, 16, W, H), shell = R(0, 16, 48, 16, W, H), spine = R(48, 0, 16, 16, W, H), window = R(48, 16, 16, 16, W, H);
            mb.AddBox(Vector3.zero, new Vector3(0.187f, 0.025f, 0.103f), new BoxUVRects { PosY = label, NegY = shell, NegZ = spine, PosZ = shell, PosX = shell, NegX = shell });
            mb.AddBox(new Vector3(0f, 0.0128f, 0.012f), new Vector3(0.09f, 0.0006f, 0.03f), BoxUVRects.All(window));   // the reel window
        }

        // padlock.png 32x32: brass (0,0,16,16) shackle (16,0,16,16) keyhole face (0,16,16,16) dial face (16,16,16,16)
        /// <summary>(iteration 3) The padlock on a locked drawer, hanging from its hasp (pivot = the hasp on the drawer front,
        /// the lock face towards +Z). <paramref name="dial"/> = a combination padlock.</summary>
        public static GameObject BuildPadlock(bool dial)
        {
            var go = new GameObject(dial ? "CombinationLock" : "Padlock");
            go.layer = Layers.World;
            var mb = new MeshBuilder();
            mb.SetMaterial(Mat("padlock"));
            Rect brass = R(0, 0, 16, 16, 32, 32), steel = R(16, 0, 16, 16, 32, 32), hole = R(0, 16, 16, 16, 32, 32), face = R(16, 16, 16, 16, 32, 32);
            // the hasp plate screwed to the drawer, its staple sticking out
            mb.AddBox(new Vector3(0f, 0.004f, 0.002f), new Vector3(0.02f, 0.034f, 0.004f), BoxUVRects.All(steel));
            Bar(mb, new Vector3(-0.007f, 0.012f, 0.004f), new Vector3(-0.007f, 0.012f, 0.014f), 0.003f, 0.003f, steel);
            Bar(mb, new Vector3(0.007f, 0.012f, 0.004f), new Vector3(0.007f, 0.012f, 0.014f), 0.003f, 0.003f, steel);
            // shackle through the staple, the body hanging under it
            Bar(mb, new Vector3(-0.008f, -0.006f, 0.014f), new Vector3(-0.008f, 0.016f, 0.014f), 0.0032f, 0.0032f, steel);
            Bar(mb, new Vector3(0.008f, -0.006f, 0.014f), new Vector3(0.008f, 0.016f, 0.014f), 0.0032f, 0.0032f, steel);
            Bar(mb, new Vector3(-0.0095f, 0.016f, 0.014f), new Vector3(0.0095f, 0.016f, 0.014f), 0.0032f, 0.0032f, steel);
            mb.AddBox(new Vector3(0f, -0.019f, 0.014f), new Vector3(0.03f, 0.028f, 0.014f),
                new BoxUVRects { PosZ = dial ? face : hole, NegZ = brass, PosX = brass, NegX = brass, PosY = brass, NegY = brass });
            mb.Build("Mesh", go.transform, Layers.World);
            return go;
        }

        /// <summary>Hangs a <see cref="Build"/>(Backpack) model on a character's back: parented to the chest bone, its
        /// top just under the shoulders, its straps against the back, scaled to the body.</summary>
        public static void WearOnBack(GameObject pack, HumanoidRig rig)
        {
            if (pack == null || rig == null || rig.Chest == null) return;
            var spec = rig.Spec;
            float s = Mathf.Clamp(rig.Height / 1.75f, 0.7f, 1.3f);
            float back = 0.13f * s, shoulder = rig.Height * 0.8f;
            if (spec != null)
            {
                back = 0f;
                for (int k = 3; k <= 7 && k < spec.Torso.Length; k++) back = Mathf.Max(back, spec.Torso[k].B);
                shoulder = spec.ShoulderY;
            }
            Vector3 center = new Vector3(0f, shoulder - 0.03f * s - BackpackH * 0.5f * s, -(back + BackpackD * 0.5f * s + 0.012f));
            Vector3 chest = rig.BindPositions != null ? rig.BindPositions[(int)BoneId.Chest] : rig.Chest.position - rig.transform.position;
            pack.transform.SetParent(rig.Chest, false);
            // bones have an identity rotation in the bind pose: root-space offsets are chest-space offsets
            pack.transform.localPosition = center - chest;
            pack.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);   // leans back a touch
            pack.transform.localScale = Vector3.one * s;
            if (spec != null) FrontStraps(pack.transform, spec, s, back, center);
        }

        /// <summary>The shoulder straps coming over the shoulders and down the chest (the clearest "he wears a backpack" cue
        /// from the front). Built in the pack's own space so they go away with it.</summary>
        static void FrontStraps(Transform pack, BodySpec spec, float s, float back, Vector3 center)
        {
            float frontZ = 0f;
            for (int k = 5; k <= 7 && k < spec.Torso.Length; k++) frontZ = Mathf.Max(frontZ, spec.Torso[k].F + spec.Torso[k].C);
            Quaternion inv = Quaternion.Inverse(pack.localRotation);
            Vector3 Local(Vector3 root) => inv * (root - center) / s;
            var mb = new MeshBuilder();
            mb.SetMaterial(Mat("backpack"));
            Rect strap = R(0, 32, 32, 8, 64, 64), buckle = R(32, 32, 16, 16, 64, 64);
            float y = spec.ShoulderY;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Vector3[] p =
                {
                    new Vector3(sx * 0.085f, 0.2f, 0.066f),   // the top of the strap on the bag (pack space)
                    Local(new Vector3(sx * 0.075f * s, y - 0.02f * s, -(back - 0.005f))),
                    Local(new Vector3(sx * 0.085f * s, y + 0.045f * s, -0.03f * s)),
                    Local(new Vector3(sx * 0.095f * s, y + 0.015f * s, frontZ * 0.6f)),
                    Local(new Vector3(sx * 0.105f * s, y - 0.16f * s, frontZ + 0.006f)),
                };
                for (int i = 0; i + 1 < p.Length; i++) Bar(mb, p[i], p[i + 1], 0.042f, 0.007f, strap);
                Box(mb, p[p.Length - 1], Quaternion.LookRotation(p[p.Length - 1] - p[p.Length - 2], Vector3.forward),
                    new Vector3(0.03f, 0.006f, 0.022f), BoxUVRects.All(buckle));
            }
            mb.Build("Straps_Front", pack, Layers.Item);
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
            // a heavy butcher's cleaver like the reference: long taped handle, big square blade
            // handle: z -0.1 .. 0.045
            mb.AddBox(new Vector3(0, -0.002f, -0.027f), new Vector3(0.021f, 0.03f, 0.145f),
                new BoxUVRects { PosX = handle, NegX = handle, PosY = wood, NegY = wood, PosZ = wood, NegZ = wood });
            mb.AddBox(new Vector3(0, 0.0f, 0.047f), new Vector3(0.016f, 0.036f, 0.012f), BoxUVRects.All(edge)); // bolster
            // blade: z 0.05 .. 0.31, spine at y +0.022, edge sagging from -0.088 to -0.104
            float z0 = 0.05f, z1 = 0.31f, ySpine = 0.022f, yEdge0 = -0.088f, yEdge1 = -0.104f;
            float th = 0.0055f;
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
                float z = -0.075f + i * 0.042f;
                mb.AddBox(new Vector3(0, -0.002f, z), new Vector3(0.023f, 0.007f, 0.007f), BoxUVRects.All(edge));
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
        /// (both jaws vertical, teeth meeting in the middle), see <see cref="SetBearTrapJaws"/>. The pan in the middle is the
        /// trigger. Heavy leaf springs at both ends (their eyes clamp the jaw ends), a latch over the pan and a chain staked
        /// into the ground.</remarks>
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
            // base: hinge bar along Z + a cross plate under the pan
            Bar(mb, new Vector3(0, 0.008f, -0.2f), new Vector3(0, 0.008f, 0.2f), 0.032f, 0.012f, steel);
            Bar(mb, new Vector3(-0.07f, 0.006f, 0), new Vector3(0.07f, 0.006f, 0), 0.04f, 0.008f, steel);
            for (int s = 0; s < 2; s++)
            {
                float sz = s == 0 ? 1f : -1f;
                // hinge post the jaw ends turn in
                Bar(mb, new Vector3(-0.016f, hy, sz * r), new Vector3(0.016f, hy, sz * r), 0.018f, 0.02f, steel);
                // leaf spring folded back on itself: lower leaf on the ground, rounded fold, upper leaf rising to the eye
                Vector3 l0 = new Vector3(0, 0.01f, sz * 0.17f), l1 = new Vector3(0, 0.01f, sz * 0.38f);
                Vector3 u0 = new Vector3(0, 0.043f, sz * 0.37f), u1 = new Vector3(0, 0.047f, sz * (r + 0.035f));
                Bar(mb, l0, l1, 0.03f, 0.007f, steel);
                Bar(mb, u0, u1, 0.03f, 0.007f, steel);
                Cyl(mb, new Vector3(-0.015f, 0.026f, sz * 0.39f), new Vector3(0.015f, 0.026f, sz * 0.39f), 0.018f, 0.018f, 6, steel);
                // the eye: a rectangular loop round both jaw ends, pressing them down
                float ez = sz * r, ey = 0.034f;
                Bar(mb, new Vector3(-0.036f, ey + 0.017f, ez), new Vector3(0.036f, ey + 0.017f, ez), 0.008f, 0.008f, steel);
                Bar(mb, new Vector3(-0.036f, ey - 0.02f, ez), new Vector3(0.036f, ey - 0.02f, ez), 0.008f, 0.006f, steel);
                Bar(mb, new Vector3(-0.036f, ey - 0.02f, ez), new Vector3(-0.036f, ey + 0.017f, ez), 0.008f, 0.03f, steel);
                Bar(mb, new Vector3(0.036f, ey - 0.02f, ez), new Vector3(0.036f, ey + 0.017f, ez), 0.008f, 0.03f, steel);
            }
            // pan (trigger plate): octagon on a stem, with the latch (dog) lying across from the left jaw onto its notch
            const int P = 8;
            Vector3 panC = new Vector3(0, 0.024f, 0);
            for (int i = 0; i < P; i++)
            {
                float a0 = (i + 0.5f) / P * Mathf.PI * 2f, a1 = (i + 1.5f) / P * Mathf.PI * 2f;
                Vector3 e0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * 0.05f, e1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * 0.05f;
                Vector2 t0 = new Vector2(pan.center.x + Mathf.Cos(a0) * pan.width * 0.5f, pan.center.y + Mathf.Sin(a0) * pan.height * 0.5f);
                Vector2 t1 = new Vector2(pan.center.x + Mathf.Cos(a1) * pan.width * 0.5f, pan.center.y + Mathf.Sin(a1) * pan.height * 0.5f);
                mb.AddTriangle(panC + Vector3.up * 0.004f, panC + e1 + Vector3.up * 0.004f, panC + e0 + Vector3.up * 0.004f, pan.center, t1, t0);
                mb.AddQuad(panC + e0 - Vector3.up * 0.003f, panC + e0 + Vector3.up * 0.004f, panC + e1 + Vector3.up * 0.004f, panC + e1 - Vector3.up * 0.003f,
                    new Vector2(steel.xMin, steel.yMin), new Vector2(steel.xMin, steel.yMin + 0.02f), new Vector2(steel.xMax, steel.yMin + 0.02f), new Vector2(steel.xMax, steel.yMin));
            }
            Bar(mb, new Vector3(0, 0.012f, 0), new Vector3(0, 0.022f, 0), 0.02f, 0.02f, steel);
            Bar(mb, new Vector3(-0.145f, 0.026f, 0.012f), new Vector3(-0.03f, 0.031f, 0.006f), 0.012f, 0.005f, steel);
            // anchor chain to a stake driven into the ground (links alternate flat / upright)
            Vector3 c0 = new Vector3(0.012f, 0.012f, -0.38f), c1 = new Vector3(0.2f, 0.012f, -0.58f);
            const int Links = 6;
            Vector3 cd = (c1 - c0) / Links;
            Quaternion along = Quaternion.LookRotation(cd.normalized);
            for (int k = 0; k < Links; k++)
            {
                Vector3 lc = c0 + cd * (k + 0.5f) + Vector3.up * (k % 2 == 0 ? 0f : 0.008f);
                Quaternion lr = along * Quaternion.Euler(0, 0, k % 2 == 0 ? 0f : 90f);
                Link(mb, lc, lr, cd.magnitude * 0.72f, 0.016f, 0.0045f, steel);
            }
            Bar(mb, c1 + new Vector3(0, -0.06f, 0), c1 + new Vector3(0, 0.035f, 0), 0.012f, 0.012f, steel);
            Cyl(mb, c1 + new Vector3(-0.012f, 0.04f, 0), c1 + new Vector3(0.012f, 0.04f, 0), 0.016f, 0.016f, 6, steel);
            mb.Build("Mesh", go.transform, Layers.Item);

            for (int side = 0; side < 2; side++)
            {
                var hinge = GeoUtil.CreateChild(go.transform, side == 0 ? "JawHinge_L" : "JawHinge_R", new Vector3(0, hy, 0),
                    Quaternion.Euler(0, side == 0 ? 90f : -90f, 0), Layers.Item);
                var jaw = GeoUtil.CreateChild(hinge, side == 0 ? "Jaw_L" : "Jaw_R", Vector3.zero, Quaternion.identity, Layers.Item);
                var jb = new MeshBuilder();
                jb.SetMaterial(mat);
                // semicircle in the jaw's local XZ plane on the -Z side, hinge axis = local X (ends at x = +-r);
                // heavy serrated teeth, offset by half a tooth on the other jaw so they interleave when shut
                const int N = 10;
                float off = side == 0 ? 0f : 0.5f;
                for (int i = 0; i < N; i++)
                {
                    float a0 = (float)i / N * Mathf.PI, a1 = (float)(i + 1) / N * Mathf.PI;
                    Vector3 q0 = new Vector3(Mathf.Cos(a0) * r, 0, -Mathf.Sin(a0) * r);
                    Vector3 q1 = new Vector3(Mathf.Cos(a1) * r, 0, -Mathf.Sin(a1) * r);
                    Bar(jb, q0, q1, 0.008f, 0.026f, steel);
                    if (side == 1 && i == N - 1) continue;
                    float am = ((float)i + 0.5f + off) / N * Mathf.PI;
                    float ah = 0.42f / N * Mathf.PI;
                    Vector3 b0 = new Vector3(Mathf.Cos(am - ah) * r, 0.003f, -Mathf.Sin(am - ah) * r);
                    Vector3 b1 = new Vector3(Mathf.Cos(am + ah) * r, 0.003f, -Mathf.Sin(am + ah) * r);
                    Vector3 mid = new Vector3(Mathf.Cos(am) * r, 0.003f, -Mathf.Sin(am) * r);
                    Vector3 inward = new Vector3(-mid.x, 0, -mid.z).normalized;
                    Tooth(jb, b0, b1, mid + inward * 0.034f + Vector3.up * 0.004f, 0.006f, teeth);
                }
                // the jaw ends run past the hinge into the spring eye
                Bar(jb, new Vector3(r, 0, 0), new Vector3(r + 0.03f, 0, 0.008f), 0.01f, 0.02f, steel);
                Bar(jb, new Vector3(-r, 0, 0), new Vector3(-r - 0.03f, 0, 0.008f), 0.01f, 0.02f, steel);
                jb.Build("JawMesh", jaw, Layers.Item);
            }
            return go;
        }

        /// <summary>Opens / shuts a bear trap model (0 = open flat, 1 = snapped shut). Safe on any model.</summary>
        public static void SetBearTrapJaws(GameObject trap, float closed01)
        {
            if (trap == null) return;
            float a = 88f * Mathf.Clamp01(closed01);
            for (int side = 0; side < 2; side++)
            {
                var hinge = trap.transform.Find(side == 0 ? "JawHinge_L" : "JawHinge_R");
                var j = hinge != null ? hinge.Find(side == 0 ? "Jaw_L" : "Jaw_R") : null;
                if (j != null) j.localRotation = Quaternion.Euler(a, 0f, 0f);
            }
        }

        /// <summary>Pyramid tooth: base edge b0..b1 (thickness t up / down), pointing at tip.</summary>
        static void Tooth(MeshBuilder mb, Vector3 b0, Vector3 b1, Vector3 tip, float t, Rect uv)
        {
            Vector3 up = Vector3.up * t * 0.5f;
            Vector2 u0 = new Vector2(uv.xMin, uv.yMin), u1 = new Vector2(uv.xMax, uv.yMin), ut = new Vector2(uv.center.x, uv.yMax);
            mb.AddTriangle(b1 + up, tip, b0 + up, u1, ut, u0);
            mb.AddTriangle(b0 - up, tip, b1 - up, u0, ut, u1);
            mb.AddTriangle(b0 + up, tip, b0 - up, u0, ut, u1);
            mb.AddTriangle(b1 - up, tip, b1 + up, u1, ut, u0);
        }

        /// <summary>Oval chain link centred at c (long axis = local Z of rot, in the local YZ plane).</summary>
        static void Link(MeshBuilder mb, Vector3 c, Quaternion rot, float len, float wid, float wire, Rect uv)
        {
            const int N = 8;
            for (int i = 0; i < N; i++)
            {
                float a0 = (float)i / N * Mathf.PI * 2f, a1 = (float)(i + 1) / N * Mathf.PI * 2f;
                Vector3 p0 = c + rot * new Vector3(0, Mathf.Sin(a0) * wid * 0.5f, Mathf.Cos(a0) * len * 0.5f);
                Vector3 p1 = c + rot * new Vector3(0, Mathf.Sin(a1) * wid * 0.5f, Mathf.Cos(a1) * len * 0.5f);
                Bar(mb, p0, p1, wire, wire, uv);
            }
        }

        /// <summary>Length of a tin can's string above its pivot (tie it to the wire this far up).</summary>
        public const float CanString = 0.025f;

        /// <summary>
        /// Tin can hung on a tripwire as a rattle (pivot = the hole at the top where its string is tied, can hangs along -Y,
        /// 7.5 cm tall).
        /// </summary>
        public static GameObject BuildTinCan(int seed)
        {
            var go = new GameObject("TinCan");
            go.layer = Layers.Item;
            var mb = new MeshBuilder();
            float k = (seed * 37 % 10) / 10f;
            var tin = Mat("tripwire_stake");
            Rect steel = R(0, 0, 32, 24, 32, 32), wire = R(0, 24, 32, 8, 32, 32);
            mb.SetMaterial(tin);
            const float rr = 0.029f, h = 0.075f;
            // string, the can body with two rolled ribs
            Bar(mb, new Vector3(0, CanString, 0), new Vector3(0, 0f, 0), 0.0025f, 0.0025f, wire);
            Cyl(mb, new Vector3(0, -h, 0), new Vector3(0, 0f, 0), rr, rr, 8, steel, steel, steel);
            Cyl(mb, new Vector3(0, -h * 0.34f, 0), new Vector3(0, -h * 0.30f, 0), rr + 0.002f, rr + 0.002f, 8, wire);
            Cyl(mb, new Vector3(0, -h * 0.70f, 0), new Vector3(0, -h * 0.66f, 0), rr + 0.002f, rr + 0.002f, 8, wire);
            mb.Build("Mesh", go.transform, Layers.Item);
            // a faded paper label on some of them
            if (k > 0.35f)
            {
                var lb = new MeshBuilder();
                lb.SetMaterial(PsxMaterials.GetColor(Color.Lerp(new Color(0.42f, 0.16f, 0.12f), new Color(0.5f, 0.45f, 0.3f), k)));
                Cyl(lb, new Vector3(0, -h * 0.62f, 0), new Vector3(0, -h * 0.38f, 0), rr + 0.0015f, rr + 0.0015f, 8, new Rect(0, 0, 1, 1));
                lb.Build("Label", go.transform, Layers.Item);
            }
            return go;
        }

        /// <summary>
        /// Omar's siren box wired to a tripwire post (pivot on the ground; box on a short spike, red horn facing +Z).
        /// </summary>
        public static GameObject BuildTripwireSiren()
        {
            var go = new GameObject("TripwireSiren");
            go.layer = Layers.Item;
            var mb = new MeshBuilder();
            mb.SetMaterial(Mat("tripwire_stake"));
            Rect steel = R(0, 0, 32, 24, 32, 32), wire = R(0, 24, 32, 8, 32, 32);
            Bar(mb, new Vector3(0, -0.08f, 0), new Vector3(0, 0.06f, 0), 0.012f, 0.012f, steel);
            mb.AddBox(new Vector3(0, 0.1f, 0), new Vector3(0.09f, 0.08f, 0.06f), BoxUVRects.All(steel));
            mb.AddBox(new Vector3(0, 0.145f, 0), new Vector3(0.05f, 0.012f, 0.03f), BoxUVRects.All(wire));   // battery clip
            Cyl(mb, new Vector3(0.03f, 0.12f, 0.03f), new Vector3(0.03f, 0.12f, 0.05f), 0.006f, 0.006f, 5, wire); // switch
            mb.Build("Mesh", go.transform, Layers.Item);
            var hb = new MeshBuilder();
            hb.SetMaterial(PsxMaterials.GetColor(new Color(0.5f, 0.07f, 0.05f)));
            Cyl(hb, new Vector3(-0.012f, 0.1f, 0.03f), new Vector3(-0.012f, 0.1f, 0.085f), 0.012f, 0.03f, 8, new Rect(0, 0, 1, 1), new Rect(0, 0, 1, 1), new Rect(0, 0, 1, 1));
            hb.Build("Horn", go.transform, Layers.Item);
            return go;
        }
    }
}
