using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// The body of a participant on this machine: procedural character, held item, light sources,
    /// footsteps, Omar's breathing. Local / AI avatars are driven through <see cref="SetLocalState"/>;
    /// remote avatars interpolate snapshots (100 ms behind).
    /// </summary>
    public sealed class Avatar : MonoBehaviour
    {
        struct Snap { public float Time; public AvatarNetState State; }

        public int Id { get; private set; }
        public PlayerInfo Info { get; private set; }
        public bool IsLocal { get; private set; }
        public bool IsOmar => Info.Role == PlayerRole.Omar;
        /// <summary>Simulated on this machine (local player or host AI).</summary>
        public bool Simulated { get; private set; }
        public HumanoidRig Rig { get; private set; }
        public HumanoidAnimator Anim { get; private set; }
        public AvatarNetState State;
        public CapsuleCollider Hitbox { get; private set; }

        readonly List<Snap> _buffer = new List<Snap>(16);
        int _lastTeleport = -1;
        ItemType _heldType = ItemType.None;
        GameObject _heldModel;
        PsxLight _lighterLight, _flashLight, _flashFill;
        GameObject _flame;
        AudioSource _breath, _flameLoop;
        GameObject _blob;
        Vector3 _lastPos;
        bool _visible = true;
        CharacterPose _statusPose = CharacterPose.Normal;
        float _remoteSpeedSmoothed;

        public const float InterpDelay = 0.1f;

        public Vector3 Position => transform.position;
        public float EyeHeight => IsOmar ? Tuning.OmarEyeHeight : ((State.Flags & AvatarFlags.Crouch) != 0 ? Tuning.CrouchEyeHeight : Tuning.EyeHeight);
        public Vector3 EyePosition => transform.position + Vector3.up * EyeHeight;
        public Vector3 ChestPosition => transform.position + Vector3.up * (EyeHeight * 0.72f);
        public bool LighterOn => (State.Flags & AvatarFlags.LighterOn) != 0;
        public bool FlashlightOn => (State.Flags & AvatarFlags.FlashlightOn) != 0;
        public bool Crouching => (State.Flags & AvatarFlags.Crouch) != 0;
        public bool Sprinting => (State.Flags & AvatarFlags.Sprint) != 0;
        public Vector3 Forward => Quaternion.Euler(0, State.Yaw, 0) * Vector3.forward;

        public static Avatar Spawn(PlayerInfo info, bool isLocal, bool simulated, Transform parent, Pose pose)
        {
            var go = new GameObject("Avatar_" + info.Id + "_" + info.Name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, Quaternion.Euler(0, pose.rotation.eulerAngles.y, 0));
            int layer = info.Role == PlayerRole.Omar ? Layers.Omar : Layers.Player;
            go.layer = layer;
            var a = go.AddComponent<Avatar>();
            a.Id = info.Id;
            a.Info = info;
            a.IsLocal = isLocal;
            a.Simulated = simulated;
            a.State.Position = pose.position;
            a.State.Yaw = pose.rotation.eulerAngles.y;
            a._lastPos = pose.position;
            try
            {
                a.Rig = HumanoidFactory.Build(info.Role == PlayerRole.Omar ? CharacterSkin.Omar : info.Skin, go.transform, isLocal ? Layers.LocalBody : layer);
                a.Anim = a.Rig != null ? a.Rig.GetComponent<HumanoidAnimator>() : null;
                if (a.Anim != null) a.Anim.Footstep += a.OnFootstep;
                if (isLocal && a.Rig != null) a.Rig.SetLayer(Layers.LocalBody);
                if (a.IsOmar && a.Rig != null && a.Rig.RightHandSocket != null)
                {
                    var cleaver = ItemMeshFactory.BuildCleaver();
                    cleaver.transform.SetParent(a.Rig.RightHandSocket, false);
                    GeoUtil.SetLayerRecursive(cleaver, isLocal ? Layers.LocalBody : layer);
                    if (a.Anim != null) a.Anim.Hold = HoldPose.Cleaver;
                }
            }
            catch (System.Exception e) { Debug.LogException(e); }

            if (!simulated)
            {
                a.Hitbox = go.AddComponent<CapsuleCollider>();
                float h = a.IsOmar ? 1.95f : 1.75f;
                a.Hitbox.height = h;
                a.Hitbox.radius = 0.32f;
                a.Hitbox.center = new Vector3(0, h * 0.5f, 0);
            }
            if (!isLocal)
            {
                try { a._blob = BlobShadow.Attach(go.transform, a.IsOmar ? 0.55f : 0.45f); } catch { }
                if (a.IsOmar) a._breath = AudioManager.Loop3D(Snd.OmarBreathLoop, pose.position, 0.75f, 12f, AudioCategory.Omar, go.transform, 1f);
            }
            return a;
        }

        void OnDestroy()
        {
            AudioManager.Stop(_breath, 0.2f);
            AudioManager.Stop(_flameLoop, 0.1f);
            if (Anim != null) Anim.Footstep -= OnFootstep;
        }

        // ------------------------------------------------------------------ state input

        /// <summary>Host takes over simulation of this avatar (AI replaces a disconnected Omar).</summary>
        public void MakeSimulated()
        {
            Simulated = true;
            _buffer.Clear();
            if (Hitbox != null) { Destroy(Hitbox); Hitbox = null; }
        }

        /// <summary>Remote snapshot received.</summary>
        public void PushRemoteState(AvatarNetState s)
        {
            if (Simulated) return;
            if (s.TeleportSeq != _lastTeleport)
            {
                _lastTeleport = s.TeleportSeq;
                _buffer.Clear();
                transform.position = s.Position;
                _lastPos = s.Position;
            }
            _buffer.Add(new Snap { Time = UnityEngine.Time.time, State = s });
            if (_buffer.Count > 12) _buffer.RemoveAt(0);
        }

        /// <summary>State produced locally (local player or AI). Moves nothing; the controller already moved the transform.</summary>
        public void SetLocalState(AvatarNetState s)
        {
            State = s;
            ApplyVisualState(Time.deltaTime);
        }

        void Update()
        {
            if (Simulated) return;
            Interpolate();
            ApplyVisualState(Time.deltaTime);
        }

        void Interpolate()
        {
            if (_buffer.Count == 0) return;
            float renderTime = Time.time - InterpDelay;
            // drop old
            while (_buffer.Count >= 2 && _buffer[1].Time <= renderTime) _buffer.RemoveAt(0);
            AvatarNetState s;
            if (_buffer.Count >= 2 && _buffer[0].Time <= renderTime)
            {
                var a = _buffer[0]; var b = _buffer[1];
                float t = Mathf.InverseLerp(a.Time, b.Time, renderTime);
                s = b.State;
                s.Position = Vector3.Lerp(a.State.Position, b.State.Position, t);
                s.Yaw = Mathf.LerpAngle(a.State.Yaw, b.State.Yaw, t);
                s.Pitch = Mathf.Lerp(a.State.Pitch, b.State.Pitch, t);
            }
            else
            {
                s = _buffer[_buffer.Count - 1].State;
                // gentle extrapolation guard: move towards the newest position
                s.Position = Vector3.Lerp(transform.position, s.Position, Mathf.Clamp01(Time.deltaTime * 10f));
            }
            State = s;
            transform.SetPositionAndRotation(s.Position, Quaternion.Euler(0, s.Yaw, 0));
        }

        void ApplyVisualState(float dt)
        {
            Vector3 pos = transform.position;
            Vector3 vel = dt > 0f ? (pos - _lastPos) / dt : Vector3.zero;
            _lastPos = pos;
            if (vel.sqrMagnitude > 100f) vel = Vector3.zero; // teleport
            if (!Simulated)
            {
                float sp = new Vector2(vel.x, vel.z).magnitude;
                _remoteSpeedSmoothed = Mathf.Lerp(_remoteSpeedSmoothed, sp, Mathf.Clamp01(dt * 8f));
            }

            if (Anim != null)
            {
                Anim.Velocity = vel;
                Anim.Crouching = Crouching;
                Anim.Sprinting = Sprinting;
                Anim.LookPitch = State.Pitch;
                Anim.Grounded = true;
                var st = MatchWorld.Instance != null ? MatchWorld.Instance.StatusOf(Id) : null;
                Anim.Injured = st != null && st.Injured;
                Anim.Pose = _statusPose;
                if (IsOmar) Anim.Hold = HoldPose.Cleaver;
                else Anim.Hold = State.Held == ItemType.None ? HoldPose.None : ItemDefs.Get(State.Held).Hold;
            }

            if (!IsOmar) UpdateHeldItem();
        }

        // ------------------------------------------------------------------ held item & lights (third person)

        void UpdateHeldItem()
        {
            ItemType want = (_visible && (_statusPose == CharacterPose.Normal || _statusPose == CharacterPose.Trapped)) ? State.Held : ItemType.None;
            if (IsLocal) want = ItemType.None; // local view model handles it
            if (want != _heldType)
            {
                _heldType = want;
                if (_heldModel != null) Destroy(_heldModel);
                _heldModel = null;
                _flame = null;
                if (want != ItemType.None && Rig != null && Rig.RightHandSocket != null)
                {
                    try
                    {
                        _heldModel = ItemMeshFactory.Build(want);
                        _heldModel.transform.SetParent(Rig.RightHandSocket, false);
                        GeoUtil.SetLayerRecursive(_heldModel, gameObject.layer);
                    }
                    catch (System.Exception e) { Debug.LogException(e); }
                }
            }

            bool lighter = !IsLocal && LighterOn && _heldType == ItemType.Lighter && _visible;
            bool flash = !IsLocal && FlashlightOn && _heldType == ItemType.Flashlight && _visible;

            if (lighter)
            {
                var anchor = FindAnchor("Anchor_Flame");
                if (_lighterLight == null)
                {
                    _lighterLight = PsxLight.Create(anchor, Vector3.up * 0.04f, new Color(1f, 0.7f, 0.36f), 1.25f, 5.5f, PsxFlicker.Candle, "LighterLight");
                    _lighterLight.Priority = 8;
                }
                if (_flame == null && anchor != null)
                {
                    try { _flame = PsxFx.CreateFlame(anchor, 1f); } catch { }
                }
                if (_flameLoop == null) _flameLoop = AudioManager.Loop3D(Snd.FlameLoop, transform.position, 0.25f, 4f, AudioCategory.Sfx, anchor, 0.2f);
            }
            else
            {
                if (_lighterLight != null) { Destroy(_lighterLight.gameObject); _lighterLight = null; }
                if (_flame != null) { Destroy(_flame); _flame = null; }
                if (_flameLoop != null) { AudioManager.Stop(_flameLoop, 0.2f); _flameLoop = null; }
            }

            if (flash)
            {
                var anchor = FindAnchor("Anchor_Light");
                if (_flashLight == null)
                {
                    _flashLight = PsxLight.CreateSpot(anchor, Vector3.zero, Quaternion.identity, new Color(1f, 0.95f, 0.8f), 2.2f, 16f, 42f, PsxFlicker.None, "FlashBeam");
                    _flashLight.Priority = 8;
                    _flashFill = PsxLight.Create(anchor, Vector3.forward * 0.6f, new Color(0.9f, 0.88f, 0.8f), 0.45f, 3f, PsxFlicker.None, "FlashFill");
                }
                // aim the beam with the look pitch
                _flashLight.transform.rotation = Quaternion.Euler(State.Pitch, State.Yaw, 0);
            }
            else
            {
                if (_flashLight != null) { Destroy(_flashLight.gameObject); _flashLight = null; }
                if (_flashFill != null) { Destroy(_flashFill.gameObject); _flashFill = null; }
            }
        }

        Transform FindAnchor(string name)
        {
            if (_heldModel != null)
            {
                var t = FindDeep(_heldModel.transform, name);
                if (t != null) return t;
                return _heldModel.transform;
            }
            return Rig != null && Rig.RightHandSocket != null ? Rig.RightHandSocket : transform;
        }

        public static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        // ------------------------------------------------------------------ status / actions

        public void PlayAction(CharacterAction a)
        {
            if (Anim != null && a != CharacterAction.None) Anim.Play(a);
        }

        public void ApplyStatus(PlayerStatus s)
        {
            CharacterPose pose = CharacterPose.Normal;
            bool visible = true;
            switch (s.Life)
            {
                case LifeState.Caged: pose = CharacterPose.CagedSit; break;
                case LifeState.Dead: pose = CharacterPose.Dead; break;
                case LifeState.Escaped:
                case LifeState.Gone: visible = false; break;
            }
            if (s.Life == LifeState.Free)
            {
                if (s.Hidden) { pose = CharacterPose.Hidden; visible = false; }
                else if (s.Trapped) pose = CharacterPose.Trapped;
                else if (s.InCar) pose = CharacterPose.Seated;
            }
            _statusPose = pose;
            SetVisible(visible && pose != CharacterPose.Hidden);
            if (Hitbox != null) Hitbox.enabled = visible && s.Life == LifeState.Free && !s.Hidden;
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            if (Rig != null) Rig.SetVisible(v && !IsLocal);
            if (_blob != null) _blob.SetActive(v);
            if (_heldModel != null) _heldModel.SetActive(v);
            if (_breath != null) AudioManager.SetVolume(_breath, v ? 0.75f : 0f);
        }

        public bool Visible => _visible;

        void OnFootstep(int foot)
        {
            if (IsLocal || !_visible) return;
            if (!Simulated && _remoteSpeedSmoothed < 0.3f) return;
            var surf = CharacterMotor.SurfaceAt(transform.position);
            bool crouch = Crouching;
            float vol = crouch ? 0.18f : Sprinting ? 0.95f : 0.55f;
            float maxD = crouch ? 6f : Sprinting ? 28f : 16f;
            if (IsOmar)
            {
                AudioManager.Play3D(AudioManager.Variant(Snd.OmarStep, 4), transform.position, Mathf.Min(1f, vol + 0.3f), Random.Range(0.9f, 1.05f), 2f, maxD + 8f, AudioCategory.Omar);
            }
            else
            {
                AudioManager.Play3D(Snd.Step(surf), transform.position, vol, Random.Range(0.92f, 1.08f), 1.5f, maxD, AudioCategory.Sfx);
            }
        }
    }
}
