using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // A caged prisoner rattling the lock: everyone nearby hears it, a human Omar gets a moment to come and punish them.
    public sealed partial class MatchWorld
    {
        void OnCageRattle(int sender, NetReader r)
        {
            int cage = r.ReadByte();
            int prisoner = r.ReadByte();
            if (Cages == null || cage < 0 || cage >= Cages.Length) return;
            var c = Cages[cage];
            c.RattleAt = UnityEngine.Time.time;
            if (prisoner != LocalId) // the prisoner already hears their own rattling
                AudioManager.Play3D(Snd.CageRattle, c.Info.Inside.position + Vector3.up, 0.8f, Random.Range(0.9f, 1.05f), 2f, 18f);
            if (LocalIsOmar)
                Pings.Add(new OmarPing { Position = c.Info.Inside.position, Expire = UnityEngine.Time.time + 2.5f, Kind = 0, Radius = 11f });
        }
    }
}
