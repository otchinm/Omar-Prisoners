using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// The local prisoner: first person movement (walk / sprint / crouch), stamina, noise, items, interaction,
    /// traps, hiding, cages, car seats and all the first-person horror feedback.
    /// </summary>
    public sealed class PrisonerController : MonoBehaviour
    {
        MatchWorld _w;
        Avatar _avatar;
        CharacterMotor _motor;
        FirstPersonArms _arms;
        readonly Interactor _who = new Interactor();

        float _yaw, _pitch;
        bool _crouch;
        float _stamina = 1f;
        float _staminaDelay;
        bool _exhausted;
        float _stepDistance;
        float _bobPhase;
        float _noiseSmoothed;
        float _noiseSendTimer;
        float _painkillersUntil;
        float _terrorUntil;
        float _airTime;
        byte _teleportSeq;

        // lights in the first-person hand
        bool _lighterOn, _flashOn;
        PsxLight _lighterLight, _flashLight, _flashFill;
        GameObject _flame;
        AudioSource _flameLoop;
        ItemType _shownItem = (ItemType)255;
        ItemType _actionItem = ItemType.None;

        // interaction
        IInteractable _target;
        InteractPrompt _prompt;
        bool _hasPrompt;
        float _hold;
        bool _holdLock;
        float _holdNoiseTimer;
        // item use with hold (bandages, fuel)
        float _useHold;
        ItemType _useHoldItem;
        // door dragging (hold LMB on a leaf, move the mouse)
        DoorEntity _drag;
        Vector3 _dragLocal;
        Vector3 _targetPoint;
        float _dragVel, _dragSendTimer;
        bool _lmbOnDoor;
        static readonly int DragBlockMask = Layers.Mask(Layers.Player);

        // states
        Vector3 _lastFeet;
        readonly System.Collections.Generic.HashSet<int> _trapsReported = new System.Collections.Generic.HashSet<int>();
        Pose _seatLocal;
        bool _seated;
        float _captureFx;
        float _hitFx;
        float _bloodDist;
        float _lastBreath;

        public float Stamina => _stamina;
        public float NoiseLevel => _noiseSmoothed;
        public bool HasPrompt => _hasPrompt;
        public InteractPrompt Prompt => _prompt;
        public float HoldProgress => _prompt.HoldTime > 0 ? Mathf.Clamp01(_hold / _prompt.HoldTime) : 0f;
        public float UseHoldProgress => _useHoldItem != ItemType.None ? Mathf.Clamp01(_useHold / UseHoldTime(_useHoldItem)) : 0f;
        public ItemType UseHoldItem => _useHoldItem;
        public bool Crouching => _crouch;
        public bool PainkillersActive => Time.time < _painkillersUntil;
        public bool DraggingDoor => _drag != null;

        public static PrisonerController Attach(Avatar avatar, MatchWorld w)
        {
            var c = avatar.gameObject.AddComponent<PrisonerController>();
            c._w = w;
            c._avatar = avatar;
            c._motor = new CharacterMotor(avatar.gameObject, 0.3f, 1.75f, 1.05f);
            c._yaw = avatar.State.Yaw;
            c._lastFeet = avatar.transform.position;
            var rig = PsxCameraRig.Instance;
            if (rig != null)
            {
                try { c._arms = FirstPersonArms.Create(avatar.Info.Skin, rig.transform); } catch (System.Exception e) { Debug.LogException(e); }
            }
            c._who.PlayerId = avatar.Id;
            c._who.Inventory = w.Inventory;
            VhsEffect.Mode = VhsMode.Prisoner;
            GameInput.SetCursorLocked(true);
            return c;
        }

        void OnDestroy()
        {
            EndDrag();
            if (_arms != null) Destroy(_arms.gameObject);
            DestroyHandLights();
        }

        PlayerStatus Status => _w.StatusOf(_avatar.Id);

        // ================================================================== update

        void Update()
        {
            var st = Status;
            if (st == null) return;
            float dt = Time.deltaTime;
            bool active = st.Life == LifeState.Free || st.Life == LifeState.Caged;
            if (!active)
            {
                EndDrag();
                if (_arms != null) _arms.SetVisible(false);
                SetHandLights(false, false);
                // let the capture / death static fade out, clear the rest
                _captureFx = Mathf.MoveTowards(_captureFx, 0f, dt * 0.6f);
                VhsEffect.StaticOverride = Mathf.Clamp01(_captureFx * 1.4f);
                if (_captureFx <= 0f) AudioManager.SetDistortion(0f);
                VhsEffect.Damage = 0f;
                VhsEffect.Hiding = 0f;
                AudioManager.SetMuffle(0f);
                return;
            }

            HandleMenus();
            HandleDoorDrag(st, dt);
            Look(st);
            bool canMove = _w.Running && st.Life != LifeState.Dead && !st.Hidden && !st.Trapped && !st.InCar && _captureFx <= 0f;
            Move(st, dt, canMove);
            HandleItems(st, dt);
            HandleInteraction(st, dt);
            CheckTraps(st);
            UpdateHandVisuals(st);
            UpdateEffects(st, dt);
            PublishState(st);
        }

        void LateUpdate()
        {
            var st = Status;
            if (st == null) return;
            if (st.Life != LifeState.Free && st.Life != LifeState.Caged) return;
            PlaceCamera(st);
        }

        void HandleMenus()
        {
            var ui = UI.UIManager.Instance;
            if (ui == null) return;
            if (!ui.AnyModal && GameInput.InventoryToggle) ui.Push(new UI.InventoryScreen(_w));
            else if (!ui.AnyModal && GameInput.PauseToggle) ui.Push(new UI.PauseScreen());
            GameInput.SetCursorLocked(!ui.AnyModal);
        }

        // ================================================================== look & move

        void Look(PlayerStatus st)
        {
            var d = GameInput.Look;
            if (_drag != null) d = Vector2.zero; // the mouse moves the door, not the view
            _yaw += d.x;
            _pitch = Mathf.Clamp(_pitch - d.y, -85f, 85f);
            if (st.Hidden && st.HidingSpot < _w.Hiding.Length)
            {
                float baseYaw = _w.Hiding[st.HidingSpot].Info.HiddenView.rotation.eulerAngles.y;
                _yaw = baseYaw + Mathf.Clamp(Mathf.DeltaAngle(baseYaw, _yaw), -65f, 65f);
                _pitch = Mathf.Clamp(_pitch, -35f, 40f);
            }
            if (_arms != null) _arms.LookDelta = d;
        }

        void Move(PlayerStatus st, float dt, bool canMove)
        {
            if (GameInput.CrouchPressed && canMove)
            {
                if (_crouch && !_motor.CanStand()) { /* stay down */ }
                else _crouch = !_crouch;
            }
            _motor.SetCrouched(_crouch, dt);
            if (st.InCar && _seated && _w.Map.Car != null && _w.Map.Car.Root != null)
            {
                // ride along (the controller is disabled while seated)
                _avatar.transform.position = _w.Map.Car.Root.TransformPoint(_seatLocal.position) - Vector3.up * 1.0f;
            }

            Vector2 input = canMove ? GameInput.Move : Vector2.zero;
            bool injured = st.Injured && !PainkillersActive;
            bool wantSprint = canMove && GameInput.Sprint && input.y > 0.1f && !_crouch && !_exhausted;
            float speed = _crouch ? Tuning.CrouchSpeed : wantSprint ? Tuning.RunSpeed : Tuning.WalkSpeed;
            if (injured) speed *= Tuning.InjuredSpeedMul;
            if (st.Life == LifeState.Caged) speed = Mathf.Min(speed, 1.2f);
            var held = _w.Inventory.HeldType;
            if (held == ItemType.GasCan || held == ItemType.CarBattery) speed *= 0.88f;

            // stamina
            bool sprinting = wantSprint && input.sqrMagnitude > 0.01f;
            if (sprinting)
            {
                _stamina -= dt / (Tuning.StaminaSeconds * (PainkillersActive ? 1.8f : 1f));
                _staminaDelay = Tuning.StaminaRegenDelay;
                if (_stamina <= 0f) { _stamina = 0f; _exhausted = true; BreathHeavy(); }
            }
            else
            {
                _staminaDelay -= dt;
                if (_staminaDelay <= 0f) _stamina = Mathf.Min(1f, _stamina + Tuning.StaminaRegenRate * dt);
                if (_exhausted && _stamina > 0.35f) _exhausted = false;
            }

            Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
            Vector3 wish = yawRot * new Vector3(input.x, 0, input.y) * speed;
            if (!_motor.Controller.enabled) wish = Vector3.zero;
            float moved = _motor.Move(wish, dt);
            _avatar.transform.rotation = yawRot;

            // falling / landing noise
            if (!_motor.Grounded) _airTime += dt;
            else
            {
                if (_airTime > 0.45f) { PlayStep(1.2f); MakeNoise(8f); }
                _airTime = 0f;
            }

            // footsteps
            if (_motor.Grounded && moved > 0.2f)
            {
                _stepDistance += moved * dt;
                float stride = _crouch ? 0.62f : sprinting ? 1.05f : 0.78f;
                _bobPhase += moved * dt / stride * Mathf.PI;
                if (_stepDistance >= stride)
                {
                    _stepDistance = 0f;
                    float vol = _crouch ? 0.12f : sprinting ? 0.6f : 0.32f;
                    PlayStep(vol);
                    float noise = _crouch ? 1.2f : sprinting ? 14f : 5f;
                    if (injured) noise += 2f;
                    if (held == ItemType.GasCan && sprinting) noise += 3f;
                    MakeNoise(noise);
                }
            }
            float instantNoise = moved < 0.2f ? 0f : _crouch ? 1.2f : sprinting ? 14f : 5f;
            _noiseSmoothed = Mathf.MoveTowards(_noiseSmoothed, instantNoise / 14f, dt * 2.5f);

            if (_arms != null)
            {
                _arms.MoveSpeed = moved;
                _arms.Sprinting = sprinting;
                _arms.Crouching = _crouch;
            }
            _sprintingNow = sprinting;
        }

        bool _sprintingNow;

        void PlayStep(float volume)
        {
            AudioManager.Play2D(Snd.Step(_motor.GroundSurface), volume, Random.Range(0.92f, 1.08f));
        }

        void BreathHeavy()
        {
            if (Time.time - _lastBreath < 3f) return;
            _lastBreath = Time.time;
            AudioManager.Play2D(AudioManager.Variant(Snd.BreathHeavy, 3), 0.55f);
            MakeNoise(4f);
        }

        /// <summary>Report noise to the host (Omar hears it). Throttled.</summary>
        public void MakeNoise(float radius)
        {
            if (radius < 2.5f) return;
            _noiseSendTimer -= Time.deltaTime;
            if (_noiseSendTimer > 0f && radius < 10f) return;
            _noiseSendTimer = 0.3f;
            _w.SendNoise(_avatar.Position + Vector3.up, radius);
        }

        // ================================================================== camera

        void PlaceCamera(PlayerStatus st)
        {
            var rig = PsxCameraRig.Instance;
            if (rig == null) return;
            Vector3 pos;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0);
            if (st.Hidden && st.HidingSpot < _w.Hiding.Length)
            {
                pos = _w.Hiding[st.HidingSpot].Info.HiddenView.position;
            }
            else if (st.InCar && _seated && _w.Map.Car != null && _w.Map.Car.Root != null)
            {
                var car = _w.Map.Car.Root;
                pos = car.TransformPoint(_seatLocal.position);
                float carYaw = car.rotation.eulerAngles.y;
                rot = Quaternion.Euler(_pitch, carYaw + Mathf.Clamp(Mathf.DeltaAngle(carYaw, _yaw), -110f, 110f), 0);
            }
            else
            {
                float eye = Mathf.Lerp(Tuning.CrouchEyeHeight, Tuning.EyeHeight, Mathf.InverseLerp(1.05f, 1.75f, _motor.Height));
                pos = _avatar.transform.position + Vector3.up * eye;
                float bobAmp = _sprintingNow ? 0.05f : 0.028f;
                if (st.Injured && !PainkillersActive) bobAmp *= 1.5f;
                pos += Vector3.up * (Mathf.Abs(Mathf.Sin(_bobPhase)) * bobAmp) + (rot * Vector3.right) * (Mathf.Sin(_bobPhase * 0.5f) * bobAmp * 0.5f);
                if (_hitFx > 0f) pos += Random.insideUnitSphere * 0.04f * _hitFx;
                if (st.Trapped) pos += Vector3.down * 0.5f;
            }
            float roll = 0f;
            if (st.Injured && !PainkillersActive) roll = Mathf.Sin(Time.time * 0.7f) * 2.2f;
            if (_captureFx > 0f) roll += Mathf.Sin(Time.time * 13f) * 8f * _captureFx;
            rig.transform.SetPositionAndRotation(pos, rot * Quaternion.Euler(0, 0, roll));
            rig.FieldOfView = Settings.FieldOfView + (_sprintingNow ? 5f : 0f) + (Time.time < _terrorUntil ? 6f : 0f);
        }

        // ================================================================== items

        static float UseHoldTime(ItemType t) => t == ItemType.Bandages ? 2.5f : t == ItemType.LighterFuel ? 1.5f : t == ItemType.Batteries ? 1.2f : 0.5f;

        void HandleItems(PlayerStatus st, float dt)
        {
            var inv = _w.Inventory;
            int slot = GameInput.SlotPressed;
            if (slot >= 0) SelectSlot(slot);
            float scroll = GameInput.Scroll;
            if (scroll > 0.1f) SelectSlot((inv.Selected + 2) % 3);
            else if (scroll < -0.1f) SelectSlot((inv.Selected + 1) % 3);

            var held = inv.Held;
            ItemType ht = held != null ? held.Type : ItemType.None;
            bool hidden = st.Hidden;

            // drain lights
            if (_lighterOn)
            {
                var l = ItemOfType(ItemType.Lighter);
                if (l == null || ht != ItemType.Lighter || hidden) _lighterOn = false;
                else
                {
                    l.Charge = Mathf.Max(0f, l.Charge - dt / Tuning.LighterBurnSeconds);
                    if (l.Charge <= 0f) { _lighterOn = false; AudioManager.Play2D(Snd.LighterClose, 0.6f); _w.AddMessage("THE LIGHTER IS OUT OF FUEL", 3f); }
                }
            }
            if (_flashOn)
            {
                var f = ItemOfType(ItemType.Flashlight);
                if (f == null || ht != ItemType.Flashlight || hidden) _flashOn = false;
                else
                {
                    f.Charge = Mathf.Max(0f, f.Charge - dt / Tuning.FlashlightBurnSeconds);
                    if (f.Charge <= 0f) { _flashOn = false; AudioManager.Play2D(Snd.FlashlightClick, 0.6f); _w.AddMessage("THE BATTERIES ARE DEAD", 3f); }
                }
            }

            if (held == null || !_w.Running) { _useHoldItem = ItemType.None; return; }

            // hold-to-use consumables
            if (_useHoldItem != ItemType.None)
            {
                if ((Input.GetKey(KeyCode.F) || (Input.GetMouseButton(0) && _drag == null)) && !GameInput.GameplayBlocked && ht == _useHoldItem)
                {
                    _useHold += dt;
                    if (_useHold >= UseHoldTime(_useHoldItem))
                    {
                        _w.SendUse(UseTarget.Self, 0, held.Id, held.Charge);
                        _useHoldItem = ItemType.None;
                    }
                }
                else _useHoldItem = ItemType.None;
                return;
            }

            if (GameInput.DropDown && !hidden) { Drop(held); return; }
            bool useDown = GameInput.ToggleItemDown || (GameInput.PrimaryDown && !_lmbOnDoor && _drag == null);
            if (!useDown || hidden) return;

            switch (ht)
            {
                case ItemType.Lighter:
                    if (held.Charge <= 0.001f) { AudioManager.Play2D(Snd.LighterFlick, 0.5f); _w.AddMessage("NO FUEL", 2f); break; }
                    _lighterOn = !_lighterOn;
                    if (_lighterOn) { AudioManager.Play2D(Snd.LighterOpen, 0.6f); AudioManager.Play2D(Snd.LighterFlick, 0.7f, Random.Range(0.95f, 1.05f)); MakeNoise(3f); }
                    else AudioManager.Play2D(Snd.LighterClose, 0.6f);
                    break;
                case ItemType.Flashlight:
                    if (held.Charge <= 0.001f) { AudioManager.Play2D(Snd.FlashlightClick, 0.5f); _w.AddMessage("THE BATTERIES ARE DEAD", 2f); break; }
                    _flashOn = !_flashOn;
                    AudioManager.Play2D(Snd.FlashlightClick, 0.7f);
                    break;
                case ItemType.Bandages:
                    if (!st.Injured) { _w.AddMessage("I'M NOT BLEEDING. NOT YET.", 2.5f); break; }
                    StartUseHold(ItemType.Bandages);
                    break;
                case ItemType.Pills:
                    _w.SendUse(UseTarget.Self, 0, held.Id);
                    break;
                case ItemType.LighterFuel:
                    {
                        var l = ItemOfType(ItemType.Lighter);
                        if (l == null) { _w.AddMessage("I DON'T HAVE A LIGHTER TO REFILL", 2.5f); break; }
                        if (l.Charge > 0.95f) { _w.AddMessage("THE LIGHTER IS ALREADY FULL", 2.5f); break; }
                        StartUseHold(ItemType.LighterFuel);
                        break;
                    }
                case ItemType.Batteries:
                    {
                        var f = ItemOfType(ItemType.Flashlight);
                        if (f == null) { _w.AddMessage("THESE FIT A FLASHLIGHT", 2.5f); break; }
                        StartUseHold(ItemType.Batteries);
                        break;
                    }
                case ItemType.Bottle:
                    Throw(held);
                    break;
                case ItemType.SoundMeter:
                    AudioManager.Play2D(Snd.SoundMeterTick, 0.4f);
                    _w.AddMessage("THE NEEDLE SHOWS HOW MUCH NOISE I MAKE", 2.5f);
                    break;
                default:
                    _w.AddMessage("I NEED TO USE THIS ON SOMETHING (E)", 2.5f);
                    break;
            }
        }

        void StartUseHold(ItemType t)
        {
            _useHoldItem = t;
            _useHold = 0f;
            _arms?.Play(t == ItemType.Bandages ? CharacterAction.Heal : t == ItemType.LighterFuel ? CharacterAction.Pour : CharacterAction.UseItem);
            _w.SendAction(t == ItemType.Bandages ? CharacterAction.Heal : CharacterAction.UseItem);
            if (t == ItemType.Bandages) AudioManager.Play2D(Snd.BandageRip, 0.7f);
            else if (t == ItemType.LighterFuel) AudioManager.Play2D(Snd.FuelPour, 0.5f, 1.3f);
            else AudioManager.Play2D(Snd.BatteryInsert, 0.6f);
        }

        void SelectSlot(int slot)
        {
            var inv = _w.Inventory;
            if (slot == inv.Selected) return;
            inv.Select(slot);
            _useHoldItem = ItemType.None;
            AudioManager.Play2D(Snd.ItemEquip, 0.4f);
        }

        ItemEntity ItemOfType(ItemType t) => _w.GetItem(_w.Inventory.Find(t));

        void Drop(ItemEntity it)
        {
            Vector3 from = _avatar.Position + Vector3.up * 1.2f;
            Vector3 dir = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 p = from + dir * 0.7f;
            if (Physics.Raycast(from, dir, out var wallHit, 0.8f, Layers.Solid, QueryTriggerInteraction.Ignore)) p = from + dir * Mathf.Max(0.1f, wallHit.distance - 0.25f);
            if (Physics.Raycast(p, Vector3.down, out var hit, 3f, Layers.Solid, QueryTriggerInteraction.Ignore)) p = hit.point;
            else p = _avatar.Position;
            if (it.Type == ItemType.Lighter) _lighterOn = false;
            if (it.Type == ItemType.Flashlight) _flashOn = false;
            _w.SendDrop(it.Id, p, _yaw, it.Charge);
            _arms?.Play(CharacterAction.Pickup);
            MakeNoise(it.Def.DropNoise);
        }

        void Throw(ItemEntity it)
        {
            var rig = PsxCameraRig.Instance;
            Vector3 from = rig != null ? rig.transform.position + rig.transform.forward * 0.4f : _avatar.EyePosition;
            Vector3 dir = rig != null ? rig.transform.forward : Quaternion.Euler(_pitch, _yaw, 0) * Vector3.forward;
            Vector3 vel = (dir + Vector3.up * 0.18f).normalized * 13f;
            _w.SendThrow(it.Id, from, vel);
            ThrownBottle.Spawn(from, vel, it.Id, _w.transform);
            _arms?.Play(CharacterAction.Throw);
            _w.SendAction(CharacterAction.Throw);
            AudioManager.Play2D(Snd.BottleThrow, 0.6f);
        }

        /// <summary>The host consumed one of our items (used / thrown / broken).</summary>
        public void OnItemConsumed(ItemEntity it, byte how)
        {
            switch (it.Type)
            {
                case ItemType.Pills:
                    _painkillersUntil = Time.time + 60f;
                    _stamina = 1f; _exhausted = false;
                    AudioManager.Play2D(Snd.Pills, 0.7f);
                    _w.AddMessage("THE PAIN FADES. FOR NOW.", 3f);
                    break;
                case ItemType.Bandages:
                    _w.AddMessage("THE BLEEDING STOPPED", 3f);
                    break;
                case ItemType.LighterFuel:
                    if (how == 0) _w.AddMessage("LIGHTER REFILLED", 2f);
                    break;
                case ItemType.Batteries:
                    _w.AddMessage("FRESH BATTERIES", 2f);
                    break;
                case ItemType.Crowbar:
                    if (how == 1) _w.AddMessage("THE CROWBAR IS GONE", 3f);
                    break;
            }
        }

        // ================================================================== interaction

        /// <summary>Closest interactable along the ray; trigger volumes up to 0.6 m behind the first solid hit
        /// still count (interaction volumes often sit inside furniture).</summary>
        static IInteractable PickInteractable(RaycastHit[] hits, out Vector3 point)
        {
            float block = float.MaxValue;
            point = Vector3.zero;
            foreach (var h in hits)
            {
                if (h.distance > block + 0.6f) break;
                var it = InteractableRef.From(h.collider);
                if (it != null && (!h.collider.isTrigger || h.distance <= block + 0.6f)) { point = h.point; return it; }
                if (!h.collider.isTrigger && block == float.MaxValue) block = h.distance;
            }
            return null;
        }

        void HandleInteraction(PlayerStatus st, float dt)
        {
            _who.Status = st;
            _who.Position = _avatar.Position;
            _who.Crouching = _crouch;
            _hasPrompt = false;
            _prompt = default;
            if (!_w.Running || GameInput.GameplayBlocked) { _hold = 0; return; }

            // special states first
            if (st.Life == LifeState.Caged)
            {
                _hasPrompt = true;
                _prompt = InteractPrompt.Press("STRUGGLE WITH THE LOCK (MASH E)");
                if (GameInput.InteractDown) _w.SendStruggle(0);
                _target = null;
                return;
            }
            if (st.Trapped)
            {
                _hasPrompt = true;
                _prompt = InteractPrompt.Press("PULL FREE OF THE TRAP (MASH E)");
                if (GameInput.InteractDown) _w.SendStruggle(1);
                _target = null;
                return;
            }
            if (st.Hidden)
            {
                _hasPrompt = true;
                _prompt = InteractPrompt.Press("LEAVE THE HIDING SPOT");
                if (GameInput.InteractDown) _w.SendUse(UseTarget.Hiding, st.HidingSpot, -1);
                _target = null;
                return;
            }

            // holding a door: the hand is busy
            if (_drag != null) { _target = _drag; _hold = 0; return; }

            // look-at target
            IInteractable target = null;
            var rig = PsxCameraRig.Instance;
            if (rig != null)
            {
                var ray = new Ray(rig.transform.position, rig.transform.forward);
                var hits = Physics.RaycastAll(ray, Tuning.InteractRange, Layers.InteractRay, QueryTriggerInteraction.Collide);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                target = PickInteractable(hits, out _targetPoint);
            }
            // in a car: the car itself (we look around from inside)
            if (st.InCar)
            {
                foreach (var o in _w.ObjectiveTargets) if (o.Kind == ObjectiveKind.CarDriver) target = o;
            }

            if (target != _target) { _target = target; _hold = 0; }
            if (_target == null) { _holdLock = false; return; }
            if (!_target.GetPrompt(_who, out _prompt)) { _hold = 0; return; }
            _hasPrompt = true;
            if (!_prompt.Enabled) { _hold = 0; return; }

            if (_prompt.HoldTime <= 0f)
            {
                if (GameInput.InteractDown && !_prompt.IsDrag)
                {
                    _target.Interact(_who);
                    _arms?.Play(CharacterAction.Interact);
                }
                return;
            }

            if (!GameInput.InteractHeld) { _hold = 0; _holdLock = false; _actionItem = ItemType.None; return; }
            if (_holdLock) return;
            if (_hold == 0f)
            {
                _arms?.Play(CharacterAction.Interact);
                _w.SendAction(_prompt.UsesItem == ItemType.BoltCutters ? CharacterAction.Cut : _prompt.UsesItem == ItemType.GasCan || _prompt.UsesItem == ItemType.LighterFuel ? CharacterAction.Pour : CharacterAction.Interact);
                if (_prompt.UsesItem == ItemType.Lockpick) AudioManager.Play2D(Snd.Lockpick, 0.6f);
                if (_prompt.UsesItem == ItemType.Crowbar) AudioManager.Play2D(Snd.CrowbarPry, 0.7f);
                if (_prompt.UsesItem == ItemType.GasCan) AudioManager.Play2D(Snd.GasPour, 0.6f);
                if (_prompt.UsesItem == ItemType.LighterFuel) AudioManager.Play2D(Snd.FuelPour, 0.6f);
            }
            _actionItem = _prompt.UsesItem;
            _hold += dt;
            if (_prompt.NoiseWhileHolding > 0f)
            {
                _holdNoiseTimer -= dt;
                if (_holdNoiseTimer <= 0f) { _holdNoiseTimer = 0.8f; MakeNoise(_prompt.NoiseWhileHolding); }
            }
            if (_hold >= _prompt.HoldTime)
            {
                _target.Interact(_who);
                _hold = 0f;
                _holdLock = true;
                _actionItem = ItemType.None;
            }
        }

        // ================================================================== doors

        /// <summary>
        /// Hold LMB on a door leaf and move the mouse: forward / back pushes and pulls the leaf, sideways swings it.
        /// The view is frozen while holding; the leaf stops against our own body; letting go keeps its momentum.
        /// </summary>
        void HandleDoorDrag(PlayerStatus st, float dt)
        {
            _lmbOnDoor = false;
            bool can = _w.Running && !GameInput.GameplayBlocked && st.Life == LifeState.Free && !st.Hidden && !st.Trapped && !st.InCar && _captureFx <= 0f;
            var rig = PsxCameraRig.Instance;
            if (_drag == null)
            {
                if (!can || rig == null || !GameInput.PrimaryDown) return;
                var door = _target as DoorEntity;
                if (door == null || door.Boarded || door.Info.Pivot == null) return;
                if (Vector3.Distance(rig.transform.position, _targetPoint) > Tuning.InteractRange + 0.3f) return;
                _lmbOnDoor = true; // this click belongs to the door, not to the item in hand
                if (door.Locked)
                {
                    door.Rattle();
                    _arms?.Play(CharacterAction.Interact);
                    MakeNoise(4f);
                    return;
                }
                if (door.Grabber >= 0 && door.Grabber != _avatar.Id) return;
                BeginDrag(door);
                return;
            }

            var d = _drag;
            bool keep = can && rig != null && Input.GetMouseButton(0) && d.LocalDrive && !d.Locked && !d.Boarded;
            Vector3 grab = d.Info.Pivot.TransformPoint(_dragLocal);
            if (keep)
            {
                Vector3 eye = rig.transform.position;
                Vector3 flat = grab - eye; flat.y = 0f;
                if (flat.magnitude > Tuning.InteractRange + 1.1f || Mathf.Abs(grab.y - eye.y) > 2f) keep = false;
            }
            if (!keep) { EndDrag(); return; }

            // mouse motion -> a push of the grabbed point (forward = away from us, sideways = along our right)
            Vector2 look = GameInput.Look;
            Vector3 right = rig.transform.right; right.y = 0f; right.Normalize();
            Vector3 fwd = Quaternion.Euler(0, _yaw, 0) * Vector3.forward;
            Vector3 push = (right * look.x + fwd * look.y) * 0.013f;
            Vector3 r = grab - d.Hinge; r.y = 0f;
            float rl = Mathf.Max(r.magnitude, 0.3f);
            Vector3 tangent = d.Sign * Vector3.Cross(Vector3.up, r / rl); // where the grabbed point goes when the door opens
            float delta = Mathf.Clamp(Mathf.Rad2Deg * Vector3.Dot(push, tangent) / rl, -28f, 28f);

            float prev = d.Angle;
            float next = Mathf.Clamp(prev + delta, 0f, d.MaxAngle);
            if (Mathf.Abs(next - prev) > 0.0001f && d.Blocked(next, DragBlockMask) && !d.Blocked(prev, DragBlockMask))
            {
                // the leaf runs into us / someone: go as far as it fits
                float half = (prev + next) * 0.5f;
                next = d.Blocked(half, DragBlockMask) ? prev : half;
            }
            d.Angle = next;
            float inst = dt > 0f ? (next - prev) / dt : 0f;
            _dragVel = Mathf.Lerp(_dragVel, inst, 1f - Mathf.Exp(-dt * 16f));
            if (Mathf.Abs(look.x) + Mathf.Abs(look.y) < 0.01f) _dragVel = Mathf.MoveTowards(_dragVel, 0f, dt * 900f);
            d.Velocity = Mathf.Clamp(_dragVel, -420f, 420f);

            _dragSendTimer -= dt;
            if (_dragSendTimer <= 0f)
            {
                _dragSendTimer = 0.05f;
                _w.SendDoorDrag(d.Index, d.Angle, d.Velocity);
            }
            // moving a door is not silent
            if (Mathf.Abs(d.Velocity) > 140f) MakeNoise(5f);
        }

        void BeginDrag(DoorEntity door)
        {
            _drag = door;
            Vector3 local = door.Info.Pivot.InverseTransformPoint(_targetPoint);
            // grabbing right next to the hinge gives no leverage: take it further out along the leaf
            local.x = Mathf.Clamp(local.x, door.LeafLength * 0.45f, door.LeafLength);
            _dragLocal = local;
            door.LocalDrive = true;
            door.PredictUntil = 0f;
            door.Grabber = _avatar.Id;
            door.Velocity = 0f;
            _dragVel = 0f;
            _dragSendTimer = 0.05f;
            _w.SendDoorGrab(door.Index, true, door.Angle, 0f);
            _arms?.Play(CharacterAction.Interact);
            _w.SendAction(CharacterAction.Interact);
            if (door.Shut) AudioManager.Play3D(AudioManager.Variant(Snd.DoorLatch, 2), door.Info.Center + Vector3.up, 0.35f, 1.2f, 1f, 8f);
        }

        void EndDrag()
        {
            var d = _drag;
            _drag = null;
            if (d == null || !d.LocalDrive) return;
            d.LocalDrive = false;
            // keep swinging locally until the host's stream catches up (the host continues from these values)
            d.PredictUntil = Time.time + 0.6f;
            if (_w != null) _w.SendDoorGrab(d.Index, false, d.Angle, d.Velocity);
        }

        // ================================================================== traps

        void CheckTraps(PlayerStatus st)
        {
            Vector3 feet = _avatar.transform.position;
            if (st.Life == LifeState.Free && !st.Hidden && !st.InCar && _w.Running)
            {
                for (int i = 0; i < _w.Traps.Count; i++)
                {
                    var t = _w.Traps[i];
                    if (t.State != TrapState.Armed) { _trapsReported.Remove(i); continue; }
                    if (_trapsReported.Contains(i)) continue;
                    if (t.Crossed(_lastFeet, feet))
                    {
                        _trapsReported.Add(i);
                        _w.SendTrapTrigger(i);
                    }
                }
            }
            _lastFeet = feet;
        }

        // ================================================================== visuals

        void UpdateHandVisuals(PlayerStatus st)
        {
            if (_arms == null) return;
            bool show = !st.Hidden && !st.InCar;
            ItemType want = _actionItem != ItemType.None && _w.Inventory.Has(_actionItem) ? _actionItem : _w.Inventory.HeldType;
            if (want != _shownItem)
            {
                _shownItem = want;
                DestroyHandLights();
                try { _arms.SetHeld(want); } catch (System.Exception e) { Debug.LogException(e); }
            }
            _arms.SetVisible(show);
            if (want == ItemType.SoundMeter && _arms.HeldModel != null)
            {
                var needle = Avatar.FindDeep(_arms.HeldModel.transform, "Needle");
                if (needle != null) needle.localRotation = Quaternion.Euler(0, 0, ItemMeshFactory.SoundMeterNeedleAngle(_noiseSmoothed));
            }
            SetHandLights(_lighterOn && want == ItemType.Lighter && show, _flashOn && want == ItemType.Flashlight && show);
        }

        void SetHandLights(bool lighter, bool flash)
        {
            if (lighter)
            {
                if (_lighterLight == null && _arms != null)
                {
                    var anchor = _arms.HeldModel != null ? (Avatar.FindDeep(_arms.HeldModel.transform, "Anchor_Flame") ?? _arms.HeldModel.transform) : _arms.HandSocket;
                    _lighterLight = PsxLight.Create(anchor, Vector3.up * 0.03f, new Color(1f, 0.7f, 0.36f), 1.35f, 5.5f, PsxFlicker.Candle, "LocalLighter");
                    _lighterLight.Priority = 10;
                    try { _flame = PsxFx.CreateFlame(anchor, 1f); } catch { }
                    _flameLoop = AudioManager.Loop2D(Snd.FlameLoop, 0.18f, AudioCategory.Sfx, 0.2f);
                }
            }
            else if (_lighterLight != null || _flame != null)
            {
                if (_lighterLight != null) Destroy(_lighterLight.gameObject);
                if (_flame != null) Destroy(_flame);
                _lighterLight = null; _flame = null;
                AudioManager.Stop(_flameLoop, 0.15f); _flameLoop = null;
            }

            if (flash)
            {
                var rig = PsxCameraRig.Instance;
                if (_flashLight == null && rig != null)
                {
                    _flashLight = PsxLight.CreateSpot(rig.transform, new Vector3(0.15f, -0.15f, 0.2f), Quaternion.identity, new Color(1f, 0.95f, 0.8f), 2.2f, 16f, 42f, PsxFlicker.None, "LocalFlashlight");
                    _flashLight.Priority = 10;
                    _flashFill = PsxLight.Create(rig.transform, new Vector3(0, 0, 0.8f), new Color(0.9f, 0.88f, 0.8f), 0.45f, 3.5f, PsxFlicker.None, "LocalFlashFill");
                    _flashFill.Priority = 9;
                }
            }
            else
            {
                if (_flashLight != null) Destroy(_flashLight.gameObject);
                if (_flashFill != null) Destroy(_flashFill.gameObject);
                _flashLight = null; _flashFill = null;
            }
        }

        void DestroyHandLights()
        {
            if (_lighterLight != null) Destroy(_lighterLight.gameObject);
            if (_flame != null) Destroy(_flame);
            if (_flashLight != null) Destroy(_flashLight.gameObject);
            if (_flashFill != null) Destroy(_flashFill.gameObject);
            _lighterLight = null; _flame = null; _flashLight = null; _flashFill = null;
            AudioManager.Stop(_flameLoop, 0.1f); _flameLoop = null;
        }

        void UpdateEffects(PlayerStatus st, float dt)
        {
            _hitFx = Mathf.MoveTowards(_hitFx, 0f, dt * 1.5f);
            float dmg = 0f;
            if (st.Injured && !PainkillersActive) dmg = 0.25f + 0.12f * Mathf.Sin(Time.time * 4f);
            dmg = Mathf.Max(dmg, _hitFx);
            if (_captureFx > 0f)
            {
                _captureFx = Mathf.MoveTowards(_captureFx, 0f, dt * 0.6f);
                VhsEffect.StaticOverride = Mathf.Clamp01(_captureFx * 1.4f);
                if (_captureFx <= 0f) AudioManager.SetDistortion(0f);
            }
            else VhsEffect.StaticOverride = 0f;
            VhsEffect.Damage = dmg;
            VhsEffect.Hiding = st.Hidden ? 1f : 0f;
            AudioManager.SetMuffle(st.Hidden ? 0.75f : 0f);

            // blood trail is drawn by MatchWorld for every injured avatar
            if (st.Injured && !PainkillersActive && _sprintingNow && Random.value < dt * 0.4f) AudioManager.Play2D(AudioManager.Variant(Snd.Hurt, 3), 0.3f, 1.1f);
        }

        void PublishState(PlayerStatus st)
        {
            var s = _avatar.State;
            s.Position = _avatar.transform.position;
            s.Yaw = _yaw;
            s.Pitch = _pitch;
            AvatarFlags f = AvatarFlags.None;
            if (_crouch) f |= AvatarFlags.Crouch;
            if (_sprintingNow) f |= AvatarFlags.Sprint;
            if (_lighterOn) f |= AvatarFlags.LighterOn;
            if (_flashOn) f |= AvatarFlags.FlashlightOn;
            if (_motor.Grounded) f |= AvatarFlags.Grounded;
            s.Flags = f;
            s.Held = _shownItem == (ItemType)255 ? ItemType.None : _shownItem;
            s.TeleportSeq = _teleportSeq;
            _avatar.SetLocalState(s);
        }

        // ================================================================== status changes / damage

        public void OnStatusChanged(PlayerStatus before, PlayerStatus now)
        {
            // captured -> into the cage
            if (now.Life == LifeState.Caged && now.Cage >= 0 && now.Cage < _w.Cages.Length && (before.Life != LifeState.Caged || before.Cage != now.Cage))
            {
                var p = _w.Cages[now.Cage].Info.Inside;
                if (before.Life == LifeState.Free) StartCaptureFx();
                Teleport(p.position, p.rotation.eulerAngles.y);
                _crouch = true;
                _lighterOn = false; _flashOn = false;
            }
            if (now.Life == LifeState.Dead && before.Life != LifeState.Dead)
            {
                StartCaptureFx();
                AudioManager.Play2D(Snd.StingDeath, 1f, 1f, AudioCategory.Stinger);
                VhsEffect.Desaturate = 0.6f;
            }
            // entered a hiding spot
            if (now.Hidden && !before.Hidden)
            {
                _motor.Controller.enabled = false;
                _lighterOn = false; _flashOn = false;
                var hv = _w.Hiding[now.HidingSpot].Info.HiddenView;
                _yaw = hv.rotation.eulerAngles.y; _pitch = 0;
                AudioManager.Play2D(Snd.WardrobeClose, 0.5f);
            }
            // left / pulled out of a hiding spot
            if (!now.Hidden && before.Hidden && before.HidingSpot >= 0 && before.HidingSpot < _w.Hiding.Length)
            {
                var ex = _w.Hiding[before.HidingSpot].Info.ExitPose;
                _motor.Controller.enabled = true;
                Teleport(ex.position, ex.rotation.eulerAngles.y);
            }
            // car
            if (now.InCar && !before.InCar && _w.Map.Car != null && _w.Map.Car.Root != null && now.CarSeat < _w.Map.Car.Seats.Length)
            {
                var seat = _w.Map.Car.Seats[now.CarSeat];
                var car = _w.Map.Car.Root;
                _seatLocal = new Pose(car.InverseTransformPoint(seat.position), Quaternion.Inverse(car.rotation) * seat.rotation);
                _seated = true;
                _motor.Controller.enabled = false;
                _yaw = seat.rotation.eulerAngles.y;
                AudioManager.Play2D(Snd.CarDoor, 0.8f);
                Teleport(seat.position - Vector3.up * 1.0f, _yaw);
            }
            if (!now.InCar && before.InCar)
            {
                _seated = false;
                _motor.Controller.enabled = true;
                AudioManager.Play2D(Snd.CarDoor, 0.8f);
                if (now.Life == LifeState.Free && _w.Map.Car != null && _w.Map.Car.DriverDoor != null)
                {
                    Vector3 p = _w.Map.Car.DriverDoor.bounds.center;
                    Vector3 side = _w.Map.Car.Root != null ? -_w.Map.Car.Root.right : Vector3.left;
                    Teleport(new Vector3(p.x, _w.Map.Car.Root != null ? _w.Map.Car.Root.position.y : 0f, p.z) + side * 1.1f, _yaw);
                }
            }
            if (now.Life == LifeState.Escaped)
            {
                _motor.Controller.enabled = false;
                VhsEffect.Damage = 0; VhsEffect.Hiding = 0;
                AudioManager.SetMuffle(0f);
            }
            if (before.Injured && !now.Injured) VhsEffect.Damage = 0f;
            if (now.TeleportSeq != before.TeleportSeq && now.Life == LifeState.Free && !now.Hidden && !now.InCar && before.Hidden == now.Hidden)
            {
                // generic host-requested reposition (e.g. freed from a trap) - nothing extra to do
            }
        }

        void StartCaptureFx()
        {
            _captureFx = 1f;
            AudioManager.Play2D(Snd.StingCapture, 1f, 1f, AudioCategory.Stinger);
            AudioManager.Play2D(Snd.ScreamsLong, 0.5f, 1f, AudioCategory.Stinger);
            AudioManager.SetDistortion(0.5f);
            VhsEffect.TriggerRoll(1.2f);
            VhsEffect.TriggerGlitch(1f, 1.5f);
        }

        void Teleport(Vector3 pos, float yaw)
        {
            _motor.Teleport(pos, yaw);
            _yaw = yaw;
            _lastFeet = pos;
            _teleportSeq++;
        }

        /// <summary>Omar's cleaver connected.</summary>
        public void OnHit(bool captured)
        {
            _hitFx = 1f;
            AudioManager.Play2D(AudioManager.Variant(Snd.Hurt, 3), 1f);
            if (!captured)
            {
                AudioManager.Play2D(Snd.StingJumpscare, 0.9f, 1f, AudioCategory.Stinger);
                VhsEffect.TriggerGlitch(1f, 0.6f);
                _w.AddMessage("YOU'RE BLEEDING. RUN!", 3f);
                _stamina = Mathf.Max(_stamina, 0.6f); // adrenaline
                _exhausted = false;
            }
        }

        public void Terrify()
        {
            _terrorUntil = Time.time + 2.5f;
            _w.Proximity?.AddTerror(0.6f);
            AudioManager.Play2D(Snd.HeartbeatFast, 0.7f, 1f, AudioCategory.Stinger);
        }
    }
}
