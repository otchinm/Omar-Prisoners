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
            for (int i = 0; i < W.Cages.Length; i++) if (W.Cages[i].Occupant < 0) return i;
            return -1;
        }
    }
}
