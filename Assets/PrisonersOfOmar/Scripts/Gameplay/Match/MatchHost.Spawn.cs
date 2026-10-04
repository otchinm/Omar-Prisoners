using System.Collections.Generic;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Prisoner spawns / cage slots, owned by the "session" work package.
    public sealed partial class MatchHost
    {
        /// <summary>Which cage a captured prisoner is locked in (-1 = none left: they die).
        /// <paramref name="capturedAt"/> = where they were caught.</summary>
        int PickCageFor(int prisoner, Vector3 capturedAt)
        {
            // back into their own cage if it is free, else the nearest free cage already standing,
            // else a new slot (it appears for everyone when the occupant is broadcast: CageEntity.Apply activates it)
            if (W.StartCages.TryGetValue(prisoner, out int own) && own >= 0 && own < W.Cages.Length && W.Cages[own].Occupant < 0) return own;
            int best = -1;
            float bestD = float.MaxValue;
            for (int pass = 0; pass < 2 && best < 0; pass++)
                for (int i = 0; i < W.Cages.Length; i++)
                {
                    var c = W.Cages[i];
                    if (c.Occupant >= 0 || c.Active != (pass == 0)) continue;
                    float d = (c.Info.Inside.position - capturedAt).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = i; }
                }
            return best;
        }
    }
}
