using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
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
        float _lighterLitAt, _flickAt = -1f, _lidOpen;
        bool _flickRetry;
        bool LighterLit => _lighterOn && Time.time >= _lighterLitAt;
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
        // camera path for climbing into / out of hiding spots
        struct CamKey { public Vector3 Pos; public Quaternion Rot; public float T; }
        readonly System.Collections.Generic.List<CamKey> _camPath = new System.Collections.Generic.List<CamKey>();
        float _camPathTime;
        bool _camPathEntering;
        int _camPathSpot = -1;

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
        public bool InHidingTransition => CamPathActive;
        bool CamPathActive => _camPath.Count > 1 && _camPathTime < _camPath[_camPath.Count - 1].T;

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
            if (_w.Ending != null)
            {
                // the ending screen owns the picture: drop every local effect once and stop driving them
                EndDrag();
                if (_arms != null) _arms.SetVisible(false);
                SetHandLights(false, false);
                _captureFx = 0f; _hitFx = 0f;
                VhsEffect.StaticOverride = 0f; VhsEffect.Damage = 0f; VhsEffect.Hiding = 0f;
                AudioManager.SetDistortion(0f); AudioManager.SetMuffle(0f);
                return;
            }
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
            HandleDoorPeek(st, dt);
            Look(st);
            bool canMove = _w.Running && st.Life != LifeState.Dead && !st.Hidden && !st.Trapped && !st.InCar && _captureFx <= 0f && !CamPathActive && !AdminFreeCam.Active;
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
            if (_w.Ending != null) return;
            PlaceCamera(st);
        }

        void HandleMenus()
        {
            var ui = UI.UIManager.Instance;
            if (ui == null) return;
            if (!ui.AnyModal && GameInput.InventoryToggle) ui.Push(new UI.InventoryScreen(_w));
            else if (!ui.AnyModal && GameInput.JournalToggle) ui.Push(new UI.JournalScreen(_w));
            else if (!ui.AnyModal && GameInput.PauseToggle) ui.Push(new UI.PauseScreen());
            GameInput.SetCursorLocked(!ui.AnyModal);
        }

        // ================================================================== look & move

        void Look(PlayerStatus st)
        {
            var d = GameInput.Look;
            if (_drag != null || CamPathActive || AdminFreeCam.Active) d = Vector2.zero; // the mouse moves the door / we are climbing in or out
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

        float _stumbleUntil;

        /// <summary>Stagger after running into a tripwire (slow, no sprint) with a lurch of the camera.</summary>
        public void Stumble(float seconds)
        {
            _stumbleUntil = Time.time + seconds;
            _pitch = Mathf.Clamp(_pitch + 14f, -85f, 85f);
            if (_arms != null) _arms.Play(CharacterAction.HitReact);
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
            if (Time.time < _stumbleUntil)
            {
                // tripped over a wire: staggering, no sprint until you catch your balance
                wantSprint = false;
                speed = Mathf.Min(speed, Tuning.WalkSpeed) * Tuning.TripStumbleSpeed;
            }
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

            speed *= AdminState.SpeedMultiplier;
            if (AdminState.InfiniteStamina) { _stamina = 1f; _exhausted = false; }
            Quaternion yawRot = Quaternion.Euler(0, _yaw, 0);
            if (AdminState.Noclip && canMove) { Fly(input, speed, dt); _avatar.transform.rotation = yawRot; return; }
            if (_noclipWas) { _noclipWas = false; if (!st.Hidden && !st.InCar) _motor.Controller.enabled = true; }
            Vector3 wish = yawRot * new Vector3(input.x, 0, input.y) * speed;
            if (!_motor.Controller.enabled) wish = Vector3.zero;
            float moved = _motor.Move(wish, dt);
            _avatar.transform.rotation = yawRot;

            // falling / landing noise
            if (!_motor.Grounded) _airTime += dt;
            else
            {
                if (_airTime > 0.45f) { PlayStep(1.2f); MakeNoise(5f); }
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
                    // quiet: walking only carries a few metres, sprinting ~9 m (Omar found people far too easily)
                    float noise = _crouch ? 1.2f : sprinting ? 9f : 3f;
                    if (injured) noise += 1.5f;
                    if (held == ItemType.GasCan && sprinting) noise += 2f;
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
        bool _noclipWas;

        /// <summary>Admin noclip: fly where you look, through walls (Space up, Ctrl down).</summary>
        void Fly(Vector2 input, float speed, float dt)
        {
            _noclipWas = true;
            _motor.Controller.enabled = false;
            Vector3 v = Quaternion.Euler(_pitch, _yaw, 0) * new Vector3(input.x, 0, input.y);
            if (Input.GetKey(KeyCode.Space)) v += Vector3.up;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) v += Vector3.down;
            _avatar.transform.position += v * Mathf.Max(speed, 4f) * 1.5f * dt;
            _sprintingNow = false;
            _lastFeet = _avatar.transform.position;
        }

        /// <summary>Admin teleport (the host follows our position).</summary>
        public void AdminTeleport(Vector3 p)
        {
            var st = Status;
            if (st == null || st.Hidden || st.InCar) return;
            EndDrag();
            Teleport(p, _yaw);
        }

        void PlayStep(float volume)
        {
            AudioManager.Play2D(Snd.Step(_motor.GroundSurface), volume, Random.Range(0.92f, 1.08f));
        }

        void BreathHeavy()
        {
            if (Time.time - _lastBreath < 3f) return;
            _lastBreath = Time.time;
            AudioManager.Play2D(_avatar.Voice(VoiceLine.Breath), 0.55f);
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
            if (CamPathActive)
            {
                _camPathTime += Time.deltaTime;
                if (EvalCamPath(out var cp, out var cr))
                {
                    rig.transform.SetPositionAndRotation(cp, cr);
                    rig.FieldOfView = Settings.FieldOfView;
                    // our body follows the climb so the others see us go in
                    if (_camPathEntering && st.Hidden && _camPathSpot >= 0 && _camPathSpot < _w.Hiding.Length)
                    {
                        var info = _w.Hiding[_camPathSpot].Info;
                        _avatar.transform.position = new Vector3(cp.x, info.ExitPose.position.y, cp.z);
                    }
                    return;
                }
            }
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
                float eye = Mathf.Lerp(_avatar.CrouchEyeHeight, _avatar.StandEyeHeight, Mathf.InverseLerp(1.05f, 1.75f, _motor.Height));
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
            if (slot >= inv.Capacity) _w.AddMessage("I CAN ONLY CARRY THREE THINGS. A BAG WOULD HELP...", 2f);
            else if (slot >= 0) SelectSlot(slot);
            float scroll = GameInput.Scroll;
            if (scroll > 0.1f) SelectSlot((inv.Selected + inv.Capacity - 1) % inv.Capacity);
            else if (scroll < -0.1f) SelectSlot((inv.Selected + 1) % inv.Capacity);

            var held = inv.Held;
            ItemType ht = held != null ? held.Type : ItemType.None;
            bool hidden = st.Hidden;

            // drain lights
            if (_lighterOn)
            {
                var l = ItemOfType(ItemType.Lighter);
                if (l == null || ht != ItemType.Lighter || hidden) _lighterOn = false;
                else if (LighterLit)
                {
                    if (!AdminState.InfiniteLight) l.Charge = Mathf.Max(0f, l.Charge - dt / Tuning.LighterBurnSeconds);
                    if (l.Charge <= 0f) { _lighterOn = false; AudioManager.Play2D(Snd.LighterClose, 0.6f); _w.AddMessage("THE LIGHTER IS OUT OF FUEL", 3f); }
                }
            }
            if (_flashOn)
            {
                var f = ItemOfType(ItemType.Flashlight);
                if (f == null || ht != ItemType.Flashlight || hidden) _flashOn = false;
                else
                {
                    if (!AdminState.InfiniteLight) f.Charge = Mathf.Max(0f, f.Charge - dt / Tuning.FlashlightBurnSeconds);
                    if (f.Charge <= 0f) { _flashOn = false; AudioManager.Play2D(Snd.FlashlightClick, 0.6f); _w.AddMessage("THE BATTERIES ARE DEAD", 3f); }
                }
            }

            ReportLightCharges();

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
                    if (_lighterOn)
                    {
                        // flick the lid open, then strike the wheel (sometimes it takes a second strike)
                        AudioManager.Play2D(Snd.LighterOpen, 0.6f);
                        _flickRetry = Random.value < 0.22f;
                        _flickAt = Time.time + 0.13f;
                        _lighterLitAt = Time.time + (_flickRetry ? 0.5f : 0.19f);
                        MakeNoise(3f);
                    }
                    else { AudioManager.Play2D(Snd.LighterClose, 0.6f); _flickAt = -1f; }
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
                case ItemType.Revolver:
                    Fire(held);
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

        float _nextShot, _cockAt = -1f;

        /// <summary>Revolver: one deafening shot (the host decides what it hit). Two rounds in the cylinder.</summary>
        void Fire(ItemEntity gun)
        {
            if (Time.time < _nextShot) return;
            _nextShot = Time.time + 0.75f;
            if (gun.Charge < 0.49f)
            {
                AudioManager.Play2D(Snd.GunEmpty, 0.8f, Random.Range(0.95f, 1.05f));
                _arms?.Play(CharacterAction.Shoot);
                return;
            }
            gun.Charge = Mathf.Max(0f, gun.Charge - 0.5f); // predicted, the host confirms
            var rig = PsxCameraRig.Instance;
            Vector3 origin = rig != null ? rig.transform.position : _avatar.EyePosition;
            Vector3 dir = rig != null ? rig.transform.forward : Quaternion.Euler(_pitch, _yaw, 0) * Vector3.forward;
            _w.SendShoot(origin, dir);
            _arms?.Play(CharacterAction.Shoot);
            AudioManager.Play2D(Snd.GunShot, 1f, Random.Range(0.96f, 1.03f));
            var muzzle = _arms != null && _arms.HeldModel != null ? Avatar.FindDeep(_arms.HeldModel.transform, "Anchor_Muzzle") : null;
            try { PsxFx.MuzzleFlash(muzzle != null ? muzzle.position : origin + dir * 0.5f, dir, muzzle != null ? Layers.ViewModel : -1); } catch { }
            // the kick throws the view up; the tape tears for a moment
            _pitch = Mathf.Clamp(_pitch - 4.5f, -85f, 85f);
            _yaw += Random.Range(-1.2f, 1.2f);
            _hitFx = Mathf.Max(_hitFx, 0.35f);
            VhsEffect.TriggerGlitch(0.35f, 0.18f);
            if (gun.Charge >= 0.49f) _cockAt = Time.time + 0.4f;
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
            if (slot == inv.Selected || slot >= inv.Capacity) return;
            inv.Select(slot);
            _useHoldItem = ItemType.None;
            AudioManager.Play2D(Snd.ItemEquip, 0.4f);
        }

        ItemEntity ItemOfType(ItemType t) => _w.GetItem(_w.Inventory.Find(t));

        float _chargeReportAt;
        readonly System.Collections.Generic.Dictionary<int, float> _reportedCharge = new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>
        /// Lights burn on the owner's machine: every 2 s tell the host how much is left, so what a captured / disconnected
        /// player drops (and what the host hands to the next holder) is the real amount, not a full refill.
        /// </summary>
        void ReportLightCharges()
        {
            if (_w.IsHost || Time.time < _chargeReportAt) return;
            _chargeReportAt = Time.time + 2f;
            for (int k = 0; k < 2; k++)
            {
                var it = ItemOfType(k == 0 ? ItemType.Lighter : ItemType.Flashlight);
                if (it == null) continue;
                if (_reportedCharge.TryGetValue(it.Id, out var last))
                {
                    if (it.Charge > last) { _reportedCharge[it.Id] = it.Charge; continue; }   // refilled by the host
                    if (last - it.Charge < 0.02f) continue;
                }
                _reportedCharge[it.Id] = it.Charge;
                _w.SendChargeReport(it.Id, it.Charge);
            }
        }

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
            SetGlowTarget(null);
            if (!_w.Running || GameInput.GameplayBlocked) { _hold = 0; return; }

            // special states first
            if (st.Life == LifeState.Caged)
            {
                _hasPrompt = true;
                _prompt = InteractPrompt.Press("FORCE THE LOCK (MASH E) - IT CLANKS, HE MAY HEAR");
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
            // Omar is busy at the butcher table: sneak up behind him and smash him
            if (_w.KitchenBusy && HitOmarPrompt()) { _target = null; _hold = 0; return; }

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
            SetGlowTarget(_target as WorldItem);
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
                if (_prompt.UsesItem == ItemType.Screwdriver) AudioManager.Play2D(AudioManager.Variant(Snd.VentScrew, 3), 0.55f);
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
            if (Mathf.Abs(d.Velocity) > 180f) MakeNoise(3f);
        }

        // ================================================================== peeking (RMB on a door)

        /// <summary>A door opened no more than this is "a crack": peeking through it is silent and hides you.</summary>
        public const float PeekMaxAngle = 14f;
        const float PeekAngle = 9f, PeekSpeed = 16f;
        DoorEntity _peek;
        float _peekGoal;
        int _peekDoor = -1;

        /// <summary>
        /// RMB on a shut door eases it open just a crack (silently) so you can watch the other side; RMB again eases it
        /// shut. While you stay right at the crack Omar on the other side can not see you.
        /// </summary>
        void HandleDoorPeek(PlayerStatus st, float dt)
        {
            bool can = _w.Running && !GameInput.GameplayBlocked && st.Life == LifeState.Free && !st.Hidden && !st.Trapped && !st.InCar && _captureFx <= 0f && _drag == null;
            var rig = PsxCameraRig.Instance;
            if (_peek == null)
            {
                if (!can || rig == null || !GameInput.SecondaryDown) return;
                var door = _target as DoorEntity;
                if (door == null || door.Boarded || door.Locked || door.Info.Pivot == null || door.Angle > PeekMaxAngle + 1f) return;
                if (door.Grabber >= 0 && door.Grabber != _avatar.Id) return;
                if (Vector3.Distance(rig.transform.position, _targetPoint) > Tuning.InteractRange + 0.3f) return;
                _peek = door;
                _peekDoor = door.Index;
                _peekGoal = door.Angle < PeekAngle * 0.5f ? PeekAngle : 0f;   // shut: crack it open; cracked: ease it shut
                door.LocalDrive = true;
                door.PredictUntil = 0f;
                door.Grabber = _avatar.Id;
                door.Velocity = 0f;
                _dragSendTimer = 0.05f;
                _w.SendDoorGrab(door.Index, true, door.Angle, 0f);
                return;
            }
            var d = _peek;
            if (!can || !d.LocalDrive || d.Locked || d.Boarded || GeoUtil.FlatDistance(_avatar.Position, d.Info.Center) > 2.6f) { EndPeek(); return; }
            float prev = d.Angle;
            float next = Mathf.MoveTowards(prev, _peekGoal, PeekSpeed * dt);
            if (Mathf.Abs(next - prev) > 0.0001f && d.Blocked(next, DragBlockMask) && !d.Blocked(prev, DragBlockMask)) next = prev;
            d.Angle = next;
            d.Velocity = dt > 0f ? (next - prev) / dt : 0f;
            _dragSendTimer -= dt;
            if (_dragSendTimer <= 0f) { _dragSendTimer = 0.05f; _w.SendDoorDrag(d.Index, d.Angle, d.Velocity); }
            if (Mathf.Abs(next - _peekGoal) < 0.01f || next == prev) EndPeek();
        }

        void EndPeek()
        {
            var d = _peek;
            _peek = null;
            if (d == null || !d.LocalDrive) return;
            d.LocalDrive = false;
            d.Velocity = 0f;
            d.PredictUntil = Time.time + 0.6f;
            if (_w != null) _w.SendDoorGrab(d.Index, false, d.Angle, 0f);
        }

        /// <summary>Standing at the door we cracked open (it is still only a crack).</summary>
        bool PeekingNow()
        {
            if (_peekDoor < 0 || _w.Doors == null || _peekDoor >= _w.Doors.Length) return false;
            var d = _w.Doors[_peekDoor];
            float dist = GeoUtil.FlatDistance(_avatar.Position, d.Info.Center);
            if (dist > 2.6f) { _peekDoor = -1; return false; }
            return d.Angle > 0.3f && d.Angle <= PeekMaxAngle && dist < 1.6f;
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

        // ================================================================== hiding transitions

        static CamKey K(Vector3 p, Quaternion r, float t) => new CamKey { Pos = p, Rot = r, T = t };

        static Quaternion Flat(Vector3 fwd, float pitch = 0f)
        {
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
            return Quaternion.LookRotation(fwd.normalized, Vector3.up) * Quaternion.Euler(pitch, 0, 0);
        }

        void StartEnterPath(HidingSpotInfo info, int spot)
        {
            var rig = PsxCameraRig.Instance;
            if (rig == null) return;
            Vector3 p0 = rig.transform.position;
            Quaternion r0 = rig.transform.rotation;
            var hv = info.HiddenView;
            var ex = info.ExitPose;
            _camPath.Clear();
            _camPath.Add(K(p0, r0, 0f));
            if (info.Kind == HidingKind.UnderBed)
            {
                // get down on the floor beside the bed, look under it, then roll in and face the room
                Pose cs = info.CrawlStart.position != Vector3.zero ? info.CrawlStart : new Pose(ex.position, Flat(-ex.forward));
                Vector3 kneel = cs.position + Vector3.up * 0.75f;
                Vector3 low = cs.position + Vector3.up * 0.28f;
                Vector3 side = Vector3.Cross(Vector3.up, cs.forward);
                _camPath.Add(K(kneel, Flat(cs.forward, 32f), 0.3f));
                _camPath.Add(K(low, Flat(cs.forward, 12f), 0.55f));
                _camPath.Add(K(Vector3.Lerp(low, hv.position, 0.55f), Flat(side, 6f) * Quaternion.Euler(0, 0, 18f), 0.85f));
                _camPath.Add(K(hv.position, hv.rotation, 1.15f));
                _crouch = true; // the others see us get down
            }
            else
            {
                // step in front of it facing the doors, back in, and turn around to look out through the slats
                Vector3 front = ex.position + Vector3.up * (_avatar.StandEyeHeight - 0.05f);
                Quaternion faceIn = Flat(-ex.forward, 4f);
                Vector3 side = Vector3.Cross(Vector3.up, ex.forward);
                _camPath.Add(K(front, faceIn, 0.3f));
                _camPath.Add(K(Vector3.Lerp(front, hv.position, 0.6f), Flat(side, 2f), 0.6f));
                _camPath.Add(K(hv.position, hv.rotation, 0.85f));
            }
            _camPathTime = 0f;
            _camPathEntering = true;
            _camPathSpot = spot;
        }

        void StartExitPath(HidingSpotInfo info, int spot, bool yanked)
        {
            var hv = info.HiddenView;
            var ex = info.ExitPose;
            float k = yanked ? 0.45f : 1f;
            Vector3 eye = ex.position + Vector3.up * _avatar.StandEyeHeight;
            Quaternion outRot = Flat(ex.forward);
            _camPath.Clear();
            _camPath.Add(K(hv.position, hv.rotation, 0f));
            if (info.Kind == HidingKind.UnderBed)
            {
                Pose cs = info.CrawlStart.position != Vector3.zero ? info.CrawlStart : new Pose(ex.position, Flat(-ex.forward));
                Vector3 low = cs.position + Vector3.up * 0.3f;
                _camPath.Add(K(low, Flat(ex.forward, 8f) * Quaternion.Euler(0, 0, -10f), 0.5f * k));
                _camPath.Add(K(cs.position + Vector3.up * 0.9f, Flat(ex.forward, 14f), 0.8f * k));
                _camPath.Add(K(eye, outRot, 1.05f * k));
            }
            else
            {
                _camPath.Add(K(Vector3.Lerp(hv.position, eye, 0.5f), Flat(ex.forward, 3f), 0.35f * k));
                _camPath.Add(K(eye, outRot, 0.65f * k));
            }
            _camPathTime = 0f;
            _camPathEntering = false;
            _camPathSpot = spot;
        }

        bool EvalCamPath(out Vector3 pos, out Quaternion rot)
        {
            pos = default; rot = Quaternion.identity;
            if (_camPath.Count < 2) return false;
            float t = _camPathTime;
            for (int i = 1; i < _camPath.Count; i++)
            {
                var a = _camPath[i - 1];
                var b = _camPath[i];
                if (t > b.T && i < _camPath.Count - 1) continue;
                float u = Mathf.Clamp01(Mathf.InverseLerp(a.T, b.T, t));
                u = u * u * (3f - 2f * u);
                pos = Vector3.Lerp(a.Pos, b.Pos, u);
                rot = Quaternion.Slerp(a.Rot, b.Rot, u);
                return true;
            }
            return false;
        }

        bool HitOmarPrompt()
        {
            var omar = _w.AvatarOf(_w.KitchenOmar);
            var rig = PsxCameraRig.Instance;
            if (omar == null || rig == null) return false;
            Vector3 to = _avatar.Position - omar.Position; to.y = 0f;
            if (to.magnitude > 2.1f || Vector3.Angle(omar.Forward, to) < 100f) return false;
            Vector3 look = omar.ChestPosition - rig.transform.position;
            if (Vector3.Angle(rig.transform.forward, look) > 40f) return false;
            var held = _w.Inventory.Held;
            bool weapon = held != null && (held.Type == ItemType.Bottle || held.Type == ItemType.Crowbar);
            _hasPrompt = true;
            if (!weapon) { _prompt = InteractPrompt.Info("HE HASN'T NOTICED YOU... (BOTTLE / CROWBAR)"); return true; }
            _prompt = InteractPrompt.Press(held.Type == ItemType.Bottle ? "SMASH THE BOTTLE ON HIS HEAD" : "HIT HIM WITH THE CROWBAR");
            if (GameInput.InteractDown)
            {
                _w.SendOmarHit(held.Id);
                _arms?.Play(CharacterAction.Throw);
                AudioManager.Play2D(AudioManager.Variant(Snd.CleaverSwing, 2), 0.5f, 1.3f);
            }
            return true;
        }

        WorldItem _glowItem;

        /// <summary>The item we aim at glows brighter.</summary>
        void SetGlowTarget(WorldItem wi)
        {
            if (wi == _glowItem) return;
            if (_glowItem != null && _glowItem.Glow != null) _glowItem.Glow.Highlighted = false;
            _glowItem = wi;
            if (_glowItem != null && _glowItem.Glow != null) _glowItem.Glow.Highlighted = true;
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
            if (_cockAt >= 0f && Time.time >= _cockAt) { _cockAt = -1f; AudioManager.Play2D(Snd.GunCock, 0.6f); }
            if (!_lighterOn) _flickAt = -1f;
            if (_flickAt >= 0f && Time.time >= _flickAt)
            {
                AudioManager.Play2D(Snd.LighterFlick, 0.7f, Random.Range(0.94f, 1.06f));
                if (_flickRetry) { _flickRetry = false; _flickAt = Time.time + 0.31f; try { if (_arms.HeldModel != null) PsxFx.Sparks(_arms.HeldModel.transform.position + _arms.HeldModel.transform.up * 0.05f, Vector3.up); } catch { } }
                else _flickAt = -1f;
            }
            _lidOpen = Mathf.MoveTowards(_lidOpen, _lighterOn ? 1f : 0f, Time.deltaTime / 0.09f);
            if (want == ItemType.Lighter) ItemMeshFactory.SetLighterLid(_arms.HeldModel, _lighterOn ? 1f - (1f - _lidOpen) * (1f - _lidOpen) : _lidOpen * _lidOpen);
            SetHandLights(LighterLit && want == ItemType.Lighter && show, _flashOn && want == ItemType.Flashlight && show);
        }

        void SetHandLights(bool lighter, bool flash)
        {
            if (lighter)
            {
                if (_lighterLight == null && _arms != null)
                {
                    var anchor = _arms.HeldModel != null ? (Avatar.FindDeep(_arms.HeldModel.transform, "Anchor_Flame") ?? _arms.HeldModel.transform) : _arms.HandSocket;
                    _lighterLight = PsxLight.Create(anchor, Vector3.up * 0.03f, new Color(1f, 0.76f, 0.48f), 1.55f, 7.5f, PsxFlicker.Candle, "LocalLighter");
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
            bool bed = st.Hidden && st.HidingSpot >= 0 && st.HidingSpot < _w.Hiding.Length && _w.Hiding[st.HidingSpot].IsBed;
            VhsEffect.Hiding = Mathf.MoveTowards(VhsEffect.Hiding, st.Hidden && !CamPathActive ? (bed ? 0.7f : 1f) : 0f, dt * 1.6f);
            AudioManager.SetMuffle(st.Hidden ? (bed ? 0.35f : 0.7f) : 0f);

            // blood trail is drawn by MatchWorld for every injured avatar
            if (st.Injured && !PainkillersActive && _sprintingNow && Random.value < dt * 0.4f) AudioManager.Play2D(_avatar.Voice(VoiceLine.Hurt), 0.3f, 1.05f);
        }

        void PublishState(PlayerStatus st)
        {
            var s = _avatar.State;
            s.Position = _avatar.transform.position;
            s.Yaw = _yaw;
            s.Pitch = _pitch;
            AvatarFlags f = AvatarFlags.None;
            if (_crouch) f |= AvatarFlags.Crouch;
            if (PeekingNow()) f |= AvatarFlags.Peek;
            if (_sprintingNow) f |= AvatarFlags.Sprint;
            if (LighterLit) f |= AvatarFlags.LighterOn;
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
                AudioManager.Play2D(_avatar.Voice(VoiceLine.Death), 0.9f);
                VhsEffect.Desaturate = 0.6f;
            }
            // entered a hiding spot
            if (now.Hidden && !before.Hidden && now.HidingSpot < _w.Hiding.Length)
            {
                EndDrag();
                _motor.Controller.enabled = false;
                _lighterOn = false; _flashOn = false;
                var info = _w.Hiding[now.HidingSpot].Info;
                var hv = info.HiddenView;
                _yaw = hv.rotation.eulerAngles.y;
                _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, hv.rotation.eulerAngles.x), -35f, 40f);
                StartEnterPath(info, now.HidingSpot);
            }
            // left / pulled out of a hiding spot
            if (!now.Hidden && before.Hidden && before.HidingSpot >= 0 && before.HidingSpot < _w.Hiding.Length)
            {
                var info = _w.Hiding[before.HidingSpot].Info;
                var ex = info.ExitPose;
                _motor.Controller.enabled = true;
                _crouch = false;
                Teleport(ex.position, ex.rotation.eulerAngles.y);
                _pitch = 0f;
                // pulled out / the bed flipped over us: up on our feet fast
                bool yanked = (now.Injured && !before.Injured) || now.Life != LifeState.Free || _w.Hiding[before.HidingSpot].Lifting;
                if (now.Life == LifeState.Free) StartExitPath(info, before.HidingSpot, yanked);
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
            AudioManager.Play2D(_avatar.Voice(VoiceLine.Scream), 0.85f);   // your own scream as you are dragged away
            AudioManager.Play2D(Snd.ScreamsLong, 0.2f, 1f, AudioCategory.Stinger);
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
            AudioManager.Play2D(_avatar.Voice(VoiceLine.Hurt), 1f);
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
