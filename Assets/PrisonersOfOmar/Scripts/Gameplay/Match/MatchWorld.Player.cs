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
            s.On(Msg.DoorGrabReq, (id, r) => Host?.OnDoorGrabReq(id, r));
            s.On(Msg.DoorDragReq, (id, r) => Host?.OnDoorDragReq(id, r));
            s.On(Msg.DoorGrab, OnDoorGrab);
            s.On(Msg.DoorAngles, OnDoorAngles);
            s.On(Msg.DoorFx, OnDoorFx);
        }

        void UnregisterPlayerHandlers(NetSession s)
        {
            s.Off(Msg.DoorState);
            s.Off(Msg.HideState);
            s.Off(Msg.DoorReq);
            s.Off(Msg.DoorGrabReq);
            s.Off(Msg.DoorDragReq);
            s.Off(Msg.DoorGrab);
            s.Off(Msg.DoorAngles);
            s.Off(Msg.DoorFx);
        }

        /// <summary>Every frame (doors, hiding spots).</summary>
        void TickPlayer(float dt)
        {
            bool authority = IsHost;
            for (int i = 0; i < Doors.Length; i++) Doors[i].Tick(dt, authority);
            for (int i = 0; i < Hiding.Length; i++) Hiding[i].Tick(dt);
        }

        public void SendDoor(int door, bool open)
        {
            var w = Session.Begin(Msg.DoorReq);
            w.WriteShort((short)door);
            w.WriteBool(open);
            Session.SendToHost(NetChannel.Reliable);
        }

        /// <summary>
        /// A prisoner peeking (AvatarFlags.Peek) next to a door that is open only a crack is hidden from an eye on the
        /// other side of that door's wall.
        /// </summary>
        public bool PeekHides(Avatar target, Vector3 eye)
        {
            if (target == null || !target.Peeking || Doors == null) return false;
            Vector3 p = target.Position;
            for (int i = 0; i < Doors.Length; i++)
            {
                var d = Doors[i];
                if (d.Angle > PrisonerController.PeekMaxAngle) continue;
                Vector3 c = d.Info.Center;
                if (Mathf.Abs(p.y - c.y) > 1.5f) continue;
                Vector3 flat = p - c; flat.y = 0f;
                if (flat.magnitude > 1.8f) continue;
                Vector3 n = d.Info.SwingDirection; n.y = 0f;
                if (n.sqrMagnitude < 0.01f) continue;
                n.Normalize();
                float sp = Vector3.Dot(p - c, n), se = Vector3.Dot(eye - c, n);
                if (sp * se < 0f && Mathf.Abs(se) > 0.25f) return true;
            }
            return false;
        }

        public void SendDoorGrab(int door, bool grab, float angle, float velocity)
        {
            var w = Session.Begin(Msg.DoorGrabReq);
            w.WriteShort((short)door);
            w.WriteBool(grab);
            w.WriteFloat(angle);
            w.WriteFloat(velocity);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendDoorDrag(int door, float angle, float velocity)
        {
            var w = Session.Begin(Msg.DoorDragReq);
            w.WriteShort((short)door);
            w.WriteFloat(angle);
            w.WriteFloat(velocity);
            Session.SendToHost(NetChannel.Unreliable);
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
            float angle = r.ReadFloat();
            float vel = r.ReadFloat();
            if (id < 0 || id >= Doors.Length) return;
            var d = Doors[id];
            d.ApplyState((f & 2) != 0, (f & 4) != 0, angle, vel, IsHost);
            if (d.LocalDrive && (d.Locked || d.Boarded)) d.LocalDrive = false;
        }

        void OnDoorGrab(int sender, NetReader r)
        {
            int id = r.ReadShort();
            int g = r.ReadByte(); if (g == 255) g = -1;
            float angle = r.ReadFloat();
            float vel = r.ReadFloat();
            if (id < 0 || id >= Doors.Length) return;
            var d = Doors[id];
            d.Grabber = g;
            if (g != LocalId && d.LocalDrive)
            {
                // refused, or Omar tore it out of our hand
                d.LocalDrive = false;
                d.PredictUntil = 0f;
            }
            if (!IsHost && !d.LocalDrive) d.SetNet(angle, vel);
        }

        void OnDoorAngles(int sender, NetReader r)
        {
            int n = r.ReadByte();
            for (int k = 0; k < n; k++)
            {
                int id = r.ReadShort();
                float angle = r.ReadFloat();
                float vel = r.ReadFloat();
                if (id < 0 || id >= Doors.Length || IsHost) continue;
                var d = Doors[id];
                if (!d.LocalDrive) d.SetNet(angle, vel);
            }
        }

        void OnDoorFx(int sender, NetReader r)
        {
            int id = r.ReadShort();
            int kind = r.ReadByte();
            float strength = r.ReadUnit();
            if (id < 0 || id >= Doors.Length) return;
            var d = Doors[id];
            if (kind == 10)
            {
                // Omar's shoulder hits the leaf
                AudioManager.Play3D(Snd.OmarDoorPush, d.Info.Center + Vector3.up, 0.9f, Random.Range(0.9f, 1.02f), 2f, 30f, AudioCategory.Omar);
                d.PlayHit(DoorHit.Bump, 0.8f);
                return;
            }
            d.PlayHit((DoorHit)kind, strength);
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
