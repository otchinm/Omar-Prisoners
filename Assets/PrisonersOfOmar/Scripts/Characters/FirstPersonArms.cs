using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// First person view model: forearm(s) + hand(s) in the skin's sleeves, holding the equipped item.
    /// Lives under the camera on Layers.ViewModel. Gameplay feeds movement state for bob/sway and calls Play() for actions.
    /// Omar always holds his cleaver (child "Cleaver" of the hand socket, independent of <see cref="SetHeld"/>).
    /// Camera space: +Z forward, +X right. Tuned for the view model camera FOV ~60 (works from 4:3 to 21:9).
    /// </summary>
    public sealed class FirstPersonArms : MonoBehaviour
    {
        /// <summary>Item model attach point in the right hand (item pivot = grip).</summary>
        public Transform HandSocket { get; private set; }
        public CharacterSkin Skin { get; private set; }

        /// <summary>Horizontal movement speed (m/s) for bobbing.</summary>
        public float MoveSpeed;
        public bool Sprinting;
        public bool Crouching;
        /// <summary>Mouse delta this frame (for sway).</summary>
        public Vector2 LookDelta;

        public ItemType HeldItem { get; private set; }
        /// <summary>The instantiated model of the held item (null when empty-handed).</summary>
        public GameObject HeldModel { get; private set; }

        /// <summary>The action currently playing (None when idle).</summary>
        public CharacterAction CurrentAction { get; private set; }

        Transform _rHand, _lHand, _rArm, _lArm, _lSocket;
        GameObject _cleaver;
        float _time, _bobPhase, _bobAmp, _raise = 1f, _raiseTarget = 1f, _actTime, _sprintW, _crouchW, _leftW;
        Vector2 _sway, _swayVel;
        Vector3 _lastDp;
        Transform _gripL;

        // rest pose of the right hand (camera space)
        static readonly Vector3 RestPos = new Vector3(0.17f, -0.19f, 0.35f);
        static readonly Vector3 RestEuler = new Vector3(-4f, -8f, 4f);
        static readonly Vector3 ElbowR = new Vector3(0.30f, -0.50f, 0.12f);
        static readonly Vector3 ElbowL = new Vector3(-0.30f, -0.50f, 0.12f);

        public static FirstPersonArms Create(CharacterSkin skin, Transform cameraTransform)
        {
            var go = new GameObject("FirstPersonArms");
            go.layer = Layers.ViewModel;
            go.transform.SetParent(cameraTransform, false);
            var arms = go.AddComponent<FirstPersonArms>();
            arms.Skin = skin;
            arms.BuildRig();
            return arms;
        }

        void BuildRig()
        {
            string tex = CharacterAtlas.TexturePath(Skin);
            var mat = PsxMaterials.Get(tex, PsxSurface.Lit);
            bool omar = Skin == CharacterSkin.Omar;
            var spec = BodySpec.For(Skin);
            float hs = Mathf.Clamp(spec.HandScale, 0.85f, 1.25f);
            float armR = Mathf.Clamp(spec.ArmR[1] * 1.05f, 0.026f, 0.05f);

            _rHand = GeoUtil.CreateChild(transform, "RightHand", RestPos, Quaternion.Euler(RestEuler), Layers.ViewModel);
            BuildFist(_rHand, mat, hs, 1f);
            HandSocket = GeoUtil.CreateChild(_rHand, "HandSocket", Vector3.zero, Quaternion.identity, Layers.ViewModel);
            _rArm = GeoUtil.CreateChild(transform, "RightForearm", Vector3.zero, Quaternion.identity, Layers.ViewModel);
            BuildForearm(_rArm, mat, armR, 1f, omar);

            _lHand = GeoUtil.CreateChild(transform, "LeftHand", new Vector3(-RestPos.x, RestPos.y, RestPos.z), Quaternion.identity, Layers.ViewModel);
            BuildFist(_lHand, mat, hs, -1f);
            _lSocket = GeoUtil.CreateChild(_lHand, "LeftHandSocket", Vector3.zero, Quaternion.identity, Layers.ViewModel);
            _lArm = GeoUtil.CreateChild(transform, "LeftForearm", Vector3.zero, Quaternion.identity, Layers.ViewModel);
            BuildForearm(_lArm, mat, armR, -1f, omar);
            SetLeftVisible(false);

            // empty-handed prisoners start with the hand lowered out of view
            _raise = _raiseTarget = omar ? 1f : 0f;
            if (omar)
            {
                _cleaver = ItemMeshFactory.BuildCleaver();
                _cleaver.name = "Cleaver";
                _cleaver.transform.SetParent(HandSocket, false);
                GeoUtil.SetLayerRecursive(_cleaver, Layers.ViewModel);
            }
            UpdatePose(0f);
        }

        /// <summary>Fist around the grip axis (camera / item +Z) with the socket at the origin; side = +1 right, -1 left.</summary>
        static void BuildFist(Transform hand, Material mat, float hs, float side)
        {
            var mb = new MeshBuilder();
            mb.SetMaterial(mat);
            var reg = CharacterAtlas.Hand;
            var sk = CharacterAtlas.SwatchSkin;
            Rect back = RectOf(reg, 0f, 0.5f, 1f, 1f), fingers = RectOf(reg, 0f, 0f, 1f, 0.5f), skin = RectOf(sk, 0.2f, 0.2f, 0.8f, 0.8f);
            float s = hs;
            // back of the hand / palm block: knuckles face up-right, wraps the grip on the right side
            mb.Push(new Vector3(side * 0.021f * s, -0.004f * s, -0.004f * s), Quaternion.Euler(0, 0, side * -18f));
            mb.AddBox(Vector3.zero, new Vector3(0.034f, 0.064f, 0.084f) * s,
                new BoxUVRects { PosX = side > 0 ? back : skin, NegX = side > 0 ? skin : back, PosY = skin, NegY = skin, PosZ = skin, NegZ = skin });
            mb.Pop();
            // curled fingers over the top of the grip and down the far side
            mb.Push(new Vector3(side * -0.004f * s, 0.022f * s, 0f), Quaternion.Euler(0, 0, side * 25f));
            mb.AddBox(Vector3.zero, new Vector3(0.042f, 0.022f, 0.080f) * s,
                new BoxUVRects { PosY = fingers, NegY = skin, PosX = skin, NegX = skin, PosZ = fingers, NegZ = skin });
            mb.Pop();
            mb.Push(new Vector3(side * -0.021f * s, 0.002f * s, 0.002f * s), Quaternion.Euler(0, 0, side * 70f));
            mb.AddBox(Vector3.zero, new Vector3(0.036f, 0.018f, 0.076f) * s, BoxUVRects.All(fingers));
            mb.Pop();
            // thumb along the left side pointing forward
            mb.Push(new Vector3(side * -0.012f * s, -0.016f * s, 0.03f * s), Quaternion.Euler(-10f, side * -12f, 0));
            mb.AddBox(Vector3.zero, new Vector3(0.02f, 0.02f, 0.05f) * s, BoxUVRects.All(skin));
            mb.Pop();
            mb.Build("Fist", hand, Layers.ViewModel);
        }

        /// <summary>Forearm tube along +Z (wrist at 0, elbow at 0.34) using the atlas arm strip (wrist .. elbow).</summary>
        static void BuildForearm(Transform arm, Material mat, float r, float side, bool omar)
        {
            var mb = new MeshBuilder();
            mb.SetMaterial(mat);
            var reg = CharacterAtlas.Arm;
            const int N = 8;
            float[] zs = { 0f, 0.06f, 0.2f, 0.34f };
            float[] rs = { r * 0.82f, r * 0.95f, r * 1.12f, r * 1.25f };
            float[] vs = { 0.0f, 0.12f, 0.32f, 0.5f };
            int first = mb.VertexCount;
            for (int k = 0; k < zs.Length; k++)
            {
                for (int i = 0; i <= N; i++)
                {
                    // start at the bottom so u = 0.5 (front of the arm strip) lands on the top of the forearm
                    float a = ((float)i / N + 0.5f) * Mathf.PI * 2f;
                    Vector3 d = new Vector3(Mathf.Sin(a) * side, Mathf.Cos(a), 0f);
                    Vector3 p = new Vector3(d.x * rs[k], d.y * rs[k] * 0.9f, zs[k]);
                    float u = (float)i / N;
                    if (side < 0) u = 1f - u;
                    mb.AddVertex(p, d, reg.UV(u, vs[k]));
                }
            }
            int row = N + 1;
            for (int k = 0; k < zs.Length - 1; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = first + k * row + i;
                    if (side > 0) { mb.AddTriangle(a, a + row, a + row + 1); mb.AddTriangle(a, a + row + 1, a + 1); }
                    else { mb.AddTriangle(a, a + row + 1, a + row); mb.AddTriangle(a, a + 1, a + row + 1); }
                }
            mb.Build("Forearm", arm, Layers.ViewModel);
        }

        static Rect RectOf(AtlasRect r, float u0, float v0, float u1, float v1)
        {
            Vector2 a = r.UV(u0, v0), b = r.UV(u1, v1);
            return new Rect(a.x, a.y, b.x - a.x, b.y - a.y);
        }

        // ================================================================================================ API
        /// <summary>Show this item in hand (builds the model via ItemMeshFactory). None = empty hand / lowered arms.</summary>
        public void SetHeld(ItemType item)
        {
            if (HeldItem == item && (HeldModel != null || item == ItemType.None)) return;
            HeldItem = item;
            if (HeldModel != null) Destroy(HeldModel);
            HeldModel = null;
            _gripL = null;
            HandSocket.localPosition = SocketOffset(item);
            HandSocket.localRotation = SocketRotation(item);
            if (item != ItemType.None)
            {
                HeldModel = ItemMeshFactory.Build(item);
                HeldModel.transform.SetParent(HandSocket, false);
                // the Zippo sits close to the lens in the corner of the view, like the reference
                if (item == ItemType.Lighter) HeldModel.transform.localScale = Vector3.one * 1.35f;
                GeoUtil.SetLayerRecursive(HeldModel, Layers.ViewModel);
                _gripL = HeldModel.transform.Find("Grip_L");
                foreach (var r in HeldModel.GetComponentsInChildren<Renderer>(true)) r.enabled = _visible;
            }
            // switch animation: the new item rises from below
            _raise = 0f;
            _raiseTarget = item == ItemType.None && Skin != CharacterSkin.Omar ? 0f : 1f;
            UpdatePose(0f);
        }

        /// <summary>One-shot hand animation (pickup, interact, use, pour, throw, attack for Omar's cleaver...).</summary>
        public void Play(CharacterAction action)
        {
            CurrentAction = action;
            _actTime = 0f;
        }

        bool _visible = true;

        public void SetVisible(bool visible)
        {
            _visible = visible;
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            if (visible) SetLeftVisible(_leftW > 0.02f);
        }

        Renderer[] _leftRenderers;

        void SetLeftVisible(bool v)
        {
            if (_lHand == null) return;
            if (_leftRenderers == null)
            {
                var a = _lHand.GetComponentsInChildren<Renderer>(true);
                var b = _lArm.GetComponentsInChildren<Renderer>(true);
                _leftRenderers = new Renderer[a.Length + b.Length];
                a.CopyTo(_leftRenderers, 0);
                b.CopyTo(_leftRenderers, a.Length);
            }
            for (int i = 0; i < _leftRenderers.Length; i++) if (_leftRenderers[i] != null) _leftRenderers[i].enabled = v && _visible;
        }

        /// <summary>Where the item's grip sits relative to the fist (lighter on top of the fist, others through it).</summary>
        static Vector3 SocketOffset(ItemType item)
        {
            switch (item)
            {
                case ItemType.Lighter: return new Vector3(-0.004f, 0.052f, 0.006f);
                case ItemType.LighterFuel: return new Vector3(-0.006f, 0.035f, 0.01f);
                case ItemType.Bandages: return new Vector3(-0.01f, 0.03f, 0.02f);
                case ItemType.Pills: return new Vector3(-0.006f, 0.02f, 0.01f);
                case ItemType.SoundMeter: return new Vector3(-0.012f, 0.0f, 0.03f);
                case ItemType.Bottle: return new Vector3(0f, 0.02f, 0f);
                case ItemType.Batteries: return new Vector3(-0.005f, 0.03f, 0.01f);
                case ItemType.Fuse: return new Vector3(-0.005f, 0.026f, 0.015f);
                default: return Vector3.zero;
            }
        }

        /// <summary>Per item adjustment of the right hand rest pose (camera space).</summary>
        static Vector3 HoldOffset(ItemType item)
        {
            switch (ItemMeshFactory.HoldPoseFor(item))
            {
                case HoldPose.TwoHanded:
                    if (item == ItemType.GasCan) return new Vector3(-0.05f, 0.13f, 0.08f);
                    if (item == ItemType.CarBattery) return new Vector3(-0.02f, 0.06f, 0.08f);
                    return new Vector3(-0.03f, 0.04f, 0.02f);
                case HoldPose.Flashlight: return new Vector3(0f, 0.02f, 0.02f);
                case HoldPose.Lighter: return new Vector3(-0.05f, 0.05f, -0.13f);
                case HoldPose.Pistol: return new Vector3(-0.07f, 0.075f, 0.04f);
                case HoldPose.OneHandSmall: return new Vector3(-0.01f, 0.02f, 0f);
                default: return Vector3.zero;
            }
        }

        static Vector3 HoldEuler(ItemType item)
        {
            switch (item)
            {
                case ItemType.BoltCutters: return new Vector3(-12f, -6f, 0f);
                case ItemType.Crowbar: return new Vector3(-25f, -20f, 20f);
                case ItemType.GasCan: return new Vector3(0f, -25f, 0f);
                case ItemType.Flashlight: return new Vector3(0f, -4f, 0f);
                case ItemType.Lighter: return new Vector3(-2f, 26f, -3f);
                case ItemType.Revolver: return new Vector3(2f, 4f, 0f);
                default: return Vector3.zero;
            }
        }

        static Quaternion SocketRotation(ItemType item)
        {
            switch (item)
            {
                case ItemType.CarKeys: return Quaternion.Euler(0f, -15f, 0f);
                case ItemType.CageKey: return Quaternion.Euler(0f, -10f, 0f);
                case ItemType.SoundMeter: return Quaternion.Euler(-20f, -8f, 0f);  // dial (-Z face) towards the player
                case ItemType.Bottle: return Quaternion.Euler(-150f, 0f, 0f);     // held by the neck, body up / back
                default: return Quaternion.identity;
            }
        }

        // ================================================================================================ animation
        void LateUpdate()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f) return;
            UpdatePose(dt);
        }

        void UpdatePose(float dt)
        {
            _time += dt;
            float speed = Mathf.Max(0f, MoveSpeed);
            _sprintW = Mathf.MoveTowards(_sprintW, Sprinting && speed > 2.5f ? 1f : 0f, dt * 4f);
            _crouchW = Mathf.MoveTowards(_crouchW, Crouching ? 1f : 0f, dt * 5f);
            float targetAmp = Mathf.Clamp01(speed / 2f) * (1f + 0.8f * _sprintW) * (1f - 0.4f * _crouchW);
            _bobAmp = Mathf.MoveTowards(_bobAmp, targetAmp, dt * 3f);
            _bobPhase += dt * (speed > 0.05f ? Mathf.Lerp(7.5f, 11f, Mathf.Clamp01((speed - 2f) / 2.5f)) : 0f);
            // empty hands come up for interactions (doors, pickups...) and go down again afterwards
            float raiseGoal = CurrentAction != CharacterAction.None && CurrentAction != CharacterAction.Search ? 1f : _raiseTarget;
            _raise = Mathf.MoveTowards(_raise, raiseGoal, dt / (raiseGoal > _raise && CurrentAction != CharacterAction.None ? 0.15f : 0.32f));

            // sway lags behind the mouse
            Vector2 target = new Vector2(Mathf.Clamp(-LookDelta.x * 0.35f, -5f, 5f), Mathf.Clamp(LookDelta.y * 0.35f, -5f, 5f));
            _sway.x = Mathf.SmoothDamp(_sway.x, target.x, ref _swayVel.x, 0.09f, 200f, Mathf.Max(dt, 1e-4f));
            _sway.y = Mathf.SmoothDamp(_sway.y, target.y, ref _swayVel.y, 0.09f, 200f, Mathf.Max(dt, 1e-4f));

            float bx = Mathf.Sin(_bobPhase) * 0.011f * _bobAmp;
            float by = -Mathf.Abs(Mathf.Cos(_bobPhase)) * 0.012f * _bobAmp + 0.006f * _bobAmp;
            float breathe = Mathf.Sin(_time * 1.6f) * 0.0028f + Mathf.Sin(_time * 0.53f) * 0.0015f;

            Vector3 pos = RestPos + HoldOffset(HeldItem) + new Vector3(bx, by + breathe, 0f);
            Vector3 eul = RestEuler + HoldEuler(HeldItem) + new Vector3(_sway.y + by * 120f, _sway.x + bx * 80f, -_sway.x * 0.6f);
            // sprint: arm pulled down / tilted, crouch: lower
            pos += new Vector3(0.01f, -0.06f, -0.04f) * _sprintW + new Vector3(0f, -0.015f, 0f) * _crouchW;
            eul += new Vector3(18f, -6f, 10f) * _sprintW;
            // raise / lower (item switch, empty hands)
            float low = 1f - Smooth(_raise);
            pos += new Vector3(0.05f, -0.42f, -0.08f) * low;
            eul += new Vector3(35f, 0f, 0f) * low;
            if (Skin == CharacterSkin.Omar)
            {
                // cleaver raised at chest height, blade up and forward, twitching now and then
                float tw = Mathf.Max(0f, Mathf.Sin(_time * 0.7f) * 6f - 4.5f);
                pos += new Vector3(0.01f, 0.0f, 0.02f);
                eul += new Vector3(-38f + tw * 5f, -12f, 12f + tw * 3f);
            }

            // actions
            Vector3 dp = Vector3.zero, de = Vector3.zero;
            float leftTarget = 0f;
            Vector3 lp = new Vector3(-RestPos.x, RestPos.y, RestPos.z), le = new Vector3(RestEuler.x, -RestEuler.y, -RestEuler.z);
            if (CurrentAction != CharacterAction.None)
            {
                float d = HumanoidAnimator.DurationOf(CurrentAction);
                _actTime += dt;
                float t = Mathf.Clamp01(_actTime / d);
                ActionOffsets(CurrentAction, t, ref dp, ref de, ref leftTarget, ref lp, ref le);
                if (_actTime >= d) CurrentAction = CharacterAction.None;
            }
            bool twoHanded = ItemMeshFactory.HoldPoseFor(HeldItem) == HoldPose.TwoHanded;
            if (twoHanded) leftTarget = Mathf.Max(leftTarget, Smooth(_raise));
            _leftW = Mathf.MoveTowards(_leftW, leftTarget, dt * 6f);

            _rHand.localPosition = pos + dp;
            _rHand.localRotation = Quaternion.Euler(eul + de);
            AimForearm(_rArm, _rHand, new Vector3(0.026f, -0.03f, -0.05f), ElbowR + (dp + pos - RestPos) * 0.5f);

            bool showLeft = _leftW > 0.02f;
            if (showLeft)
            {
                Vector3 lpos;
                Quaternion lrot;
                if (twoHanded && _gripL != null && CurrentAction == CharacterAction.None)
                {
                    lpos = transform.InverseTransformPoint(_gripL.position);
                    lrot = _rHand.localRotation;
                }
                else
                {
                    lpos = lp + new Vector3(-bx, by + breathe, 0f);
                    lrot = Quaternion.Euler(le);
                }
                Vector3 hidden = new Vector3(-0.2f, -0.55f, 0.1f);
                _lHand.localPosition = Vector3.Lerp(hidden, lpos, Smooth(_leftW));
                _lHand.localRotation = lrot;
                AimForearm(_lArm, _lHand, new Vector3(-0.026f, -0.03f, -0.05f), ElbowL + (_lHand.localPosition - new Vector3(-RestPos.x, RestPos.y, RestPos.z)) * 0.5f);
            }
            if (_leftVisible != showLeft)
            {
                _leftVisible = showLeft;
                SetLeftVisible(showLeft);
            }
        }

        bool _leftVisible;

        void AimForearm(Transform arm, Transform hand, Vector3 wristLocal, Vector3 elbow)
        {
            Vector3 wrist = transform.InverseTransformPoint(hand.TransformPoint(wristLocal));
            Vector3 dir = elbow - wrist;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.back;
            arm.localPosition = wrist;
            arm.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float Hold(float t, float a, float b, float c, float d)
        {
            if (t <= a || t >= d) return 0f;
            if (t < b) return Smooth((t - a) / (b - a));
            if (t <= c) return 1f;
            return 1f - Smooth((t - c) / (d - c));
        }

        void ActionOffsets(CharacterAction a, float t, ref Vector3 dp, ref Vector3 de, ref float left, ref Vector3 lp, ref Vector3 le)
        {
            switch (a)
            {
                case CharacterAction.Pickup:
                {
                    float k = Hold(t, 0.05f, 0.42f, 0.55f, 0.95f);
                    dp = new Vector3(-0.06f, -0.17f, 0.14f) * k;
                    de = new Vector3(35f, 0f, -10f) * k;
                    break;
                }
                case CharacterAction.Interact:
                {
                    float k = Hold(t, 0.05f, 0.4f, 0.55f, 0.95f);
                    dp = new Vector3(-0.07f, 0.03f, 0.13f) * k;
                    de = new Vector3(-10f, -10f, 0f) * k;
                    break;
                }
                case CharacterAction.UseItem:
                {
                    float k = Hold(t, 0.1f, 0.4f, 0.65f, 0.95f);
                    dp = new Vector3(-0.15f, 0.09f, -0.12f) * k;
                    de = new Vector3(-25f, -20f, 25f) * k;
                    break;
                }
                case CharacterAction.Pour:
                {
                    float k = Hold(t, 0.05f, 0.25f, 0.82f, 0.98f);
                    float tilt = Hold(t, 0.18f, 0.32f, 0.75f, 0.88f);
                    dp = new Vector3(-0.06f, 0.04f, 0.1f) * k;
                    de = new Vector3(10f * k, 0f, -75f * tilt);
                    break;
                }
                case CharacterAction.Throw:
                {
                    Vector3 back = new Vector3(0.04f, 0.17f, -0.14f), rel = new Vector3(-0.06f, 0.07f, 0.26f), fol = new Vector3(-0.08f, -0.3f, 0.12f);
                    if (t < 0.32f) { float k = Smooth(t / 0.32f); dp = back * k; de = new Vector3(-55f, 0f, 0f) * k; }
                    else if (t < 0.45f) { float k = (t - 0.32f) / 0.13f; k *= k; dp = Vector3.Lerp(back, rel, k); de = Vector3.Lerp(new Vector3(-55f, 0f, 0f), new Vector3(40f, 0f, 0f), k); }
                    else if (t < 0.66f) { float k = Smooth((t - 0.45f) / 0.21f); dp = Vector3.Lerp(rel, fol, k); de = Vector3.Lerp(new Vector3(40f, 0f, 0f), new Vector3(60f, 0f, 0f), k); }
                    else { float k = Smooth((t - 0.66f) / 0.34f); dp = Vector3.Lerp(fol, Vector3.zero, k); de = Vector3.Lerp(new Vector3(60f, 0f, 0f), Vector3.zero, k); }
                    break;
                }
                case CharacterAction.Attack:
                {
                    // Omar's chop: big and violent, the cleaver comes over the top of the screen and slams down through the view
                    Vector3 up = new Vector3(0.0f, 0.34f, -0.12f), hit = new Vector3(-0.15f, -0.02f, 0.16f), fol = new Vector3(-0.17f, -0.46f, 0.08f);
                    Vector3 eUp = new Vector3(-70f, 10f, -15f), eHit = new Vector3(28f, -10f, 25f), eFol = new Vector3(85f, -5f, 15f);
                    if (t < 0.42f) { float k = Smooth(t / 0.42f); dp = up * k; de = eUp * k; dp += new Vector3(0f, 0f, -0.05f) * k; }
                    else if (t < 0.58f) { float k = (t - 0.42f) / 0.16f; k = k * k * k; dp = Vector3.Lerp(up + new Vector3(0f, 0f, -0.05f), hit, k); de = Vector3.Lerp(eUp, eHit, k); }
                    else if (t < 0.72f) { float k = Smooth((t - 0.58f) / 0.14f); dp = Vector3.Lerp(hit, fol, k); de = Vector3.Lerp(eHit, eFol, k); }
                    else { float k = Smooth((t - 0.72f) / 0.28f); dp = Vector3.Lerp(fol, Vector3.zero, k); de = Vector3.Lerp(eFol, Vector3.zero, k); }
                    if (t > 0.3f && t < 0.46f) dp += new Vector3(Mathf.Sin(_time * 80f), Mathf.Sin(_time * 67f), 0f) * 0.004f; // straining at the top
                    float shake = t > 0.56f && t < 0.66f ? Mathf.Sin(_time * 90f) * 0.01f : 0f;
                    dp += new Vector3(shake, shake, 0f);
                    break;
                }
                case CharacterAction.Cut:
                {
                    float k = Hold(t, 0.05f, 0.3f, 0.75f, 0.98f);
                    float sq = Mathf.Sin(Mathf.Clamp01((t - 0.38f) / 0.34f) * Mathf.PI);
                    dp = new Vector3(-0.02f, 0.03f, 0.06f) * k + new Vector3(Mathf.Sin(_time * 60f) * 0.003f * sq, 0f, 0f);
                    left = k;
                    lp = new Vector3(-0.1f + 0.06f * sq, -0.19f, 0.46f);
                    le = new Vector3(0f, 10f, 0f);
                    break;
                }
                case CharacterAction.Heal:
                {
                    float k = Hold(t, 0.05f, 0.18f, 0.88f, 0.99f);
                    float ang = Smooth((t - 0.15f) / 0.7f) * Mathf.PI * 6f;
                    dp = (new Vector3(-0.11f, 0.03f, 0.0f) + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * 0.035f) * k;
                    de = new Vector3(-10f, -30f, 40f) * k;
                    left = k;
                    lp = new Vector3(-0.08f, -0.24f, 0.36f);
                    le = new Vector3(10f, 60f, 80f);
                    break;
                }
                case CharacterAction.Grab:
                {
                    float k = Hold(t, 0.05f, 0.35f, 0.6f, 0.95f);
                    dp = new Vector3(-0.07f, 0.06f, 0.24f) * k;
                    de = new Vector3(-10f, -10f, 0f) * k;
                    left = k;
                    lp = new Vector3(-0.13f, -0.15f, 0.62f);
                    le = new Vector3(-10f, 10f, 0f);
                    break;
                }
                case CharacterAction.Stunned:
                {
                    float k = Hold(t, 0.02f, 0.12f, 0.85f, 0.98f);
                    float sh = Mathf.Sin(_time * 23f) * 0.006f;
                    dp = new Vector3(-0.13f, 0.14f, -0.18f) * k + new Vector3(sh, sh, 0f);
                    de = new Vector3(-30f, -25f, 30f) * k;
                    left = k;
                    lp = new Vector3(-0.07f, -0.07f, 0.22f);
                    le = new Vector3(-30f, 25f, -30f);
                    break;
                }
                case CharacterAction.HitReact:
                {
                    float k = Smooth(t / 0.2f) * (1f - Smooth((t - 0.2f) / 0.8f));
                    dp = new Vector3(0.02f, -0.06f, -0.05f) * k;
                    de = new Vector3(15f, 10f, 10f) * k;
                    break;
                }
                case CharacterAction.Struggle:
                {
                    float k = Hold(t, 0f, 0.1f, 0.85f, 1f);
                    float w1 = Mathf.Sin(_time * 13f), w2 = Mathf.Sin(_time * 9.7f + 1f);
                    dp = new Vector3(w1 * 0.03f, w2 * 0.025f, 0.02f) * k;
                    de = new Vector3(w2 * 12f, w1 * 10f, 0f) * k;
                    left = k;
                    lp = new Vector3(-0.2f - w2 * 0.03f, -0.2f + w1 * 0.025f, 0.4f);
                    break;
                }
                case CharacterAction.Scream:
                {
                    float k = Hold(t, 0f, 0.15f, 0.8f, 1f);
                    float sh = Mathf.Sin(_time * 50f) * 0.006f;
                    dp = new Vector3(0.18f, -0.08f, -0.05f) * k + new Vector3(sh, sh, 0f);
                    de = new Vector3(10f, 25f, -20f) * k;
                    break;
                }
                case CharacterAction.Search:
                {
                    float k = Hold(t, 0.05f, 0.2f, 0.8f, 0.95f);
                    dp = new Vector3(Mathf.Sin(t * Mathf.PI * 2f) * 0.04f, -0.02f, 0f) * k;
                    break;
                }
                case CharacterAction.PlaceTrap:
                {
                    float k = Hold(t, 0.05f, 0.38f, 0.72f, 0.97f);
                    dp = new Vector3(-0.08f, -0.3f, 0.12f) * k;
                    de = new Vector3(50f, 0f, 0f) * k;
                    left = k;
                    lp = new Vector3(-0.12f, -0.48f, 0.5f);
                    le = new Vector3(50f, 0f, 0f);
                    break;
                }
                case CharacterAction.Shoot:
                {
                    // sharp kick up and back, slow settle
                    float kick = t < 0.12f ? Smooth(t / 0.12f) : 1f - Smooth((t - 0.12f) / 0.88f);
                    dp = new Vector3(0.01f, 0.05f, -0.07f) * kick;
                    de = new Vector3(-28f, 3f, -6f) * kick;
                    break;
                }
                case CharacterAction.Wave:
                {
                    float k = Hold(t, 0.03f, 0.18f, 0.82f, 1f);
                    dp = new Vector3(0.02f, 0.2f, 0.05f) * k;
                    de = new Vector3(-60f, 0f, Mathf.Sin(_time * 15f) * 25f) * k;
                    break;
                }
            }
        }
    }
}
