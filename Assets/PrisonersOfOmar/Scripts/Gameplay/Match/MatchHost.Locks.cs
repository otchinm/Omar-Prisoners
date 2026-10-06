using System.Collections.Generic;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3) Code locks, host side: a prisoner at the lock tries a code (one try a second per player, so codes can't
    // be machine-gunned); a wrong one clanks, the right one opens it for everybody and runs its result.
    public sealed partial class MatchHost
    {
        readonly Dictionary<int, float> _codeTryAt = new Dictionary<int, float>();

        public void OnCodeReq(int sender, NetReader r)
        {
            int id = r.ReadShort();
            string code = r.ReadString();
            if (id < 0 || id >= W.CodeLocks.Count || !W.Running || !IsPrisoner(sender)) return;
            var lk = W.CodeLocks[id];
            var st = W.StatusOf(sender);
            if (lk.Open || st == null || st.Life != LifeState.Free || st.Hidden || st.Trapped || st.InCar) return;
            if (!Near(sender, lk.InteractPoint, 4f)) return;
            if (_codeTryAt.TryGetValue(sender, out var last) && W.Time - last < 1f) return;
            _codeTryAt[sender] = W.Time;
            bool ok = code == lk.Code;
            var w = S.Begin(Msg.CodeResult);
            w.WriteShort((short)id);
            w.WriteBool(ok);
            S.SendTo(sender, NetChannel.Reliable);
            if (!ok) { DeliverNoise(lk.InteractPoint, lk.Info.WrongNoise); return; }
            OpenCodeLockHost(id, sender);
        }

        /// <summary>Opens a code lock for everybody and runs its result (also admin).</summary>
        void OpenCodeLockHost(int id, int player)
        {
            if (id < 0 || id >= W.CodeLocks.Count || W.CodeLocks[id].Open) return;
            var lk = W.CodeLocks[id];
            var w = S.Begin(Msg.CodeLockState);
            w.WriteShort((short)id);
            w.WriteBool(true);
            w.WriteByte((byte)Mathf.Clamp(player, 0, 255));
            S.SendToAll(NetChannel.Reliable);
            DeliverNoise(lk.InteractPoint, lk.Info.OpenNoise);
            ApplyPuzzleResult(lk.Info.Result, lk.Info.ResultDoor, lk.Info.ResultItem, lk.Info.ResultPose);
            OnCodeLockOpenedFeature(lk.Info.Name, id, player);
        }

        /// <summary>Feature results keyed on the lock name.</summary>
        void OnCodeLockOpenedFeature(string name, int id, int player)
        {
            if (name == "Shelter")
            {
                if (!W.Objectives.ShelterOpen) { var o = EditObj(); o.ShelterOpen = true; CommitObj(o); }
                return;
            }
            DrawerCodeOpened(id);
        }

        partial void DrawerCodeOpened(int codeLock);
    }
}
