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
