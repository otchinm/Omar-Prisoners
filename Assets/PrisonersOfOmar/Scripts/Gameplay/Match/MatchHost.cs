using System.Collections.Generic;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Authoritative game rules, only on the host. Validates every client request (positions, items,
    /// cooldowns), mutates state by broadcasting events (which the host also applies to itself through the
    /// loopback), schedules world events and decides the ending.
    /// Never mutate MatchWorld state objects directly: edit a copy and broadcast it, so every peer
    /// (including the host's own presentation code) sees the same before/after transition.
    /// </summary>
    public sealed class MatchHost
    {
        readonly MatchWorld W;
        readonly NetSession S;
        readonly Dictionary<int, AvatarNetState> _latest = new Dictionary<int, AvatarNetState>();
        readonly Dictionary<int, int[]> _inv = new Dictionary<int, int[]>();
        readonly Dictionary<int, int> _struggle = new Dictionary<int, int>();
        readonly Dictionary<int, int> _thrownBy = new Dictionary<int, int>();
        readonly Dictionary<int, float> _lastAttack = new Dictionary<int, float>();
        readonly Dictionary<int, float> _lastScream = new Dictionary<int, float>();
        readonly DeterministicRandom _rng;
        float _snapTimer, _endCheckTimer, _nextEventAt = 70f, _powerRestoreAt = -1f;
        bool _cagesOpened, _omarAwake, _ended;
        float _allCagedAt = -1f;
        int _wires = Tuning.TripwireCharges, _bears = Tuning.BearTrapCharges;
        float _trapRecharge;
        float _omarStunUntil;
        readonly List<OmarAI> _ais = new List<OmarAI>();

        public MatchHost(MatchWorld w)
        {
            W = w;
            S = w.Session;
            _rng = new DeterministicRandom(w.Seed ^ System.Environment.TickCount, 99);
            foreach (var p in S.Players) _inv[p.Id] = new[] { -1, -1, -1 };
            foreach (var it in W.Items)
                if (it.Holder >= 0 && _inv.TryGetValue(it.Holder, out var slots) && it.Slot >= 0 && it.Slot < 3) slots[it.Slot] = it.Id;
        }

        public void RegisterAI(OmarAI ai) { if (!_ais.Contains(ai)) _ais.Add(ai); }
        public bool OmarStunned => W.Time < _omarStunUntil;

        // ================================================================== helpers

        PlayerInfo Info(int id) => S.Find(id);
        bool IsOmar(int id) { var p = Info(id); return p != null && p.IsOmar; }
        bool IsPrisoner(int id) { var p = Info(id); return p != null && p.IsPrisoner; }

        public Vector3 PosOf(int id)
        {
            var av = W.AvatarOf(id);
            if (av != null && av.Simulated) return av.transform.position;
            if (_latest.TryGetValue(id, out var s)) return s.Position;
            return av != null ? av.Position : Vector3.zero;
        }

        PlayerStatus Edit(int id)
        {
            var cur = W.StatusOf(id);
            var c = new PlayerStatus { Id = id };
            if (cur != null)
            {
                c.Life = cur.Life; c.Injured = cur.Injured; c.Captures = cur.Captures; c.Cage = cur.Cage; c.HidingSpot = cur.HidingSpot;
                c.TrappedBy = cur.TrappedBy; c.CarSeat = cur.CarSeat; c.Route = cur.Route; c.TeleportSeq = cur.TeleportSeq;
            }
            return c;
        }

        void Commit(PlayerStatus st)
        {
            var w = S.Begin(Msg.PlayerStatus);
            st.Write(w);
            S.SendToAll(NetChannel.Reliable);
        }

        ObjectiveData EditObj()
        {
            var o = W.Objectives;
            return new ObjectiveData
            {
                GateCut = o.GateCut, CarFueled = o.CarFueled, CarBatteryOk = o.CarBatteryOk, CarStarted = o.CarStarted, CarDriver = o.CarDriver,
                CarGone = o.CarGone, ShelterOpen = o.ShelterOpen, FuseIn = o.FuseIn, RadioCalled = o.RadioCalled, RescueAt = o.RescueAt,
                RescuePresent = o.RescuePresent, RescueLeaveAt = o.RescueLeaveAt, RescueGone = o.RescueGone, BarrelsPoured = o.BarrelsPoured,
                IgniteAt = o.IgniteAt, Exploded = o.Exploded,
            };
        }

        void CommitObj(ObjectiveData o)
        {
            var w = S.Begin(Msg.ObjectiveState);
            o.Write(w);
            S.SendToAll(NetChannel.Reliable);
        }

        void Event(WorldEventKind k, float a = 0f, float b = 0f)
        {
            var w = S.Begin(Msg.WorldEvent);
            w.WriteByte((byte)k);
            w.WriteFloat(a);
            w.WriteFloat(b);
            S.SendToAll(NetChannel.Reliable);
        }

        void Message(string text, float seconds = 4f, int onlyTo = -1)
        {
            var w = S.Begin(Msg.Message);
            w.WriteString(text);
            w.WriteFloat(seconds);
            if (onlyTo >= 0) S.SendTo(onlyTo, NetChannel.Reliable);
            else S.SendToAll(NetChannel.Reliable);
        }

        bool Holds(int player, int itemId, ItemType type)
        {
            if (itemId < 0 || !_inv.TryGetValue(player, out var slots)) return false;
            var it = W.GetItem(itemId);
            if (it == null || it.Consumed || it.Type != type) return false;
            for (int i = 0; i < 3; i++) if (slots[i] == itemId) return true;
            return false;
        }

        int FindHeld(int player, ItemType type)
        {
            if (!_inv.TryGetValue(player, out var slots)) return -1;
            for (int i = 0; i < 3; i++)
            {
                var it = W.GetItem(slots[i]);
                if (it != null && !it.Consumed && it.Type == type) return it.Id;
            }
            return -1;
        }

        void RemoveFromInv(int player, int itemId)
        {
            if (!_inv.TryGetValue(player, out var slots)) return;
            for (int i = 0; i < 3; i++) if (slots[i] == itemId) slots[i] = -1;
        }

        void Consume(int player, int itemId, byte how = 0)
        {
            RemoveFromInv(player, itemId);
            var w = S.Begin(Msg.ItemConsumed);
            w.WriteShort((short)itemId);
            w.WriteByte((byte)player);
            w.WriteByte(how);
            S.SendToAll(NetChannel.Reliable);
        }

        void SetCharge(int itemId, float charge)
        {
            var w = S.Begin(Msg.ItemCharge);
            w.WriteShort((short)itemId);
            w.WriteUnit(charge);
            S.SendToAll(NetChannel.Reliable);
        }

        void DropItem(int player, int itemId, Vector3 pos, float yaw, float charge)
        {
            RemoveFromInv(player, itemId);
            var w = S.Begin(Msg.ItemDropped);
            w.WriteShort((short)itemId);
            w.WriteVector3(pos);
            w.WriteAngle(yaw);
            w.WriteUnit(charge);
            S.SendToAll(NetChannel.Reliable);
        }

        void BroadcastDoor(DoorEntity d, bool open, bool locked, bool boarded, bool slam)
        {
            var w = S.Begin(Msg.DoorState);
            w.WriteShort((short)d.Index);
            byte f = 0;
            if (open) f |= 1; if (locked) f |= 2; if (boarded) f |= 4; if (slam) f |= 8;
            w.WriteByte(f);
            S.SendToAll(NetChannel.Reliable);
        }

        void BroadcastHide(int spot, int occupant, bool searched)
        {
            var w = S.Begin(Msg.HideState);
            w.WriteByte((byte)spot);
            w.WriteByte((byte)(occupant < 0 ? 255 : occupant));
            w.WriteBool(searched);
            S.SendToAll(NetChannel.Reliable);
        }

        void BroadcastCage(int cage, bool open, int occupant)
        {
            var w = S.Begin(Msg.CageState);
            w.WriteByte((byte)cage);
            w.WriteBool(open);
            w.WriteByte((byte)(occupant < 0 ? 255 : occupant));
            S.SendToAll(NetChannel.Reliable);
        }

        void BroadcastTrap(TrapEntity t, TrapState state, int victim, bool fired)
        {
            var w = S.Begin(Msg.TrapState);
            w.WriteShort((short)t.Index);
            w.WriteByte((byte)state);
            w.WriteByte((byte)(victim < 0 ? 255 : victim));
            w.WriteBool(fired);
            S.SendToAll(NetChannel.Reliable);
        }

        void SetChase(int target, bool active)
        {
            if (W.ChaseTargets.Contains(target) == active) return;
            var w = S.Begin(Msg.ChaseState);
            w.WriteByte((byte)target);
            w.WriteBool(active);
            S.SendToAll(NetChannel.Reliable);
        }

        /// <summary>Noise heard by Omar (human: HUD ping; AI: investigation).</summary>
        public void DeliverNoise(Vector3 pos, float radius)
        {
            foreach (var ai in _ais) if (ai != null) ai.OnNoise(pos, radius);
            var omar = S.FindOmar();
            if (omar == null || omar.IsBot || !omar.Connected) return;
            var w = S.Begin(Msg.Noise);
            w.WriteVector3(pos);
            w.WriteByte((byte)Mathf.Clamp(Mathf.RoundToInt(radius), 1, 255));
            S.SendTo(omar.Id, NetChannel.Unreliable);
        }

        bool Near(int player, Vector3 point, float max) => Vector3.Distance(PosOf(player), point) <= max;

        // ================================================================== lifecycle

        public void OnBegin()
        {
            _nextEventAt = _rng.Range(55f, 90f);
            CommitObj(EditObj()); // initial objective state (car battery etc.)
            SendTrapCharges();
        }

        public void Tick(float dt)
        {
            if (!W.Running || _ended) return;
            float t = W.Time;

            _snapTimer -= dt;
            if (_snapTimer <= 0f) { _snapTimer = 0.05f; SendSnapshot(); }

            if (!_cagesOpened && t >= Tuning.CagesOpenAt)
            {
                _cagesOpened = true;
                for (int i = 0; i < W.Cages.Length; i++)
                {
                    int occ = W.Cages[i].Occupant;
                    BroadcastCage(i, true, -1);
                    if (occ >= 0)
                    {
                        var st = Edit(occ);
                        if (st.Life == LifeState.Caged) { st.Life = LifeState.Free; st.Cage = -1; Commit(st); }
                    }
                }
                Event(WorldEventKind.CagesOpen);
            }
            if (!_omarAwake && t >= Tuning.OmarIntroSeconds)
            {
                _omarAwake = true;
                Event(WorldEventKind.OmarAwake);
            }

            var o = W.Objectives;
            if (o.RadioCalled && !o.RescuePresent && !o.RescueGone && o.RescueAt > 0 && t >= o.RescueAt)
            {
                var e = EditObj(); e.RescuePresent = true; e.RescueLeaveAt = t + Tuning.RescueStay; CommitObj(e);
                DeliverNoise(W.Map.Radio != null ? W.Map.Radio.HelicopterPosition : Vector3.zero, 120f);
            }
            if (o.RescuePresent && t >= o.RescueLeaveAt)
            {
                var e = EditObj(); e.RescuePresent = false; e.RescueGone = true; CommitObj(e);
            }
            if (o.IgniteAt > 0f && !o.Exploded && t >= o.IgniteAt) Explode();

            if (_powerRestoreAt > 0f && t >= _powerRestoreAt) { _powerRestoreAt = -1f; Event(WorldEventKind.PowerOn); }
            if (t >= _nextEventAt) RandomEvent();

            _trapRecharge += dt;
            if (_trapRecharge >= Tuning.TrapRecharge)
            {
                _trapRecharge = 0f;
                if (_wires < Tuning.TripwireCharges) { _wires++; SendTrapCharges(); }
                else if (_bears < Tuning.BearTrapCharges) { _bears++; SendTrapCharges(); }
            }

            _endCheckTimer -= dt;
            if (_endCheckTimer <= 0f) { _endCheckTimer = 0.5f; CheckEnd(); }
        }

        void SendSnapshot()
        {
            var w = S.Begin(Msg.Snapshot);
            w.WriteFloat(W.Time);
            int countPos = w.Length;
            w.WriteByte(0);
            int n = 0;
            foreach (var kv in W.Avatars)
            {
                var av = kv.Value;
                if (av == null) continue;
                AvatarNetState st;
                if (av.Simulated) st = av.State;
                else if (!_latest.TryGetValue(kv.Key, out st)) continue;
                w.WriteByte((byte)kv.Key);
                st.Write(w);
                n++;
            }
            w.Buffer[countPos] = (byte)n;
            S.SendToAll(NetChannel.Unreliable, false);
        }

        public void OnPlayerLeft(int id)
        {
            var p = Info(id);
            if (p == null) return;
            if (p.IsPrisoner)
            {
                Vector3 pos = PosOf(id);
                if (_inv.TryGetValue(id, out var slots))
                    for (int i = 0; i < 3; i++) if (slots[i] >= 0) DropItem(id, slots[i], pos + new Vector3(i * 0.3f - 0.3f, 0, 0), 0, W.GetItem(slots[i])?.Charge ?? 1f);
                var st = Edit(id);
                if (st.HidingSpot >= 0) BroadcastHide(st.HidingSpot, -1, false);
                if (st.Cage >= 0) BroadcastCage(st.Cage, W.Cages[st.Cage].Open, -1);
                if (st.TrappedBy >= 0 && st.TrappedBy < W.Traps.Count) BroadcastTrap(W.Traps[st.TrappedBy], TrapState.Disarmed, -1, false);
                if (W.Objectives.CarDriver == id) { var o = EditObj(); o.CarDriver = -1; CommitObj(o); }
                st.Life = LifeState.Gone; st.HidingSpot = -1; st.Cage = -1; st.TrappedBy = -1; st.CarSeat = -1;
                Commit(st);
                SetChase(id, false);
                Message(p.Name + " LOST THE SIGNAL", 4f);
            }
            else if (p.IsOmar)
            {
                // nobody can report 'lost' for the chases his detector was tracking any more
                foreach (var t in new List<int>(W.ChaseTargets)) SetChase(t, false);
                // the AI takes over Omar's body
                var av = W.AvatarOf(id);
                if (av != null)
                {
                    av.MakeSimulated();
                    var ai = OmarAI.Attach(av, W);
                    RegisterAI(ai);
                }
                Message("OMAR'S PLAYER LEFT. SOMETHING ELSE MOVES HIS BODY NOW...", 5f);
            }
        }

        // ================================================================== avatar / misc requests

        public void OnAvatarState(int sender, NetReader r)
        {
            var st = AvatarNetState.Read(r);
            if (sender == S.LocalId) return;
            var av = W.AvatarOf(sender);
            if (av == null || av.Simulated) return;
            _latest[sender] = st;
            av.PushRemoteState(st);
        }

        public void OnActionReq(int sender, NetReader r)
        {
            var a = (CharacterAction)r.ReadByte();
            BroadcastAction(sender, a);
        }

        public void BroadcastAction(int id, CharacterAction a)
        {
            var w = S.Begin(Msg.Action);
            w.WriteByte((byte)id);
            w.WriteByte((byte)a);
            S.SendToAll(NetChannel.Reliable);
        }

        public void OnNoiseReq(int sender, NetReader r)
        {
            Vector3 p = r.ReadVector3();
            float radius = r.ReadByte();
            if (!IsPrisoner(sender)) return;
            if (Vector3.Distance(p, PosOf(sender)) > 6f) p = PosOf(sender);
            DeliverNoise(p, Mathf.Min(radius, 60f));
        }

        // ================================================================== items

        public void OnPickupReq(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            var it = W.GetItem(itemId);
            var st = W.StatusOf(sender);
            if (it == null || it.Consumed || it.Holder >= 0 || it.World == null) return;
            if (!IsPrisoner(sender) || st == null || st.Life != LifeState.Free || st.Hidden || st.InCar) return;
            if (!Near(sender, it.World.transform.position, 3.5f)) return;
            if (!_inv.TryGetValue(sender, out var slots)) return;
            int slot = -1;
            for (int i = 0; i < 3; i++) if (slots[i] < 0) { slot = i; break; }
            if (slot < 0) return;
            slots[slot] = itemId;
            var w = S.Begin(Msg.ItemPicked);
            w.WriteShort((short)itemId);
            w.WriteByte((byte)sender);
            w.WriteByte((byte)slot);
            S.SendToAll(NetChannel.Reliable);
        }

        public void OnDropReq(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            Vector3 pos = r.ReadVector3();
            float yaw = r.ReadAngle();
            float charge = r.ReadUnit();
            var it = W.GetItem(itemId);
            if (it == null || !Holds(sender, itemId, it.Type)) return;
            if (Vector3.Distance(pos, PosOf(sender)) > 3f) pos = PosOf(sender);
            DropItem(sender, itemId, pos, yaw, charge);
            DeliverNoise(pos, it.Def.DropNoise);
        }

        public void OnThrowReq(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            Vector3 from = r.ReadVector3();
            Vector3 vel = r.ReadVector3();
            if (!Holds(sender, itemId, ItemType.Bottle)) return;
            if (vel.magnitude > 25f) vel = vel.normalized * 25f;
            RemoveFromInv(sender, itemId);
            _thrownBy[itemId] = sender;
            var w = S.Begin(Msg.ItemConsumed);
            w.WriteShort((short)itemId);
            w.WriteByte((byte)sender);
            w.WriteByte(2);
            w.WriteVector3(from);
            w.WriteVector3(vel);
            S.SendToAll(NetChannel.Reliable);
        }

        public void OnBottleImpact(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            Vector3 pos = r.ReadVector3();
            if (!_thrownBy.TryGetValue(itemId, out var by) || by != sender) return;
            _thrownBy.Remove(itemId);
            if (Vector3.Distance(pos, PosOf(sender)) > 45f) return;
            var w = S.Begin(Msg.BottleShatter);
            w.WriteVector3(pos);
            S.SendToAll(NetChannel.Reliable);
            DeliverNoise(pos, 24f);
        }

        // ================================================================== doors

        public void OnDoorReq(int sender, NetReader r)
        {
            int id = r.ReadShort();
            bool open = r.ReadBool();
            RequestDoor(sender, id, open);
        }

        /// <summary>Open / close a door (also used by the AI).</summary>
        public void RequestDoor(int sender, int id, bool open)
        {
            if (id < 0 || id >= W.Doors.Length) return;
            var d = W.Doors[id];
            if (d.Boarded) return;
            if (!Near(sender, d.Info.Center, 3.6f)) return;
            bool omar = IsOmar(sender);
            if (d.Locked && !omar) return;
            if (!omar)
            {
                var st = W.StatusOf(sender);
                if (st == null || st.Life != LifeState.Free || st.Hidden) return;
            }
            bool slam = omar && (_latest.TryGetValue(sender, out var s) ? (s.Flags & AvatarFlags.Sprint) != 0 : W.AvatarOf(sender)?.Sprinting == true);
            BroadcastDoor(d, open, omar ? false : d.Locked, d.Boarded, slam);
            if (!omar)
            {
                bool sprinting = _latest.TryGetValue(sender, out var ps) && (ps.Flags & AvatarFlags.Sprint) != 0;
                DeliverNoise(d.Info.Center, sprinting ? 10f : 4f);
            }
        }

        /// <summary>Omar (or AI) smashes the boards of a door.</summary>
        public void SmashBoards(int omarId, int door)
        {
            var d = W.Doors[door];
            if (!d.Boarded) return;
            BroadcastDoor(d, true, false, false, true);
            DeliverNoise(d.Info.Center, 18f);
        }

        // ================================================================== use

        public void OnUseReq(int sender, NetReader r)
        {
            var target = (UseTarget)r.ReadByte();
            int tid = r.ReadShort();
            int itemId = r.ReadShort();
            float charge = r.ReadUnit();
            var st = W.StatusOf(sender);
            if (st == null) return;
            bool omar = IsOmar(sender);

            if (omar)
            {
                if (target == UseTarget.Door && tid >= 0 && tid < W.Doors.Length && Near(sender, W.Doors[tid].Info.Center, 3.6f)) SmashBoards(sender, tid);
                return;
            }
            if (!IsPrisoner(sender)) return;

            // leaving states that are not "Free & visible"
            if (target == UseTarget.Hiding) { UseHiding(sender, st, tid); return; }
            if (target == UseTarget.CarExit) { UseCarExit(sender, st); return; }
            if (target == UseTarget.CarDriver && st.InCar) { UseCarDriver(sender, st, itemId); return; }
            if (st.Life != LifeState.Free || st.Hidden || st.Trapped) return;

            switch (target)
            {
                case UseTarget.Self: UseSelf(sender, st, itemId); break;
                case UseTarget.Door: UseDoor(sender, tid, itemId); break;
                case UseTarget.Cage: UseCage(sender, tid, itemId); break;
                case UseTarget.Gate: UseGate(sender, itemId); break;
                case UseTarget.CarFuel: UseCarFuel(sender, itemId); break;
                case UseTarget.CarHood: UseCarHood(sender, itemId); break;
                case UseTarget.CarDriver: UseCarDriver(sender, st, itemId); break;
                case UseTarget.CarPassenger: UseCarPassenger(sender, st); break;
                case UseTarget.FuseBox: UseFuseBox(sender, itemId); break;
                case UseTarget.Radio: UseRadio(sender); break;
                case UseTarget.Barrels: UseBarrels(sender, itemId); break;
                case UseTarget.Ignite: UseIgnite(sender, itemId, charge); break;
                case UseTarget.Trap: UseTrap(sender, tid); break;
            }
        }

        void UseSelf(int p, PlayerStatus st, int itemId)
        {
            var it = W.GetItem(itemId);
            if (it == null || !Holds(p, itemId, it.Type)) return;
            switch (it.Type)
            {
                case ItemType.Bandages:
                    if (!st.Injured) return;
                    var e = Edit(p); e.Injured = false; Commit(e);
                    Consume(p, itemId);
                    break;
                case ItemType.Pills:
                    Consume(p, itemId);
                    break;
                case ItemType.LighterFuel:
                    {
                        int l = FindHeld(p, ItemType.Lighter);
                        if (l < 0) return;
                        SetCharge(l, 1f);
                        Consume(p, itemId);
                        break;
                    }
                case ItemType.Batteries:
                    {
                        int f = FindHeld(p, ItemType.Flashlight);
                        if (f < 0) return;
                        SetCharge(f, 1f);
                        Consume(p, itemId);
                        break;
                    }
            }
        }

        void UseDoor(int p, int id, int itemId)
        {
            if (id < 0 || id >= W.Doors.Length) return;
            var d = W.Doors[id];
            if (!Near(p, d.Info.Center, 3.6f)) return;
            if (d.Boarded)
            {
                if (!Holds(p, itemId, ItemType.Crowbar)) return;
                BroadcastDoor(d, true, false, false, false);
                DeliverNoise(d.Info.Center, 12f);
            }
            else if (d.Locked)
            {
                if (!Holds(p, itemId, ItemType.Lockpick)) return;
                BroadcastDoor(d, true, false, false, false);
                Consume(p, itemId);
                Message("THE LOCKPICK SNAPPED, BUT THE DOOR IS OPEN", 3f, p);
            }
        }

        void UseCage(int p, int cage, int itemId)
        {
            if (cage < 0 || cage >= W.Cages.Length) return;
            var c = W.Cages[cage];
            if (c.Open || c.Occupant < 0) return;
            if (!Near(p, c.Info.Outside.position, 3.6f)) return;
            var it = W.GetItem(itemId);
            if (it == null || !Holds(p, itemId, it.Type)) return;
            if (it.Type == ItemType.Lockpick) Consume(p, itemId);
            else if (it.Type == ItemType.BoltCutters) DeliverNoise(c.Info.Outside.position, 22f);
            else if (it.Type != ItemType.CageKey) return;
            ReleaseCage(cage);
        }

        void ReleaseCage(int cage)
        {
            var c = W.Cages[cage];
            int occ = c.Occupant;
            BroadcastCage(cage, true, -1);
            if (occ >= 0)
            {
                var st = Edit(occ);
                if (st.Life == LifeState.Caged) { st.Life = LifeState.Free; st.Cage = -1; Commit(st); }
            }
        }

        void UseGate(int p, int itemId)
        {
            var g = W.Map.MainGate;
            if (g == null || W.Objectives.GateCut || !Holds(p, itemId, ItemType.BoltCutters)) return;
            Vector3 at = g.Interact != null ? g.Interact.bounds.center : PosOf(p);
            if (!Near(p, at, 4f)) return;
            var o = EditObj(); o.GateCut = true; CommitObj(o);
            DeliverNoise(at, 30f);
        }

        Vector3 CarPos => W.Map.Car != null && W.Map.Car.Root != null ? W.Map.Car.Root.position : Vector3.zero;

        void UseCarFuel(int p, int itemId)
        {
            if (W.Map.Car == null || W.Objectives.CarFueled || !Holds(p, itemId, ItemType.GasCan) || !Near(p, CarPos, 5.5f)) return;
            var o = EditObj(); o.CarFueled = true; CommitObj(o);
            Consume(p, itemId);
        }

        void UseCarHood(int p, int itemId)
        {
            if (W.Map.Car == null || W.Objectives.CarBatteryOk || !Holds(p, itemId, ItemType.CarBattery) || !Near(p, CarPos, 5.5f)) return;
            var o = EditObj(); o.CarBatteryOk = true; CommitObj(o);
            Consume(p, itemId);
            DeliverNoise(CarPos, 6f);
        }

        void UseCarDriver(int p, PlayerStatus st, int itemId)
        {
            var o = W.Objectives;
            if (W.Map.Car == null || o.CarGone) return;
            if (o.CarStarted)
            {
                if (o.CarDriver == p && st.CarSeat == 0) DriveAway();
                return;
            }
            if (!Holds(p, itemId, ItemType.CarKeys) || !Near(p, CarPos, 5f)) return;
            if (!o.CarBatteryOk) { Event(WorldEventKind.CarNoBattery, p); DeliverNoise(CarPos, 5f); return; }
            if (!o.CarFueled) { Event(WorldEventKind.CarNoFuel, p); DeliverNoise(CarPos, 25f); return; }
            if (_rng.Chance(0.35f)) { Event(WorldEventKind.CarCrankFail, p); DeliverNoise(CarPos, 30f); return; }
            var e = EditObj(); e.CarStarted = true; e.CarDriver = p; CommitObj(e);
            var s2 = Edit(p); s2.CarSeat = 0; s2.TeleportSeq++; Commit(s2);
            DeliverNoise(CarPos, 55f);
        }

        void UseCarPassenger(int p, PlayerStatus st)
        {
            var o = W.Objectives;
            if (W.Map.Car == null || !o.CarStarted || o.CarGone || st.InCar || !Near(p, CarPos, 5f)) return;
            int seat = -1;
            if (o.CarDriver < 0) seat = 0;
            else
            {
                var used = new HashSet<int>();
                foreach (var kv in W.Statuses) if (kv.Value.CarSeat >= 0) used.Add(kv.Value.CarSeat);
                for (int i = 1; i < 4 && i < W.Map.Car.Seats.Length; i++) if (!used.Contains(i)) { seat = i; break; }
            }
            if (seat < 0) return;
            if (seat == 0) { var e = EditObj(); e.CarDriver = p; CommitObj(e); }
            var s2 = Edit(p); s2.CarSeat = seat; s2.TeleportSeq++; Commit(s2);
        }

        void UseCarExit(int p, PlayerStatus st)
        {
            if (!st.InCar || W.Objectives.CarGone) return;
            if (W.Objectives.CarDriver == p) { var e = EditObj(); e.CarDriver = -1; CommitObj(e); }
            var s2 = Edit(p); s2.CarSeat = -1; s2.TeleportSeq++; Commit(s2);
        }

        void DriveAway()
        {
            var e = EditObj(); e.CarGone = true; CommitObj(e);
            var w = S.Begin(Msg.CarDrive);
            w.WriteFloat(W.Time);
            S.SendToAll(NetChannel.Reliable);
            DeliverNoise(CarPos, 80f);
            var seated = new List<int>();
            foreach (var kv in W.Statuses) if (kv.Value.CarSeat >= 0 && kv.Value.Life == LifeState.Free) seated.Add(kv.Key);
            foreach (var id in seated) Escape(id, EscapeRoute.Car, false);
            CheckEnd();
        }

        void UseFuseBox(int p, int itemId)
        {
            var r = W.Map.Radio;
            if (r == null || r.FuseBox == null || W.Objectives.FuseIn || !Holds(p, itemId, ItemType.Fuse)) return;
            if (!Near(p, r.FuseBox.bounds.center, 4f)) return;
            var o = EditObj(); o.FuseIn = true; CommitObj(o);
            Consume(p, itemId);
            DeliverNoise(r.FuseBox.bounds.center, 8f);
        }

        void UseRadio(int p)
        {
            var r = W.Map.Radio;
            var o0 = W.Objectives;
            if (r == null || r.RadioSet == null || !o0.FuseIn || o0.RadioCalled) return;
            if (!Near(p, r.RadioSet.bounds.center, 4f)) return;
            var o = EditObj();
            o.RadioCalled = true;
            o.RescueAt = Mathf.Min(W.Time + Tuning.RescueDelay, Mathf.Max(W.Time + 30f, W.NightLength - Tuning.RescueStay - 5f));
            CommitObj(o);
            DeliverNoise(r.RadioSet.bounds.center, 30f);
        }

        void UseBarrels(int p, int itemId)
        {
            var f = W.Map.FuelDepot;
            if (f == null || f.Barrels == null || W.Objectives.BarrelsPoured || !Holds(p, itemId, ItemType.LighterFuel)) return;
            if (!Near(p, f.Barrels.bounds.center, 5f)) return;
            var o = EditObj(); o.BarrelsPoured = true; CommitObj(o);
            Consume(p, itemId);
        }

        void UseIgnite(int p, int itemId, float charge)
        {
            var f = W.Map.FuelDepot;
            var o0 = W.Objectives;
            if (f == null || f.Barrels == null || !o0.BarrelsPoured || o0.IgniteAt > 0f || o0.Exploded) return;
            if (!Holds(p, itemId, ItemType.Lighter) || charge < 0.005f) return;
            if (!Near(p, f.Barrels.bounds.center, 5f)) return;
            var o = EditObj(); o.IgniteAt = W.Time + Tuning.IgniteFuse; CommitObj(o);
            DeliverNoise(f.Barrels.bounds.center, 20f);
        }

        void Explode()
        {
            var f = W.Map.FuelDepot;
            var o = EditObj(); o.Exploded = true; CommitObj(o);
            Vector3 c = f != null ? f.ExplosionCenter : Vector3.zero;
            DeliverNoise(c, 150f);
            var omar = S.FindOmar();
            if (omar != null && Vector3.Distance(PosOf(omar.Id), c) < Tuning.ExplosionStunRadius) Stun(omar.Id, 7f);
            foreach (var kv in W.Statuses)
            {
                var st = kv.Value;
                if (!IsPrisoner(kv.Key) || st.Life != LifeState.Free || st.Injured) continue;
                if (Vector3.Distance(PosOf(kv.Key), c) < 6f) { var e = Edit(kv.Key); e.Injured = true; Commit(e); }
            }
        }

        public void Stun(int omarId, float seconds)
        {
            _omarStunUntil = Mathf.Max(_omarStunUntil, W.Time + seconds);
            foreach (var ai in _ais) if (ai != null) ai.Stun(seconds);
            var w = S.Begin(Msg.OmarStun);
            w.WriteFloat(seconds);
            S.SendToAll(NetChannel.Reliable);
        }

        void UseTrap(int p, int id)
        {
            if (id < 0 || id >= W.Traps.Count) return;
            var t = W.Traps[id];
            if (!Near(p, t.InteractPoint, 3.2f)) return;
            if (t.Victim >= 0 && t.Victim != p)
            {
                int victim = t.Victim;
                BroadcastTrap(t, TrapState.Disarmed, -1, false);
                var st = Edit(victim); st.TrappedBy = -1; Commit(st);
                return;
            }
            if (t.State == TrapState.Armed) BroadcastTrap(t, TrapState.Disarmed, -1, false);
        }

        void UseHiding(int p, PlayerStatus st, int spot)
        {
            if (spot < 0 || spot >= W.Hiding.Length) return;
            var h = W.Hiding[spot];
            if (st.HidingSpot == spot)
            {
                var e = Edit(p); e.HidingSpot = -1; e.TeleportSeq++; Commit(e);
                BroadcastHide(spot, -1, false);
                return;
            }
            if (st.Life != LifeState.Free || st.Hidden || st.Trapped || st.InCar || h.Occupant >= 0) return;
            if (!Near(p, h.InteractPoint, 3.6f)) return;
            var e2 = Edit(p); e2.HidingSpot = spot; Commit(e2);
            BroadcastHide(spot, p, false);
            // an AI that watched them go in remembers
            foreach (var ai in _ais) if (ai != null) ai.OnSawHide(p, spot);
        }

        // ================================================================== struggle / traps

        public void OnStruggleReq(int sender, NetReader r)
        {
            byte kind = r.ReadByte();
            var st = W.StatusOf(sender);
            if (st == null) return;
            int key = sender * 2 + (kind == 0 ? 0 : 1);
            int n = (_struggle.TryGetValue(key, out var c) ? c : 0) + 1;
            _struggle[key] = n;
            if (kind == 0 && st.Life == LifeState.Caged && st.Cage >= 0 && st.Cage < W.Cages.Length && !W.Cages[st.Cage].Open)
            {
                var cage = W.Cages[st.Cage];
                if (n % 10 == 0) DeliverNoise(cage.Info.Inside.position, 9f);
                if (n > 8 && _rng.Chance(0.013f))
                {
                    _struggle[key] = 0;
                    ReleaseCage(st.Cage);
                    DeliverNoise(cage.Info.Inside.position, 15f);
                    Message("THE RUSTY LOCK GAVE WAY!", 3f, sender);
                }
            }
            else if (kind == 1 && st.Trapped)
            {
                if (n % 6 == 0) DeliverNoise(PosOf(sender), 7f);
                if (n >= 16)
                {
                    _struggle[key] = 0;
                    int trap = st.TrappedBy;
                    var e = Edit(sender); e.TrappedBy = -1; Commit(e);
                    if (trap >= 0 && trap < W.Traps.Count) BroadcastTrap(W.Traps[trap], TrapState.Disarmed, -1, false);
                }
            }
        }

        public void OnTrapTriggerReq(int sender, NetReader r)
        {
            int id = r.ReadShort();
            if (id < 0 || id >= W.Traps.Count) return;
            var t = W.Traps[id];
            var st = W.StatusOf(sender);
            if (t.State != TrapState.Armed || st == null || st.Life != LifeState.Free || !IsPrisoner(sender)) return;
            Vector3 p = PosOf(sender);
            float d = t.Kind == TrapKind.Tripwire ? DistanceToSegment(p, t.A, t.B) : GeoUtil.FlatDistance(p, t.A);
            if (d > 3f) return;
            TriggerTrap(t, sender);
        }

        void TriggerTrap(TrapEntity t, int victim)
        {
            if (t.Kind == TrapKind.Tripwire)
            {
                BroadcastTrap(t, TrapState.Triggered, victim, true);
                foreach (var ai in _ais) if (ai != null) ai.OnAlarm(t.InteractPoint);
                DeliverNoise(t.InteractPoint, 15f);
            }
            else
            {
                BroadcastTrap(t, TrapState.Triggered, victim, true);
                _struggle[victim * 2 + 1] = 0;
                var st = Edit(victim);
                st.TrappedBy = t.Index;
                st.Injured = true;
                Commit(st);
                foreach (var ai in _ais) if (ai != null) ai.OnAlarm(t.InteractPoint);
                DeliverNoise(t.InteractPoint, 25f);
            }
        }

        static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        public void OnTrapPlaceReq(int sender, NetReader r)
        {
            var kind = (TrapKind)r.ReadByte();
            Vector3 a = r.ReadVector3(), b = r.ReadVector3();
            if (!IsOmar(sender)) return;
            if (!PlaceTrap(sender, kind, a, b)) Message(kind == TrapKind.Tripwire ? "THE WIRE WON'T HOLD THERE" : "THE TRAP WON'T SET THERE", 2f, sender);
            SendTrapCharges();
        }

        /// <summary>Tell Omar's player the real trap counts (their HUD and local checks follow the host).</summary>
        void SendTrapCharges()
        {
            var omar = S.FindOmar();
            if (omar == null || omar.IsBot || !omar.Connected) return;
            var w = S.Begin(Msg.TrapCharges);
            w.WriteByte((byte)_wires);
            w.WriteByte((byte)_bears);
            S.SendTo(omar.Id, NetChannel.Reliable);
        }

        /// <summary>Spawn a new trap for Omar (human or AI). Returns false when out of charges / invalid.</summary>
        public bool PlaceTrap(int omarId, TrapKind kind, Vector3 a, Vector3 b)
        {
            if (_ended || !W.Running) return false;
            if (!Near(omarId, a, 6f) || (b - a).magnitude > 4.5f) return false;
            if (kind == TrapKind.Tripwire) { if (_wires <= 0) return false; _wires--; }
            else { if (_bears <= 0) return false; _bears--; }
            int id = W.Traps.Count;
            var w = S.Begin(Msg.TrapSpawned);
            w.WriteShort((short)id);
            w.WriteByte((byte)kind);
            w.WriteVector3(a);
            w.WriteVector3(b);
            S.SendToAll(NetChannel.Reliable);
            return true;
        }

        // ================================================================== Omar actions

        public void OnAttackReq(int sender, NetReader r)
        {
            int target = r.ReadByte();
            if (!IsOmar(sender)) return;
            DoAttack(sender, target);
        }

        /// <summary>Resolve a cleaver hit (human Omar request or AI). target 255 / invalid = swing only.</summary>
        public void DoAttack(int omarId, int target)
        {
            if (OmarStunned || _ended || !W.Running) return;
            float last = _lastAttack.TryGetValue(omarId, out var l) ? l : -99f;
            if (W.Time - last < Tuning.AttackCooldown * 0.7f) return;
            _lastAttack[omarId] = W.Time;
            var st = W.StatusOf(target);
            bool valid = st != null && IsPrisoner(target) && st.Life == LifeState.Free && !st.Hidden && Near(omarId, PosOf(target), Tuning.AttackRange + 1.4f);
            if (!valid) { BroadcastAttack(omarId, 255, 0); return; }

            if (st.Injured)
            {
                int crowbar = FindHeld(target, ItemType.Crowbar);
                if (crowbar >= 0)
                {
                    BroadcastAttack(omarId, target, 3);
                    Consume(target, crowbar, 1);
                    Stun(omarId, 3.5f);
                    return;
                }
                BroadcastAttack(omarId, target, 2);
                Capture(target);
            }
            else
            {
                BroadcastAttack(omarId, target, 1);
                var e = Edit(target); e.Injured = true; Commit(e);
            }
        }

        void BroadcastAttack(int omar, int target, byte result)
        {
            var w = S.Begin(Msg.AttackFx);
            w.WriteByte((byte)omar);
            w.WriteByte((byte)Mathf.Clamp(target, 0, 255));
            w.WriteByte(result);
            S.SendToAll(NetChannel.Reliable);
        }

        /// <summary>Drag a prisoner back to the pens (or kill them on the last capture).</summary>
        public void Capture(int target)
        {
            var st = Edit(target);
            Vector3 pos = PosOf(target);
            // Omar takes everything but the lighter
            if (_inv.TryGetValue(target, out var slots))
            {
                for (int i = 0; i < 3; i++)
                {
                    var it = W.GetItem(slots[i]);
                    if (it == null || it.Type == ItemType.Lighter) continue;
                    Vector3 scatter = new Vector3(_rng.Range(-0.6f, 0.6f), 0, _rng.Range(-0.6f, 0.6f));
                    Vector3 p = pos + scatter + Vector3.up * 0.5f;
                    if (Physics.Raycast(p, Vector3.down, out var hit, 3f, Layers.Solid, QueryTriggerInteraction.Ignore)) p = hit.point;
                    else p = pos;
                    DropItem(target, it.Id, p, _rng.Range(0f, 360f), it.Charge);
                }
            }
            if (st.HidingSpot >= 0) BroadcastHide(st.HidingSpot, -1, true);
            if (st.TrappedBy >= 0 && st.TrappedBy < W.Traps.Count) BroadcastTrap(W.Traps[st.TrappedBy], TrapState.Disarmed, -1, false);
            if (st.CarSeat >= 0 && W.Objectives.CarDriver == target) { var o = EditObj(); o.CarDriver = -1; CommitObj(o); }
            st.HidingSpot = -1; st.TrappedBy = -1; st.CarSeat = -1;
            st.Captures++;
            st.Injured = false;
            _struggle[target * 2] = 0;
            st.TeleportSeq++;
            SetChase(target, false);
            if (st.Captures >= Tuning.MaxCaptures)
            {
                st.Life = LifeState.Dead;
                st.Cage = -1;
            }
            else
            {
                int cage = -1;
                for (int i = 0; i < W.Cages.Length; i++) if (W.Cages[i].Occupant < 0) { cage = i; break; }
                if (cage < 0) { st.Life = LifeState.Dead; }
                else
                {
                    st.Life = LifeState.Caged;
                    st.Cage = cage;
                    BroadcastCage(cage, false, target);
                }
            }
            Commit(st);
            CheckEnd();
        }

        public void OnDetectReq(int sender, NetReader r)
        {
            int target = r.ReadByte();
            bool spotted = r.ReadBool();
            if (!IsOmar(sender)) return;
            DoDetect(target, spotted);
        }

        public void DoDetect(int target, bool spotted)
        {
            var st = W.StatusOf(target);
            if (spotted && (_ended || !W.Running || st == null || st.Life != LifeState.Free)) return;
            SetChase(target, spotted);
        }

        public void OnScreamReq(int sender, NetReader r)
        {
            if (!IsOmar(sender)) return;
            DoScream(sender);
        }

        public bool DoScream(int omarId)
        {
            if (_ended || !W.Running) return false;
            float last = _lastScream.TryGetValue(omarId, out var l) ? l : -99f;
            if (W.Time - last < Tuning.ScreamCooldown - 2f) return false;
            _lastScream[omarId] = W.Time;
            var w = S.Begin(Msg.Scream);
            w.WriteByte((byte)omarId);
            w.WriteByte((byte)_rng.Range(1, 5));
            S.SendToAll(NetChannel.Reliable);
            return true;
        }

        public void OnSearchReq(int sender, NetReader r)
        {
            int spot = r.ReadByte();
            if (!IsOmar(sender)) return;
            DoSearch(sender, spot);
        }

        /// <summary>Omar tears a hiding spot open. Returns the prisoner found (or -1).</summary>
        public int DoSearch(int omarId, int spot)
        {
            if (spot < 0 || spot >= W.Hiding.Length || OmarStunned) return -1;
            var h = W.Hiding[spot];
            if (!Near(omarId, h.InteractPoint, 3.8f)) return -1;
            int occ = h.Occupant;
            BroadcastHide(spot, -1, true);
            if (occ < 0) return -1;
            var st = Edit(occ);
            st.HidingSpot = -1;
            st.TeleportSeq++;
            if (st.Injured)
            {
                Commit(st);
                BroadcastAttack(omarId, occ, 2);
                Capture(occ);
            }
            else
            {
                st.Injured = true;
                Commit(st);
                BroadcastAttack(omarId, occ, 1);
                SetChase(occ, true);
            }
            return occ;
        }

        // ================================================================== keypad / escapes

        public void OnKeypadReq(int sender, NetReader r)
        {
            string code = r.ReadString();
            var sh = W.Map.Shelter;
            if (sh == null || sh.Keypad == null || !IsPrisoner(sender)) return;
            if (!Near(sender, sh.Keypad.bounds.center, 4f)) return;
            bool ok = code == string.Concat(W.ShelterCode[0], W.ShelterCode[1], W.ShelterCode[2], W.ShelterCode[3]);
            var w = S.Begin(Msg.KeypadResult);
            w.WriteBool(ok);
            S.SendTo(sender, NetChannel.Reliable);
            if (ok && !W.Objectives.ShelterOpen) { var o = EditObj(); o.ShelterOpen = true; CommitObj(o); DeliverNoise(sh.Keypad.bounds.center, 18f); }
            else if (!ok) DeliverNoise(sh.Keypad.bounds.center, 5f);
        }

        public void OnEscapeReq(int sender, NetReader r)
        {
            var route = (EscapeRoute)r.ReadByte();
            var st = W.StatusOf(sender);
            if (st == null || st.Life != LifeState.Free || !IsPrisoner(sender)) return;
            Vector3 p = PosOf(sender) + Vector3.up * 0.5f;
            var o = W.Objectives;
            var m = W.Map;
            bool ok = false;
            switch (route)
            {
                case EscapeRoute.Road: ok = (o.GateCut || o.CarGone) && m.MainGate != null && Expand(m.MainGate.ExitZone).Contains(p); break;
                case EscapeRoute.Shelter: ok = o.ShelterOpen && m.Shelter != null && Expand(m.Shelter.TunnelExitZone).Contains(p); break;
                case EscapeRoute.Radio: ok = o.RescuePresent && m.Radio != null && Expand(m.Radio.LandingZone).Contains(p); break;
                case EscapeRoute.Fire: ok = o.Exploded && m.FuelDepot != null && Expand(m.FuelDepot.BreachExitZone).Contains(p); break;
            }
            if (ok) Escape(sender, route, true);
        }

        static Bounds Expand(Bounds b) { b.Expand(6f); return b; }

        void Escape(int id, EscapeRoute route, bool checkEnd)
        {
            var st = Edit(id);
            if (st.Life != LifeState.Free) return;
            st.Life = LifeState.Escaped;
            st.Route = route;
            st.HidingSpot = -1; st.TrappedBy = -1;
            Commit(st);
            SetChase(id, false);
            var w = S.Begin(Msg.Escaped);
            w.WriteByte((byte)id);
            w.WriteByte((byte)route);
            S.SendToAll(NetChannel.Reliable);
            if (checkEnd) CheckEnd();
        }

        // ================================================================== events

        void RandomEvent()
        {
            _nextEventAt = W.Time + _rng.Range(40f, 100f);
            float r = _rng.NextFloat();
            if (r < 0.34f) Event(WorldEventKind.AnomalyPulse, _rng.Range(0.3f, 1f), _rng.Range(3f, 9f));
            else if (r < 0.46f)
            {
                if (W.PowerOn && _powerRestoreAt < 0f)
                {
                    Event(WorldEventKind.PowerOut);
                    _powerRestoreAt = W.Time + _rng.Range(25f, 55f);
                }
                else Event(WorldEventKind.Thunder);
            }
            else if (r < 0.56f) Event(WorldEventKind.PhoneRing);
            else if (r < 0.66f) Event(WorldEventKind.TvOn);
            else if (r < 0.79f) Event(WorldEventKind.Thunder);
            else if (r < 0.9f)
            {
                float a = _rng.Range(0f, Mathf.PI * 2f);
                Event(WorldEventKind.DistantScream, Mathf.Cos(a) * 110f, Mathf.Sin(a) * 110f);
            }
            else Event(WorldEventKind.Whispers);
        }

        // ================================================================== ending

        void CheckEnd()
        {
            if (_ended || !W.Running) return;
            if (!_cagesOpened) return; // everyone starts caged
            int free = 0, caged = 0, prisoners = 0;
            foreach (var p in S.Players)
            {
                if (!p.IsPrisoner) continue;
                prisoners++;
                var st = W.StatusOf(p.Id);
                if (st == null) continue;
                if (st.Life == LifeState.Free) free++;
                else if (st.Life == LifeState.Caged) caged++;
            }
            bool timeUp = W.Time >= W.NightLength;
            // everyone left is in the pens: the caged still get a last chance to break the rusty lock
            if (free == 0 && caged > 0 && !timeUp && prisoners > 0)
            {
                if (_allCagedAt < 0f)
                {
                    _allCagedAt = W.Time;
                    Message("EVERYONE IS CAGED. BREAK THE LOCK (MASH E)!", 6f);
                }
                if (W.Time - _allCagedAt < Tuning.AllCagedGrace) return;
            }
            else _allCagedAt = -1f;
            if (prisoners == 0 || free == 0 || timeUp) EndMatch(timeUp);
        }

        void EndMatch(bool timeUp)
        {
            _ended = true;
            var res = new EndingResult { Duration = W.Time };
            var routes = new Dictionary<EscapeRoute, int>();
            EscapeRoute firstRoute = EscapeRoute.None;
            foreach (var p in S.Players)
            {
                if (p.Role == PlayerRole.Spectator) continue;
                var st = W.StatusOf(p.Id);
                var e = new EndingEntry { Id = p.Id, Name = p.Name, Role = p.Role, Life = st != null ? st.Life : LifeState.Gone, Route = st != null ? st.Route : EscapeRoute.None, Captures = st != null ? st.Captures : 0 };
                if (p.IsPrisoner && e.Life == LifeState.Free) e.Life = timeUp ? LifeState.Caged : LifeState.Free;
                res.Entries.Add(e);
                if (!p.IsPrisoner) continue;
                if (e.Life == LifeState.Escaped)
                {
                    res.Escaped++;
                    routes[e.Route] = (routes.TryGetValue(e.Route, out var c) ? c : 0) + 1;
                    if (firstRoute == EscapeRoute.None) firstRoute = e.Route;
                }
                else res.Lost++;
            }
            if (res.Escaped > 0)
            {
                EscapeRoute best = firstRoute; int bestN = 0;
                foreach (var kv in routes) if (kv.Value > bestN || (kv.Value == bestN && kv.Key == firstRoute)) { best = kv.Key; bestN = kv.Value; }
                res.Id = Endings.ForRoute(best);
            }
            else res.Id = timeUp ? EndingId.Dawn : EndingId.SecondClass;
            res.Twist = _rng.Chance(0.25f) && Endings.HasTwist(res.Id) ? (int)res.Id : -1;
            var w = S.Begin(Msg.MatchEnd);
            res.Write(w);
            S.SendToAll(NetChannel.Reliable);
        }

        /// <summary>Host "END MATCH" from the pause menu.</summary>
        public void ForceEnd() { if (!_ended) EndMatch(false); }
    }
}
