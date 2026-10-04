using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// The local human Omar: heavy first person movement, cleaver attacks, screams, tripwires / bear traps,
    /// sensing, searching hiding spots, smashing boarded doors, and the detection system that drives the
    /// prisoners' "find" / chase audio.
    /// </summary>
    public sealed class OmarController : MonoBehaviour
    {
        MatchWorld _w;
        Avatar _avatar;
        CharacterMotor _motor;
        FirstPersonArms _arms;
        DetectionSystem _detect;
        readonly Interactor _who = new Interactor();

        float _yaw, _pitch;
        float _stamina = 1f, _staminaDelay;
        bool _exhausted, _sprintingNow;
        float _bobPhase, _stepDist;
        float _attackCooldown, _attackPending = -1f, _swingSoundAt = -1f;
        float _screamReadyAt, _senseReadyAt;
        float _stunUntil;
        byte _teleportSeq;

        IInteractable _target;
        InteractPrompt _prompt;
        bool _hasPrompt;
        float _hold;
        bool _holdLock;

        public int TripwireCharges = Tuning.TripwireCharges;
        public int BearTrapCharges = Tuning.BearTrapCharges;

        public float Stamina => _stamina;
        public float ScreamCooldown01 => Mathf.Clamp01((_screamReadyAt - _w.Time) / Tuning.ScreamCooldown);
        public float SenseCooldown01 => Mathf.Clamp01((_senseReadyAt - _w.Time) / Tuning.SenseCooldown);
        public bool HasPrompt => _hasPrompt;
        public InteractPrompt Prompt => _prompt;
        public float HoldProgress => _prompt.HoldTime > 0 ? Mathf.Clamp01(_hold / _prompt.HoldTime) : 0f;
        public bool Waking => _w.Time < Tuning.OmarIntroSeconds;
        public float WakeRemaining => Mathf.Max(0f, Tuning.OmarIntroSeconds - _w.Time);
        public bool Stunned => _w.Time < _stunUntil;
        public DetectionSystem Detection => _detect;

        /// <summary>Authoritative trap counts from the host (Msg.TrapCharges).</summary>
        public void SetTrapCharges(int wires, int bears)
        {
            TripwireCharges = wires;
            BearTrapCharges = bears;
        }

        public static OmarController Attach(Avatar avatar, MatchWorld w)
        {
            var c = avatar.gameObject.AddComponent<OmarController>();
            c._w = w;
            c._avatar = avatar;
            c._motor = new CharacterMotor(avatar.gameObject, 0.36f, 1.95f, 1.2f);
            c._yaw = avatar.State.Yaw;
            c._detect = new DetectionSystem(w);
            var rig = PsxCameraRig.Instance;
            if (rig != null)
            {
                try
                {
                    c._arms = FirstPersonArms.Create(CharacterSkin.Omar, rig.transform);
                    if (c._arms.HandSocket != null && Avatar.FindDeep(c._arms.transform, "Cleaver") == null)
                    {
                        var cl = ItemMeshFactory.BuildCleaver();
                        cl.transform.SetParent(c._arms.HandSocket, false);
                        GeoUtil.SetLayerRecursive(cl, Layers.ViewModel);
                    }
                }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            c._who.PlayerId = avatar.Id;
            c._who.IsOmar = true;
            VhsEffect.Mode = VhsMode.Omar;
            GameInput.SetCursorLocked(true);
            return c;
        }

        void OnDestroy()
        {
            if (_arms != null) Destroy(_arms.gameObject);
        }

        public void ClearStun() => _stunUntil = 0f;

        /// <summary>Admin teleport (the host follows our position).</summary>
        public void AdminTeleport(Vector3 p)
        {
            _motor.Teleport(p, _yaw);
            _teleportSeq++;
        }

        public void Stun(float seconds)
        {
            _stunUntil = Mathf.Max(_stunUntil, _w.Time + seconds);
            AudioManager.Play2D(AudioManager.Variant(Snd.Growl, 2), 0.8f, 0.85f, AudioCategory.Omar);
            VhsEffect.TriggerGlitch(1f, 1f);
            VhsEffect.TriggerRoll(0.8f);
            _w.AddMessage("STUNNED!", seconds);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var ui = UI.UIManager.Instance;
            if (ui != null)
            {
                if (!ui.AnyModal && GameInput.PauseToggle) ui.Push(new UI.PauseScreen());
                GameInput.SetCursorLocked(!ui.AnyModal);
            }

            bool frozen = !_w.Running || Waking || Stunned || _w.Ending != null || AdminFreeCam.Active;
            var look = GameInput.Look;
            _yaw += look.x * (Stunned ? 0.3f : 1f);
            _pitch = Mathf.Clamp(_pitch - look.y, -80f, 80f);
            if (_arms != null) _arms.LookDelta = look;

            Move(dt, frozen);

            // detection (blind while waking)
            var rig = PsxCameraRig.Instance;
            Vector3 eye = _avatar.EyePosition;
            Vector3 fwd = Quaternion.Euler(_pitch, _yaw, 0) * Vector3.forward;
            if (_w.Running && _w.Ending == null) _detect.Tick(dt, eye, fwd, Waking, (id, spotted) => _w.SendDetect(id, spotted));

            if (!frozen)
            {
                HandleAttack(dt);
                HandleAbilities();
                HandleInteraction(dt);
            }
            else { _hasPrompt = false; _hold = 0; }

            Publish();
        }

        void LateUpdate()
        {
            var rig = PsxCameraRig.Instance;
            if (rig == null) return;
            Vector3 pos = _avatar.transform.position + Vector3.up * _avatar.EyeHeight;
            float amp = _sprintingNow ? 0.07f : 0.045f;
            pos += Vector3.up * (Mathf.Abs(Mathf.Sin(_bobPhase)) * amp);
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, Mathf.Sin(_bobPhase * 0.5f) * (_sprintingNow ? 2.5f : 1.2f));
            if (Stunned) rot *= Quaternion.Euler(Mathf.Sin(Time.time * 9f) * 4f, Mathf.Sin(Time.time * 6f) * 6f, 0);
            rig.transform.SetPositionAndRotation(pos, rot);
            rig.FieldOfView = Settings.FieldOfView + (_sprintingNow ? 6f : 0f);
        }

        void Move(float dt, bool frozen)
        {
            Vector2 input = frozen ? Vector2.zero : GameInput.Move;
            bool wantSprint = !frozen && GameInput.Sprint && input.y > 0.1f && !_exhausted;
            float speed = (wantSprint ? Tuning.OmarRunSpeed : Tuning.OmarWalkSpeed) * AdminState.SpeedMultiplier;
            if (AdminState.InfiniteStamina) { _stamina = 1f; _exhausted = false; }
            if (AdminState.Noclip && !frozen)
            {
                _motor.Controller.enabled = false;
                Vector3 fv = Quaternion.Euler(_pitch, _yaw, 0) * new Vector3(input.x, 0, input.y);
                if (Input.GetKey(KeyCode.Space)) fv += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl)) fv += Vector3.down;
                _avatar.transform.position += fv * Mathf.Max(speed, 4f) * 1.5f * dt;
                _avatar.transform.rotation = Quaternion.Euler(0, _yaw, 0);
                return;
            }
            if (!_motor.Controller.enabled) _motor.Controller.enabled = true;
            _sprintingNow = wantSprint && input.sqrMagnitude > 0.01f;
            if (_sprintingNow)
            {
                _stamina -= dt / Tuning.OmarStaminaSeconds;
                _staminaDelay = 1.5f;
                if (_stamina <= 0f)
                {
                    _stamina = 0f; _exhausted = true;
                    AudioManager.Play2D(AudioManager.Variant(Snd.Growl, 2), 0.5f, 1.1f, AudioCategory.Omar);
                }
            }
            else
            {
                _staminaDelay -= dt;
                if (_staminaDelay <= 0f) _stamina = Mathf.Min(1f, _stamina + 0.2f * dt);
                if (_exhausted && _stamina > 0.4f) _exhausted = false;
            }
            Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
            float moved = _motor.Move(yawRot * new Vector3(input.x, 0, input.y) * speed, dt);
            _avatar.transform.rotation = yawRot;
            // doors give way the moment we walk into them (the host runs the real swing; we predict it)
            if (!_w.IsHost && moved > 0.2f)
            {
                Vector3 p = _avatar.transform.position;
                Vector3 v = _motor.Velocity; v.y = 0f;
                foreach (var d in _w.Doors)
                    if ((d.Info.Center - p).sqrMagnitude < 9f && d.Shove(p, v)) d.PredictUntil = Time.time + 0.5f;
            }
            if (_motor.Grounded && moved > 0.2f)
            {
                float stride = _sprintingNow ? 1.25f : 0.9f;
                _stepDist += moved * dt;
                _bobPhase += moved * dt / stride * Mathf.PI;
                if (_stepDist >= stride)
                {
                    _stepDist = 0f;
                    AudioManager.Play2D(AudioManager.Variant(Snd.OmarStep, 4), _sprintingNow ? 0.75f : 0.55f, Random.Range(0.88f, 1.0f), AudioCategory.Omar);
                }
            }
            if (_arms != null) { _arms.MoveSpeed = moved; _arms.Sprinting = _sprintingNow; }
        }

        // ------------------------------------------------------------------ attack

        void HandleAttack(float dt)
        {
            _attackCooldown -= dt;
            if (GameInput.PrimaryDown && _attackCooldown <= 0f)
            {
                _attackCooldown = Tuning.AttackCooldown;
                _attackPending = Tuning.AttackWindup;
                _arms?.Play(CharacterAction.Attack);
                _w.SendAction(CharacterAction.Attack);
                AudioManager.Play2D(AudioManager.Variant(Snd.OmarWindup, 2), 0.8f, Random.Range(0.92f, 1.04f), AudioCategory.Omar);
                _swingSoundAt = Time.time + 0.45f;
            }
            if (_swingSoundAt > 0f && Time.time >= _swingSoundAt)
            {
                _swingSoundAt = -1f;
                AudioManager.Play2D(AudioManager.Variant(Snd.CleaverSwing, 2), 0.85f, Random.Range(0.9f, 1.05f), AudioCategory.Omar);
            }
            if (_attackPending >= 0f)
            {
                _attackPending -= dt;
                if (_attackPending < 0f) ResolveAttack();
            }
        }

        void ResolveAttack()
        {
            Vector3 eye = _avatar.EyePosition;
            Vector3 fwd = Quaternion.Euler(Mathf.Clamp(_pitch, -30f, 30f), _yaw, 0) * Vector3.forward;
            int best = -1; float bestScore = float.MaxValue;
            foreach (var kv in _w.Avatars)
            {
                var a = kv.Value;
                if (a == null || a.IsOmar || !a.Visible) continue;
                var st = _w.StatusOf(a.Id);
                if (st == null || st.Life != LifeState.Free || st.Hidden) continue;
                Vector3 to = a.ChestPosition - eye;
                float d = to.magnitude;
                if (d > Tuning.AttackRange + 0.4f) continue;
                float ang = Vector3.Angle(fwd, to);
                if (ang > 55f) continue;
                if (Physics.Linecast(eye, a.ChestPosition, Layers.SightBlockers, QueryTriggerInteraction.Ignore)) continue;
                float score = d + ang * 0.02f;
                if (score < bestScore) { bestScore = score; best = a.Id; }
            }
            if (best >= 0)
            {
                _w.SendAttack(best);
                VhsEffect.TriggerGlitch(0.4f, 0.2f);
            }
            else if (Physics.Raycast(eye, fwd, out var hit, Tuning.AttackRange, Layers.Solid, QueryTriggerInteraction.Ignore))
            {
                AudioManager.Play3D(Snd.CleaverHitWall, hit.point, 0.8f, Random.Range(0.9f, 1.1f), 1f, 15f, AudioCategory.Omar);
                try { PsxFx.Sparks(hit.point, hit.normal); } catch { }
            }
        }

        // ------------------------------------------------------------------ abilities

        void HandleAbilities()
        {
            if (GameInput.SecondaryDown && _w.Time >= _screamReadyAt)
            {
                _screamReadyAt = _w.Time + Tuning.ScreamCooldown;
                _arms?.Play(CharacterAction.Scream);
                _w.SendScream();
            }
            if (GameInput.Ability1Down) PlaceTripwire();
            if (GameInput.Ability2Down) PlaceBearTrap();
            if (GameInput.Ability3Down && _w.Time >= _senseReadyAt)
            {
                _senseReadyAt = _w.Time + Tuning.SenseCooldown;
                AudioManager.Play2D(AudioManager.Variant(Snd.Growl, 2), 0.6f, 0.9f, AudioCategory.Omar);
                VhsEffect.TriggerGlitch(0.5f, 0.4f);
                int n = 0;
                foreach (var kv in _w.Avatars)
                {
                    var a = kv.Value;
                    if (a == null || a.IsOmar) continue;
                    var st = _w.StatusOf(a.Id);
                    if (st == null || st.Life != LifeState.Free) continue;
                    if (Vector3.Distance(a.Position, _avatar.Position) > Tuning.SenseRadius) continue;
                    _w.Pings.Add(new OmarPing { Position = a.Position + Vector3.up, Expire = Time.time + 3.5f, Kind = 3 });
                    n++;
                }
                _w.AddMessage(n > 0 ? "YOU SMELL THEM (" + n + ")" : "NOTHING NEARBY", 2.5f);
            }
        }

        bool FloorPoint(float maxDist, out Vector3 p)
        {
            var rig = PsxCameraRig.Instance;
            p = Vector3.zero;
            if (rig == null) return false;
            var ray = new Ray(rig.transform.position, rig.transform.forward);
            if (Physics.Raycast(ray, out var hit, maxDist, Layers.Solid, QueryTriggerInteraction.Ignore) && hit.normal.y > 0.7f)
            {
                p = hit.point;
                return true;
            }
            // aim not on the floor: drop down from a point in front of us
            Vector3 front = _avatar.Position + Quaternion.Euler(0, _yaw, 0) * Vector3.forward * 1.5f + Vector3.up;
            if (Physics.Raycast(front, Vector3.down, out hit, 2f, Layers.Solid, QueryTriggerInteraction.Ignore))
            {
                p = hit.point;
                return true;
            }
            return false;
        }

        void PlaceTripwire()
        {
            if (TripwireCharges <= 0) { _w.AddMessage("NO WIRE LEFT (RECHARGING)", 2f); return; }
            if (!FloorPoint(3.5f, out var p)) { _w.AddMessage("AIM AT THE FLOOR OF A DOORWAY OR PASSAGE", 2.5f); return; }
            Vector3 right = Quaternion.Euler(0, _yaw, 0) * Vector3.right;
            Vector3 o = p + Vector3.up * 0.12f;
            float maxSide = 1.8f;
            bool hitR = Physics.Raycast(o, right, out var hr, maxSide, Layers.Solid, QueryTriggerInteraction.Ignore);
            bool hitL = Physics.Raycast(o, -right, out var hl, maxSide, Layers.Solid, QueryTriggerInteraction.Ignore);
            if (!hitR || !hitL) { _w.AddMessage("TOO WIDE - STRING IT ACROSS A DOORWAY OR NARROW PATH", 2.5f); return; }
            Vector3 a = hl.point + right * 0.03f, b = hr.point - right * 0.03f;
            if ((b - a).magnitude < 0.5f) { _w.AddMessage("TOO NARROW", 2f); return; }
            TripwireCharges--;
            _w.SendTrapPlace(TrapKind.Tripwire, a, b);
            _arms?.Play(CharacterAction.PlaceTrap);
            _w.SendAction(CharacterAction.PlaceTrap);
        }

        void PlaceBearTrap()
        {
            if (BearTrapCharges <= 0) { _w.AddMessage("NO TRAPS LEFT (RECHARGING)", 2f); return; }
            if (!FloorPoint(2.8f, out var p)) { _w.AddMessage("AIM AT THE FLOOR", 2f); return; }
            BearTrapCharges--;
            _w.SendTrapPlace(TrapKind.BearTrap, p, p);
            _arms?.Play(CharacterAction.PlaceTrap);
            _w.SendAction(CharacterAction.PlaceTrap);
        }

        // ------------------------------------------------------------------ interaction

        /// <summary>Closest interactable along the ray; trigger volumes up to 0.6 m behind the first solid hit
        /// still count (interaction volumes often sit inside furniture).</summary>
        static IInteractable PickInteractable(RaycastHit[] hits)
        {
            float block = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.distance > block + 0.6f) break;
                var it = InteractableRef.From(h.collider);
                if (it != null && (!h.collider.isTrigger || h.distance <= block + 0.6f)) return it;
                if (!h.collider.isTrigger && block == float.MaxValue) block = h.distance;
            }
            return null;
        }

        void HandleInteraction(float dt)
        {
            _hasPrompt = false;
            if (GameInput.GameplayBlocked) { _hold = 0; return; }
            var rig = PsxCameraRig.Instance;
            IInteractable target = null;
            if (rig != null)
            {
                var hits = Physics.RaycastAll(new Ray(rig.transform.position, rig.transform.forward), Tuning.InteractRange + 0.3f, Layers.InteractRay, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                target = PickInteractable(hits);
            }
            if (target != _target) { _target = target; _hold = 0; }
            if (_target == null) { _holdLock = false; return; }
            _who.Status = _w.StatusOf(_avatar.Id);
            _who.Position = _avatar.Position;
            if (!_target.GetPrompt(_who, out _prompt)) return;
            _hasPrompt = true;
            if (!_prompt.Enabled) return;
            if (_prompt.HoldTime <= 0f)
            {
                if (GameInput.InteractDown)
                {
                    _target.Interact(_who);
                    bool chop = _prompt.Text == "CHOP MEAT";
                    _arms?.Play(chop ? CharacterAction.Attack : CharacterAction.Interact);
                    if (chop) AudioManager.Play2D(AudioManager.Variant(Snd.CleaverSwing, 2), 0.7f, Random.Range(0.9f, 1.05f), AudioCategory.Omar);
                }
                return;
            }
            if (!GameInput.InteractHeld) { _hold = 0; _holdLock = false; return; }
            if (_holdLock) return;
            if (_hold == 0f) { _arms?.Play(CharacterAction.Search); _w.SendAction(CharacterAction.Search); }
            _hold += dt;
            if (_hold >= _prompt.HoldTime)
            {
                _target.Interact(_who);
                _hold = 0; _holdLock = true;
            }
        }

        void Publish()
        {
            var s = _avatar.State;
            s.Position = _avatar.transform.position;
            s.Yaw = _yaw;
            s.Pitch = _pitch;
            AvatarFlags f = AvatarFlags.Grounded;
            if (_sprintingNow) f |= AvatarFlags.Sprint;
            if (Stunned) f |= AvatarFlags.Stunned;
            s.Flags = f;
            s.Held = ItemType.None;
            s.TeleportSeq = _teleportSeq;
            _avatar.SetLocalState(s);
        }
    }
}
