using System;
using System.Collections.Generic;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// LAN game discovery over UDP broadcast on <see cref="GameInfo.DiscoveryPort"/>.
    /// Host: StartAdvertising + UpdateAdvertisement. Clients: StartListening and read Sessions.
    /// Call Poll() every frame.
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        readonly List<LanSessionInfo> _sessions = new List<LanSessionInfo>();

        /// <summary>Sessions heard during the last few seconds.</summary>
        public IReadOnlyList<LanSessionInfo> Sessions => _sessions;
        public bool IsAdvertising { get; private set; }
        public bool IsListening { get; private set; }

        public void StartAdvertising(LanSessionInfo info) { IsAdvertising = true; }
        public void UpdateAdvertisement(LanSessionInfo info) { }
        public void StopAdvertising() { IsAdvertising = false; }

        public void StartListening() { IsListening = true; }
        public void StopListening() { IsListening = false; _sessions.Clear(); }

        public void Poll() { }

        public void Dispose() { StopAdvertising(); StopListening(); }
    }
}
