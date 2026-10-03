using System.Collections.Generic;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// (iteration 2) Live admin flags. Written by the admin implementation ("session" work package), READ by the
    /// gameplay code of the other packages, which must honour them:
    ///   host side ("world"):  GodMode (no hits / captures / traps), Invisible (not detected, noises ignored),
    ///                         OmarFrozen / OmarBlind / OmarDeaf / OmarSleepUntil / OmarHuntTarget (AI),
    ///                         GrandmaDisabled, ClockPaused;
    ///   local ("player"):     Noclip, SpeedMultiplier, InfiniteStamina, InfiniteLight (own prisoner / Omar).
    /// Host-side sets hold player ids. Everything resets when a match is torn down (Reset()).
    /// </summary>
    public static class AdminState
    {
        // ---- host side
        public static readonly HashSet<int> GodMode = new HashSet<int>();
        public static readonly HashSet<int> Invisible = new HashSet<int>();
        public static bool OmarFrozen, OmarBlind, OmarDeaf;
        /// <summary>Match time (MatchWorld.Time) until which the AI Omar does nothing.</summary>
        public static float OmarSleepUntil = -1f;
        /// <summary>Player id the AI Omar must hunt (-1 = none).</summary>
        public static int OmarHuntTarget = -1;
        public static bool GrandmaDisabled;
        public static bool ClockPaused;

        // ---- local (this machine's own character)
        public static bool Noclip, InfiniteStamina, InfiniteLight;
        public static float SpeedMultiplier = 1f;

        public static void Reset()
        {
            GodMode.Clear(); Invisible.Clear();
            OmarFrozen = OmarBlind = OmarDeaf = false;
            OmarSleepUntil = -1f; OmarHuntTarget = -1;
            GrandmaDisabled = false; ClockPaused = false;
            Noclip = InfiniteStamina = InfiniteLight = false;
            SpeedMultiplier = 1f;
        }
    }
}
