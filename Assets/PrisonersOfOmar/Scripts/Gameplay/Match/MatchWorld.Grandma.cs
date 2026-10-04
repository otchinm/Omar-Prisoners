using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) The grandmother: client side (presentation + network state).
    public sealed partial class MatchWorld
    {
        /// <summary>The old woman in her wheelchair (null when the map has no TV room).</summary>
        public GrandmaEntity Grandma;

        void BuildGrandma()
        {
            if (Map == null || Map.Grandma == null) return;
            Grandma = new GrandmaEntity(Map.Grandma, _dynamicRoot);
        }

        partial void RegisterGrandmaHandlers(NetSession s)
        {
            s.On(Msg.GrandmaState, OnGrandmaState);
            s.On(Msg.GrandmaSnap, OnGrandmaSnap);
        }

        partial void UnregisterGrandmaHandlers(NetSession s)
        {
            s.Off(Msg.GrandmaState);
            s.Off(Msg.GrandmaSnap);
        }

        partial void TickGrandma(float dt)
        {
            Grandma?.Tick(dt, IsHost, this);
        }

        void OnGrandmaState(int sender, NetReader r)
        {
            var mode = (GrandmaMode)r.ReadByte();
            byte stage = r.ReadByte();
            Vector3 pos = r.ReadVector3();
            float yaw = r.ReadFloat();
            byte voice = r.ReadByte();
            int target = r.ReadByte(); if (target == 255) target = -1;
            Grandma?.ApplyState(mode, stage, pos, yaw, voice, target, IsHost);
        }

        void OnGrandmaSnap(int sender, NetReader r)
        {
            Vector3 pos = r.ReadVector3();
            float yaw = r.ReadFloat();
            int target = r.ReadByte(); if (target == 255) target = -1;
            if (Grandma == null || IsHost) return;
            Grandma.SetNet(pos, yaw);
            Grandma.Target = target;
        }
    }
}
