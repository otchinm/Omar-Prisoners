using System;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>Connects the transport's warning hook to the game log.</summary>
    static class NetLogHook
    {
        public static void Install(Action<string> sink)
        {
            Net.NetLog.Warning = sink;
        }
    }
}
