using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // Breaking out of a cage by force: every press on E is a small chance that the rusty lock gives way, but every few
    // presses the whole lock clanks. Omar may hear it (the closer he is, the likelier); then he comes, swings the cage
    // door open and kills whoever is still inside.
    public sealed partial class MatchHost
    {
        float[] _cageRattleAt;

        void CageRattled(int cage, int prisoner)
        {
            if (_cageRattleAt == null || _cageRattleAt.Length != W.Cages.Length)
            {
                _cageRattleAt = new float[W.Cages.Length];
                for (int i = 0; i < _cageRattleAt.Length; i++) _cageRattleAt[i] = -999f;
            }
            _cageRattleAt[cage] = W.Time;
            Vector3 p = W.Cages[cage].Info.Inside.position;
            var w = S.Begin(Msg.CageRattle);
            w.WriteByte((byte)cage);
            w.WriteByte((byte)prisoner);
            S.SendToAll(NetChannel.Reliable);
            DeliverNoise(p, 11f);
            if (!AdminState.OmarDeaf) foreach (var ai in _ais) if (ai != null) ai.OnCageRattle(cage, p);
        }

        /// <summary>The prisoner in this cage was rattling the lock a moment ago (Omar may punish them).</summary>
        public bool CageRattledRecently(int cage)
            => _cageRattleAt != null && cage >= 0 && cage < _cageRattleAt.Length && W.Time - _cageRattleAt[cage] < Tuning.CagePunishWindow;

        /// <summary>Omar swings the cage door open and kills the prisoner who tried to break out.</summary>
        public void PunishCage(int omarId, int cage)
        {
            if (cage < 0 || cage >= W.Cages.Length || OmarStunned || _ended || !W.Running) return;
            var c = W.Cages[cage];
            int occ = c.Occupant;
            if (occ < 0 || c.Open || !CageRattledRecently(cage) || !Near(omarId, c.Info.Outside.position, 3.4f)) return;
            var st = Edit(occ);
            if (st.Life != LifeState.Caged || AdminState.GodMode.Contains(occ)) return;
            BroadcastCage(cage, true, -1);
            BroadcastAttack(omarId, occ, 2);
            st.Life = LifeState.Dead;
            st.Cage = -1;
            st.Injured = false;
            st.TeleportSeq++;
            _struggle[occ * 2] = 0;
            _cageRattleAt[cage] = -999f;
            Commit(st);
            Message("HE HEARD YOU.", 4f, occ);
            CheckEnd();
        }
    }
}
