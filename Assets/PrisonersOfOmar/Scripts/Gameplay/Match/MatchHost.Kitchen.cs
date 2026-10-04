using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Omar's butcher routine: he stands at the kitchen table chopping meat (AI now and then, a human
    // Omar whenever he likes). While he is busy a prisoner can sneak up behind him and smash him with a bottle or a
    // crowbar - he staggers for a few seconds and then comes for them.
    public sealed partial class MatchHost
    {
        public const byte KitchenStop = 0, KitchenStart = 1, KitchenChopFx = 2;
        /// <summary>Omar is chopping at the table right now.</summary>
        public bool KitchenBusy { get; private set; }
        int _kitchenOmar = -1;
        float _kitchenHumanUntil;

        void BroadcastKitchen(byte kind, int omarId)
        {
            var w = S.Begin(Msg.KitchenState);
            w.WriteByte(kind);
            w.WriteByte((byte)(omarId < 0 ? 255 : omarId));
            S.SendToAll(NetChannel.Reliable);
        }

        /// <summary>Omar (AI) arrived at the table.</summary>
        public void StartChopping(int omarId)
        {
            if (W.Map.Kitchen == null) return;
            _kitchenOmar = omarId;
            if (!KitchenBusy) { KitchenBusy = true; BroadcastKitchen(KitchenStart, omarId); }
        }

        public void StopChopping(int omarId)
        {
            if (!KitchenBusy) return;
            KitchenBusy = false;
            _kitchenOmar = -1;
            BroadcastKitchen(KitchenStop, omarId);
        }

        /// <summary>One swing of the cleaver into the meat.</summary>
        public void Chop(int omarId)
        {
            BroadcastAction(omarId, CharacterAction.ChopMeat);
            BroadcastKitchen(KitchenChopFx, omarId);
        }

        partial void TickKitchen(float dt)
        {
            // a human Omar is "busy" for a couple of seconds after each chop
            if (KitchenBusy && _kitchenHumanUntil > 0f && W.Time > _kitchenHumanUntil)
            {
                _kitchenHumanUntil = 0f;
                StopChopping(_kitchenOmar);
            }
        }

        public void OnChopReq(int sender, NetReader r)
        {
            var k = W.Map.Kitchen;
            if (k == null || !IsOmar(sender) || OmarStunned) return;
            if (!Near(sender, k.ChopPose.position, 2.6f)) return;
            StartChopping(sender);
            _kitchenHumanUntil = W.Time + 2.5f;
            Chop(sender);
        }

        public void OnOmarHitReq(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            if (!KitchenBusy || !IsPrisoner(sender) || OmarStunned) return;
            var st = W.StatusOf(sender);
            if (st == null || st.Life != LifeState.Free || st.Hidden || st.Trapped) return;
            bool bottle = Holds(sender, itemId, ItemType.Bottle);
            if (!bottle && !Holds(sender, itemId, ItemType.Crowbar)) return;
            int omar = _kitchenOmar;
            var oa = W.AvatarOf(omar);
            if (oa == null) return;
            Vector3 to = PosOf(sender) - PosOf(omar); to.y = 0f;
            if (to.magnitude > 2.4f) return;
            // only from behind: he is facing the table
            if (to.sqrMagnitude > 0.01f && Vector3.Angle(oa.Forward, to) < 95f) return;
            StopChopping(omar);
            BroadcastAction(sender, CharacterAction.Throw);
            if (bottle)
            {
                Consume(sender, itemId, 1);
                var w = S.Begin(Msg.BottleShatter);
                w.WriteVector3(oa.EyePosition);
                S.SendToAll(NetChannel.Reliable);
            }
            Stun(omar, 6f);
            DeliverNoise(oa.Position, 14f);
            Message("HE REELS... RUN!", 3f, sender);
            foreach (var ai in _ais) if (ai != null) ai.OnHitBy(sender, 6f);
        }
    }
}
