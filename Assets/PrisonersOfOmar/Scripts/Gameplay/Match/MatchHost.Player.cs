using System.Collections.Generic;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Host rules for the local-player interactions owned by the "player" work package:
    // doors (physical drag + Omar pushing), hiding spots (wardrobes, under beds, bed lifting).
    public sealed partial class MatchHost
    {
        /// <summary>Called every host tick while the match runs (door simulation etc.).</summary>
        partial void TickPlayer(float dt);
        /// <summary>Called first when a player leaves (release grabbed doors etc.).</summary>
        partial void OnPlayerLeftPlayer(int id);

        // ------------------------------------------------------------------ doors (host simulation)

        bool[] _doorDirty, _doorMoving;
        float[] _doorDragAt, _doorNoiseAt;
        float _doorRelay;
        readonly Dictionary<int, Vector3> _omarLastPos = new Dictionary<int, Vector3>();
        readonly Dictionary<int, Vector3> _omarVel = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> _omarMovedAt = new Dictionary<int, float>();
        static readonly int DoorBlockMask = Layers.Mask(Layers.Player);

        void EnsureDoorArrays()
        {
            int n = W.Doors.Length;
            if (_doorDirty != null && _doorDirty.Length == n) return;
            _doorDirty = new bool[n]; _doorMoving = new bool[n];
            _doorDragAt = new float[n]; _doorNoiseAt = new float[n];
        }

        partial void TickPlayer(float dt)
        {
            var doors = W.Doors;
            if (doors.Length == 0 || dt <= 0f) return;
            EnsureDoorArrays();
            OmarShovesDoors(dt);
            for (int i = 0; i < doors.Length; i++)
            {
                var d = doors[i];
                if (d.Grabber >= 0)
                {
                    if (GrabberValid(d)) { _doorMoving[i] = true; continue; }
                    ReleaseDoor(d, d.Angle, d.Velocity * 0.5f);
                }
                if (d.Velocity != 0f)
                {
                    float v = d.Velocity;
                    var hit = d.Integrate(dt, DoorBlockMask);
                    if (hit != DoorHit.None) DoorHitFx(d, hit, Mathf.Abs(v));
                    else if (Mathf.Abs(v) > 160f) DoorNoise(d, 5f);
                    _doorDirty[i] = true;
                    _doorMoving[i] = true;
                }
                else if (_doorMoving[i])
                {
                    _doorMoving[i] = false;
                    BroadcastDoor(d, d.Locked, d.Boarded, d.Angle, 0f); // reliable resting angle
                }
            }
            _doorRelay -= dt;
            if (_doorRelay <= 0f) { _doorRelay = 1f / 15f; RelayDoorAngles(); }
        }

        partial void OnPlayerLeftPlayer(int id)
        {
            foreach (var d in W.Doors) if (d.Grabber == id) ReleaseDoor(d, d.Angle, 0f);
            _omarLastPos.Remove(id); _omarVel.Remove(id); _omarMovedAt.Remove(id);
        }

        bool GrabberValid(DoorEntity d)
        {
            int g = d.Grabber;
            var p = Info(g);
            if (p == null || !p.IsPrisoner || d.Locked || d.Boarded) return false;
            var st = W.StatusOf(g);
            if (st == null || st.Life != LifeState.Free || st.Hidden || st.Trapped || st.InCar) return false;
            if (!Near(g, d.Info.Center, 4.4f)) return false;
            return W.Time - _doorDragAt[d.Index] < 2.5f;
        }

        void RelayDoorAngles()
        {
            int count = 0;
            for (int i = 0; i < _doorDirty.Length; i++) if (_doorDirty[i]) count++;
            if (count == 0) return;
            var w = S.Begin(Msg.DoorAngles);
            int n = Mathf.Min(count, 60);
            w.WriteByte((byte)n);
            int written = 0;
            for (int i = 0; i < _doorDirty.Length && written < n; i++)
            {
                if (!_doorDirty[i]) continue;
                _doorDirty[i] = false;
                var d = W.Doors[i];
                w.WriteShort((short)i);
                w.WriteFloat(d.Angle);
                w.WriteFloat(d.Velocity);
                written++;
            }
            S.SendToAll(NetChannel.Unreliable, false);
        }

        /// <summary>Reliable door state (locks, boards, angle). Everybody applies it, the host included.</summary>
        void BroadcastDoor(DoorEntity d, bool locked, bool boarded, float angle, float velocity)
        {
            var w = S.Begin(Msg.DoorState);
            w.WriteShort((short)d.Index);
            byte f = 0;
            if (locked) f |= 2; if (boarded) f |= 4;
            w.WriteByte(f);
            w.WriteFloat(Mathf.Clamp(angle, 0f, d.MaxAngle));
            w.WriteFloat(velocity);
            S.SendToAll(NetChannel.Reliable);
            if (_doorMoving != null && d.Index < _doorMoving.Length) _doorMoving[d.Index] = velocity != 0f;
        }

        void BroadcastGrab(DoorEntity d, int grabber, int onlyTo = -1)
        {
            var w = S.Begin(Msg.DoorGrab);
            w.WriteShort((short)d.Index);
            w.WriteByte((byte)(grabber < 0 ? 255 : grabber));
            w.WriteFloat(d.Angle);
            w.WriteFloat(d.Velocity);
            if (onlyTo >= 0) S.SendTo(onlyTo, NetChannel.Reliable);
            else S.SendToAll(NetChannel.Reliable);
        }

        void DoorHitFx(DoorEntity d, DoorHit hit, float speed)
        {
            var w = S.Begin(Msg.DoorFx);
            w.WriteShort((short)d.Index);
            w.WriteByte((byte)hit);
            w.WriteUnit(Mathf.Clamp01(speed / 400f));
            S.SendToAll(NetChannel.Reliable);
            if (hit == DoorHit.Slam) DoorNoise(d, 13f, true);
            else if (hit == DoorHit.Bump || hit == DoorHit.Blocked) DoorNoise(d, 6f);
            else if (hit == DoorHit.Latch) DoorNoise(d, 3f);
        }

        void DoorNoise(DoorEntity d, float radius, bool force = false)
        {
            int i = d.Index;
            if (!force && W.Time < _doorNoiseAt[i]) return;
            _doorNoiseAt[i] = W.Time + 0.9f;
            DeliverNoise(d.Info.Center + Vector3.up, radius);
        }

        void ReleaseDoor(DoorEntity d, float angle, float velocity)
        {
            d.Grabber = -1;
            d.Angle = Mathf.Clamp(angle, 0f, d.MaxAngle);
            d.Velocity = Mathf.Clamp(velocity, -420f, 420f);
            BroadcastGrab(d, -1);
            EnsureDoorArrays();
            _doorDirty[d.Index] = true;
            _doorMoving[d.Index] = true;
        }

        public void OnDoorGrabReq(int sender, NetReader r)
        {
            int id = r.ReadShort();
            bool grab = r.ReadBool();
            float angle = r.ReadFloat();
            float vel = r.ReadFloat();
            if (id < 0 || id >= W.Doors.Length || !W.Running) return;
            EnsureDoorArrays();
            var d = W.Doors[id];
            if (float.IsNaN(angle) || float.IsNaN(vel)) { angle = d.Angle; vel = 0f; }
            if (!grab)
            {
                if (d.Grabber == sender) ReleaseDoor(d, angle, vel);
                return;
            }
            var st = W.StatusOf(sender);
            bool ok = IsPrisoner(sender) && !d.Locked && !d.Boarded && (d.Grabber < 0 || d.Grabber == sender)
                && st != null && st.Life == LifeState.Free && !st.Hidden && !st.Trapped && !st.InCar
                && Near(sender, d.Info.Center, 3.9f);
            if (!ok)
            {
                if (d.Grabber < 0 && d.Locked && IsPrisoner(sender)) { /* rattled a locked door: they hear it locally */ }
                BroadcastGrab(d, d.Grabber, sender); // refused: tell the requester who really holds it
                return;
            }
            // one door per hand
            foreach (var other in W.Doors) if (other != d && other.Grabber == sender) ReleaseDoor(other, other.Angle, 0f);
            d.Grabber = sender;
            // the grabber saw the leaf a little in the past; accept its angle when it is close to ours
            if (Mathf.Abs(angle - d.Angle) < 25f) d.Angle = Mathf.Clamp(angle, 0f, d.MaxAngle);
            d.Velocity = 0f;
            _doorDragAt[id] = W.Time;
            _doorDirty[id] = true;
            BroadcastGrab(d, sender);
        }

        public void OnDoorDragReq(int sender, NetReader r)
        {
            int id = r.ReadShort();
            float angle = r.ReadFloat();
            float vel = r.ReadFloat();
            if (id < 0 || id >= W.Doors.Length || float.IsNaN(angle) || float.IsNaN(vel)) return;
            EnsureDoorArrays();
            var d = W.Doors[id];
            if (d.Grabber != sender) return;
            float prev = d.Angle;
            angle = Mathf.Clamp(angle, 0f, d.MaxAngle);
            vel = Mathf.Clamp(vel, -420f, 420f);
            d.Angle = Mathf.MoveTowards(prev, angle, 90f); // no teleporting leaves
            d.Velocity = vel;
            _doorDragAt[id] = W.Time;
            _doorDirty[id] = true;
            if (prev > 0.5f && d.Angle <= 0.5f) DoorHitFx(d, vel < -150f ? DoorHit.Slam : DoorHit.Latch, Mathf.Abs(vel));
            else if (prev < d.MaxAngle - 0.5f && d.Angle >= d.MaxAngle - 0.5f && vel > 90f) DoorHitFx(d, DoorHit.Bump, vel);
            else if (Mathf.Abs(vel) > 150f) DoorNoise(d, 5f);
        }

        /// <summary>Omar never stops at a door: whatever Omar body walks into a leaf shoves it out of the way.</summary>
        void OmarShovesDoors(float dt)
        {
            foreach (var kv in W.Avatars)
            {
                var av = kv.Value;
                if (av == null || !av.IsOmar) continue;
                int id = kv.Key;
                Vector3 p = PosOf(id);
                // velocity from position changes (a remote Omar's position arrives in steps, ~20 per second)
                float now = Time.time;
                if (_omarLastPos.TryGetValue(id, out var last))
                {
                    Vector3 delta = p - last; delta.y = 0f;
                    if (delta.sqrMagnitude > 0.000001f)
                    {
                        float span = Mathf.Max(dt, now - (_omarMovedAt.TryGetValue(id, out var at) ? at : now - dt));
                        Vector3 v = delta / Mathf.Min(span, 0.25f);
                        if (v.magnitude > 15f) v = Vector3.zero; // teleport
                        _omarVel[id] = Vector3.Lerp(_omarVel.TryGetValue(id, out var ov) ? ov : Vector3.zero, v, 0.6f);
                        _omarMovedAt[id] = now;
                        _omarLastPos[id] = p;
                    }
                    else if (_omarVel.TryGetValue(id, out var cur) && now - (_omarMovedAt.TryGetValue(id, out var at2) ? at2 : 0f) > 0.15f)
                        _omarVel[id] = Vector3.MoveTowards(cur, Vector3.zero, dt * 8f);
                }
                else { _omarLastPos[id] = p; _omarMovedAt[id] = now; }
                if (!_omarVel.TryGetValue(id, out var vel) || vel.sqrMagnitude < 0.04f) continue;
                for (int i = 0; i < W.Doors.Length; i++)
                {
                    var d = W.Doors[i];
                    if (d.Locked || d.Boarded) continue;
                    if ((d.Info.Center - p).sqrMagnitude > 9f) continue;
                    float before = d.Velocity;
                    if (!d.Shove(p, vel)) continue;
                    if (d.Grabber >= 0)
                    {
                        // nobody holds a door shut against him
                        float v = d.Velocity;
                        ReleaseDoor(d, d.Angle, v);
                    }
                    _doorDirty[i] = true;
                    _doorMoving[i] = true;
                    if (d.Velocity > 330f && before < 60f) PushFx(d); // a running shoulder charge
                }
            }
        }

        void PushFx(DoorEntity d)
        {
            var w = S.Begin(Msg.DoorFx);
            w.WriteShort((short)d.Index);
            w.WriteByte(10); // Omar shoves it
            w.WriteUnit(1f);
            S.SendToAll(NetChannel.Reliable);
            DoorNoise(d, 9f);
        }

        public void OnDoorReq(int sender, NetReader r)
        {
            int id = r.ReadShort();
            bool open = r.ReadBool();
            RequestDoor(sender, id, open);
        }

        /// <summary>Omar unlocks a locked door with his keys (human Omar's E, the AI). Prisoners drag doors instead.</summary>
        public void RequestDoor(int sender, int id, bool open)
        {
            if (id < 0 || id >= W.Doors.Length) return;
            var d = W.Doors[id];
            if (d.Boarded || !IsOmar(sender)) return;
            if (!Near(sender, d.Info.Center, 3.6f)) return;
            if (!d.Locked) return;
            if (d.Grabber >= 0) ReleaseDoor(d, d.Angle, 0f);
            BroadcastDoor(d, false, false, d.Angle, open ? 150f : 0f);
            EnsureDoorArrays();
            _doorDirty[id] = true;
        }

        /// <summary>Omar (or AI) smashes the boards of a door.</summary>
        public void SmashBoards(int omarId, int door)
        {
            var d = W.Doors[door];
            if (!d.Boarded) return;
            BroadcastDoor(d, false, false, d.Angle, 320f);
            EnsureDoorArrays();
            _doorDirty[door] = true;
            DeliverNoise(d.Info.Center, 18f);
        }

        void UseDoor(int p, int id, int itemId)
        {
            if (id < 0 || id >= W.Doors.Length) return;
            var d = W.Doors[id];
            if (!Near(p, d.Info.Center, 3.6f)) return;
            if (d.Boarded)
            {
                if (!Holds(p, itemId, ItemType.Crowbar)) return;
                BroadcastDoor(d, false, false, d.Angle, 0f);
                DeliverNoise(d.Info.Center, 12f);
            }
            else if (d.Locked)
            {
                if (!Holds(p, itemId, ItemType.Lockpick)) return;
                BroadcastDoor(d, false, false, d.Angle, 45f); // the lock gives and the leaf pops ajar
                EnsureDoorArrays();
                _doorDirty[id] = true;
                Consume(p, itemId);
                Message("THE LOCKPICK SNAPPED, BUT THE DOOR IS OPEN", 3f, p);
            }
        }

        void BroadcastHide(int spot, int occupant, bool searched)
        {
            var w = S.Begin(Msg.HideState);
            w.WriteByte((byte)spot);
            w.WriteByte((byte)(occupant < 0 ? 255 : occupant));
            w.WriteBool(searched);
            S.SendToAll(NetChannel.Reliable);
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
    }
}
