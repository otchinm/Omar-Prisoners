using System;
using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// Procedural animation of a <see cref="HumanoidRig"/>. Controllers (local player, network proxies, AI)
    /// write the state fields every frame; the animator turns them into bone rotations in LateUpdate.
    /// Fully procedural (no Animator component), allocation free per frame.
    /// Locomotion uses a distance driven gait phase with planted feet (two-bone IK): the left foot plants at
    /// phase 0, the right at 0.5, and <see cref="Footstep"/> fires exactly then.
    /// </summary>
    [RequireComponent(typeof(HumanoidRig))]
    public sealed class HumanoidAnimator : MonoBehaviour
    {
        /// <summary>World space velocity of the character (m/s).</summary>
        public Vector3 Velocity;
        public bool Grounded = true;
        public bool Crouching;
        public bool Sprinting;
        /// <summary>Limp + hand on wound.</summary>
        public bool Injured;
        /// <summary>Camera pitch in degrees (+ = looking down); bends spine / neck / head.</summary>
        public float LookPitch;
        public HoldPose Hold = HoldPose.None;
        public CharacterPose Pose = CharacterPose.Normal;

        /// <summary>Walk / run reference speeds (m/s) used to pick the gait and stride. Defaults depend on the skin.</summary>
        public float WalkSpeed = 2f, RunSpeed = 4.5f;
        /// <summary>Optional extra yaw of the head (degrees, + = right), e.g. Omar looking around; added on top of everything.</summary>
        public float LookYaw;

        /// <summary>Raised on each foot plant (0 = left, 1 = right) while walking/running.</summary>
        public event Action<int> Footstep;
        /// <summary>Raised at the key frame of an action (e.g. the cleaver hits at the bottom of Attack). See <see cref="ImpactTimeOf"/>.</summary>
        public event Action<CharacterAction> ActionImpact;

        public CharacterAction CurrentAction { get; private set; }

        /// <summary>Normalized progress (0..1) of <see cref="CurrentAction"/> (0 when none).</summary>
        public float ActionProgress => CurrentAction == CharacterAction.None ? 0f : Mathf.Clamp01(_actTime / DurationOf(CurrentAction));

        /// <summary>Starts a one-shot action (restarts if already playing).</summary>
        public void Play(CharacterAction action)
        {
            if (action == CharacterAction.None) { Stop(); return; }
            if (CurrentAction != CharacterAction.None)
            {
                _prevAction = CurrentAction;
                _prevTime = _actTime;
                _prevFade = CurrentEnvelope();
            }
            CurrentAction = action;
            _actTime = 0f;
            _impactDone = false;
            _actSide = ((_noiseSeed + _actCounter++) & 1) == 0 ? 1f : -1f;
        }

        /// <summary>Stops the current action (blends out).</summary>
        public void Stop()
        {
            if (CurrentAction == CharacterAction.None) return;
            _prevAction = CurrentAction;
            _prevTime = _actTime;
            _prevFade = CurrentEnvelope();
            CurrentAction = CharacterAction.None;
        }

        public bool IsPlaying(CharacterAction action) => CurrentAction == action;

        /// <summary>Duration in seconds of a one-shot action (matches the procedural implementation).</summary>
        public static float DurationOf(CharacterAction action)
        {
            switch (action)
            {
                case CharacterAction.Pickup: return 1.0f;
                case CharacterAction.Interact: return 0.8f;
                case CharacterAction.UseItem: return 1.2f;
                case CharacterAction.Attack: return 0.9f;
                case CharacterAction.Scream: return 2.2f;
                case CharacterAction.Search: return 2.5f;
                case CharacterAction.Grab: return 1.2f;
                case CharacterAction.PlaceTrap: return 1.6f;
                case CharacterAction.Stunned: return 3f;
                case CharacterAction.HitReact: return 0.5f;
                case CharacterAction.Struggle: return 1.5f;
                case CharacterAction.Pour: return 2f;
                case CharacterAction.Throw: return 0.8f;
                case CharacterAction.Cut: return 1.0f;
                case CharacterAction.Heal: return 2.5f;
                case CharacterAction.Wave: return 1.5f;
                default: return 0.8f;
            }
        }

        /// <summary>Normalized time (0..1) at which <see cref="ActionImpact"/> fires for an action.</summary>
        public static float ImpactTimeOf(CharacterAction action)
        {
            switch (action)
            {
                case CharacterAction.Pickup: return 0.45f;    // hand reaches the item
                case CharacterAction.Interact: return 0.45f;  // hand touches the handle / button
                case CharacterAction.UseItem: return 0.5f;
                case CharacterAction.Attack: return 0.48f;    // cleaver hits
                case CharacterAction.Scream: return 0.1f;     // scream starts
                case CharacterAction.Search: return 0.5f;
                case CharacterAction.Grab: return 0.42f;      // hands close on the victim
                case CharacterAction.PlaceTrap: return 0.6f;  // trap touches the ground
                case CharacterAction.Stunned: return 0.05f;
                case CharacterAction.HitReact: return 0.1f;
                case CharacterAction.Struggle: return 0.5f;
                case CharacterAction.Pour: return 0.3f;       // liquid starts flowing
                case CharacterAction.Throw: return 0.45f;     // release
                case CharacterAction.Cut: return 0.55f;       // jaws close
                case CharacterAction.Heal: return 0.85f;      // bandage done
                case CharacterAction.Wave: return 0.5f;
                default: return 0.5f;
            }
        }

        // Kept so subclasses / implementation can raise them.
        void RaiseFootstep(int foot) => Footstep?.Invoke(foot);
        void RaiseImpact(CharacterAction a) => ActionImpact?.Invoke(a);

        // ================================================================================================ state
        HumanoidRig _rig;
        BodySpec _spec;
        PoseApplier _ap;
        readonly PoseBuffer _acc = new PoseBuffer();
        readonly PoseBuffer _layer = new PoseBuffer();
        readonly PoseBuffer _base = new PoseBuffer();
        bool _ready;
        bool _heavy;
        float _s;              // body scale (height / 1.8)
        float _legLen, _hipH, _footX;

        float _time;
        Vector3 _smoothVel;
        float _speed, _speedVel;
        Vector3 _moveDir = Vector3.forward;
        float _phase, _prevPhase;
        float _moveW, _runW, _crouchW, _sprintW, _injW, _airW, _exert;
        readonly float[] _holdW = new float[7];
        readonly float[] _poseW = new float[7];
        bool _hiddenApplied;

        float _actTime;
        CharacterAction _prevAction;
        float _prevTime, _prevFade;
        bool _impactDone;
        float _actSide = 1f;
        int _actCounter;

        int _noiseSeed;
        float _glance, _glanceVel, _glanceTarget, _glanceTimer, _glancePitch;
        uint _rand;

        Transform _gripL;
        Transform _gripSearchedFor;
        int _gripChildCount = -1;

        void Awake()
        {
            _rig = GetComponent<HumanoidRig>();
            _noiseSeed = (GetInstanceID() * 7919) & 0x7FFF;
            _rand = (uint)(_noiseSeed * 2654435761u + 1u);
        }

        bool EnsureReady()
        {
            if (_ready) return true;
            if (_rig == null) _rig = GetComponent<HumanoidRig>();
            if (_rig == null || _rig.BoneArray == null || _rig.Spec == null) return false;
            _spec = _rig.Spec;
            _ap = new PoseApplier(_rig);
            _s = _spec.Scale;
            _heavy = _spec.Heavy;
            if (_rig.Skin == CharacterSkin.Omar && _heavy)
            {
                WalkSpeed = BodySpec.WalkSpeedOf(CharacterSkin.Omar);
                RunSpeed = BodySpec.RunSpeedOf(CharacterSkin.Omar);
            }
            _legLen = (_spec.HipJointY - _spec.KneeY) + (_spec.KneeY - _spec.AnkleY);
            _hipH = _spec.HipJointY - _spec.AnkleY;
            _footX = _spec.HipJointX * 1.05f;
            _poseW[0] = 1f;
            _ready = true;
            return true;
        }

        void OnDisable()
        {
            if (_hiddenApplied && _rig != null) { _rig.SetVisible(true); _hiddenApplied = false; }
        }

        // ================================================================================================ update
        void LateUpdate()
        {
            if (!EnsureReady()) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f) return;
            _time += dt;

            UpdateLocomotionState(dt);
            UpdateWeights(dt);

            // 1. idle / locomotion base
            BuildBase(_acc);
            // 2. hold poses (upper body)
            for (int h = 1; h < _holdW.Length; h++)
            {
                if (_holdW[h] < 0.002f) continue;
                _layer.CopyFrom(_acc);
                ApplyHold(_layer, (HoldPose)h);
                _acc.LerpTo(_layer, _holdW[h]);
            }
            // 3. injured: left hand presses the left flank
            if (_injW > 0.002f && Hold != HoldPose.TwoHanded)
            {
                _layer.CopyFrom(_acc);
                float waistY = Mathf.Lerp(_spec.CrotchY, _spec.NeckY, 0.42f);
                _layer.SetHandGoal(0, new Vector3(-(_spec.Torso[3].W + 0.025f * _s), waistY, _spec.Torso[3].F * 0.35f), 1f);
                _layer.ElbowHintL = new Vector3(-1f, -0.4f, -0.8f);
                _layer.SetHandRot(0, new Vector3(0f, 80f, 0f), 0.6f);
                _acc.LerpTo(_layer, _injW * 0.9f * _poseW[0]);
            }
            // 4. persistent poses
            for (int p = 1; p < _poseW.Length; p++)
            {
                if (_poseW[p] < 0.002f || p == (int)CharacterPose.Hidden) continue;
                _layer.CopyFrom(_acc);
                BuildPersistent(_layer, (CharacterPose)p);
                _acc.LerpTo(_layer, _poseW[p]);
            }
            // 5. actions (previous one fading out, then the current one)
            UpdateActions(dt);
            // 6. look pitch / yaw
            ApplyLook(_acc);
            // 7. apply
            _ap.Apply(_acc);
            if (_holdW[(int)HoldPose.TwoHanded] > 0.01f) SecondHandGrip();

            // footsteps
            if (_moveW > 0.35f && Grounded && _poseW[0] > 0.6f && _airW < 0.5f)
            {
                if (Crossed(_prevPhase, _phase, 0f)) RaiseFootstep(0);
                if (Crossed(_prevPhase, _phase, 0.5f)) RaiseFootstep(1);
            }
            _prevPhase = _phase;
        }

        /// <summary>True when the gait phase moved past <paramref name="mark"/> this frame (phase wraps at 1).</summary>
        static bool Crossed(float from, float to, float mark)
        {
            if (to >= from) return from < mark && to >= mark;
            return mark > from || mark <= to;
        }

        // ================================================================================================ locomotion
        void UpdateLocomotionState(float dt)
        {
            Vector3 v = Velocity; v.y = 0f;
            if (float.IsNaN(v.x) || float.IsNaN(v.z)) v = Vector3.zero;
            float k = 1f - Mathf.Exp(-dt / 0.1f);
            _smoothVel = Vector3.Lerp(_smoothVel, v, k);
            Vector3 local = transform.InverseTransformDirection(_smoothVel);
            local.y = 0f;
            float sp = local.magnitude;
            _speed = Mathf.SmoothDamp(_speed, sp, ref _speedVel, 0.12f, 60f, dt);
            if (_speed < 0.02f) _speed = 0f;
            if (sp > 0.08f)
            {
                Vector3 d = local / sp;
                _moveDir = Vector3.Slerp(_moveDir, d, 1f - Mathf.Exp(-dt / 0.12f));
                if (_moveDir.sqrMagnitude < 1e-4f) _moveDir = d;
                _moveDir.Normalize();
            }
            float crouch = _crouchW;
            float runW = Smooth01((_speed - WalkSpeed * 1.08f) / Mathf.Max(0.1f, RunSpeed * 0.92f - WalkSpeed * 1.08f)) * (1f - crouch);
            // Omar never breaks into a human run: his heavy walk just gets faster (longer, quicker strides)
            if (_heavy) runW = 0f;
            _runW = runW;
            float sc = Mathf.Max(0.6f, _hipH / 0.855f);
            float L;
            if (_heavy) L = Mathf.Lerp(0.95f + 0.62f * _speed, 1.35f + 0.47f * _speed, runW);
            else L = Mathf.Lerp(0.75f + 0.44f * _speed, 1.15f + 0.34f * _speed, runW);
            L = Mathf.Lerp(L, 0.55f + 0.55f * _speed, crouch) * sc;
            float D = Duty();
            // keep the stance travel reachable (otherwise raise the cadence)
            float sMax = MaxStance();
            if (D * L > sMax) L = sMax / D;
            _phase += _speed * dt / Mathf.Max(0.3f, L);
            _phase -= Mathf.Floor(_phase);
        }

        float Duty() => Mathf.Lerp(Mathf.Lerp(0.62f, 0.37f, _runW), 0.66f, _crouchW);

        float HipsDrop() => _legLen * Mathf.Lerp(Mathf.Lerp(0.035f, 0.075f, _runW) * _moveW + 0.012f, 0.33f, _crouchW);

        float MaxStance()
        {
            float h = _hipH - HipsDrop() - 0.03f * _s;
            float r = _legLen * 0.985f;
            float half = Mathf.Sqrt(Mathf.Max(0.01f, r * r - h * h));
            return 2f * half + 0.2f * _s; // heel / toe roll adds reach
        }

        void UpdateWeights(float dt)
        {
            _moveW = Approach(_moveW, Smooth01((_speed - 0.12f) / 0.45f), dt, 0.08f);
            _crouchW = Approach(_crouchW, Crouching && Grounded ? 1f : 0f, dt, 0.2f);
            _sprintW = Approach(_sprintW, !_heavy && Sprinting && _speed > WalkSpeed * 1.2f ? 1f : 0f, dt, 0.25f);
            _injW = Approach(_injW, Injured ? 1f : 0f, dt, 0.4f);
            _airW = Approach(_airW, Grounded ? 0f : 1f, dt, Grounded ? 0.08f : 0.15f);
            _exert = Mathf.Clamp01(_exert + (_runW > 0.5f ? dt / 6f : -dt / 14f));
            for (int h = 0; h < _holdW.Length; h++) _holdW[h] = Approach(_holdW[h], (int)Hold == h ? 1f : 0f, dt, 0.16f);
            for (int p = 0; p < _poseW.Length; p++) _poseW[p] = Approach(_poseW[p], (int)Pose == p ? 1f : 0f, dt, 0.3f);
            bool hidden = Pose == CharacterPose.Hidden;
            if (hidden != _hiddenApplied)
            {
                _rig.SetVisible(!hidden);
                _hiddenApplied = hidden;
            }
        }

        static float Approach(float w, float target, float dt, float tau) => w + (target - w) * (1f - Mathf.Exp(-dt / Mathf.Max(1e-3f, tau)));

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        // ================================================================================================ base pose
        void BuildBase(PoseBuffer p)
        {
            p.Clear();
            float s = _s;
            float breathPeriod = _heavy ? 4.6f : Mathf.Lerp(3.6f, 1.4f, _exert);
            float breath = Mathf.Sin(_time * Mathf.PI * 2f / breathPeriod + 0.3f * Noise(_time * 0.3f, 3));
            float breathAmp = _heavy ? 2.6f : Mathf.Lerp(1.2f, 3.2f, _exert);
            float idleW = 1f - _moveW;

            // ---------------------------------------------------------------- legs / hips
            float drop = HipsDrop();
            float D = Duty();
            float lift = _legLen * Mathf.Lerp(Mathf.Lerp(0.14f, 0.36f, _runW), 0.1f, _crouchW);
            Vector3 aL, aR;
            float pL, pR;
            GaitFoot(_phase, 0, D, lift, out aL, out pL);
            GaitFoot(_phase + 0.5f, 1, D, lift, out aR, out pR);
            // idle stance with slow weight shift
            float shift = Noise(_time * 0.11f, 11) * (_heavy ? 0.6f : 1f);
            Vector3 iL = new Vector3(-_footX * (1f + 0.25f * _crouchW), _spec.AnkleY, 0.02f * s + 0.04f * s * _crouchW);
            Vector3 iR = new Vector3(_footX * (1f + 0.25f * _crouchW), _spec.AnkleY, -0.03f * s - 0.06f * s * _crouchW);
            p.AnkleL = Vector3.Lerp(iL, aL, _moveW);
            p.AnkleR = Vector3.Lerp(iR, aR, _moveW);
            p.FootPitchL = pL * _moveW;
            p.FootPitchR = pR * _moveW;
            p.FootYawL = p.FootYawR = 6f + 6f * _crouchW;
            p.LegIK = 1f - _airW;

            float bobAmp = _legLen * Mathf.Lerp(0.022f, 0.04f, _runW) * (_heavy ? 1.3f : 1f) * (1f - 0.5f * _crouchW);
            float bob = _runW > 0.5f
                ? -bobAmp * Mathf.Cos(4f * Mathf.PI * (_phase - D * 0.5f))
                : -bobAmp * Mathf.Cos(4f * Mathf.PI * (_phase - 0.05f));
            float swayAmp = (_heavy ? 0.035f : 0.022f) * s * (1f - 0.5f * _runW);
            float sway = -swayAmp * Mathf.Sin(2f * Mathf.PI * (_phase + 0.25f - D * 0.5f));
            float limpDip = _injW * _moveW * 0.035f * s * Mathf.Max(0f, Mathf.Sin(2f * Mathf.PI * (_phase / Mathf.Max(0.1f, D * 2f))));
            p.HipsPos = new Vector3(sway * _moveW + shift * 0.025f * s * idleW,
                -drop + bob * _moveW - limpDip + breath * 0.002f * idleW,
                -0.06f * s * _crouchW);
            float yawAmp = Mathf.Lerp(Mathf.Lerp(6f, 11f, _runW), 3f, _crouchW) * _moveW;
            float pelvisYaw = yawAmp * Mathf.Cos(2f * Mathf.PI * _phase);
            float rollAmp = Mathf.Lerp(4f, 3f, _runW) * _moveW * (_heavy ? 1.15f : 1f);
            float pelvisRoll = -rollAmp * Mathf.Sin(2f * Mathf.PI * (_phase + 0.25f - D * 0.5f)) + shift * 2.5f * idleW;
            // Omar stays upright (no forward lean while chasing), just a slight heavy hunch
            float lean = _heavy ? 2.5f + 1.5f * _moveW
                : Mathf.Lerp(Mathf.Lerp(3f, 11f, _runW) * _moveW, 20f, _crouchW) + 9f * _sprintW;
            lean += _injW * 6f * _moveW;
            p.Rot[(int)BoneId.Hips] = new Vector3(lean * 0.35f, pelvisYaw, pelvisRoll);
            p.Rot[(int)BoneId.Spine] = new Vector3(lean * 0.3f + (_heavy ? 4f : 0f), -pelvisYaw * 0.45f, -pelvisRoll * 0.5f);
            float chestRoll = -pelvisRoll * 0.4f + _injW * _moveW * 5f;
            p.Rot[(int)BoneId.Chest] = new Vector3(lean * 0.35f - breath * breathAmp * idleW - breath * breathAmp * 0.5f * _moveW * _exert,
                -pelvisYaw * 0.9f, chestRoll);
            // head stabilization
            float headCounter = -(lean * 1.0f);
            p.Rot[(int)BoneId.Neck] = new Vector3(headCounter * 0.35f + (_heavy ? -4f : 0f), pelvisYaw * 0.3f, 0f);
            p.Rot[(int)BoneId.Head] = new Vector3(headCounter * 0.55f + breath * 0.8f * idleW, pelvisYaw * 0.45f, -chestRoll * 0.6f);

            // ---------------------------------------------------------------- arms (FK)
            float swing = Mathf.Lerp(Mathf.Lerp(15f, 44f, _runW) * Mathf.Clamp01(_speed / Mathf.Max(0.5f, WalkSpeed) + 0.2f), 8f, _crouchW) * _moveW;
            swing *= 1f + 0.35f * _sprintW;
            if (_heavy) swing = Mathf.Min(swing * 0.35f, 9f); // arms mostly hang, a stiff small sway
            float c = Mathf.Cos(2f * Mathf.PI * _phase);
            float elbow = Mathf.Lerp(Mathf.Lerp(12f, 78f, _runW) * _moveW + 9f * idleW, 45f, _crouchW);
            float flexBias = Mathf.Lerp(Mathf.Lerp(3f, -8f, _runW) * _moveW, -22f, _crouchW) + (_heavy ? -6f : 0f);
            float abd = Mathf.Lerp(5f + 0.6f * breath * idleW + 3f * _airW * 6f, 12f, _runW * _moveW) + (_heavy ? 3f : 0f);
            float rF = flexBias - swing * c, lF = flexBias + swing * c;
            float rE = elbow + Mathf.Max(0f, -c) * swing * 0.5f, lE = elbow + Mathf.Max(0f, c) * swing * 0.5f;
            if (_injW > 0f) { lF *= 1f - 0.7f * _injW; }
            float armNoise = Noise(_time * 0.4f, 21) * 2f;
            p.Arm(0, lF + armNoise, abd + armNoise * 0.5f, 0f, lE);
            p.Arm(1, rF - armNoise, abd - armNoise * 0.4f, 0f, rE);
            p.HandFK(0, 8f + 10f * _runW, 0f, 0f);
            p.HandFK(1, 8f + 10f * _runW, 0f, 0f);

            // ---------------------------------------------------------------- air
            if (_airW > 0.001f)
            {
                _base.CopyFrom(p);
                _base.LegIK = 0f;
                _base.Leg(0, -28f, 4f, 0f, 48f, 25f);
                _base.Leg(1, -12f, 4f, 0f, 30f, 25f);
                _base.Arm(0, -20f, 28f, 0f, 30f);
                _base.Arm(1, -20f, 28f, 0f, 30f);
                p.LerpTo(_base, _airW);
            }

            // ---------------------------------------------------------------- idle character
            if (idleW > 0.001f) IdleCharacter(p, idleW, breath);
        }

        /// <summary>Planted-foot gait: ankle goal (root space) + foot pitch for one leg.</summary>
        void GaitFoot(float phase, int side, float D, float lift, out Vector3 ankle, out float pitch)
        {
            phase -= Mathf.Floor(phase);
            float s = _s;
            float L = _spec.FootLen * Mathf.Lerp(1f, s, 0.5f);
            float heelBack = 0.22f * L, ballFwd = 0.45f * L;
            float h = _spec.AnkleY;
            float D2 = Mathf.Max(0.2f, D);
            float stance = MaxStanceTravel(D2);
            float heelStrike = Mathf.Lerp(Mathf.Lerp(14f, 2f, _runW), 4f, _crouchW);
            float toeOff = Mathf.Lerp(Mathf.Lerp(32f, 48f, _runW), 22f, _crouchW);
            Vector3 dir = _moveDir;
            float x = (side == 0 ? -_footX : _footX) * (1f - 0.25f * _runW);
            bool limp = side == 0 && _injW > 0.01f;
            if (phase < D2)
            {
                StanceAnkle(phase / D2, stance, dir, x, h, heelBack, ballFwd, heelStrike, toeOff, out ankle, out pitch);
            }
            else
            {
                float sw = (phase - D2) / (1f - D2);
                StanceAnkle(1f, stance, dir, x, h, heelBack, ballFwd, heelStrike, toeOff, out Vector3 a0, out float p0);
                StanceAnkle(0f, stance, dir, x, h, heelBack, ballFwd, heelStrike, toeOff, out Vector3 a1, out float p1);
                float e = sw * sw * (3f - 2f * sw);
                float arcT = _runW > 0.5f ? Mathf.Pow(sw, 0.75f) : sw;
                float arc = Mathf.Sin(arcT * Mathf.PI);
                float lf = limp ? lift * Mathf.Lerp(1f, 0.25f, _injW) : lift;
                ankle = Vector3.Lerp(a0, a1, e) + Vector3.up * (lf * arc);
                ankle -= dir * (_runW * 0.12f * s * Mathf.Sin(sw * Mathf.PI) * (1f - sw));
                pitch = Mathf.Lerp(p0, p1, e) - 10f * Mathf.Sin(sw * Mathf.PI) * (1f - _runW);
                if (limp) pitch += 25f * _injW * Mathf.Sin(sw * Mathf.PI);
            }
        }

        float MaxStanceTravel(float D)
        {
            float sc = Mathf.Max(0.6f, _hipH / 0.855f);
            float L;
            if (_heavy) L = Mathf.Lerp(0.95f + 0.62f * _speed, 1.35f + 0.47f * _speed, _runW);
            else L = Mathf.Lerp(0.75f + 0.44f * _speed, 1.15f + 0.34f * _speed, _runW);
            L = Mathf.Lerp(L, 0.55f + 0.55f * _speed, _crouchW) * sc;
            return Mathf.Min(D * L, MaxStance());
        }

        void StanceAnkle(float sp, float S, Vector3 dir, float x, float h, float heelBack, float ballFwd,
            float heelStrike, float toeOff, out Vector3 ankle, out float pitch)
        {
            float travel = S * (0.5f - sp);
            // the swing foot lands slightly ahead of the hips, the body vaults over it
            Vector3 g = new Vector3(x, 0f, 0.015f * _s) + dir * travel;
            if (sp < 0.14f) pitch = -heelStrike * (1f - sp / 0.14f);
            else if (sp > 0.6f) pitch = toeOff * Mathf.Pow((sp - 0.6f) / 0.4f, 1.6f);
            else pitch = 0f;
            float r = pitch * Mathf.Deg2Rad;
            float cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            if (pitch < 0f)
            {
                // pivot on the heel: vector heel -> ankle = (0, h, heelBack) rotated about X
                Vector3 heel = g + new Vector3(0f, 0f, -heelBack);
                ankle = heel + new Vector3(0f, h * cs - heelBack * sn, h * sn + heelBack * cs);
            }
            else
            {
                Vector3 ball = g + new Vector3(0f, 0f, ballFwd);
                ankle = ball + new Vector3(0f, h * cs + ballFwd * sn, h * sn - ballFwd * cs);
            }
        }

        void IdleCharacter(PoseBuffer p, float w, float breath)
        {
            float t = _time;
            if (_heavy)
            {
                // Omar: heavy, hunched, slow creepy head tilts, cleaver hand twitching
                float tilt = Noise(t * 0.09f, 31) * 16f;
                float yaw = Noise(t * 0.07f, 32) * 18f;
                p.Rot[(int)BoneId.Spine].x += 5f * w;
                p.Rot[(int)BoneId.Chest].x += 4f * w;
                p.Rot[(int)BoneId.Neck].x += -5f * w;
                p.Rot[(int)BoneId.Head].x += (6f + Noise(t * 0.13f, 33) * 6f) * w;
                p.Rot[(int)BoneId.Head].y += yaw * w;
                p.Rot[(int)BoneId.Head].z += tilt * w;
                p.Rot[(int)BoneId.Neck].z += tilt * 0.4f * w;
                float tw = Mathf.Max(0f, Noise(t * 1.7f, 34) * 2.2f - 1.1f);
                tw += Mathf.Max(0f, Noise(t * 5.3f, 35) * 3f - 2.4f) * 0.6f;
                p.Rot[(int)BoneId.RHand].x += tw * 30f * w;
                p.Rot[(int)BoneId.RLowerArm].x -= tw * 12f * w;
                p.Rot[(int)BoneId.RHand].y += Noise(t * 3.1f, 36) * 6f * tw * w;
            }
            else
            {
                // prisoners: nervous glances around
                _glanceTimer -= Time.deltaTime;
                if (_glanceTimer <= 0f)
                {
                    float r = Rand01();
                    _glanceTarget = r < 0.35f ? 0f : (Rand01() < 0.5f ? -1f : 1f) * (25f + Rand01() * 45f);
                    _glancePitch = (Rand01() - 0.4f) * 18f;
                    _glanceTimer = 1.2f + Rand01() * 3.5f;
                }
                _glance = Mathf.SmoothDamp(_glance, _glanceTarget, ref _glanceVel, 0.18f, 400f, Mathf.Max(1e-3f, Time.deltaTime));
                p.Rot[(int)BoneId.Head].y += _glance * 0.7f * w;
                p.Rot[(int)BoneId.Neck].y += _glance * 0.3f * w;
                p.Rot[(int)BoneId.Chest].y += _glance * 0.1f * w;
                p.Rot[(int)BoneId.Head].x += (_glancePitch + Noise(t * 0.5f, 41) * 3f) * w;
                p.Rot[(int)BoneId.Head].z += Noise(t * 0.23f, 42) * 4f * w;
                // fidget fingers / shoulders
                p.Rot[(int)BoneId.LHand].x += Noise(t * 0.9f, 43) * 8f * w;
                p.Rot[(int)BoneId.RHand].x += Noise(t * 0.8f, 44) * 8f * w;
                p.Rot[(int)BoneId.LUpperArm].z -= breath * 1.2f * w;
                p.Rot[(int)BoneId.RUpperArm].z += breath * 1.2f * w;
            }
        }

        // ================================================================================================ holds
        void ApplyHold(PoseBuffer p, HoldPose h)
        {
            float s = _s;
            float pitch = Mathf.Clamp(LookPitch, -70f, 70f);
            float bob = Mathf.Sin(_time * 1.7f) * 0.004f;
            switch (h)
            {
                case HoldPose.Lighter:
                    p.SetHandGoalChest(1, new Vector3(0.075f * s, 0.15f * s + bob - pitch * 0.0012f, 0.30f * s), 1f);
                    p.SetHandRot(1, new Vector3(pitch * 0.35f, -8f, 0f), 1f);
                    p.ElbowHintR = new Vector3(1f, -1.2f, -0.2f);
                    break;
                case HoldPose.Flashlight:
                    p.SetHandGoalChest(1, new Vector3(0.16f * s, 0.10f * s + bob - pitch * 0.002f, 0.40f * s), 1f);
                    p.SetHandRot(1, new Vector3(pitch, -4f, 0f), 1f);
                    p.ElbowHintR = new Vector3(0.8f, -1f, -0.3f);
                    break;
                case HoldPose.OneHandSmall:
                    p.SetHandGoalChest(1, new Vector3(0.17f * s, -0.12f * s + bob, 0.24f * s), 1f);
                    p.SetHandRot(1, new Vector3(25f + pitch * 0.3f, -10f, 0f), 1f);
                    p.ElbowHintR = new Vector3(1f, -1f, -0.4f);
                    break;
                case HoldPose.TwoHanded:
                    p.SetHandGoalChest(1, new Vector3(0.12f * s, -0.17f * s + bob, 0.30f * s), 1f);
                    p.SetHandGoalChest(0, new Vector3(-0.12f * s, -0.17f * s + bob, 0.30f * s), 1f);
                    p.SetHandRot(1, new Vector3(8f + pitch * 0.2f, 0f, 0f), 1f);
                    p.ElbowHintR = new Vector3(1f, -1f, -0.3f);
                    p.ElbowHintL = new Vector3(-1f, -1f, -0.3f);
                    break;
                case HoldPose.Cleaver:
                    p.Arm(1, -10f + p.Rot[(int)BoneId.RUpperArm].x * 0.5f, 11f, 0f, 22f + p.Rot[(int)BoneId.RLowerArm].x * -0.3f);
                    p.Rot[(int)BoneId.RHand] = new Vector3(38f + p.Rot[(int)BoneId.RHand].x, 0f, 0f);
                    p.HandIKR = 0f;
                    p.HandRotWR = 0f;
                    break;
                case HoldPose.Bottle:
                    p.SetHandGoalChest(1, new Vector3(0.22f * s, 0.27f * s + bob, -0.02f * s), 1f);
                    p.SetHandRot(1, new Vector3(115f, -10f, 0f), 1f);
                    p.ElbowHintR = new Vector3(1f, 0.1f, -0.9f);
                    break;
            }
        }

        /// <summary>Two handed items: the left hand goes to the model's "Grip_L" child when it has one.</summary>
        void SecondHandGrip()
        {
            Transform socket = _rig.RightHandSocket;
            if (socket == null) return;
            if (socket.childCount != _gripChildCount || (_gripL == null && _gripSearchedFor != socket))
            {
                _gripChildCount = socket.childCount;
                _gripSearchedFor = socket;
                _gripL = null;
                for (int i = 0; i < socket.childCount && _gripL == null; i++)
                {
                    Transform c = socket.GetChild(i);
                    _gripL = c.Find("Grip_L");
                }
            }
            if (_gripL == null) return;
            float w = _holdW[(int)HoldPose.TwoHanded] * _poseW[0];
            _ap.SolveArmWorld(0, _gripL.position, transform.TransformDirection(new Vector3(-1f, -1f, -0.3f)), w);
        }

        // ================================================================================================ persistent poses
        void BuildPersistent(PoseBuffer p, CharacterPose pose)
        {
            float breathe = Mathf.Sin(_time * 1.6f) * 1.5f;
            switch (pose)
            {
                case CharacterPose.CagedSit:
                    HumanoidPoses.CagedSit(p, _spec, _time * 1.3f, breathe);
                    p.Rot[(int)BoneId.Head].y += Noise(_time * 0.2f, 51) * 25f;
                    p.Rot[(int)BoneId.Head].x += Noise(_time * 0.17f, 52) * 10f;
                    break;
                case CharacterPose.Downed:
                    float crawlW = Smooth01((_speed - 0.05f) / 0.3f);
                    _downCrawl += Time.deltaTime * (1.2f + _speed * 3f) * crawlW;
                    HumanoidPoses.Downed(p, _spec, _downCrawl, crawlW, breathe);
                    break;
                case CharacterPose.Dead:
                    HumanoidPoses.Lying(p, _spec, null, true);
                    break;
                case CharacterPose.Seated:
                    HumanoidPoses.Seated(p, _spec, breathe);
                    p.Rot[(int)BoneId.Head].y += Noise(_time * 0.15f, 53) * 20f;
                    break;
                case CharacterPose.Trapped:
                    HumanoidPoses.Trapped(p, _spec, _time, breathe);
                    break;
            }
        }

        float _downCrawl;

        // ================================================================================================ actions
        float CurrentEnvelope() => CurrentAction == CharacterAction.None ? 0f : Envelope(CurrentAction, _actTime);

        static float BlendIn(CharacterAction a)
        {
            switch (a)
            {
                case CharacterAction.Attack: return 0.08f;
                case CharacterAction.HitReact: return 0.04f;
                case CharacterAction.Stunned: return 0.06f;
                case CharacterAction.Throw: return 0.1f;
                default: return 0.18f;
            }
        }

        static float Envelope(CharacterAction a, float time)
        {
            float d = DurationOf(a);
            float i = BlendIn(a), o = Mathf.Min(0.3f, d * 0.3f);
            if (a == CharacterAction.HitReact) o = d * 0.7f;
            return Smooth01(time / i) * (1f - Smooth01((time - (d - o)) / o));
        }

        void UpdateActions(float dt)
        {
            if (_prevAction != CharacterAction.None)
            {
                _prevTime += dt;
                _prevFade = Mathf.Max(0f, _prevFade - dt / 0.15f);
                if (_prevFade <= 0f || _prevTime >= DurationOf(_prevAction)) _prevAction = CharacterAction.None;
                else
                {
                    _layer.CopyFrom(_acc);
                    ActionPose(_layer, _prevAction, _prevTime);
                    _acc.LerpTo(_layer, _prevFade);
                }
            }
            if (CurrentAction == CharacterAction.None) return;
            float d = DurationOf(CurrentAction);
            float before = _actTime;
            _actTime += dt;
            float imp = ImpactTimeOf(CurrentAction) * d;
            if (!_impactDone && before <= imp && _actTime >= imp)
            {
                _impactDone = true;
                RaiseImpact(CurrentAction);
            }
            if (_actTime >= d)
            {
                CurrentAction = CharacterAction.None;
                return;
            }
            _layer.CopyFrom(_acc);
            ActionPose(_layer, CurrentAction, _actTime);
            _acc.LerpTo(_layer, Envelope(CurrentAction, _actTime));
        }

        void ActionPose(PoseBuffer p, CharacterAction a, float time)
        {
            float d = DurationOf(a);
            float t = Mathf.Clamp01(time / d);
            float s = _s;
            BodySpec b = _spec;
            float chestY = b.ChestY;
            float eyeY = b.Height - b.HeadH * (1f - CharacterAtlas.EyeY);
            switch (a)
            {
                case CharacterAction.Pickup:
                {
                    float k = HoldCurve(t, 0.0f, 0.38f, 0.58f, 0.95f);
                    p.HipsPos += new Vector3(0f, -0.30f * _legLen * k, -0.07f * s * k);
                    p.Rot[(int)BoneId.Spine].x += 22f * k;
                    p.Rot[(int)BoneId.Chest].x += 20f * k;
                    p.Rot[(int)BoneId.Neck].x += -6f * k;
                    p.Rot[(int)BoneId.Head].x += 14f * k;
                    float hk = HoldCurve(t, 0.1f, 0.42f, 0.55f, 0.9f);
                    p.SetHandGoal(1, new Vector3(0.14f * s, 0.10f * s, 0.40f * s), hk);
                    p.ElbowHintR = new Vector3(1f, 0f, -1f);
                    p.SetHandGoal(0, new Vector3(-0.12f * s, b.KneeY - 0.02f, 0.18f * s), 0.7f * k);
                    break;
                }
                case CharacterAction.Interact:
                {
                    float k = HoldCurve(t, 0.05f, 0.4f, 0.55f, 0.95f);
                    p.SetHandGoal(1, new Vector3(0.11f * s, chestY + 0.12f * s, 0.52f * s), k);
                    p.ElbowHintR = new Vector3(1f, -1f, -0.2f);
                    p.Rot[(int)BoneId.Spine].x += 5f * k;
                    p.Rot[(int)BoneId.Chest].y += 8f * k;
                    break;
                }
                case CharacterAction.UseItem:
                {
                    float k = HoldCurve(t, 0.1f, 0.4f, 0.65f, 0.95f);
                    p.SetHandGoal(1, new Vector3(0.035f * s, eyeY - 0.095f * s, 0.15f * s), k);
                    p.ElbowHintR = new Vector3(0.35f, -1f, 0.45f);
                    p.Rot[(int)BoneId.Head].x += -14f * Bell(t, 0.35f, 0.75f);
                    p.Rot[(int)BoneId.Neck].x += -6f * Bell(t, 0.35f, 0.75f);
                    break;
                }
                case CharacterAction.Attack: AttackPose(p, t); break;
                case CharacterAction.Scream:
                {
                    float k = HoldCurve(t, 0.0f, 0.14f, 0.82f, 1f);
                    float sh = Mathf.Sin(time * 70f) * 0.5f + Mathf.Sin(time * 47f + 1.3f) * 0.5f;
                    float heave = Mathf.Sin(time * 2f * Mathf.PI * 2.6f);
                    p.Rot[(int)BoneId.Spine].x += -8f * k;
                    p.Rot[(int)BoneId.Chest].x += (-16f + heave * 4f) * k + sh * 1.5f * k;
                    p.Rot[(int)BoneId.Neck].x += -22f * k;
                    p.Rot[(int)BoneId.Head].x += (-34f + sh * 3f) * k;
                    p.Rot[(int)BoneId.Head].z += sh * 3f * k;
                    p.HipsPos += new Vector3(0f, -0.04f * s * k, 0f);
                    p.Arm(0, Mathf.Lerp(p.Rot[(int)BoneId.LUpperArm].x, -28f + sh * 4f, k), Mathf.Lerp(-p.Rot[(int)BoneId.LUpperArm].z, 68f, k), 30f * k, 28f * k);
                    p.Arm(1, Mathf.Lerp(p.Rot[(int)BoneId.RUpperArm].x, -28f - sh * 4f, k), Mathf.Lerp(p.Rot[(int)BoneId.RUpperArm].z, 68f, k), 30f * k, 28f * k);
                    p.HandIKL = p.HandIKR = 0f;
                    p.HandRotWL = p.HandRotWR = 0f;
                    p.Rot[(int)BoneId.LHand].x += -25f * k;
                    p.Rot[(int)BoneId.RHand].x += -25f * k;
                    break;
                }
                case CharacterAction.Search:
                {
                    float yaw = Keys3(t, 0.0f, 0f, 0.22f, -62f, 0.42f, -62f, 0.62f, 62f, 0.82f, 62f, 1f, 0f);
                    float lean = Bell(t, 0.05f, 0.95f);
                    p.Rot[(int)BoneId.Head].y += yaw * 0.6f;
                    p.Rot[(int)BoneId.Neck].y += yaw * 0.25f;
                    p.Rot[(int)BoneId.Chest].y += yaw * 0.25f;
                    p.Rot[(int)BoneId.Spine].y += yaw * 0.1f;
                    p.Rot[(int)BoneId.Spine].x += 8f * lean;
                    p.Rot[(int)BoneId.Head].z += -yaw * 0.18f;
                    p.Rot[(int)BoneId.Head].x += 6f * lean;
                    break;
                }
                case CharacterAction.Grab:
                {
                    float reach = HoldCurve(t, 0.05f, 0.35f, 0.5f, 0.72f);
                    float pull = HoldCurve(t, 0.42f, 0.6f, 0.85f, 1f);
                    float lunge = Mathf.Max(reach, pull * 0.6f);
                    p.HipsPos += new Vector3(0f, -0.06f * s * lunge, 0.14f * s * lunge);
                    p.Rot[(int)BoneId.Spine].x += 16f * lunge;
                    p.Rot[(int)BoneId.Chest].x += 14f * lunge;
                    p.Rot[(int)BoneId.Head].x += -10f * lunge;
                    Vector3 reachR = new Vector3(0.17f * s, chestY + 0.06f * s, 0.66f * s);
                    Vector3 pullR = new Vector3(0.12f * s, chestY - 0.08f * s, 0.36f * s);
                    float pw = pull / Mathf.Max(0.01f, reach + pull);
                    Vector3 gR = Vector3.Lerp(reachR, pullR, pw);
                    p.SetHandGoal(1, gR, Mathf.Max(reach, pull));
                    p.SetHandGoal(0, new Vector3(-gR.x, gR.y, gR.z), Mathf.Max(reach, pull));
                    p.ElbowHintR = new Vector3(1f, -0.5f, -0.6f);
                    p.ElbowHintL = new Vector3(-1f, -0.5f, -0.6f);
                    break;
                }
                case CharacterAction.PlaceTrap:
                {
                    float k = HoldCurve(t, 0.05f, 0.38f, 0.72f, 0.97f);
                    p.HipsPos += new Vector3(0f, -0.42f * _legLen * k, -0.1f * s * k);
                    p.Rot[(int)BoneId.Spine].x += 30f * k;
                    p.Rot[(int)BoneId.Chest].x += 24f * k;
                    p.Rot[(int)BoneId.Neck].x += -8f * k;
                    p.Rot[(int)BoneId.Head].x += 12f * k;
                    float hk = HoldCurve(t, 0.18f, 0.45f, 0.7f, 0.92f);
                    p.SetHandGoal(1, new Vector3(0.11f * s, 0.07f * s, 0.46f * s), hk);
                    p.SetHandGoal(0, new Vector3(-0.11f * s, 0.07f * s, 0.46f * s), hk);
                    p.ElbowHintR = new Vector3(1f, 0.2f, -1f);
                    p.ElbowHintL = new Vector3(-1f, 0.2f, -1f);
                    break;
                }
                case CharacterAction.Stunned:
                {
                    float back = HoldCurve(t, 0f, 0.08f, 0.4f, 0.95f);
                    float dbl = HoldCurve(t, 0.1f, 0.25f, 0.8f, 0.98f);
                    float hands = HoldCurve(t, 0.02f, 0.12f, 0.85f, 0.98f);
                    float sway = Mathf.Sin(time * 3.4f) * dbl;
                    p.HipsPos += new Vector3(sway * 0.03f * s, -0.07f * s * dbl, -0.18f * s * back);
                    p.Rot[(int)BoneId.Spine].x += -12f * Bell(t, 0f, 0.22f) + 14f * dbl;
                    p.Rot[(int)BoneId.Chest].x += -8f * Bell(t, 0f, 0.22f) + 14f * dbl;
                    p.Rot[(int)BoneId.Chest].z += sway * 6f;
                    p.Rot[(int)BoneId.Head].x += 16f * dbl;
                    p.Rot[(int)BoneId.Head].z += sway * 12f;
                    float faceDy = (eyeY - b.ChestY) - 0.03f * s;
                    p.SetHandGoalChest(0, new Vector3(-0.045f * s, faceDy, 0.15f * s + 0.02f * s), hands);
                    p.SetHandGoalChest(1, new Vector3(0.045f * s, faceDy - 0.01f * s, 0.15f * s + 0.02f * s), hands);
                    p.ElbowHintL = new Vector3(-1f, -1f, 0f);
                    p.ElbowHintR = new Vector3(1f, -1f, 0f);
                    p.HandRotWL = p.HandRotWR = 0f;
                    break;
                }
                case CharacterAction.HitReact:
                {
                    float k = Smooth01(t / 0.18f) * (1f - Smooth01((t - 0.18f) / 0.82f));
                    float side = _actSide;
                    p.HipsPos += new Vector3(0f, -0.02f * s * k, -0.05f * s * k);
                    p.Rot[(int)BoneId.Spine].x += 10f * k;
                    p.Rot[(int)BoneId.Chest].x += 12f * k;
                    p.Rot[(int)BoneId.Chest].y += 14f * side * k;
                    p.Rot[(int)BoneId.Head].x += 16f * k;
                    p.Rot[(int)BoneId.Head].z += 12f * side * k;
                    p.Rot[(int)BoneId.LUpperArm].x += -25f * k;
                    p.Rot[(int)BoneId.RUpperArm].x += -25f * k;
                    p.Rot[(int)BoneId.LLowerArm].x += -35f * k;
                    p.Rot[(int)BoneId.RLowerArm].x += -35f * k;
                    break;
                }
                case CharacterAction.Struggle:
                {
                    float k = HoldCurve(t, 0f, 0.1f, 0.85f, 1f);
                    float w1 = Mathf.Sin(time * 2f * Mathf.PI * 2.1f), w2 = Mathf.Sin(time * 2f * Mathf.PI * 1.55f + 0.8f);
                    p.Rot[(int)BoneId.Chest].y += 22f * w1 * k;
                    p.Rot[(int)BoneId.Spine].y += 9f * w2 * k;
                    p.Rot[(int)BoneId.Chest].z += 7f * w2 * k;
                    p.Rot[(int)BoneId.Chest].x += 8f * k;
                    p.Rot[(int)BoneId.Head].z += -10f * w1 * k;
                    p.Rot[(int)BoneId.Head].y += -12f * w2 * k;
                    p.SetHandGoalChest(1, new Vector3(0.16f * s, -0.06f * s + w1 * 0.04f, 0.28f * s + w2 * 0.05f), k);
                    p.SetHandGoalChest(0, new Vector3(-0.16f * s, -0.06f * s - w1 * 0.04f, 0.28f * s - w2 * 0.05f), k);
                    break;
                }
                case CharacterAction.Pour:
                {
                    float k = HoldCurve(t, 0.05f, 0.25f, 0.82f, 0.98f);
                    float tilt = HoldCurve(t, 0.18f, 0.32f, 0.75f, 0.88f);
                    p.SetHandGoalChest(1, new Vector3(0.15f * s, -0.08f * s, 0.42f * s), k);
                    p.SetHandRot(1, new Vector3(15f + 65f * tilt, -10f, -25f * tilt), k);
                    p.ElbowHintR = new Vector3(1f, -1f, -0.2f);
                    p.Rot[(int)BoneId.Spine].x += 9f * k;
                    p.Rot[(int)BoneId.Head].x += 14f * k;
                    break;
                }
                case CharacterAction.Throw: ThrowPose(p, t); break;
                case CharacterAction.Cut:
                {
                    float k = HoldCurve(t, 0.05f, 0.3f, 0.75f, 0.98f);
                    float sq = Bell(t, 0.38f, 0.72f);
                    p.SetHandGoalChest(1, new Vector3(0.13f * s, -0.04f * s, 0.44f * s), k);
                    p.SetHandGoalChest(0, new Vector3((-0.13f + 0.09f * sq) * s, -0.04f * s, (0.46f + 0.02f * sq) * s), k);
                    p.SetHandRot(1, new Vector3(5f, 0f, 0f), k);
                    p.Rot[(int)BoneId.Chest].x += (6f + 6f * sq) * k;
                    p.Rot[(int)BoneId.Spine].x += 4f * k;
                    p.Rot[(int)BoneId.Head].x += 10f * k;
                    break;
                }
                case CharacterAction.Heal:
                {
                    float k = HoldCurve(t, 0.05f, 0.18f, 0.88f, 0.99f);
                    Vector3 arm = new Vector3(-0.03f * s, -0.12f * s, 0.30f * s);
                    float ang = Smooth01((t - 0.15f) / 0.7f) * Mathf.PI * 2f * 3f;
                    Vector3 wrap = arm + new Vector3(0.10f * s + 0.035f * s * Mathf.Cos(ang), 0.045f * s * Mathf.Sin(ang), 0.02f * s * Mathf.Cos(ang));
                    p.SetHandGoalChest(0, arm, k);
                    p.SetHandGoalChest(1, wrap, k);
                    p.SetHandRot(0, new Vector3(0f, 75f, 90f), k * 0.8f);
                    p.ElbowHintL = new Vector3(-1f, -1f, -0.2f);
                    p.ElbowHintR = new Vector3(1f, -0.6f, -0.4f);
                    p.Rot[(int)BoneId.Head].x += 22f * k;
                    p.Rot[(int)BoneId.Neck].x += 8f * k;
                    p.Rot[(int)BoneId.Chest].x += 6f * k;
                    break;
                }
                case CharacterAction.Wave:
                {
                    float k = HoldCurve(t, 0.03f, 0.18f, 0.82f, 1f);
                    float wv = Mathf.Sin(time * 2f * Mathf.PI * 2.4f);
                    p.Arm(1, Mathf.Lerp(p.Rot[(int)BoneId.RUpperArm].x, -25f, k), Mathf.Lerp(p.Rot[(int)BoneId.RUpperArm].z, 78f, k),
                        Mathf.Lerp(0f, 72f + wv * 28f, k), Mathf.Lerp(-p.Rot[(int)BoneId.RLowerArm].x, 95f, k));
                    p.HandIKR = 0f;
                    p.HandRotWR = 0f;
                    p.Rot[(int)BoneId.RHand].x += -10f * k;
                    p.Rot[(int)BoneId.Head].z += -6f * k;
                    break;
                }
            }
        }

        void AttackPose(PoseBuffer p, float t)
        {
            // Omar's overhead cleaver chop: wind-up (arm high behind the head), fast swing down, follow-through.
            float s = _s;
            // key times
            const float tUp = 0.36f, tHit = 0.48f, tFollow = 0.64f;
            float flex, abd, twist, elbow, hand, chestY, chestX, spineX, hipsDrop, hipsFwd, lFlex, lAbd, lElbow;
            if (t < tUp)
            {
                float k = Smooth01(t / tUp);
                flex = Mathf.Lerp(-12f, -172f, k); abd = Mathf.Lerp(12f, 38f, k); twist = Mathf.Lerp(0f, -10f, k);
                elbow = Mathf.Lerp(22f, 72f, k); hand = Mathf.Lerp(38f, -45f, k);
                chestY = Mathf.Lerp(0f, -22f, k); chestX = Mathf.Lerp(0f, -10f, k); spineX = Mathf.Lerp(0f, -5f, k);
                hipsDrop = 0f; hipsFwd = Mathf.Lerp(0f, -0.04f, k);
                lFlex = Mathf.Lerp(0f, -50f, k); lAbd = Mathf.Lerp(5f, 20f, k); lElbow = Mathf.Lerp(10f, 50f, k);
            }
            else if (t < tHit)
            {
                float k = (t - tUp) / (tHit - tUp);
                k = k * k; // accelerate into the hit
                flex = Mathf.Lerp(-172f, -55f, k); abd = Mathf.Lerp(38f, 10f, k); twist = Mathf.Lerp(-10f, 0f, k);
                elbow = Mathf.Lerp(72f, 8f, k); hand = Mathf.Lerp(-45f, 25f, k);
                chestY = Mathf.Lerp(-22f, 10f, k); chestX = Mathf.Lerp(-10f, 24f, k); spineX = Mathf.Lerp(-5f, 14f, k);
                hipsDrop = Mathf.Lerp(0f, 0.05f, k); hipsFwd = Mathf.Lerp(-0.04f, 0.07f, k);
                lFlex = Mathf.Lerp(-50f, -15f, k); lAbd = Mathf.Lerp(20f, 26f, k); lElbow = Mathf.Lerp(50f, 30f, k);
            }
            else if (t < tFollow)
            {
                float k = Smooth01((t - tHit) / (tFollow - tHit));
                flex = Mathf.Lerp(-55f, -18f, k); abd = Mathf.Lerp(10f, 4f, k); twist = 0f;
                elbow = Mathf.Lerp(8f, 25f, k); hand = Mathf.Lerp(25f, 40f, k);
                chestY = Mathf.Lerp(10f, 14f, k); chestX = Mathf.Lerp(24f, 26f, k); spineX = Mathf.Lerp(14f, 16f, k);
                hipsDrop = 0.05f; hipsFwd = 0.07f;
                lFlex = -15f; lAbd = 26f; lElbow = 30f;
            }
            else
            {
                float k = Smooth01((t - tFollow) / (1f - tFollow));
                flex = Mathf.Lerp(-18f, -12f, k); abd = Mathf.Lerp(4f, 12f, k); twist = 0f;
                elbow = Mathf.Lerp(25f, 22f, k); hand = Mathf.Lerp(40f, 38f, k);
                chestY = Mathf.Lerp(14f, 0f, k); chestX = Mathf.Lerp(26f, 0f, k); spineX = Mathf.Lerp(16f, 0f, k);
                hipsDrop = Mathf.Lerp(0.05f, 0f, k); hipsFwd = Mathf.Lerp(0.07f, 0f, k);
                lFlex = Mathf.Lerp(-15f, 0f, k); lAbd = Mathf.Lerp(26f, 5f, k); lElbow = Mathf.Lerp(30f, 10f, k);
            }
            p.Arm(1, flex, abd, twist, elbow);
            p.Rot[(int)BoneId.RHand] = new Vector3(hand, 0f, 0f);
            p.HandIKR = 0f; p.HandRotWR = 0f;
            p.Arm(0, lFlex, lAbd, 0f, lElbow);
            p.HandIKL = 0f; p.HandRotWL = 0f;
            p.Rot[(int)BoneId.Chest].y += chestY;
            p.Rot[(int)BoneId.Chest].x += chestX;
            p.Rot[(int)BoneId.Spine].x += spineX;
            p.Rot[(int)BoneId.Spine].y += chestY * 0.3f;
            p.Rot[(int)BoneId.Head].x += -chestX * 0.55f;
            p.Rot[(int)BoneId.Head].y += -chestY * 0.6f;
            p.HipsPos += new Vector3(0f, -hipsDrop * s, hipsFwd * s);
        }

        void ThrowPose(PoseBuffer p, float t)
        {
            float s = _s;
            const float tBack = 0.32f, tRel = 0.45f, tFol = 0.66f;
            float flex, abd, twist, elbow, chestY, chestX;
            if (t < tBack)
            {
                float k = Smooth01(t / tBack);
                flex = Mathf.Lerp(-20f, -140f, k); abd = Mathf.Lerp(15f, 55f, k); twist = Mathf.Lerp(0f, 70f, k); elbow = Mathf.Lerp(40f, 95f, k);
                chestY = Mathf.Lerp(0f, -32f, k); chestX = Mathf.Lerp(0f, -8f, k);
            }
            else if (t < tRel)
            {
                float k = (t - tBack) / (tRel - tBack); k *= k;
                flex = Mathf.Lerp(-140f, -112f, k); abd = Mathf.Lerp(55f, 22f, k); twist = Mathf.Lerp(70f, 0f, k); elbow = Mathf.Lerp(95f, 8f, k);
                chestY = Mathf.Lerp(-32f, 18f, k); chestX = Mathf.Lerp(-8f, 12f, k);
            }
            else if (t < tFol)
            {
                float k = Smooth01((t - tRel) / (tFol - tRel));
                flex = Mathf.Lerp(-112f, -35f, k); abd = Mathf.Lerp(22f, -12f, k); twist = 0f; elbow = Mathf.Lerp(8f, 30f, k);
                chestY = Mathf.Lerp(18f, 30f, k); chestX = Mathf.Lerp(12f, 16f, k);
            }
            else
            {
                float k = Smooth01((t - tFol) / (1f - tFol));
                flex = Mathf.Lerp(-35f, -10f, k); abd = Mathf.Lerp(-12f, 8f, k); twist = 0f; elbow = Mathf.Lerp(30f, 15f, k);
                chestY = Mathf.Lerp(30f, 0f, k); chestX = Mathf.Lerp(16f, 0f, k);
            }
            p.Arm(1, flex, abd, twist, elbow);
            p.HandIKR = 0f; p.HandRotWR = 0f;
            p.Arm(0, -35f * Bell(t, 0.05f, 0.75f), 25f, 0f, 40f);
            p.HandIKL = 0f; p.HandRotWL = 0f;
            p.Rot[(int)BoneId.Chest].y += chestY;
            p.Rot[(int)BoneId.Chest].x += chestX;
            p.Rot[(int)BoneId.Spine].y += chestY * 0.35f;
            p.Rot[(int)BoneId.Head].y += -chestY * 0.7f;
            p.HipsPos += new Vector3(0f, -0.02f * s * Bell(t, 0.2f, 0.8f), 0.05f * s * Bell(t, 0.35f, 0.9f));
        }

        // ================================================================================================ look
        void ApplyLook(PoseBuffer p)
        {
            float upright = _poseW[0] + _poseW[(int)CharacterPose.Seated] + _poseW[(int)CharacterPose.Trapped] * 0.6f
                + _poseW[(int)CharacterPose.CagedSit] * 0.5f + _poseW[(int)CharacterPose.Downed] * 0.4f;
            upright = Mathf.Clamp01(upright);
            float pitch = Mathf.Clamp(LookPitch, -80f, 80f) * upright;
            p.Rot[(int)BoneId.Spine].x += pitch * 0.12f;
            p.Rot[(int)BoneId.Chest].x += pitch * 0.18f;
            p.Rot[(int)BoneId.Neck].x += pitch * 0.25f;
            p.Rot[(int)BoneId.Head].x += pitch * 0.45f;
            float yaw = Mathf.Clamp(LookYaw, -100f, 100f) * upright;
            p.Rot[(int)BoneId.Chest].y += yaw * 0.2f;
            p.Rot[(int)BoneId.Neck].y += yaw * 0.3f;
            p.Rot[(int)BoneId.Head].y += yaw * 0.5f;
            // tiny life-like jitter
            float n = Noise(_time * 0.9f, 61);
            p.Rot[(int)BoneId.Head].x += n * 1.2f;
            p.Rot[(int)BoneId.Spine].z += Noise(_time * 0.31f, 62) * 0.8f;
        }

        // ================================================================================================ helpers
        static float HoldCurve(float t, float a, float b, float c, float d)
        {
            if (t <= a || t >= d) return 0f;
            if (t < b) return Smooth01((t - a) / Mathf.Max(1e-4f, b - a));
            if (t <= c) return 1f;
            return 1f - Smooth01((t - c) / Mathf.Max(1e-4f, d - c));
        }

        static float Bell(float t, float a, float b)
        {
            if (t <= a || t >= b) return 0f;
            float x = (t - a) / (b - a);
            return Mathf.Sin(x * Mathf.PI);
        }

        /// <summary>Piecewise smooth keys: (t0, v0, t1, v1, ...).</summary>
        static float Keys3(float t, float t0, float v0, float t1, float v1, float t2, float v2, float t3, float v3, float t4, float v4, float t5, float v5)
        {
            if (t <= t1) return Mathf.Lerp(v0, v1, Smooth01((t - t0) / (t1 - t0)));
            if (t <= t2) return Mathf.Lerp(v1, v2, Smooth01((t - t1) / (t2 - t1)));
            if (t <= t3) return Mathf.Lerp(v2, v3, Smooth01((t - t2) / (t3 - t2)));
            if (t <= t4) return Mathf.Lerp(v3, v4, Smooth01((t - t3) / (t4 - t3)));
            return Mathf.Lerp(v4, v5, Smooth01((t - t4) / (t5 - t4)));
        }

        /// <summary>Smooth 1D value noise in [-1, 1], per-instance seeded.</summary>
        float Noise(float x, int channel)
        {
            int seed = _noiseSeed * 31 + channel * 1013;
            int i = Mathf.FloorToInt(x);
            float f = x - i;
            f = f * f * (3f - 2f * f);
            return Mathf.Lerp(Hash(i + seed), Hash(i + 1 + seed), f);
        }

        static float Hash(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                int m = n * (n * n * 15731 + 789221) + 1376312589;
                return 1f - (m & 0x7fffffff) / 1073741824f;
            }
        }

        float Rand01()
        {
            unchecked
            {
                _rand ^= _rand << 13; _rand ^= _rand >> 17; _rand ^= _rand << 5;
                return (_rand & 0xFFFFFF) / 16777216f;
            }
        }
    }
}
