using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // =====================================================================================
    //  Client-side representations of the level's dynamic objects. State is authoritative on the
    //  host and arrives through MatchWorld handlers (Apply*); interaction sends requests.
    // =====================================================================================

    public sealed class ItemEntity
    {
        public int Id;
        public ItemType Type;
        public float Charge = 1f;
        public int Holder = -1;
        public int Slot = -1;
        public bool Consumed;
        public WorldItem World;
        public ItemDef Def => ItemDefs.Get(Type);
        public bool InWorld => World != null && !Consumed && Holder < 0;
    }

    /// <summary>An item lying in the level.</summary>
    public sealed class WorldItem : MonoBehaviour, IInteractable
    {
        public ItemEntity Entity;
        /// <summary>Pixelated halo around the pickup (set Highlighted while aimed at).</summary>
        public PsxItemGlow Glow;

        public Vector3 InteractPoint => transform.position;

        public static WorldItem Create(ItemEntity e, Vector3 pos, float yaw, Transform parent)
        {
            var root = new GameObject("WorldItem_" + e.Id + "_" + e.Type);
            root.layer = Layers.Item;
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            GameObject model = null;
            try
            {
                model = ItemMeshFactory.Build(e.Type);
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = ItemMeshFactory.RestOffset(e.Type);
                model.transform.localRotation = ItemMeshFactory.RestRotation(e.Type);
                GeoUtil.SetLayerRecursive(model, Layers.Item);
            }
            catch (System.Exception ex) { Debug.LogException(ex); }
            // generous trigger so tiny items are easy to target
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            Bounds b = new Bounds(pos + Vector3.up * 0.08f, new Vector3(0.25f, 0.2f, 0.25f));
            if (model != null)
            {
                bool any = false;
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                {
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                }
            }
            Vector3 size = b.size;
            size.x = Mathf.Max(size.x + 0.1f, 0.3f); size.y = Mathf.Max(size.y + 0.1f, 0.25f); size.z = Mathf.Max(size.z + 0.1f, 0.3f);
            col.center = root.transform.InverseTransformPoint(b.center);
            col.size = size;
            var wi = root.AddComponent<WorldItem>();
            wi.Entity = e;
            e.World = wi;
            InteractableRef.Attach(col, wi);
            try { wi.Glow = PsxItemGlow.Attach(root, Mathf.Max(size.x, size.z) * 0.5f); } catch (System.Exception ex) { Debug.LogException(ex); }
            return wi;
        }

        public bool GetPrompt(Interactor who, out InteractPrompt prompt)
        {
            prompt = default;
            if (who.IsOmar || Entity.Consumed || Entity.Holder >= 0) return false;
            if (who.Inventory != null && who.Inventory.Full) { prompt = InteractPrompt.Info(Entity.Def.Name + " - INVENTORY FULL (G TO DROP)"); return true; }
            prompt = InteractPrompt.Press("TAKE " + Entity.Def.Name);
            return true;
        }

        public void Interact(Interactor who) => MatchWorld.Instance?.SendPickup(Entity.Id);
    }

    /// <summary>What happened at a swing limit (see <see cref="DoorEntity.Integrate"/>).</summary>
    public enum DoorHit : byte { None = 0, Latch = 1, Slam = 2, Bump = 3, Blocked = 4 }

    /// <summary>
    /// A physical hinged door. The leaf has an opening angle (0 = shut .. <see cref="MaxAngle"/>) and an angular
    /// velocity; it swings freely with friction, latches when it closes gently, slams when it closes hard and
    /// bounces off its stop. Prisoners drag it with the mouse (the grabber drives it), Omar shoves it open just by
    /// walking into it. The host simulates free doors and relays the angles; everybody else follows smoothly.
    /// </summary>
    public sealed class DoorEntity : IInteractable
    {
        public readonly int Index;
        public readonly DoorInfo Info;
        public bool Locked, Boarded;
        /// <summary>Player holding the leaf (-1 = nobody).</summary>
        public int Grabber = -1;
        /// <summary>Degrees opened, 0 = shut. Always positive; <see cref="Sign"/> maps it onto the hinge.</summary>
        public float Angle;
        /// <summary>Degrees per second (positive = opening).</summary>
        public float Velocity;
        public readonly float MaxAngle;
        public readonly float Sign;
        /// <summary>The local player is dragging this leaf (its local angle wins over the network).</summary>
        public bool LocalDrive;
        /// <summary>Until this time (Time.time) the leaf is simulated locally (after a local release / a local shove).</summary>
        public float PredictUntil;

        public bool Open => Angle > 15f;
        public bool Shut => Angle < 0.5f;
        public bool Metal => Info.Kind == DoorKind.Metal || Info.Kind == DoorKind.Restroom || Info.Kind == DoorKind.Silo || Info.Kind == DoorKind.Shelter;
        public float LeafLength { get; }
        public float LeafThickness { get; }

        readonly Quaternion _closed;
        readonly Vector3 _boxCenter;   // leaf box centre in pivot space
        readonly Quaternion _boxRot;   // leaf box rotation in pivot space
        readonly Vector3 _boxHalf;
        float _netAngle, _netVel, _netAt = -1f;
        float _shownAngle;
        float _creakVol;
        AudioSource _creak;
        static readonly Collider[] _overlap = new Collider[8];

        public DoorEntity(int index, DoorInfo info)
        {
            Index = index; Info = info;
            _closed = info.Pivot != null ? info.Pivot.localRotation : Quaternion.identity;
            MaxAngle = Mathf.Max(10f, Mathf.Abs(info.OpenAngle));
            Sign = info.OpenAngle < 0f ? -1f : 1f;
            Locked = info.StartsLocked;
            Boarded = info.Kind == DoorKind.Boarded;
            Angle = _netAngle = _shownAngle = info.StartsOpen ? MaxAngle : 0f;
            var box = info.Leaf as BoxCollider;
            if (box != null && info.Pivot != null)
            {
                _boxCenter = info.Pivot.InverseTransformPoint(box.transform.TransformPoint(box.center));
                _boxRot = Quaternion.Inverse(info.Pivot.rotation) * box.transform.rotation;
                _boxHalf = Vector3.Scale(box.size, box.transform.lossyScale) * 0.5f;
                LeafLength = _boxHalf.x * 2f;
                LeafThickness = _boxHalf.z * 2f;
            }
            else
            {
                _boxCenter = new Vector3(0.45f, 1f, 0f); _boxRot = Quaternion.identity; _boxHalf = new Vector3(0.45f, 1f, 0.03f);
                LeafLength = 0.9f; LeafThickness = 0.05f;
            }
            ApplyRotation();
            if (info.Leaf != null) InteractableRef.Attach(info.Leaf, this);
            if (info.Boards != null)
            {
                foreach (var c in info.Boards.GetComponentsInChildren<Collider>()) InteractableRef.Attach(c, this);
                info.Boards.SetActive(Boarded);
            }
            UpdateLeafLayer();
        }

        public Vector3 InteractPoint => Info.Center + Vector3.up;

        // ------------------------------------------------------------------ geometry

        public Vector3 Hinge => Info.Pivot != null ? Info.Pivot.position : Info.Center;
        Quaternion ParentRot => Info.Pivot != null && Info.Pivot.parent != null ? Info.Pivot.parent.rotation : Quaternion.identity;
        /// <summary>World rotation of the pivot at a given opening angle.</summary>
        public Quaternion PivotRotation(float angle) => ParentRot * _closed * Quaternion.Euler(0, Sign * angle, 0);
        /// <summary>Unit vector from the hinge to the latch edge at a given angle.</summary>
        public Vector3 LeafDir(float angle) => PivotRotation(angle) * Vector3.right;
        /// <summary>Unit direction the leaf face moves in when the door opens further.</summary>
        public Vector3 OpenDir(float angle) => Sign * Vector3.Cross(Vector3.up, LeafDir(angle));

        /// <summary>Would the leaf at <paramref name="angle"/> overlap a collider on <paramref name="mask"/> (visible avatars only)?</summary>
        public bool Blocked(float angle, int mask, Collider ignore = null)
        {
            if (Info.Pivot == null) return false;
            var rot = PivotRotation(angle);
            Vector3 c = Hinge + rot * _boxCenter;
            Vector3 half = new Vector3(Mathf.Max(0.05f, _boxHalf.x - 0.05f), Mathf.Max(0.05f, _boxHalf.y - 0.08f), _boxHalf.z);
            int n = Physics.OverlapBoxNonAlloc(c, half, _overlap, rot * _boxRot, mask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var col = _overlap[i];
                if (col == null || col == ignore || col == Info.Leaf) continue;
                var av = col.GetComponentInParent<Avatar>();
                if (av != null && !av.Visible) continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Free swing with hinge friction for <paramref name="dt"/>; stops at the limits. Returns what it hit
        /// (latched shut, slammed shut, bumped the stop, or bounced off a person on <paramref name="blockMask"/>).
        /// </summary>
        public DoorHit Integrate(float dt, int blockMask)
        {
            if (Locked || Boarded) { Velocity = 0f; return DoorHit.None; }
            if (Velocity == 0f) return DoorHit.None;
            // hinge friction: a constant part (heavy doors stop) + a speed dependent part
            float friction = (Metal ? 70f : 55f) + Mathf.Abs(Velocity) * 1.6f;
            float v = Velocity;
            float nv = Mathf.MoveTowards(v, 0f, friction * dt);
            float prev = Angle;
            float next = Angle + (v + nv) * 0.5f * dt;
            Velocity = nv;
            DoorHit hit = DoorHit.None;
            if (next <= 0f)
            {
                next = 0f;
                if (v < -150f) hit = DoorHit.Slam;
                else if (v < -12f) hit = DoorHit.Latch;
                Velocity = 0f;
            }
            else if (next >= MaxAngle)
            {
                next = MaxAngle;
                if (v > 90f) hit = DoorHit.Bump;
                Velocity = v > 30f ? -v * 0.22f : 0f;
            }
            if (blockMask != 0 && Mathf.Abs(next - prev) > 0.001f && Blocked(next, blockMask) && !Blocked(prev, blockMask))
            {
                hit = Mathf.Abs(v) > 70f ? DoorHit.Blocked : DoorHit.None;
                next = prev;
                Velocity = -v * 0.25f;
                if (Mathf.Abs(Velocity) < 8f) Velocity = 0f;
            }
            Angle = next;
            if (Mathf.Abs(Velocity) < 1.5f && nv == 0f) Velocity = 0f;
            return hit;
        }

        /// <summary>
        /// Omar's body at <paramref name="p"/> (feet) moving with <paramref name="v"/> shoves the leaf out of his way:
        /// from behind it swings away ahead of him, from the swing side he rips it open towards himself.
        /// Returns true when the swing changed.
        /// </summary>
        public bool Shove(Vector3 p, Vector3 v)
        {
            if (Locked || Boarded || Info.Pivot == null) return false;
            Vector3 rel = p - Hinge;
            if (rel.y < -1.2f || rel.y > 1.2f) return false;
            rel.y = 0f;
            Vector3 dir = LeafDir(Angle); dir.y = 0f; dir.Normalize();
            Vector3 od = OpenDir(Angle); od.y = 0f; od.Normalize();
            float s = Vector3.Dot(rel, dir);
            if (s < -0.2f || s > LeafLength + 0.35f) return false;
            float q = Vector3.Dot(rel, od);
            float vn = Vector3.Dot(v, od);
            float reach = 0.45f + LeafThickness * 0.5f + 0.15f;
            if (q <= 0f && q > -reach)
            {
                // behind the leaf, walking into it: it swings away a bit faster than he walks
                if (vn < 0.15f || Angle >= MaxAngle - 2f) return false;
                float need = Mathf.Rad2Deg * (vn * 1.3f + 0.35f) / Mathf.Max(s, 0.35f);
                need = Mathf.Min(need, 460f);
                if (Velocity >= need) return false;
                Velocity = need;
                return true;
            }
            if (q > 0f && q < reach + 0.3f)
            {
                // on the side it swings towards: he yanks it open
                if (vn > -0.15f || Angle >= MaxAngle - 2f) return false;
                if (Velocity >= 230f) return false;
                Velocity = 280f;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ interaction

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            if (Boarded)
            {
                if (who.IsOmar) p = InteractPrompt.Hold("SMASH THE BOARDS", 1.6f, ItemType.None, 18f);
                else if (who.Has(ItemType.Crowbar)) p = InteractPrompt.Hold("PRY OFF THE BOARDS", 3f, ItemType.Crowbar, 12f);
                else p = InteractPrompt.Info("BOARDED UP - I NEED A CROWBAR");
                return true;
            }
            if (Locked)
            {
                if (who.IsOmar) p = InteractPrompt.Press("UNLOCK");
                else if (who.Has(ItemType.Lockpick)) p = InteractPrompt.Hold("PICK THE LOCK", 4f, ItemType.Lockpick, 2f);
                else p = InteractPrompt.Info("LOCKED");
                return true;
            }
            // Omar simply walks through doors; prisoners drag the leaf with the mouse
            if (who.IsOmar) { p = default; return false; }
            if (Grabber >= 0 && Grabber != who.PlayerId) { p = InteractPrompt.Info("SOMEONE IS HOLDING IT"); return true; }
            p = InteractPrompt.Drag((Shut ? "PULL / PUSH" : "DRAG") + (Shut ? "   [RMB] PEEK" : Angle <= PrisonerController.PeekMaxAngle ? "   [RMB] EASE SHUT" : ""));
            return true;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (Boarded) { w.SendUse(UseTarget.Door, Index, who.IsOmar ? -1 : who.ItemId(ItemType.Crowbar)); return; }
            if (Locked)
            {
                if (who.IsOmar) w.SendDoor(Index, true);
                else if (who.Has(ItemType.Lockpick)) w.SendUse(UseTarget.Door, Index, who.ItemId(ItemType.Lockpick));
                else Rattle();
            }
        }

        public void Rattle() => AudioManager.Play3D(Snd.LockedRattle, Info.Center + Vector3.up, 0.8f, Random.Range(0.92f, 1.06f));

        // ------------------------------------------------------------------ network

        /// <summary>Reliable state from the host (locks, boards, resting angle).</summary>
        public void ApplyState(bool locked, bool boarded, float angle, float velocity, bool authority)
        {
            bool boardsChanged = boarded != Boarded;
            bool unlocked = Locked && !locked;
            Locked = locked; Boarded = boarded;
            Vector3 p = Info.Center + Vector3.up;
            if (boardsChanged && !Boarded)
            {
                if (Info.Boards != null) Info.Boards.SetActive(false);
                AudioManager.Play3D(Snd.WoodBreak, p, 1f, 1f, 2f, 30f);
                try { PsxFx.Dust(p, 1.2f); } catch { }
            }
            if (boardsChanged && Boarded && Info.Boards != null) Info.Boards.SetActive(true);
            if (unlocked) AudioManager.Play3D(Snd.DoorUnlock, p, 0.8f, Random.Range(0.95f, 1.05f), 1.5f, 14f);
            UpdateLeafLayer();
            if (authority) { Angle = angle; Velocity = velocity; }
            else SetNet(angle, velocity);
        }

        /// <summary>Angle stream from the host.</summary>
        public void SetNet(float angle, float velocity)
        {
            _netAngle = Mathf.Clamp(angle, 0f, MaxAngle);
            _netVel = velocity;
            _netAt = Time.time;
        }

        /// <summary>Locked / boarded leaves block Omar too (he only passes through doors he can shove open).</summary>
        void UpdateLeafLayer()
        {
            if (Info.Leaf == null) return;
            int layer = Locked || Boarded ? Layers.World : Layers.Door;
            if (Info.Leaf.gameObject.layer != layer) Info.Leaf.gameObject.layer = layer;
        }

        /// <summary>Play the sound of a limit hit.</summary>
        public void PlayHit(DoorHit hit, float strength)
        {
            Vector3 p = Info.Center + Vector3.up;
            switch (hit)
            {
                case DoorHit.Latch:
                    AudioManager.Play3D(Metal ? Snd.MetalDoorClose : AudioManager.Variant(Snd.DoorLatch, 2), p, Metal ? 0.6f : 0.7f, Random.Range(0.93f, 1.06f), 1.5f, 16f);
                    break;
                case DoorHit.Slam:
                    AudioManager.Play3D(Snd.DoorSlam, p, Mathf.Clamp(0.6f + strength * 0.4f, 0.6f, 1f), Random.Range(0.9f, 1.04f), 2f, 38f);
                    try { PsxFx.Dust(p + Vector3.up * 0.8f, 0.5f); } catch { }
                    break;
                case DoorHit.Bump:
                case DoorHit.Blocked:
                    AudioManager.Play3D(AudioManager.Variant(Snd.DoorBump, 2), p, Mathf.Clamp(0.35f + strength * 0.4f, 0.35f, 0.8f), Random.Range(0.9f, 1.08f), 1.5f, 20f);
                    break;
            }
        }

        // ------------------------------------------------------------------ per frame

        /// <summary>Presentation. <paramref name="authority"/> = this machine runs the door simulation (host).</summary>
        public void Tick(float dt, bool authority)
        {
            if (!authority && !LocalDrive)
            {
                if (Time.time < PredictUntil)
                {
                    Integrate(dt, 0);
                    // fold the host's stream in gently while predicting
                    if (_netAt >= 0f) Angle = Mathf.Lerp(Angle, NetTarget(), 1f - Mathf.Exp(-dt * 2f));
                }
                else if (_netAt >= 0f)
                {
                    Angle = Mathf.Lerp(Angle, NetTarget(), 1f - Mathf.Exp(-dt * 14f));
                    Velocity = _netVel;
                }
            }
            Angle = Mathf.Clamp(Angle, 0f, MaxAngle);

            // the shown leaf follows the simulated angle smoothly (remote drags arrive ~20 times a second)
            float shown = LocalDrive ? Angle : Mathf.Lerp(_shownAngle, Angle, 1f - Mathf.Exp(-dt * 22f));
            if (Mathf.Abs(shown - Angle) < 0.02f) shown = Angle;
            float speed = dt > 0f ? Mathf.Abs(shown - _shownAngle) / dt : 0f;
            if (Mathf.Abs(shown - _shownAngle) > 0.0001f)
            {
                _shownAngle = shown;
                ApplyRotation();
            }
            UpdateCreak(speed, dt);
        }

        float NetTarget()
        {
            float ahead = Mathf.Clamp(Time.time - _netAt, 0f, 0.12f);
            return Mathf.Clamp(_netAngle + _netVel * ahead, 0f, MaxAngle);
        }

        void UpdateCreak(float speed, float dt)
        {
            // hinges groan while the leaf moves; louder and higher the faster it swings
            float target = speed < 6f ? 0f : Mathf.Clamp01((speed - 6f) / 160f);
            if (Angle < PrisonerController.PeekMaxAngle + 2f && speed < 40f) target = 0f; // easing it open a crack / shut again is silent
            _creakVol = Mathf.MoveTowards(_creakVol, target, dt * (target > _creakVol ? 6f : 2.5f));
            if (_creakVol > 0.01f)
            {
                if (_creak == null)
                {
                    string clip = Metal ? Snd.MetalDoorCreakLoop : AudioManager.Variant(Snd.DoorCreakLoop, 2);
                    _creak = AudioManager.Loop3D(clip, Hinge + Vector3.up * 1.2f, 0f, 16f, AudioCategory.Sfx, null, 0f);
                }
                AudioManager.SetVolume(_creak, _creakVol * (Metal ? 0.55f : 0.6f));
                AudioManager.SetPitch(_creak, 0.82f + _creakVol * 0.35f);
            }
            else if (_creak != null)
            {
                AudioManager.Stop(_creak, 0.15f);
                _creak = null;
            }
        }

        public void StopSounds()
        {
            AudioManager.Stop(_creak, 0.05f);
            _creak = null;
        }

        void ApplyRotation()
        {
            if (Info.Pivot != null) Info.Pivot.localRotation = _closed * Quaternion.Euler(0, Sign * _shownAngle, 0);
        }
    }

    public sealed class HidingEntity : IInteractable
    {
        public readonly int Index;
        public readonly HidingSpotInfo Info;
        public int Occupant = -1;
        readonly Quaternion[] _closed;
        readonly Quaternion _liftClosed;
        float _openTimer;
        float _openAmount;
        float _liftTimer, _lift;
        bool _liftDownPlayed = true;

        public bool IsBed => Info.Kind == HidingKind.UnderBed;
        /// <summary>0..1 how far Omar has tipped the bed up.</summary>
        public float Lift => _lift;
        /// <summary>Omar has just flipped the bed (it is going up / staying up).</summary>
        public bool Lifting => _liftTimer > 0f;

        public HidingEntity(int index, HidingSpotInfo info)
        {
            Index = index; Info = info;
            int n = info.Doors != null ? info.Doors.Length : 0;
            _closed = new Quaternion[n];
            for (int i = 0; i < n; i++) _closed[i] = info.Doors[i] != null ? info.Doors[i].localRotation : Quaternion.identity;
            _liftClosed = info.LiftPivot != null ? info.LiftPivot.localRotation : Quaternion.identity;
            if (info.Interact != null) InteractableRef.Attach(info.Interact, this);
        }

        public Vector3 InteractPoint => Info.Interact != null ? Info.Interact.bounds.center : Info.ExitPose.position;

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            if (who.IsOmar) { p = InteractPrompt.Hold(IsBed ? "LOOK UNDER THE BED" : "SEARCH", IsBed ? 0.5f : 0.9f); return true; }
            if (who.Status != null && who.Status.HidingSpot == Index) { p = InteractPrompt.Press("LEAVE"); return true; }
            if (Occupant >= 0) { p = InteractPrompt.Info("SOMEONE IS ALREADY HIDING HERE"); return true; }
            if (who.Status != null && who.Status.Trapped) { p = default; return false; }
            p = InteractPrompt.Press(IsBed ? "CRAWL UNDER" : "HIDE");
            return true;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (who.IsOmar) w.SendSearch(Index);
            else w.SendUse(UseTarget.Hiding, Index, -1);
        }

        public void Apply(int occupant, bool searched)
        {
            bool entered = occupant >= 0 && occupant != Occupant;
            bool left = occupant < 0 && Occupant >= 0 && !searched;
            Occupant = occupant;
            Vector3 p = InteractPoint;
            if (searched)
            {
                if (IsBed)
                {
                    // Omar grabs the frame and tips the whole bed up
                    _liftTimer = 1.9f;
                    _liftDownPlayed = false;
                    AudioManager.Play3D(Snd.BedLift, p, 1f, Random.Range(0.92f, 1.04f), 2f, 28f, AudioCategory.Omar);
                }
                else
                {
                    _openTimer = 1.4f;
                    AudioManager.Play3D(Snd.HidingRip, p, 1f, 1f, 2f, 25f, AudioCategory.Omar);
                }
                return;
            }
            if (entered || left)
            {
                if (IsBed)
                    AudioManager.Play3D(entered ? Snd.BedCrawlIn : Snd.BedCrawlOut, p, 0.55f, Random.Range(0.95f, 1.05f), 1.2f, 9f);
                else
                {
                    _openTimer = entered ? 0.95f : 0.8f;
                    string clip = Info.Kind == HidingKind.Wardrobe ? (entered ? Snd.WardrobeEnter : Snd.WardrobeExit) : (entered ? Snd.MetalDoorOpen : Snd.MetalDoorClose);
                    AudioManager.Play3D(clip, p, 0.55f, Random.Range(0.95f, 1.08f), 1.5f, 10f);
                }
            }
        }

        public void Tick(float dt)
        {
            TickLift(dt);
            if (Info.Doors == null || Info.Doors.Length == 0) return;
            _openTimer -= dt;
            float target = _openTimer > 0 ? 1f : 0f;
            if (Mathf.Approximately(_openAmount, target)) return;
            _openAmount = Mathf.MoveTowards(_openAmount, target, dt * 2.6f);
            float e = _openAmount * _openAmount * (3f - 2f * _openAmount);
            for (int i = 0; i < Info.Doors.Length; i++)
            {
                if (Info.Doors[i] == null) continue;
                float ang = Info.DoorOpenAngles != null && i < Info.DoorOpenAngles.Length ? Info.DoorOpenAngles[i] : 80f;
                Info.Doors[i].localRotation = _closed[i] * Quaternion.Euler(0, ang * e, 0);
            }
        }

        void TickLift(float dt)
        {
            if (Info.LiftPivot == null) return;
            _liftTimer -= dt;
            float target = _liftTimer > 0f ? 1f : 0f;
            if (Mathf.Approximately(_lift, target) && _liftDownPlayed) return;
            // quick heave up, slower drop back down
            _lift = Mathf.MoveTowards(_lift, target, dt * (target > _lift ? 2.6f : 1.8f));
            float e = target > 0.5f ? 1f - (1f - _lift) * (1f - _lift) : _lift * _lift;
            Info.LiftPivot.localRotation = _liftClosed * Quaternion.AngleAxis(Info.LiftAngle * e, Info.LiftAxis);
            if (!_liftDownPlayed && target < 0.5f && _lift <= 0f)
            {
                _liftDownPlayed = true;
                Vector3 p = InteractPoint;
                AudioManager.Play3D(AudioManager.Variant(Snd.DoorBump, 2), p, 0.9f, 0.7f, 2f, 22f);
                try { PsxFx.Dust(p, 0.8f); } catch { }
            }
        }
    }

    public sealed class CageEntity : IInteractable
    {
        public readonly int Index;
        public readonly CageInfo Info;
        public bool Open;
        public int Occupant = -1;
        /// <summary>Local time of the last lock rattle from the prisoner inside (Omar may punish them for a while).</summary>
        public float RattleAt = -999f;
        float _angle, _target;
        readonly Quaternion _closed;

        public CageEntity(int index, CageInfo info)
        {
            Index = index; Info = info;
            _closed = info.DoorPivot != null ? info.DoorPivot.localRotation : Quaternion.identity;
            if (info.Interact != null) InteractableRef.Attach(info.Interact, this);
            if (info.DoorCollider != null) InteractableRef.Attach(info.DoorCollider, this);
        }

        public Vector3 InteractPoint => Info.Outside.position + Vector3.up;

        /// <summary>(iteration 2) Cage slots start inactive (hidden + no collision); the spawn logic activates the ones in use.</summary>
        public bool Active => Info.Root == null || Info.Root.gameObject.activeSelf;
        public void SetActive(bool active)
        {
            if (Info.Root != null && Info.Root.gameObject.activeSelf != active) Info.Root.gameObject.SetActive(active);
        }

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            if (who.IsOmar)
            {
                if (Open || Occupant < 0 || Time.time - RattleAt > Tuning.CagePunishWindow - 1f) return false;
                p = InteractPrompt.Hold("TRIED TO GET OUT? OPEN THE CAGE...", 1.0f);
                return true;
            }
            if (who.Status != null && who.Status.Cage == Index && !Open)
            {
                p = InteractPrompt.Press("FORCE THE LOCK (MASH E) - IT CLANKS, HE MAY HEAR");
                return true;
            }
            if (Open || Occupant < 0) return false;
            if (who.Has(ItemType.CageKey)) p = InteractPrompt.Hold("UNLOCK THE CAGE", 1.5f, ItemType.CageKey);
            else if (who.Has(ItemType.BoltCutters)) p = InteractPrompt.Hold("CUT THE CAGE LOCK", 2.5f, ItemType.BoltCutters, 22f);
            else if (who.Has(ItemType.Lockpick)) p = InteractPrompt.Hold("PICK THE CAGE LOCK", 4f, ItemType.Lockpick, 2f);
            else p = InteractPrompt.Info("LOCKED - FIND THE CAGE KEY");
            return true;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (who.IsOmar) { w.SendUse(UseTarget.Cage, Index, -1); return; }
            if (who.Status != null && who.Status.Cage == Index) { w.SendStruggle(0); return; }
            int item = who.ItemId(ItemType.CageKey);
            if (item < 0) item = who.ItemId(ItemType.BoltCutters);
            if (item < 0) item = who.ItemId(ItemType.Lockpick);
            if (item >= 0) w.SendUse(UseTarget.Cage, Index, item);
        }

        public void Apply(bool open, int occupant)
        {
            if (occupant >= 0) SetActive(true); // a slot appears once somebody is locked in it
            bool changed = open != Open;
            Open = open; Occupant = occupant;
            _target = Open ? Info.OpenAngle : 0f;
            if (changed && Active) AudioManager.Play3D(Open ? Snd.CageOpen : Snd.CageClose, InteractPoint, 0.45f, 1f, 1.5f, 11f);
        }

        public void Tick(float dt)
        {
            if (Mathf.Approximately(_angle, _target)) return;
            _angle = Mathf.MoveTowards(_angle, _target, 140f * dt);
            if (Info.DoorPivot != null) Info.DoorPivot.localRotation = _closed * Quaternion.Euler(0, _angle, 0);
            if (Info.DoorCollider != null) Info.DoorCollider.enabled = Mathf.Abs(_angle) < 20f;
        }
    }

    public sealed class TrapEntity : IInteractable
    {
        public readonly int Index;
        public readonly TrapKind Kind;
        public readonly Vector3 A, B;
        public TrapState State;
        public int Victim = -1;
        public GameObject Root;
        GameObject _trap, _wire;
        readonly System.Collections.Generic.List<Transform> _cans = new System.Collections.Generic.List<Transform>();
        float _jaw;

        public TrapEntity(int index, TrapKind kind, Vector3 a, Vector3 b, Transform parent)
        {
            Index = index; Kind = kind; A = a; B = b;
            Root = new GameObject("Trap_" + index + "_" + kind);
            Root.transform.SetParent(parent, false);
            BuildVisual();
        }

        void BuildVisual()
        {
            try
            {
                if (Kind == TrapKind.Tripwire)
                {
                    Vector3 dir = B - A; dir.y = 0;
                    Quaternion rot = dir.sqrMagnitude > 0.001f ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
                    var s1 = ItemMeshFactory.BuildTripwireStake(); s1.transform.SetParent(Root.transform, false); s1.transform.SetPositionAndRotation(new Vector3(A.x, A.y - 0.12f, A.z), rot);
                    var s2 = ItemMeshFactory.BuildTripwireStake(); s2.transform.SetParent(Root.transform, false); s2.transform.SetPositionAndRotation(new Vector3(B.x, B.y - 0.12f, B.z), rot);
                    var mb = new MeshBuilder();
                    mb.SetMaterial(PsxMaterials.Get("Textures/Env/metal_galvanized", PsxSurface.Unlit, new Color(0.55f, 0.55f, 0.5f)));
                    mb.AddBeam(A, B, 0.012f);
                    _wire = mb.Build("Wire", Root.transform, Layers.World);
                    // Omar's siren box wired to the first post, and tin cans hanging off the wire as rattles
                    var siren = ItemMeshFactory.BuildTripwireSiren();
                    siren.transform.SetParent(Root.transform, false);
                    siren.transform.SetPositionAndRotation(new Vector3(A.x, A.y - 0.12f, A.z) + rot * new Vector3(0.07f, 0f, 0.09f), rot * Quaternion.Euler(0, -90f, 0));
                    int cans = (B - A).magnitude > 1.1f ? 2 : 1;
                    for (int k = 1; k <= cans; k++)
                    {
                        var can = ItemMeshFactory.BuildTinCan(Index * 3 + k);
                        can.transform.SetParent(Root.transform, false);
                        can.transform.SetPositionAndRotation(Vector3.Lerp(A, B, (float)k / (cans + 1)) - Vector3.up * ItemMeshFactory.CanString,
                            rot * Quaternion.Euler(0, k * 70f, 0));
                        _cans.Add(can.transform);
                    }
                    // interaction volume along the wire
                    var c = GeoUtil.AddBox(Root.transform, (A + B) * 0.5f, new Vector3(0.45f, 0.4f, Mathf.Max(0.3f, (B - A).magnitude)), rot, Layers.Interactable, SurfaceType.Default, true, "TrapInteract");
                    InteractableRef.Attach(c, this);
                }
                else
                {
                    var trap = ItemMeshFactory.BuildBearTrap();
                    trap.transform.SetParent(Root.transform, false);
                    trap.transform.position = A;
                    trap.transform.rotation = Quaternion.Euler(0, (Index * 47) % 360, 0);
                    _trap = trap;
                    var c = GeoUtil.AddBox(Root.transform, A + Vector3.up * 0.15f, new Vector3(0.7f, 0.35f, 0.7f), Quaternion.identity, Layers.Interactable, SurfaceType.Default, true, "TrapInteract");
                    InteractableRef.Attach(c, this);
                }
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        public Vector3 InteractPoint => (A + B) * 0.5f;

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            if (who.IsOmar)
            {
                if (State == TrapState.Armed) { p = InteractPrompt.Info(Kind == TrapKind.Tripwire ? "YOUR WIRE" : "YOUR TRAP"); return true; }
                return false;
            }
            if (Kind == TrapKind.BearTrap && Victim == who.PlayerId) { p = InteractPrompt.Press("STRUGGLE FREE (MASH E)"); return true; }
            if (Kind == TrapKind.BearTrap && Victim >= 0) { p = InteractPrompt.Hold("PRY THE TRAP OPEN", Tuning.TrapPryTime); return true; }
            if (State != TrapState.Armed) return false;
            string what = Kind == TrapKind.Tripwire ? "A TRIPWIRE" : "A BEAR TRAP";
            if (!who.Crouching) { p = InteractPrompt.Info(what + " - CROUCH (C) TO DISARM"); return true; }
            p = InteractPrompt.Hold("DISARM " + what, Tuning.TrapDisarmTime);
            return true;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (Kind == TrapKind.BearTrap && Victim == who.PlayerId) { w.SendStruggle(1); return; }
            w.SendUse(UseTarget.Trap, Index, -1);
        }

        public void Apply(TrapState state, int victim)
        {
            var prev = State;
            State = state; Victim = victim;
            if (prev == TrapState.Armed && state != TrapState.Armed && Kind == TrapKind.Tripwire)
            {
                if (_wire != null) _wire.SetActive(false);
                // the snapped wire drops its cans: they lie on their sides on the floor
                float ground = (A.y + B.y) * 0.5f - 0.12f;
                for (int i = 0; i < _cans.Count; i++)
                {
                    var c = _cans[i];
                    if (c == null) continue;
                    Vector3 p = c.position;
                    c.SetPositionAndRotation(new Vector3(p.x + (i - 0.5f) * 0.12f, ground + 0.029f, p.z + 0.04f * (i % 2 == 0 ? 1f : -1f)),
                        Quaternion.Euler(0, Index * 53f + i * 110f, 0) * Quaternion.Euler(0, 0, 90f));
                }
            }
        }

        public void Tick(float dt)
        {
            if (Kind != TrapKind.BearTrap) return;
            float target = State == TrapState.Armed ? 0f : 1f;
            if (Mathf.Approximately(_jaw, target)) return;
            _jaw = Mathf.MoveTowards(_jaw, target, dt * 12f);
            ItemMeshFactory.SetBearTrapJaws(_trap, _jaw);
        }

        /// <summary>Did a foot moving from p0 to p1 (feet positions) set this trap off?</summary>
        public bool Crossed(Vector3 p0, Vector3 p1)
        {
            if (State != TrapState.Armed) return false;
            if (Kind == TrapKind.BearTrap)
            {
                if (Mathf.Abs(p1.y - A.y) > 0.6f) return false;
                return GeoUtil.FlatDistance(p1, A) < 0.38f;
            }
            float wy = (A.y + B.y) * 0.5f;
            if (p1.y > wy + 0.05f || p1.y < wy - 0.9f) return false;
            return SegmentsIntersect2D(new Vector2(p0.x, p0.z), new Vector2(p1.x, p1.z), new Vector2(A.x, A.z), new Vector2(B.x, B.z));
        }

        static bool SegmentsIntersect2D(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
        {
            Vector2 r = p2 - p, s = q2 - q;
            float d = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(d) < 1e-6f) return false;
            Vector2 qp = q - p;
            float t = (qp.x * s.y - qp.y * s.x) / d;
            float u = (qp.x * r.y - qp.y * r.x) / d;
            return t >= 0 && t <= 1 && u >= 0 && u <= 1;
        }
    }

    public sealed class NoteEntity : IInteractable
    {
        public readonly int Index;
        public readonly NoteSpotInfo Info;
        public string Title;
        public string Text;

        public NoteEntity(int index, NoteSpotInfo info, string title, string text, Transform parent)
        {
            Index = index; Info = info; Title = title; Text = text;
            try
            {
                var mb = new MeshBuilder();
                bool wall = info.OnWall;
                var mat = wall ? PsxMaterials.Get("Textures/Props/paper_note", PsxSurface.Decal, new Color(0.85f, 0.8f, 0.7f))
                               : PsxMaterials.Get("Textures/Props/paper_note", PsxSurface.LitDoubleSided);
                mb.SetMaterial(mat);
                float w = 0.22f, h = 0.28f;
                mb.Push(info.Position + info.Rotation * Vector3.forward * 0.006f, info.Rotation);
                mb.AddQuad(new Vector3(w * 0.5f, -h * 0.5f, 0), new Vector3(w * 0.5f, h * 0.5f, 0), new Vector3(-w * 0.5f, h * 0.5f, 0), new Vector3(-w * 0.5f, -h * 0.5f, 0));
                mb.Pop();
                var go = mb.Build("Note_" + index, parent, Layers.World);
                var c = GeoUtil.AddBox(parent, info.Position, new Vector3(0.4f, 0.4f, 0.25f), info.Rotation, Layers.Interactable, SurfaceType.Default, true, "NoteInteract_" + index);
                InteractableRef.Attach(c, this);
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        public Vector3 InteractPoint => Info.Position;

        public bool GetPrompt(Interactor who, out InteractPrompt p) { p = InteractPrompt.Press("READ"); return true; }

        public void Interact(Interactor who) => MatchWorld.Instance?.OpenNote(this);
    }

    public enum ObjectiveKind { Gate, CarFuel, CarHood, CarDriver, CarPassenger, FuseBox, Radio, Barrels, Keypad }

    /// <summary>Objective interaction points (padlock, car parts, fuse box, radio, drums, keypad).</summary>
    public sealed class ObjectiveTarget : IInteractable
    {
        public readonly ObjectiveKind Kind;
        readonly Collider _col;

        public ObjectiveTarget(ObjectiveKind kind, Collider col)
        {
            Kind = kind; _col = col;
            if (col != null) InteractableRef.Attach(col, this);
        }

        public Vector3 InteractPoint => _col != null ? _col.bounds.center : Vector3.zero;

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            var w = MatchWorld.Instance;
            if (w == null || who.IsOmar) return false;
            var o = w.Objectives;
            var st = who.Status;
            switch (Kind)
            {
                case ObjectiveKind.Gate:
                    if (o.GateCut) return false;
                    p = who.Has(ItemType.BoltCutters) ? InteractPrompt.Hold("CUT THE PADLOCK", 3f, ItemType.BoltCutters, 25f) : InteractPrompt.Info("A HEAVY PADLOCK AND CHAIN");
                    return true;
                case ObjectiveKind.CarFuel:
                    if (o.CarFueled) { p = InteractPrompt.Info("THE TANK IS FULL"); return true; }
                    p = who.Has(ItemType.GasCan) ? InteractPrompt.Hold("POUR THE GASOLINE", 4f, ItemType.GasCan, 4f) : InteractPrompt.Info("THE GAS GAUGE SAID EMPTY...");
                    return true;
                case ObjectiveKind.CarHood:
                    if (o.CarBatteryOk) { p = InteractPrompt.Info("THE ENGINE LOOKS... FINE"); return true; }
                    p = who.Has(ItemType.CarBattery) ? InteractPrompt.Hold("REPLACE THE BATTERY", 4f, ItemType.CarBattery, 6f) : InteractPrompt.Info("THE BATTERY IS CORRODED. DEAD.");
                    return true;
                case ObjectiveKind.CarDriver:
                    if (o.CarGone) return false;
                    if (st != null && st.InCar)
                    {
                        if (o.CarStarted && o.CarDriver == who.PlayerId) p = InteractPrompt.Hold("DRIVE!", 1f);
                        else p = InteractPrompt.Press("GET OUT");
                        return true;
                    }
                    if (o.CarStarted) { p = InteractPrompt.Press("GET IN"); return true; }
                    p = who.Has(ItemType.CarKeys) ? InteractPrompt.Hold("START THE ENGINE", 2f, ItemType.CarKeys) : InteractPrompt.Info("LOCKED. I NEED THE KEYS");
                    return true;
                case ObjectiveKind.CarPassenger:
                    if (o.CarGone) return false;
                    if (st != null && st.InCar) { p = InteractPrompt.Press("GET OUT"); return true; }
                    p = o.CarStarted ? InteractPrompt.Press("GET IN") : InteractPrompt.Info("NO POINT SITTING IN A DEAD CAR");
                    return true;
                case ObjectiveKind.FuseBox:
                    if (o.FuseIn) { p = InteractPrompt.Info("THE FUSE IS IN. SOMETHING HUMS UPSTAIRS."); return true; }
                    p = who.Has(ItemType.Fuse) ? InteractPrompt.Hold("INSERT THE FUSE", 2f, ItemType.Fuse) : InteractPrompt.Info("ONE FUSE IS MISSING. LABEL: 'RADIO ROOM'");
                    return true;
                case ObjectiveKind.Radio:
                    if (!o.FuseIn) { p = InteractPrompt.Info("THE RADIO IS DEAD. NO POWER."); return true; }
                    if (o.RescueGone) { p = InteractPrompt.Info("ONLY STATIC NOW."); return true; }
                    if (o.RadioCalled) { p = InteractPrompt.Info(o.RescuePresent ? "THE HELICOPTER IS IN THE FIELD! GO!" : "THEY ARE COMING. GET TO THE FIELD CLEARING."); return true; }
                    p = InteractPrompt.Hold("CALL FOR HELP", 6f, ItemType.None, 22f);
                    return true;
                case ObjectiveKind.Barrels:
                    if (o.Exploded) return false;
                    if (o.IgniteAt > 0f) { p = InteractPrompt.Info("RUN!"); return true; }
                    if (o.BarrelsPoured)
                    {
                        var lighter = w.LocalLighterWithFuel();
                        p = lighter ? InteractPrompt.Hold("IGNITE", 1.5f, ItemType.Lighter) : InteractPrompt.Info("SOAKED IN FUEL. I NEED A FLAME.");
                        return true;
                    }
                    p = who.Has(ItemType.LighterFuel) ? InteractPrompt.Hold("POUR LIGHTER FUEL ON THE DRUMS", 3f, ItemType.LighterFuel, 3f) : InteractPrompt.Info("FUEL DRUMS. 'FLAMMABLE'");
                    return true;
                case ObjectiveKind.Keypad:
                    if (o.ShelterOpen) { p = InteractPrompt.Info("THE SHELTER DOOR IS OPEN"); return true; }
                    p = InteractPrompt.Press("USE THE KEYPAD");
                    return true;
            }
            return false;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            var o = w.Objectives;
            switch (Kind)
            {
                case ObjectiveKind.Gate: w.SendUse(UseTarget.Gate, 0, who.ItemId(ItemType.BoltCutters)); break;
                case ObjectiveKind.CarFuel: w.SendUse(UseTarget.CarFuel, 0, who.ItemId(ItemType.GasCan)); break;
                case ObjectiveKind.CarHood: w.SendUse(UseTarget.CarHood, 0, who.ItemId(ItemType.CarBattery)); break;
                case ObjectiveKind.CarDriver:
                    if (who.Status != null && who.Status.InCar && !(o.CarStarted && o.CarDriver == who.PlayerId)) w.SendUse(UseTarget.CarExit, 0, -1);
                    else if (o.CarStarted && (who.Status == null || !who.Status.InCar)) w.SendUse(UseTarget.CarPassenger, 0, -1);
                    else w.SendUse(UseTarget.CarDriver, 0, who.ItemId(ItemType.CarKeys));
                    break;
                case ObjectiveKind.CarPassenger:
                    w.SendUse(who.Status != null && who.Status.InCar ? UseTarget.CarExit : UseTarget.CarPassenger, 0, -1);
                    break;
                case ObjectiveKind.FuseBox: w.SendUse(UseTarget.FuseBox, 0, who.ItemId(ItemType.Fuse)); break;
                case ObjectiveKind.Radio: w.SendUse(UseTarget.Radio, 0, -1); break;
                case ObjectiveKind.Barrels:
                    if (o.BarrelsPoured) w.SendUse(UseTarget.Ignite, 0, who.ItemId(ItemType.Lighter));
                    else w.SendUse(UseTarget.Barrels, 0, who.ItemId(ItemType.LighterFuel));
                    break;
                case ObjectiveKind.Keypad: w.OpenKeypad(); break;
            }
        }
    }
}
