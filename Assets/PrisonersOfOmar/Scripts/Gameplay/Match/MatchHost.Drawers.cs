using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // Drawers (host side): a prisoner next to it slides it open / shut.
    // (iteration 3) Padlocked drawers: a small key (stays in the lock) or a lockpick (used up) opens a key padlock, the
    // right combination opens a combination padlock (MatchHost.Locks.cs), and a crowbar pries any of them open - loud.
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
            if (d.Locked || d.Open == open || !Near(sender, d.InteractPoint, 3f)) return;
            var w = S.Begin(Msg.DrawerState);
            w.WriteShort((short)id);
            w.WriteBool(open);
            S.SendToAll(NetChannel.Reliable);
            DeliverNoise(d.InteractPoint, 2f);
        }

        void UseDrawerLock(int p, int id, int itemId)
        {
            if (id < 0 || id >= W.Drawers.Length) return;
            var d = W.Drawers[id];
            if (!d.Locked || !Near(p, d.InteractPoint, 3f)) return;
            var it = W.GetItem(itemId);
            if (it == null || !Holds(p, itemId, it.Type)) return;
            switch (it.Type)
            {
                case ItemType.SmallKey:
                    if (d.Lock != DrawerLockKind.Key) return;
                    Consume(p, itemId);   // it stays in the padlock
                    UnlockDrawer(d, 0, 2f);
                    break;
                case ItemType.Lockpick:
                    if (d.Lock != DrawerLockKind.Key) return;
                    Consume(p, itemId);
                    UnlockDrawer(d, 1, 3f);
                    break;
                case ItemType.Crowbar:
                    UnlockDrawer(d, 3, 22f);
                    break;
            }
        }

        void UnlockDrawer(DrawerEntity d, byte how, float noise)
        {
            if (!d.Locked) return;
            var w = S.Begin(Msg.DrawerLock);
            w.WriteShort((short)d.Index);
            w.WriteByte(how);
            S.SendToAll(NetChannel.Reliable);
            if (noise > 0f) DeliverNoise(d.InteractPoint, noise);
            // it slides out by itself: the lock is off and the reward shows (not for the admin's mass unlock)
            if (how != 4 && !d.Open)
            {
                var o = S.Begin(Msg.DrawerState);
                o.WriteShort((short)d.Index);
                o.WriteBool(true);
                S.SendToAll(NetChannel.Reliable);
            }
        }

        partial void DrawerCodeOpened(int codeLock)
        {
            var d = W.DrawerWithCodeLock(codeLock);
            if (d != null) UnlockDrawer(d, 2, 0f);
        }
    }
}
