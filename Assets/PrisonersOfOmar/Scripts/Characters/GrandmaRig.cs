using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>(iteration 2) What the grandmother is doing (drives her animation).</summary>
    public enum GrandmaMode : byte
    {
        WatchingTv = 0, // slumped in the wheelchair, head bobbing, facing the TV
        Roaming,        // pushing the wheels, rolling through the house
        Screaming,      // spotted a prisoner: leans forward, mouth open, arms up, shaking
        Dead,           // shot: slumped forward over her knees, blood
    }

    /// <summary>
    /// (iteration 2) The grandmother in her wheelchair (see the "granny" references): white bob falling over one eye,
    /// sallow skin, sunken eyes, a faded floral house dress, white socks; a black wheelchair with big wheels.
    /// Low-poly boxes on one 128x128 atlas (Textures/Items/grandma, grandma_dead) + procedural animation.
    /// Origin = floor under the wheelchair centre, facing +Z. No colliders (gameplay adds them).
    /// </summary>
    public sealed class GrandmaRig : MonoBehaviour
    {
        public GrandmaMode Mode { get; private set; }
        /// <summary>Head height in world space (eye line for line-of-sight tests).</summary>
        public Vector3 EyePosition => _head != null ? _head.TransformPoint(new Vector3(0f, 0.1f, 0.09f)) : transform.position + transform.up * 1.15f + transform.forward * 0.1f;

        const string TexPath = "Textures/Items/grandma", DeadTexPath = "Textures/Items/grandma_dead";
        const float WheelR = 0.3f;

        Transform _torso, _head, _jaw, _shL, _shR, _elL, _elR, _wheelL, _wheelR, _castL, _castR;
        readonly System.Collections.Generic.List<Renderer> _bodyRenderers = new System.Collections.Generic.List<Renderer>();
        float _speed, _wheelAngle, _castAngle, _time, _seed;
        Vector3? _lookTarget;
        float _headYaw, _headPitch;
        // current animated angles (degrees), blended towards the mode targets
        Vector3 _torsoE, _headE, _shLE, _shRE, _elLE, _elRE;
        float _jawOpen;
        bool _deadSkin;
        static int _instances;

        public static GrandmaRig Create(Transform parent, int layer = Layers.Corpse)
        {
            var go = new GameObject("Grandma");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var rig = go.AddComponent<GrandmaRig>();
            rig.Build(layer);
            return rig;
        }

        // ============================================================================================ build
        static Rect R(float x, float y, float w, float h) => new Rect((x + 0.5f) / 128f, 1f - (y + h - 0.5f) / 128f, (w - 1f) / 128f, (h - 1f) / 128f);
        static readonly Rect Dress = R(0, 0, 64, 64), Hem = R(0, 64, 64, 32), Face = R(64, 0, 32, 32), Skin = R(96, 0, 32, 32),
            Hair = R(64, 32, 32, 32), HairBack = R(64, 64, 32, 32), Socks = R(96, 32, 32, 16), Shoes = R(96, 48, 32, 16),
            Mouth = R(96, 64, 32, 32), Metal = R(0, 96, 32, 32), Tyre = R(32, 96, 32, 32), Vinyl = R(64, 96, 32, 32), Chrome = R(96, 96, 32, 32);

        Material Mat(bool dead) => PsxMaterials.Get(dead ? DeadTexPath : TexPath, PsxSurface.Lit);

        void Build(int layer)
        {
            _seed = (_instances++ * 7.31f) % 50f + 3f;
            var mat = Mat(false);
            Transform root = transform;

            // ---------------------------------------------------------------- wheelchair (static part)
            var chair = new MeshBuilder();
            chair.SetMaterial(mat);
            Box(chair, new Vector3(0f, 0.48f, 0f), new Vector3(0.46f, 0.04f, 0.44f), Vinyl);                    // seat
            chair.Push(new Vector3(0f, 0.72f, -0.22f), Quaternion.Euler(-8f, 0f, 0f));
            Box(chair, Vector3.zero, new Vector3(0.44f, 0.46f, 0.03f), Vinyl);                                  // back
            chair.Pop();
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float x = sx * 0.235f;
                Bar(chair, new Vector3(x, 0.46f, -0.22f), new Vector3(x, 0.98f, -0.27f), 0.022f, Metal);          // back post + handle
                Bar(chair, new Vector3(x, 0.98f, -0.27f), new Vector3(x, 0.97f, -0.38f), 0.022f, Vinyl);
                Bar(chair, new Vector3(x, 0.46f, -0.22f), new Vector3(x, 0.46f, 0.22f), 0.02f, Metal);            // seat rail
                Bar(chair, new Vector3(x, 0.46f, 0.2f), new Vector3(x, 0.1f, 0.3f), 0.02f, Metal);                // front leg
                Bar(chair, new Vector3(x, 0.7f, -0.2f), new Vector3(x, 0.7f, 0.15f), 0.03f, Vinyl);               // armrest
                Bar(chair, new Vector3(x, 0.7f, 0.12f), new Vector3(x, 0.47f, 0.12f), 0.018f, Metal);
                Bar(chair, new Vector3(x, 0.46f, 0.18f), new Vector3(sx * 0.12f, 0.12f, 0.36f), 0.018f, Metal);  // footrest hanger
                Box(chair, new Vector3(sx * 0.1f, 0.1f, 0.38f), new Vector3(0.14f, 0.015f, 0.12f), Metal);       // footplate
                Bar(chair, new Vector3(x, 0.3f, -0.05f), new Vector3(sx * 0.2f, 0.07f, 0.3f), 0.016f, Metal);    // lower brace
            }
            Bar(chair, new Vector3(-0.235f, 0.44f, -0.05f), new Vector3(0.235f, 0.44f, -0.05f), 0.016f, Metal);
            chair.Build("Chair", root, layer);

            _wheelL = Wheel(root, layer, mat, -0.29f, WheelR, new Vector3(0f, WheelR, -0.05f), 0.035f, true);
            _wheelR = Wheel(root, layer, mat, 0.29f, WheelR, new Vector3(0f, WheelR, -0.05f), 0.035f, true);
            _castL = Wheel(root, layer, mat, -0.2f, 0.07f, new Vector3(0f, 0.07f, 0.31f), 0.03f, false);
            _castR = Wheel(root, layer, mat, 0.2f, 0.07f, new Vector3(0f, 0.07f, 0.31f), 0.03f, false);

            // ---------------------------------------------------------------- legs (on the footrests, never move)
            var legs = new MeshBuilder();
            legs.SetMaterial(mat);
            Box(legs, new Vector3(0f, 0.57f, 0.1f), new Vector3(0.36f, 0.14f, 0.46f), Dress);                  // lap
            Box(legs, new Vector3(0f, 0.42f, 0.33f), new Vector3(0.36f, 0.26f, 0.06f), Hem);                    // skirt over the knees
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(legs, new Vector3(sx * 0.08f, 0.22f, 0.35f), new Vector3(0.065f, 0.22f, 0.065f), Skin);     // shin
                Box(legs, new Vector3(sx * 0.08f, 0.15f, 0.355f), new Vector3(0.072f, 0.09f, 0.072f), Socks);   // sock
                Box(legs, new Vector3(sx * 0.08f, 0.135f, 0.4f), new Vector3(0.08f, 0.05f, 0.15f), Shoes);      // shoe
            }
            _bodyRenderers.Add(legs.Build("Legs", root, layer).GetComponent<Renderer>());

            // ---------------------------------------------------------------- torso + head + arms (animated)
            _torso = GeoUtil.CreateChild(root, "Torso", new Vector3(0f, 0.52f, -0.1f), Quaternion.identity, layer);
            var tmb = new MeshBuilder();
            tmb.SetMaterial(mat);
            Box(tmb, new Vector3(0f, 0.12f, 0f), new Vector3(0.3f, 0.24f, 0.2f), Dress);                         // belly
            Box(tmb, new Vector3(0f, 0.36f, 0.0f), new Vector3(0.31f, 0.24f, 0.19f), Dress);                     // chest
            Box(tmb, new Vector3(0f, 0.36f, 0.075f), new Vector3(0.22f, 0.2f, 0.06f), Dress);                    // bosom
            Box(tmb, new Vector3(0f, 0.5f, 0.0f), new Vector3(0.18f, 0.05f, 0.14f), Dress);                      // collar
            Box(tmb, new Vector3(0f, 0.55f, 0.0f), new Vector3(0.07f, 0.07f, 0.07f), Skin);                      // neck
            _bodyRenderers.Add(tmb.Build("Body", _torso, layer).GetComponent<Renderer>());

            _head = GeoUtil.CreateChild(_torso, "Head", new Vector3(0f, 0.58f, 0.015f), Quaternion.identity, layer);
            var hmb = new MeshBuilder();
            hmb.SetMaterial(mat);
            hmb.AddBox(new Vector3(0f, 0.1f, 0f), new Vector3(0.16f, 0.2f, 0.18f),
                new BoxUVRects { PosZ = Face, NegZ = HairBack, PosX = Skin, NegX = Skin, PosY = Hair, NegY = Skin });
            // white bob: cap + sides down to the jaw + back, a lock falling over one eye
            Box(hmb, new Vector3(0f, 0.2f, -0.005f), new Vector3(0.19f, 0.06f, 0.2f), Hair);
            Box(hmb, new Vector3(-0.088f, 0.1f, -0.01f), new Vector3(0.03f, 0.19f, 0.19f), Hair);
            Box(hmb, new Vector3(0.088f, 0.1f, -0.01f), new Vector3(0.03f, 0.19f, 0.19f), Hair);
            Box(hmb, new Vector3(0f, 0.1f, -0.095f), new Vector3(0.19f, 0.2f, 0.03f), HairBack);
            hmb.Push(new Vector3(-0.035f, 0.15f, 0.093f), Quaternion.Euler(0f, 0f, -12f));
            Box(hmb, Vector3.zero, new Vector3(0.075f, 0.09f, 0.012f), Hair);
            hmb.Pop();
            _bodyRenderers.Add(hmb.Build("HeadMesh", _head, layer).GetComponent<Renderer>());
            _jaw = GeoUtil.CreateChild(_head, "Jaw", new Vector3(0f, 0.045f, 0.091f), Quaternion.identity, layer);
            var jmb = new MeshBuilder();
            jmb.SetMaterial(mat);
            Box(jmb, new Vector3(0f, -0.015f, 0f), new Vector3(0.05f, 0.04f, 0.006f), Mouth);
            _bodyRenderers.Add(jmb.Build("MouthOpen", _jaw, layer).GetComponent<Renderer>());
            _jaw.localScale = new Vector3(1f, 0.01f, 1f);

            _shL = Arm(-1f, layer, mat, out _elL);
            _shR = Arm(1f, layer, mat, out _elR);

            SetTargets(GrandmaMode.WatchingTv, 0f, out _torsoE, out _headE, out _shLE, out _shRE, out _elLE, out _elRE, out _jawOpen);
            ApplyPose();
        }

        Transform Arm(float side, int layer, Material mat, out Transform elbow)
        {
            var sh = GeoUtil.CreateChild(_torso, side < 0 ? "ShoulderL" : "ShoulderR", new Vector3(side * 0.175f, 0.45f, 0f), Quaternion.identity, layer);
            var amb = new MeshBuilder();
            amb.SetMaterial(mat);
            Box(amb, new Vector3(0f, -0.06f, 0f), new Vector3(0.07f, 0.12f, 0.07f), Dress);                       // short sleeve
            Box(amb, new Vector3(0f, -0.18f, 0f), new Vector3(0.05f, 0.14f, 0.05f), Skin);                        // thin upper arm
            _bodyRenderers.Add(amb.Build("UpperArm", sh, layer).GetComponent<Renderer>());
            elbow = GeoUtil.CreateChild(sh, "Elbow", new Vector3(0f, -0.26f, 0f), Quaternion.identity, layer);
            var fmb = new MeshBuilder();
            fmb.SetMaterial(mat);
            Box(fmb, new Vector3(0f, -0.12f, 0f), new Vector3(0.042f, 0.24f, 0.042f), Skin);                      // forearm
            Box(fmb, new Vector3(0f, -0.28f, 0.005f), new Vector3(0.045f, 0.09f, 0.025f), Skin);                  // bony hand
            Box(fmb, new Vector3(0f, -0.335f, 0.008f), new Vector3(0.04f, 0.04f, 0.02f), Skin);                   // fingers
            _bodyRenderers.Add(fmb.Build("Forearm", elbow, layer).GetComponent<Renderer>());
            return sh;
        }

        Transform Wheel(Transform root, int layer, Material mat, float x, float radius, Vector3 centre, float width, bool spokes)
        {
            var t = GeoUtil.CreateChild(root, x < 0 ? "WheelL" : "WheelR", new Vector3(x, centre.y, centre.z), Quaternion.identity, layer);
            var mb = new MeshBuilder();
            mb.SetMaterial(mat);
            // tyre as a ring of boxes (reads as a round wheel at PS1 resolution)
            int n = spokes ? 14 : 8;
            float seg = 2f * Mathf.PI * radius / n * 1.08f;
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                mb.Push(Vector3.zero, Quaternion.Euler(a, 0f, 0f));
                Box(mb, new Vector3(0f, radius - 0.018f, 0f), new Vector3(width, 0.036f, seg), Tyre);
                mb.Pop();
            }
            if (spokes)
            {
                for (int i = 0; i < 6; i++)
                {
                    mb.Push(Vector3.zero, Quaternion.Euler(i * 30f, 0f, 0f));
                    Box(mb, Vector3.zero, new Vector3(0.006f, radius * 2f - 0.06f, 0.008f), Chrome);
                    mb.Pop();
                }
                for (int i = 0; i < n; i++)
                {
                    float a = i * 360f / n;
                    mb.Push(new Vector3(-Mathf.Sign(x) * 0.03f, 0f, 0f), Quaternion.Euler(a, 0f, 0f));
                    Box(mb, new Vector3(0f, radius - 0.04f, 0f), new Vector3(0.008f, 0.01f, seg), Chrome);  // push rim
                    mb.Pop();
                }
                Box(mb, Vector3.zero, new Vector3(width + 0.02f, 0.05f, 0.05f), Metal);
            }
            mb.Build("Wheel", t, layer);
            return t;
        }

        static void Box(MeshBuilder mb, Vector3 c, Vector3 size, Rect uv) => mb.AddBox(c, size, BoxUVRects.All(uv));

        static void Bar(MeshBuilder mb, Vector3 a, Vector3 b, float w, Rect uv)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            mb.Push((a + b) * 0.5f, Quaternion.LookRotation(d / len, Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up));
            mb.AddBox(Vector3.zero, new Vector3(w, w, len), BoxUVRects.All(uv));
            mb.Pop();
        }

        // ============================================================================================ API
        public void SetMode(GrandmaMode mode)
        {
            Mode = mode;
            if (mode == GrandmaMode.Dead && !_deadSkin)
            {
                _deadSkin = true;
                var m = Mat(true);
                foreach (var r in _bodyRenderers) if (r != null) r.sharedMaterial = m;
            }
        }

        /// <summary>Rolling speed in m/s (wheel spin + hand pushing animation).</summary>
        public void SetMoveSpeed(float metersPerSecond) => _speed = metersPerSecond;

        /// <summary>Turn the head towards a point (null = look ahead / at the TV).</summary>
        public void LookAt(Vector3? target) => _lookTarget = target;

        // ============================================================================================ animation
        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f) return;
            _time += dt;

            float spin = _speed / WheelR * Mathf.Rad2Deg * dt;
            _wheelAngle += spin;
            _castAngle += spin * (WheelR / 0.07f);
            if (_wheelL != null) { _wheelL.localRotation = Quaternion.Euler(_wheelAngle, 0f, 0f); _wheelR.localRotation = _wheelL.localRotation; }
            if (_castL != null) { _castL.localRotation = Quaternion.Euler(_castAngle, 0f, 0f); _castR.localRotation = _castL.localRotation; }

            SetTargets(Mode, _time, out var tT, out var tH, out var tSL, out var tSR, out var tEL, out var tER, out var tJ);
            float k = 1f - Mathf.Exp(-dt * (Mode == GrandmaMode.Screaming ? 14f : 5f));
            _torsoE = Vector3.Lerp(_torsoE, tT, k);
            _headE = Vector3.Lerp(_headE, tH, k);
            _shLE = Vector3.Lerp(_shLE, tSL, k); _shRE = Vector3.Lerp(_shRE, tSR, k);
            _elLE = Vector3.Lerp(_elLE, tEL, k); _elRE = Vector3.Lerp(_elRE, tER, k);
            _jawOpen = Mathf.Lerp(_jawOpen, tJ, 1f - Mathf.Exp(-dt * 12f));

            // head turns towards what she looks at (within what an old neck allows)
            float wantYaw = 0f, wantPitch = 0f;
            if (_lookTarget.HasValue && Mode != GrandmaMode.Dead && _torso != null)
            {
                Vector3 local = _torso.InverseTransformPoint(_lookTarget.Value) - new Vector3(0f, 0.68f, 0f);
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
            }
            _headYaw = Mathf.Lerp(_headYaw, wantYaw, 1f - Mathf.Exp(-dt * 4f));
            _headPitch = Mathf.Lerp(_headPitch, wantPitch, 1f - Mathf.Exp(-dt * 4f));
            ApplyPose();
        }

        void ApplyPose()
        {
            if (_torso == null) return;
            _torso.localRotation = Quaternion.Euler(_torsoE);
            _head.localRotation = Quaternion.Euler(_headE.x + _headPitch * 0.8f, _headE.y + _headYaw, _headE.z);
            _shL.localRotation = Quaternion.Euler(_shLE); _shR.localRotation = Quaternion.Euler(_shRE);
            _elL.localRotation = Quaternion.Euler(_elLE); _elR.localRotation = Quaternion.Euler(_elRE);
            _jaw.localScale = new Vector3(1f, Mathf.Max(0.01f, _jawOpen), 1f);
        }

        float Noise(float t, float salt) => Mathf.PerlinNoise(_seed + salt, t) * 2f - 1f;

        /// <summary>Target angles of every joint for a mode at time t (arms hang along -Y from the shoulder).</summary>
        void SetTargets(GrandmaMode m, float t, out Vector3 torso, out Vector3 head, out Vector3 shL, out Vector3 shR, out Vector3 elL, out Vector3 elR, out float jaw)
        {
            jaw = 0f;
            switch (m)
            {
                default:
                case GrandmaMode.WatchingTv:
                {
                    // slumped, hands on the armrests, a slow nod, one hand twitching now and then
                    float nod = Mathf.Sin(t * 0.9f) * 3f + Noise(t * 0.4f, 1f) * 4f;
                    float twitch = Mathf.Max(0f, Noise(t * 3.5f, 7f) - 0.55f) * 60f;
                    torso = new Vector3(9f + Mathf.Sin(t * 0.6f) * 1.2f, 0f, 2f);
                    head = new Vector3(14f + nod, Noise(t * 0.2f, 3f) * 6f, 6f + Noise(t * 0.3f, 4f) * 4f);
                    // elbows on the armrests, forearms lying forward along them
                    shL = new Vector3(-6f, 0f, -9f); shR = new Vector3(-6f + twitch * 0.2f, 0f, 9f);
                    elL = new Vector3(-84f, 0f, 0f); elR = new Vector3(-84f - twitch * 0.5f, 0f, twitch * 0.3f);
                    jaw = Mathf.Max(0f, Noise(t * 1.3f, 9f) - 0.4f) * 0.8f; // mumbling
                    break;
                }
                case GrandmaMode.Roaming:
                {
                    // hands grip the push rims and shove them round
                    float cyc = Mathf.Repeat(t * Mathf.Max(0.6f, _speed * 1.1f), 1f);
                    float push = cyc < 0.55f ? cyc / 0.55f : 1f - (cyc - 0.55f) / 0.45f;
                    float moving = Mathf.Clamp01(_speed / 0.4f);
                    torso = new Vector3(12f + 8f * push * moving, 0f, 0f);
                    head = new Vector3(8f, Noise(t * 0.5f, 3f) * 10f, 0f);
                    float sx = Mathf.Lerp(-30f, -5f + -45f * push, moving), ex = Mathf.Lerp(-55f, -20f - 40f * (1f - push), moving);
                    shL = new Vector3(sx, 0f, -16f); shR = new Vector3(sx, 0f, 16f);
                    elL = new Vector3(ex, 0f, 0f); elR = new Vector3(ex, 0f, 0f);
                    break;
                }
                case GrandmaMode.Screaming:
                {
                    // leans out of the chair, arms up and shaking, head jerking, mouth wide open
                    float sh = Noise(t * 22f, 11f), sh2 = Noise(t * 19f, 12f);
                    torso = new Vector3(20f + sh * 3f, sh2 * 4f, sh * 3f);
                    head = new Vector3(-18f + sh2 * 10f, sh * 14f, sh2 * 10f);
                    shL = new Vector3(-140f + sh * 12f, 0f, -24f + sh2 * 8f); shR = new Vector3(-140f + sh2 * 12f, 0f, 24f + sh * 8f);
                    elL = new Vector3(-28f + sh2 * 15f, 0f, 0f); elR = new Vector3(-28f + sh * 15f, 0f, 0f);
                    jaw = 0.85f + 0.15f * sh;
                    break;
                }
                case GrandmaMode.Dead:
                    torso = new Vector3(52f, 0f, 6f);
                    head = new Vector3(42f, 10f, 18f);
                    shL = new Vector3(38f, 0f, -6f); shR = new Vector3(32f, 0f, 9f); // hanging straight down
                    elL = new Vector3(-6f, 0f, 0f); elR = new Vector3(-12f, 0f, 0f);
                    jaw = 0.5f;
                    break;
            }
        }
    }
}
