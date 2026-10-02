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
        float _glintPhase;

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

    public sealed class DoorEntity : IInteractable
    {
        public readonly int Index;
        public readonly DoorInfo Info;
        public bool Open, Locked, Boarded;
        float _angle, _target;
        readonly Quaternion _closed;
        float _speed = 170f;

        public DoorEntity(int index, DoorInfo info)
        {
            Index = index; Info = info;
            _closed = info.Pivot != null ? info.Pivot.localRotation : Quaternion.identity;
            Open = info.StartsOpen;
            Locked = info.StartsLocked;
            Boarded = info.Kind == DoorKind.Boarded;
            _angle = _target = Open ? info.OpenAngle : 0f;
            ApplyRotation();
            if (info.Leaf != null) InteractableRef.Attach(info.Leaf, this);
            if (info.Boards != null)
            {
                foreach (var c in info.Boards.GetComponentsInChildren<Collider>()) InteractableRef.Attach(c, this);
                info.Boards.SetActive(Boarded);
            }
        }

        public Vector3 InteractPoint => Info.Center + Vector3.up;

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            if (Boarded)
            {
                if (who.IsOmar) p = InteractPrompt.Hold("SMASH THE BOARDS", 1.6f, ItemType.None, 18f);
                else if (who.Has(ItemType.Crowbar)) p = InteractPrompt.Hold("PRY OFF THE BOARDS", 3f, ItemType.Crowbar, 12f);
                else p = InteractPrompt.Info("BOARDED UP - I NEED A CROWBAR");
                return true;
            }
            if (Locked && !Open)
            {
                if (who.IsOmar) p = InteractPrompt.Press("UNLOCK");
                else if (who.Has(ItemType.Lockpick)) p = InteractPrompt.Hold("PICK THE LOCK", 4f, ItemType.Lockpick, 2f);
                else p = InteractPrompt.Info("LOCKED");
                return true;
            }
            p = InteractPrompt.Press(Open ? "CLOSE" : "OPEN");
            return true;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (Boarded) { w.SendUse(UseTarget.Door, Index, who.IsOmar ? -1 : who.ItemId(ItemType.Crowbar)); return; }
            if (Locked && !Open)
            {
                if (who.IsOmar) w.SendDoor(Index, true);
                else if (who.Has(ItemType.Lockpick)) w.SendUse(UseTarget.Door, Index, who.ItemId(ItemType.Lockpick));
                else AudioManager.Play3D(Snd.LockedRattle, Info.Center + Vector3.up, 0.8f);
                return;
            }
            w.SendDoor(Index, !Open);
        }

        /// <summary>Host state arrived.</summary>
        public void Apply(bool open, bool locked, bool boarded, bool slam)
        {
            bool openChanged = open != Open;
            bool boardsChanged = boarded != Boarded;
            Open = open; Locked = locked; Boarded = boarded;
            _target = Open ? Info.OpenAngle : 0f;
            _speed = slam ? 480f : 170f;
            Vector3 p = Info.Center + Vector3.up;
            if (boardsChanged && !Boarded)
            {
                if (Info.Boards != null) Info.Boards.SetActive(false);
                AudioManager.Play3D(Snd.WoodBreak, p, 1f, 1f, 2f, 30f);
                try { PsxFx.Dust(p, 1.2f); } catch { }
            }
            if (openChanged)
            {
                bool metal = Info.Kind == DoorKind.Metal || Info.Kind == DoorKind.Restroom || Info.Kind == DoorKind.Silo || Info.Kind == DoorKind.Shelter;
                string clip;
                if (slam) clip = Snd.DoorSlam;
                else if (metal) clip = Open ? Snd.MetalDoorOpen : Snd.MetalDoorClose;
                else clip = AudioManager.Variant(Open ? Snd.DoorOpen : Snd.DoorClose, 2);
                AudioManager.Play3D(clip, p, slam ? 1f : 0.75f, Random.Range(0.93f, 1.05f), 1.5f, slam ? 35f : 18f);
            }
        }

        public void Tick(float dt)
        {
            if (Mathf.Approximately(_angle, _target)) return;
            _angle = Mathf.MoveTowards(_angle, _target, _speed * dt);
            ApplyRotation();
        }

        void ApplyRotation()
        {
            if (Info.Pivot != null) Info.Pivot.localRotation = _closed * Quaternion.Euler(0, _angle, 0);
        }
    }

    public sealed class HidingEntity : IInteractable
    {
        public readonly int Index;
        public readonly HidingSpotInfo Info;
        public int Occupant = -1;
        readonly Quaternion[] _closed;
        float _openTimer;
        float _openAmount;

        public HidingEntity(int index, HidingSpotInfo info)
        {
            Index = index; Info = info;
            int n = info.Doors != null ? info.Doors.Length : 0;
            _closed = new Quaternion[n];
            for (int i = 0; i < n; i++) _closed[i] = info.Doors[i] != null ? info.Doors[i].localRotation : Quaternion.identity;
            if (info.Interact != null) InteractableRef.Attach(info.Interact, this);
        }

        public Vector3 InteractPoint => Info.Interact != null ? Info.Interact.bounds.center : Info.ExitPose.position;

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            if (who.IsOmar) { p = InteractPrompt.Hold(Info.Kind == HidingKind.UnderBed ? "LOOK UNDER THE BED" : "SEARCH", 0.9f); return true; }
            if (who.Status != null && who.Status.HidingSpot == Index) { p = InteractPrompt.Press("LEAVE"); return true; }
            if (Occupant >= 0) { p = InteractPrompt.Info("SOMEONE IS ALREADY HIDING HERE"); return true; }
            if (who.Status != null && who.Status.Trapped) { p = default; return false; }
            p = InteractPrompt.Press("HIDE");
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
            bool changed = occupant != Occupant;
            Occupant = occupant;
            if (changed || searched) _openTimer = searched ? 1.4f : 0.7f;
            Vector3 p = InteractPoint;
            if (searched)
            {
                AudioManager.Play3D(Snd.HidingRip, p, 1f, 1f, 2f, 25f, AudioCategory.Omar);
            }
            else if (changed && Info.Kind != HidingKind.UnderBed)
            {
                AudioManager.Play3D(Info.Kind == HidingKind.Wardrobe ? Snd.WardrobeOpen : Snd.MetalDoorOpen, p, 0.55f, Random.Range(0.95f, 1.1f), 1.5f, 10f);
            }
        }

        public void Tick(float dt)
        {
            if (Info.Doors == null || Info.Doors.Length == 0) return;
            _openTimer -= dt;
            float target = _openTimer > 0 ? 1f : 0f;
            if (Mathf.Approximately(_openAmount, target)) return;
            _openAmount = Mathf.MoveTowards(_openAmount, target, dt * 3f);
            for (int i = 0; i < Info.Doors.Length; i++)
            {
                if (Info.Doors[i] == null) continue;
                float ang = Info.DoorOpenAngles != null && i < Info.DoorOpenAngles.Length ? Info.DoorOpenAngles[i] : 80f;
                Info.Doors[i].localRotation = _closed[i] * Quaternion.Euler(0, ang * _openAmount, 0);
            }
        }
    }

    public sealed class CageEntity : IInteractable
    {
        public readonly int Index;
        public readonly CageInfo Info;
        public bool Open;
        public int Occupant = -1;
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

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            if (who.IsOmar) return false;
            if (who.Status != null && who.Status.Cage == Index && !Open)
            {
                p = InteractPrompt.Press("STRUGGLE WITH THE LOCK (MASH E)");
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
            if (who.Status != null && who.Status.Cage == Index) { w.SendStruggle(0); return; }
            int item = who.ItemId(ItemType.CageKey);
            if (item < 0) item = who.ItemId(ItemType.BoltCutters);
            if (item < 0) item = who.ItemId(ItemType.Lockpick);
            if (item >= 0) w.SendUse(UseTarget.Cage, Index, item);
        }

        public void Apply(bool open, int occupant)
        {
            bool changed = open != Open;
            Open = open; Occupant = occupant;
            _target = Open ? Info.OpenAngle : 0f;
            if (changed) AudioManager.Play3D(Open ? Snd.CageOpen : Snd.CageClose, InteractPoint, 0.9f, 1f, 2f, 20f);
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
        Transform _jawL, _jawR;
        GameObject _wire;
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
                    _jawL = Avatar.FindDeep(trap.transform, "Jaw_L");
                    _jawR = Avatar.FindDeep(trap.transform, "Jaw_R");
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
            if (Victim == who.PlayerId) { p = InteractPrompt.Press("STRUGGLE FREE (MASH E)"); return true; }
            if (Victim >= 0) { p = InteractPrompt.Hold("PRY THE TRAP OPEN", 2f); return true; }
            if (State != TrapState.Armed) return false;
            string what = Kind == TrapKind.Tripwire ? "A TRIPWIRE" : "A BEAR TRAP";
            if (!who.Crouching) { p = InteractPrompt.Info(what + " - CROUCH (C) TO DISARM"); return true; }
            p = InteractPrompt.Hold("DISARM " + what, 3f);
            return true;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (Victim == who.PlayerId) { w.SendStruggle(1); return; }
            w.SendUse(UseTarget.Trap, Index, -1);
        }

        public void Apply(TrapState state, int victim)
        {
            var prev = State;
            State = state; Victim = victim;
            if (prev == TrapState.Armed && state != TrapState.Armed)
            {
                if (Kind == TrapKind.Tripwire && _wire != null) _wire.SetActive(false);
            }
        }

        public void Tick(float dt)
        {
            if (Kind != TrapKind.BearTrap) return;
            float target = State == TrapState.Armed ? 0f : 1f;
            if (Mathf.Approximately(_jaw, target)) return;
            _jaw = Mathf.MoveTowards(_jaw, target, dt * 12f);
            if (_jawL != null) _jawL.localRotation = Quaternion.Euler(-80f * _jaw, 0, 0);
            if (_jawR != null) _jawR.localRotation = Quaternion.Euler(80f * _jaw, 0, 0);
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
