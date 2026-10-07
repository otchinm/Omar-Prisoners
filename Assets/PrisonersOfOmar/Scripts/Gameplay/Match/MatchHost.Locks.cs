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
            if (id < 0 || id >= W.CodeLocks.Count || !IsPrisoner(sender)) return;
            var lk = W.CodeLocks[id];
            var st = W.StatusOf(sender);
            // always answer, so the screen never hangs: 0 wrong, 1 right, 2 already open, 3 too fast, 4 refused
            if (lk.Open) { CodeAnswer(sender, id, 2); return; }
            if (!W.Running || st == null || st.Life != LifeState.Free || st.Hidden || st.Trapped || st.InCar || !Near(sender, lk.InteractPoint, 4.5f))
            {
                CodeAnswer(sender, id, 4);
                return;
            }
            // one try a second, on the real clock (the admin can pause the match clock)
            float now = Time.unscaledTime;
            if (_codeTryAt.TryGetValue(sender, out var last) && now - last < 1f) { CodeAnswer(sender, id, 3); return; }
            _codeTryAt[sender] = now;
            bool ok = code == lk.Code;
            CodeAnswer(sender, id, (byte)(ok ? 1 : 0));
            if (!ok) { DeliverNoise(lk.InteractPoint, lk.Info.WrongNoise); return; }
            OpenCodeLockHost(id, sender);
        }

        void CodeAnswer(int to, int id, byte status)
        {
            var w = S.Begin(Msg.CodeResult);
            w.WriteShort((short)id);
            w.WriteByte(status);
            S.SendTo(to, NetChannel.Reliable);
        }

        /// <summary>Opens a code lock for everybody and runs its result. <paramref name="silent"/> = admin (no noise).</summary>
        void OpenCodeLockHost(int id, int player, bool silent = false)
        {
            if (id < 0 || id >= W.CodeLocks.Count || W.CodeLocks[id].Open) return;
            var lk = W.CodeLocks[id];
            var w = S.Begin(Msg.CodeLockState);
            w.WriteShort((short)id);
            w.WriteBool(true);
            w.WriteByte((byte)Mathf.Clamp(player, 0, 255));
            S.SendToAll(NetChannel.Reliable);
            if (!silent) DeliverNoise(lk.InteractPoint, lk.Info.OpenNoise);
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
