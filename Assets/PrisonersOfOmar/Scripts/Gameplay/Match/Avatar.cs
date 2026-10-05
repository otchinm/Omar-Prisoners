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
        /// <summary>The character whose voice this avatar uses (Omar / unknown -> the first prisoner's).</summary>
        public CharacterSkin VoiceSkin => Info != null && Info.Skin != CharacterSkin.Omar ? Info.Skin : CharacterSkin.Prisoner1;
        /// <summary>Random variant of one of this prisoner's own voice lines (Audio/Voices/&lt;skin&gt;/...).</summary>
        public string Voice(VoiceLine line) => Snd.Voice(VoiceSkin, line);
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
        // others still see a prisoner climb into a hiding spot for a moment before the body disappears
        float _hideDelay;
        bool _wasHidden;
        CharacterPose _statusPose = CharacterPose.Normal;
        float _remoteSpeedSmoothed;

        public const float InterpDelay = 0.1f;

        public Vector3 Position => transform.position;
        public float EyeHeight => IsOmar ? Tuning.OmarEyeHeight - Mathf.Max(Tuning.OmarDuckDrop * DuckAmount, DoorwayEyeDrop) : ((State.Flags & AvatarFlags.Crouch) != 0 ? CrouchEyeHeight : StandEyeHeight);
        /// <summary>This prisoner's own eye height (from the body height of their character).</summary>
        public float StandEyeHeight { get; private set; } = Tuning.EyeHeight;
        public float CrouchEyeHeight => Tuning.CrouchEyeFor(StandEyeHeight);
        /// <summary>0..1 how far the giant Omar stoops right now (low door frame / ceiling over or just ahead of him).</summary>
        public float DuckAmount { get; private set; }
        float _duckTarget, _duckProbeAt;
        /// <summary>Omar squeezing through a doorway lower than he is (probed ahead, held while he passes).</summary>
        public DoorwayPass Doorway => _doorPass;
        /// <summary>0..1 how far he is folded over under a doorway right now.</summary>
        public float DoorwayBend => IsOmar && Anim != null ? Anim.DoorwayBend : 0f;
        /// <summary>He slows down a little while he hauls himself through.</summary>
        public float DoorwaySpeedFactor => 1f - 0.2f * DoorwayBend;
        /// <summary>Where the doorway fold put his eyes (below / ahead of / rolled from upright).</summary>
        public float DoorwayEyeDrop => IsOmar && Anim != null ? Anim.DoorwayEyeDrop : 0f;
        public float DoorwayEyeForward => IsOmar && Anim != null ? Anim.DoorwayEyeForward : 0f;
        public float DoorwayEyeRoll => IsOmar && Anim != null ? Anim.DoorwayEyeRoll : 0f;
        DoorwayPass _doorPass;
        int _doorCount, _doorVariantLast = -1;
        float _doorProbeAt;
        public Vector3 EyePosition => transform.position + Vector3.up * EyeHeight;
        public Vector3 ChestPosition => transform.position + Vector3.up * (EyeHeight * 0.72f);
        public bool LighterOn => (State.Flags & AvatarFlags.LighterOn) != 0;
        public bool FlashlightOn => (State.Flags & AvatarFlags.FlashlightOn) != 0;
        public bool Crouching => (State.Flags & AvatarFlags.Crouch) != 0;
        public bool Peeking => (State.Flags & AvatarFlags.Peek) != 0;
        bool _peekHidden;
        bool _selfShown;   // the local body is drawn for the admin free camera (AdminFreeCam.ShowingSelf)
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
                if (info.Role != PlayerRole.Omar) a.StandEyeHeight = Tuning.EyeHeightFor(a.Rig != null ? a.Rig.Height : BodySpec.For(info.Skin).Height);
                if (a.Anim != null) a.Anim.Footstep += a.OnFootstep;
                if (isLocal && a.Rig != null) a.Rig.SetLayer(Layers.LocalBody); // Omar's rig already holds his cleaver
            }
            catch (System.Exception e) { Debug.LogException(e); }

            if (!simulated)
            {
                a.Hitbox = go.AddComponent<CapsuleCollider>();
                float h = a.IsOmar ? 2.25f : 1.75f;
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
            if (IsLocal) UpdateSelfShown();
            if (_hideDelay > 0f)
            {
                _hideDelay -= Time.deltaTime;
                if (_hideDelay <= 0f) { _statusPose = CharacterPose.Hidden; SetVisible(false); }
            }
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
                if (IsOmar)
                {
                    Anim.Hold = HoldPose.Cleaver;
                    // a real doorway gets the full pass (hands on the frame); the simple stoop is for beams / low ceilings
                    Anim.Duck = DuckAmount * (1f - Anim.DoorwayBend);
                }
                else Anim.Hold = State.Held == ItemType.None ? HoldPose.None : ItemMeshFactory.HoldPoseFor(State.Held);
            }

            if (IsOmar) { UpdateDuck(vel, dt); UpdateDoorway(vel); }
            else { UpdateHeldItem(); UpdatePeekHidden(); }
        }

        /// <summary>
        /// Omar and the 2.2 m doors: finds the next doorway lower than him on his way (its lowest point, frame, jambs), holds
        /// on to it while he passes and picks how he squeezes through (varied, never the same style twice running).
        /// </summary>
        void UpdateDoorway(Vector3 vel)
        {
            if (Anim == null) return;
            Vector3 flat = new Vector3(vel.x, 0f, vel.z);
            float speed = flat.magnitude;
            Vector3 pos = transform.position;
            if (_doorPass.Active)
            {
                Vector3 rel = pos - _doorPass.Plane;
                float s = Vector3.Dot(rel, _doorPass.Normal);
                float side = Mathf.Abs(Vector3.Dot(rel, Vector3.Cross(Vector3.up, _doorPass.Normal)));
                _doorPass.Progress = s;
                // through, backed off, slid away along the wall or changed floors
                if (s > 1.1f || s < -2.2f || side > 1.2f || Mathf.Abs(rel.y) > 0.6f) _doorPass.Active = false;
                // turned round in the opening: the same doorway the other way (keeps the style, so no snap)
                else if (speed > 0.3f && Vector3.Dot(flat, _doorPass.Normal) < -0.25f * speed && s > -0.2f
                         && DoorwayProbe.Find(pos, flat, Rig != null ? Rig.Height : 2.5f, 1.9f, out var back)
                         && Vector3.Distance(back.Plane, _doorPass.Plane) < 0.6f)
                    _doorPass = MakePass(back, _doorPass.Variant);
            }
            if (!_doorPass.Active && speed > 0.3f && Time.time >= _doorProbeAt)
            {
                _doorProbeAt = Time.time + 0.05f;
                if (DoorwayProbe.Find(pos, flat, Rig != null ? Rig.Height : 2.5f, 1.9f, out var r) && (r.HasLeft || r.HasRight))
                {
                    _doorPass = MakePass(r, PickDoorwayVariant(r.Plane, speed));
                    _doorPass.Progress = Vector3.Dot(pos - _doorPass.Plane, _doorPass.Normal);
                }
            }
            Anim.Doorway = _doorPass;
        }

        DoorwayPass MakePass(DoorwayProbe.Result r, int variant)
        {
            var d = DoorwayPass.Make(variant, r.Plane, r.Normal, r.Lintel, r.Depth, Rig != null ? Rig.Height : 2.5f,
                r.HasLeft, r.Left, r.HasRight, r.Right);
            d.Progress = Vector3.Dot(transform.position - d.Plane, d.Normal);
            return d;
        }

        /// <summary>The style for the next doorway: running he grabs high and swings through (hook / lintel), walking any of
        /// them; never the one he just used.</summary>
        int PickDoorwayVariant(Vector3 plane, float speed)
        {
            int seed = ((Mathf.RoundToInt(plane.x * 7.3f) * 73856093 ^ Mathf.RoundToInt(plane.z * 7.3f) * 19349663) & 0xffffff) + _doorCount * 5 + (Id & 0xff);
            _doorCount++;
            int v;
            if (speed > Tuning.OmarWalkSpeed * 1.6f)
            {
                int[] fast = { 1, 3, 0 };
                v = fast[seed % fast.Length];
                if (v == _doorVariantLast) v = fast[(seed + 1) % fast.Length];
            }
            else
            {
                v = seed % DoorwayPass.Variants;
                if (v == _doorVariantLast) v = (v + 1 + seed / DoorwayPass.Variants % (DoorwayPass.Variants - 1)) % DoorwayPass.Variants;
            }
            _doorVariantLast = v;
            return v;
        }

        /// <summary>Admin "SEE MYSELF": the local body is drawn while the admin free camera looks at it.</summary>
        void UpdateSelfShown()
        {
            bool show = AdminFreeCam.ShowingSelf;
            if (show == _selfShown) return;
            _selfShown = show;
            if (Rig != null) Rig.SetVisible(_visible && show && !_peekHidden);
        }

        /// <summary>Our own body is drawn: a remote avatar, or the local one seen through the admin free camera.</summary>
        bool BodyDrawn => !IsLocal || _selfShown;

        /// <summary>On a human Omar's screen a prisoner peeking through a cracked door from the other side is not drawn.</summary>
        void UpdatePeekHidden()
        {
            var w = MatchWorld.Instance;
            bool hide = !IsLocal && Peeking && w != null && w.LocalIsOmar && w.LocalAvatar != null && w.PeekHides(this, w.LocalAvatar.EyePosition);
            if (hide == _peekHidden) return;
            _peekHidden = hide;
            if (Rig != null) Rig.SetVisible(_visible && BodyDrawn && !hide);
            if (_heldModel != null) _heldModel.SetActive(_visible && !hide);
        }

        /// <summary>Omar is taller than the door frames: probe the clearance above and just ahead of him and stoop.</summary>
        void UpdateDuck(Vector3 vel, float dt)
        {
            if (Time.time >= _duckProbeAt)
            {
                _duckProbeAt = Time.time + 0.06f;
                Vector3 flat = new Vector3(vel.x, 0f, vel.z);
                Vector3 dir = flat.sqrMagnitude > 0.04f ? flat.normalized : Forward;
                float top = Rig != null ? Rig.Height : 2.3f;
                float clearance = float.MaxValue;
                for (int i = 0; i < 3; i++)
                {
                    Vector3 o = transform.position + dir * (i * 0.35f) + Vector3.up * 1.2f;
                    if (Physics.Raycast(o, Vector3.up, out var hit, top, Layers.Solid & ~(1 << Layers.Door), QueryTriggerInteraction.Ignore))
                        clearance = Mathf.Min(clearance, 1.2f + hit.distance);
                }
                _duckTarget = clearance == float.MaxValue ? 0f : Mathf.Clamp01((top + 0.06f - clearance) / Tuning.OmarDuckDrop);
            }
            DuckAmount = Mathf.MoveTowards(DuckAmount, _duckTarget, dt * (_duckTarget > DuckAmount ? 4.5f : 2.2f));
        }

        // ------------------------------------------------------------------ held item & lights (third person)

        void UpdateHeldItem()
        {
            ItemType want = (_visible && (_statusPose == CharacterPose.Normal || _statusPose == CharacterPose.Trapped)) ? State.Held : ItemType.None;
            if (IsLocal && !_selfShown) want = ItemType.None; // local view model handles it (unless we look at ourselves)
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
            if (_heldType == ItemType.Lighter) ItemMeshFactory.SetLighterLid(_heldModel, lighter ? 1f : 0f);
            bool flash = !IsLocal && FlashlightOn && _heldType == ItemType.Flashlight && _visible;

            if (lighter)
            {
                var anchor = FindAnchor("Anchor_Flame");
                if (_lighterLight == null)
                {
                    _lighterLight = PsxLight.Create(anchor, Vector3.up * 0.04f, new Color(1f, 0.76f, 0.48f), 1.4f, 7f, PsxFlicker.Candle, "LighterLight");
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

        /// <summary>Where a shot leaves the revolver this avatar holds (falls back to in front of the chest).</summary>
        public Vector3 MuzzlePosition
        {
            get
            {
                var t = _heldModel != null ? FindDeep(_heldModel.transform, "Anchor_Muzzle") : null;
                return t != null ? t.position : ChestPosition + Forward * 0.45f + Vector3.up * 0.1f;
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
            bool hiddenNow = s.Life == LifeState.Free && s.Hidden;
            if (hiddenNow && !_wasHidden && !IsLocal && _visible) _hideDelay = 0.85f;
            if (!hiddenNow) _hideDelay = 0f;
            _wasHidden = hiddenNow;
            _statusPose = pose;
            if (_hideDelay > 0f) { _statusPose = CharacterPose.Normal; SetVisible(true); }
            else SetVisible(visible && pose != CharacterPose.Hidden);
            if (Hitbox != null) Hitbox.enabled = visible && s.Life == LifeState.Free && !s.Hidden;
        }

        public void SetVisible(bool v)
        {
            _visible = v;
            if (Rig != null) Rig.SetVisible(v && BodyDrawn && !_peekHidden);
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
            float vol = crouch ? 0.15f : Sprinting ? 0.8f : 0.45f;
            float maxD = crouch ? 5f : Sprinting ? 22f : 12f;
            if (MatchWorld.Instance != null && MatchWorld.Instance.LocalIsOmar) maxD *= 0.5f; // Omar barely hears them
            if (IsOmar)
            {
                // heavy, muffled, loud: carries far and through walls, nothing like a prisoner's step
                AudioManager.Play3D(AudioManager.Variant(Snd.OmarStep, 4), transform.position, 1f, Random.Range(0.86f, 0.98f), 7f, Sprinting ? 52f : 38f, AudioCategory.Omar);
            }
            else
            {
                AudioManager.Play3D(Snd.Step(surf), transform.position, vol, Random.Range(0.92f, 1.08f), 1.5f, maxD, AudioCategory.Sfx);
            }
        }
    }
}
