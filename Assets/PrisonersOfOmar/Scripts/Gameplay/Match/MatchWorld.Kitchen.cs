using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Omar's butcher table: client side (chop sounds / blood, the "busy" flag, the human Omar's prompt).
    public sealed partial class MatchWorld
    {
        /// <summary>Omar is chopping meat at the kitchen table right now (a chance to sneak past - or to hit him).</summary>
        public bool KitchenBusy { get; private set; }
        public int KitchenOmar { get; private set; } = -1;

        void BuildKitchen()
        {
            var k = Map != null ? Map.Kitchen : null;
            if (k == null || k.ChopInteract == null) return;
            InteractableRef.Attach(k.ChopInteract, new ButcherTable(k.ChopInteract));
        }

        partial void RegisterKitchenHandlers(NetSession s)
        {
            s.On(Msg.KitchenState, OnKitchenState);
            s.On(Msg.ChopReq, (id, r) => Host?.OnChopReq(id, r));
            s.On(Msg.OmarHitReq, (id, r) => Host?.OnOmarHitReq(id, r));
        }

        partial void UnregisterKitchenHandlers(NetSession s)
        {
            s.Off(Msg.KitchenState);
            s.Off(Msg.ChopReq);
            s.Off(Msg.OmarHitReq);
        }

        public void SendChop()
        {
            Session.Begin(Msg.ChopReq);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendOmarHit(int itemId)
        {
            var w = Session.Begin(Msg.OmarHitReq);
            w.WriteShort((short)itemId);
            Session.SendToHost(NetChannel.Reliable);
        }

        void OnKitchenState(int sender, NetReader r)
        {
            byte kind = r.ReadByte();
            int omar = r.ReadByte(); if (omar == 255) omar = -1;
            switch (kind)
            {
                case MatchHost.KitchenStart: KitchenBusy = true; KitchenOmar = omar; break;
                case MatchHost.KitchenStop: KitchenBusy = false; KitchenOmar = -1; break;
                case MatchHost.KitchenChopFx: ChopFx(); break;
            }
        }

        /// <summary>The cleaver goes into the meat (a beat after the swing starts).</summary>
        void ChopFx()
        {
            var k = Map != null ? Map.Kitchen : null;
            if (k == null) return;
            _chopFxAt = UnityEngine.Time.time + 0.5f;
        }

        float _chopFxAt = -1f;

        partial void TickKitchen(float dt)
        {
            if (_chopFxAt < 0f || UnityEngine.Time.time < _chopFxAt) return;
            _chopFxAt = -1f;
            var k = Map.Kitchen;
            Vector3 p = k.BlockTop + new Vector3(Random.Range(-0.25f, 0.25f), 0f, Random.Range(-0.15f, 0.15f));
            AudioManager.Play3D(AudioManager.Variant(Snd.OmarChop, 3), p, 1f, Random.Range(0.92f, 1.05f), 2f, 32f, AudioCategory.Omar);
            AudioManager.Play3D(AudioManager.Variant(Snd.MeatSquelch, 2), p, 0.8f, Random.Range(0.9f, 1.1f), 1.5f, 16f, AudioCategory.Omar);
            try { PsxFx.BloodBurst(p, Vector3.up + Random.insideUnitSphere * 0.4f, 0.7f); } catch { }
        }

        /// <summary>The butcher table: a human Omar can chop meat here (the AI does it on its own).</summary>
        sealed class ButcherTable : IInteractable
        {
            readonly Collider _c;
            public ButcherTable(Collider c) { _c = c; }
            public Vector3 InteractPoint => _c.bounds.center;

            public bool GetPrompt(Interactor who, out InteractPrompt p)
            {
                p = default;
                if (!who.IsOmar) return false;
                p = InteractPrompt.Press("CHOP MEAT");
                return true;
            }

            public void Interact(Interactor who) => Instance?.SendChop();
        }
    }
}
