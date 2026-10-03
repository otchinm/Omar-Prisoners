using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Client side of the local-player interactions owned by the "player" work package:
    // doors (physical drag, Omar pushing) and hiding spots. Registers its own message handlers.
    public sealed partial class MatchWorld
    {
        void RegisterPlayerHandlers(NetSession s)
        {
            s.On(Msg.DoorState, OnDoorState);
            s.On(Msg.HideState, OnHideState);
            s.On(Msg.DoorReq, (id, r) => Host?.OnDoorReq(id, r));
        }

        void UnregisterPlayerHandlers(NetSession s)
        {
            s.Off(Msg.DoorState);
            s.Off(Msg.HideState);
            s.Off(Msg.DoorReq);
        }

        /// <summary>Every frame (doors, hiding spots).</summary>
        void TickPlayer(float dt)
        {
            for (int i = 0; i < Doors.Length; i++) Doors[i].Tick(dt);
            for (int i = 0; i < Hiding.Length; i++) Hiding[i].Tick(dt);
        }

        public void SendDoor(int door, bool open)
        {
            var w = Session.Begin(Msg.DoorReq);
            w.WriteShort((short)door);
            w.WriteBool(open);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendSearch(int spot)
        {
            var w = Session.Begin(Msg.SearchReq);
            w.WriteByte((byte)spot);
            Session.SendToHost(NetChannel.Reliable);
        }

        void OnDoorState(int sender, NetReader r)
        {
            int id = r.ReadShort();
            byte f = r.ReadByte();
            if (id < 0 || id >= Doors.Length) return;
            Doors[id].Apply((f & 1) != 0, (f & 2) != 0, (f & 4) != 0, (f & 8) != 0);
        }

        void OnHideState(int sender, NetReader r)
        {
            int spot = r.ReadByte();
            int occ = r.ReadByte(); if (occ == 255) occ = -1;
            bool searched = r.ReadBool();
            if (spot < 0 || spot >= Hiding.Length) return;
            Hiding[spot].Apply(occ, searched);
        }
    }
}
