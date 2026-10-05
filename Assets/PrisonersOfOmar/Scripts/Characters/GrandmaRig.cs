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
    /// (iteration 2) The grandmother in her wheelchair (see the "granny" references): a frail, stooped old woman with a
    /// white bob, sallow liver-spotted skin, sunken dark eyes and a faded floral house dress, white socks and shoes; a
    /// black wheelchair with big spoked wheels. Her body is a smooth skinned humanoid (the same generator as the
    /// prisoners, <see cref="BodySpec.Grandma"/>, Textures/Characters/grandma[_dead]) posed procedurally in the chair;
    /// the chair uses Textures/Items/grandma. Origin = floor under the wheelchair centre, facing +Z. No colliders.
    /// </summary>
    public sealed class GrandmaRig : MonoBehaviour
    {
        public GrandmaMode Mode { get; private set; }
        /// <summary>Head height in world space (eye line for line-of-sight tests).</summary>
        public Vector3 EyePosition => _body != null && _body.EyePoint != null ? _body.EyePoint.position : transform.position + transform.up * 1.15f + transform.forward * 0.1f;

        const string TexPath = "Textures/Items/grandma";
        const float WheelR = 0.3f;
        /// <summary>Where her hips sit in the chair (rig space).</summary>
        static readonly Vector3 SeatHips = new Vector3(0f, 0.6f, -0.1f);

        HumanoidRig _body;
        Transform _mouth, _wheelL, _wheelR, _castL, _castR;
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
        static readonly Rect Metal = R(0, 96, 32, 32), Tyre = R(32, 96, 32, 32), Vinyl = R(64, 96, 32, 32), Chrome = R(96, 96, 32, 32);

        void Build(int layer)
        {
            _seed = (_instances++ * 7.31f) % 50f + 3f;
            var mat = PsxMaterials.Get(TexPath, PsxSurface.Lit);
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

            // ---------------------------------------------------------------- the old woman (smooth skinned body)
            var spec = BodySpec.Grandma(false);
            _body = HumanoidFactory.BuildRig("GrandmaBody", spec, root, layer);
            // dark open mouth on the face, scaled open when she mumbles / screams
            var mouthAt = BodyMeshGenerator.MouthPoint(spec) - _body.BindPositions[(int)BoneId.Head];
            _mouth = GeoUtil.CreateChild(_body.Head, "Mouth", mouthAt + new Vector3(0f, 0f, 0.002f), Quaternion.identity, layer);
            var mm = new MeshBuilder();
            mm.SetMaterial(PsxMaterials.Get(null, PsxSurface.Unlit, new Color(0.05f, 0.02f, 0.02f)));
            mm.AddBox(new Vector3(0f, -0.012f, 0f), new Vector3(0.034f, 0.026f, 0.006f), BoxUV.Local, 1f);
            mm.Build("MouthOpen", _mouth, layer);
            _mouth.localScale = new Vector3(1f, 0.01f, 1f);

            SetTargets(GrandmaMode.WatchingTv, 0f, out _torsoE, out _headE, out _shLE, out _shRE, out _elLE, out _elRE, out _jawOpen);
            ApplyPose();
            _lap = LapDrape(spec, layer);
        }

        MeshRenderer _lap;

        /// <summary>
        /// The house dress over her lap: a rigid sheet built in the seated pose and parented to the hips (the legs never
        /// move relative to the hips, see ApplyPose), from the belly over both thighs, round the knees and down the shins
        /// to mid-calf. Skinning a standing skirt to the thighs cannot do this (it folds under them).
        /// </summary>
        MeshRenderer LapDrape(BodySpec b, int layer)
        {
            Transform hips = _body.Hips;
            Vector3 hipL = transform.InverseTransformPoint(_body.LeftUpperLeg.position), hipR = transform.InverseTransformPoint(_body.RightUpperLeg.position);
            Vector3 kneeL = transform.InverseTransformPoint(_body.LeftLowerLeg.position), kneeR = transform.InverseTransformPoint(_body.RightLowerLeg.position);
            Vector3 hip = (hipL + hipR) * 0.5f, knee = (kneeL + kneeR) * 0.5f;
            float s = b.Scale;
            float halfLegs = Mathf.Abs(hipR.x - hipL.x) * 0.5f;
            float wHip = halfLegs + b.LegRx[7] * 1.2f + 0.012f * s, wKnee = halfLegs + b.LegRx[4] * 1.7f + 0.02f * s;
            float topHip = b.LegRz[7] + 0.016f * s, topKnee = b.LegRz[4] + 0.018f * s;
            float hem = 0.20f * s;                                   // how far the hem hangs below the knee (mid-calf)
            // rows from the belly to the hem: centre, half width, height above the centre line, v in the torso strip
            Vector3 mid = Vector3.Lerp(hip, knee, 0.5f);
            var rows = new[]
            {
                (c: hip + new Vector3(0f, topHip + 0.03f * s, 0.035f * s), w: wHip * 0.92f, v: 0.38f, top: true),
                (c: hip + new Vector3(0f, topHip, 0.10f * s), w: wHip, v: 0.32f, top: true),
                (c: mid + new Vector3(0f, (topHip + topKnee) * 0.5f, 0f), w: (wHip + wKnee) * 0.5f, v: 0.25f, top: true),
                (c: knee + new Vector3(0f, topKnee, 0.0f), w: wKnee, v: 0.17f, top: true),
                (c: knee + new Vector3(0f, topKnee * 0.45f, b.LegRz[4] + 0.035f * s), w: wKnee * 1.03f, v: 0.11f, top: false),
                (c: knee + new Vector3(0f, -hem, b.LegRz[3] + 0.05f * s), w: wKnee * 1.1f, v: 0.0f, top: false),
            };
            const int M = 8;                                          // columns across (+ a side flap each side)
            int cols = M + 3;
            var P = new Vector3[rows.Length, cols];
            var UV = new Vector2[rows.Length, cols];
            for (int r = 0; r < rows.Length; r++)
            {
                var row = rows[r];
                for (int c = 0; c < cols; c++)
                {
                    int cc = Mathf.Clamp(c - 1, 0, M);
                    float u = cc / (float)M * 2f - 1f;                // -1 .. 1 across
                    float x = u * row.w;
                    Vector3 p = row.c + new Vector3(x, 0f, 0f);
                    // sag between the knees, rounded over each thigh
                    float between = Mathf.Clamp01(1f - Mathf.Abs(x) / Mathf.Max(0.01f, halfLegs));
                    if (row.top) p.y -= 0.02f * s * between * between + 0.03f * s * Mathf.Pow(Mathf.Abs(u), 4f);
                    else p.z -= 0.02f * s * between * between;
                    if (c == 0 || c == cols - 1)
                    {
                        // side flaps hang down over the outside of the thighs / shins
                        p.x += Mathf.Sign(u) * 0.012f * s;
                        if (row.top) p.y -= 0.09f * s; else p.x += Mathf.Sign(u) * 0.01f * s;
                    }
                    P[r, c] = p;
                    UV[r, c] = CharacterAtlas.Torso.UV(0.5f - 0.2f * (x / wKnee) - (c == 0 ? -0.04f : c == cols - 1 ? 0.04f : 0f), row.v);
                }
            }
            var mb = new MeshBuilder();
            mb.SetMaterial(HumanoidFactory.MaterialsFor(b.Texture, 1)[0]);
            var toHips = hips.worldToLocalMatrix * transform.localToWorldMatrix;
            var idx = new int[rows.Length, cols, 2];
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < cols; c++)
                {
                    Vector3 dr = P[Mathf.Min(r + 1, rows.Length - 1), c] - P[Mathf.Max(r - 1, 0), c];
                    Vector3 dc = P[r, Mathf.Min(c + 1, cols - 1)] - P[r, Mathf.Max(c - 1, 0)];
                    Vector3 n = Vector3.Cross(dr, dc).normalized;
                    if (Vector3.Dot(n, new Vector3(0f, 1f, 0.6f)) < 0f) n = -n;
                    Vector3 hp = toHips.MultiplyPoint3x4(P[r, c]);
                    Vector3 hn = toHips.MultiplyVector(n).normalized;
                    idx[r, c, 0] = mb.AddVertex(hp, hn, UV[r, c]);
                    idx[r, c, 1] = mb.AddVertex(hp, -hn, UV[r, c]);
                }
            for (int r = 0; r < rows.Length - 1; r++)
                for (int c = 0; c < cols - 1; c++)
                {
                    int a = idx[r, c, 0], bb = idx[r, c + 1, 0], cq = idx[r + 1, c + 1, 0], d = idx[r + 1, c, 0];
                    // outside (normal side): clockwise seen from above / front
                    mb.AddTriangle(a, d, cq); mb.AddTriangle(a, cq, bb);
                    a = idx[r, c, 1]; bb = idx[r, c + 1, 1]; cq = idx[r + 1, c + 1, 1]; d = idx[r + 1, c, 1];
                    mb.AddTriangle(a, cq, d); mb.AddTriangle(a, bb, cq);
                }
            var go = mb.Build("LapDrape", hips, layer);
            return go.GetComponent<MeshRenderer>();
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
            if (mode == GrandmaMode.Dead && !_deadSkin && _body != null && _body.BodyRenderer != null)
            {
                _deadSkin = true;
                var smr = _body.BodyRenderer;
                smr.sharedMaterials = HumanoidFactory.MaterialsFor(BodySpec.Grandma(true).Texture, smr.sharedMesh != null ? smr.sharedMesh.subMeshCount : 1);
                if (_lap != null) _lap.sharedMaterials = HumanoidFactory.MaterialsFor(BodySpec.Grandma(true).Texture, 1);
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
            if (_lookTarget.HasValue && Mode != GrandmaMode.Dead && _body != null)
            {
                Vector3 local = transform.InverseTransformPoint(_lookTarget.Value) - new Vector3(0f, 1.15f, 0f);
                wantYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -30f, 35f);
            }
            _headYaw = Mathf.Lerp(_headYaw, wantYaw, 1f - Mathf.Exp(-dt * 4f));
            _headPitch = Mathf.Lerp(_headPitch, wantPitch, 1f - Mathf.Exp(-dt * 4f));
            ApplyPose();
        }

        void ApplyPose()
        {
            if (_body == null) return;
            // seated: pelvis on the seat, thighs forward, shins down to the footplates (legs never move)
            _body.Hips.localPosition = SeatHips;
            _body.Hips.localRotation = Quaternion.Euler(_torsoE.x * 0.15f, 0f, 0f);
            _body.LeftUpperLeg.localRotation = Quaternion.Euler(-82f, -4f, -3f);
            _body.RightUpperLeg.localRotation = Quaternion.Euler(-82f, 4f, 3f);
            _body.LeftLowerLeg.localRotation = Quaternion.Euler(77f, 0f, 0f);
            _body.RightLowerLeg.localRotation = Quaternion.Euler(77f, 0f, 0f);
            _body.LeftFoot.localRotation = Quaternion.Euler(4f, -6f, 0f);
            _body.RightFoot.localRotation = Quaternion.Euler(4f, 6f, 0f);
            // stooped back
            _body.Spine.localRotation = Quaternion.Euler(_torsoE.x * 0.45f, _torsoE.y * 0.5f, _torsoE.z * 0.5f);
            _body.Chest.localRotation = Quaternion.Euler(_torsoE.x * 0.4f + 2f, _torsoE.y * 0.5f, _torsoE.z * 0.5f);
            _body.Neck.localRotation = Quaternion.Euler((_headE.x + _headPitch * 0.8f) * 0.4f, (_headE.y + _headYaw) * 0.4f, _headE.z * 0.4f);
            _body.Head.localRotation = Quaternion.Euler((_headE.x + _headPitch * 0.8f) * 0.6f, (_headE.y + _headYaw) * 0.6f, _headE.z * 0.6f);
            _body.LeftUpperArm.localRotation = Quaternion.Euler(_shLE);
            _body.RightUpperArm.localRotation = Quaternion.Euler(_shRE);
            _body.LeftLowerArm.localRotation = Quaternion.Euler(_elLE);
            _body.RightLowerArm.localRotation = Quaternion.Euler(_elRE);
            _body.LeftHand.localRotation = Quaternion.Euler(12f, 0f, 0f);
            _body.RightHand.localRotation = Quaternion.Euler(12f, 0f, 0f);
            if (_mouth != null) _mouth.localScale = new Vector3(1f, Mathf.Max(0.01f, _jawOpen), 1f);
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
                    head = new Vector3(3f + nod, Noise(t * 0.2f, 3f) * 6f,   // looks at the TV (her face and fringe are visible)
                         6f + Noise(t * 0.3f, 4f) * 4f);
                    // elbows on the armrests, forearms lying forward along them
                    shL = new Vector3(-10f, 0f, -17f); shR = new Vector3(-10f + twitch * 0.2f, 0f, 17f);
                    elL = new Vector3(-72f, 8f, 0f); elR = new Vector3(-72f - twitch * 0.5f, -8f, twitch * 0.3f);
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
                    head = new Vector3(2f, Noise(t * 0.5f, 3f) * 10f, 0f);
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
                    // the back is folded ~55 deg forward: undo it so the arms hang straight down past her knees
                    shL = new Vector3(-48f, 0f, -7f); shR = new Vector3(-53f, 0f, 10f);
                    elL = new Vector3(-8f, 0f, 0f); elR = new Vector3(-14f, 0f, 0f);
                    jaw = 0.5f;
                    break;
            }
        }
    }
}
