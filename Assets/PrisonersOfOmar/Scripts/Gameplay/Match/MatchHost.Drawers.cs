using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // Drawers (host side): a prisoner next to it slides it open / shut.
    public sealed partial class MatchHost
    {
        public void OnDrawerReq(int sender, NetReader r)
        {
            int id = r.ReadShort();
            bool open = r.ReadBool();
            if (id < 0 || id >= W.Drawers.Length || !W.Running || !IsPrisoner(sender)) return;
            var st = W.StatusOf(sender);
            if (st == null || st.Life != LifeState.Free || st.Hidden || st.Trapped || st.InCar) return;
            var d = W.Drawers[id];
            if (d.Open == open || !Near(sender, d.InteractPoint, 3f)) return;
            var w = S.Begin(Msg.DrawerState);
            w.WriteShort((short)id);
            w.WriteBool(open);
            S.SendToAll(NetChannel.Reliable);
            DeliverNoise(d.InteractPoint, 2f);
        }
    }
}
