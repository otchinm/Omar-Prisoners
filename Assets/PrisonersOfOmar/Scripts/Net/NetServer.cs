using System;
using System.Collections.Generic;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// UDP host. Connection ids are small positive ints (1..254) assigned on accept.
    /// All events are raised from <see cref="Poll"/> on the calling (main) thread.
    /// A NetReader passed to an event is only valid during that callback.
    /// </summary>
    public sealed class NetServer : IDisposable
    {
        readonly List<int> _connections = new List<int>();

        public NetServer(int maxConnections) { MaxConnections = maxConnections; }

        public int MaxConnections { get; }
        public bool IsRunning { get; private set; }
        public int Port { get; private set; }
        public IReadOnlyList<int> Connections => _connections;
        public NetStats Stats { get; private set; }

        /// <summary>Return DisconnectReason.None to accept. Called before ClientConnected. Default accepts while there is room.</summary>
        public Func<ConnectRequest, DisconnectReason> Approve;

        public event Action<int, ConnectRequest> ClientConnected;
        public event Action<int, DisconnectReason> ClientDisconnected;
        public event Action<int, NetReader, NetChannel> DataReceived;

        public bool Start(int port) { Port = port; IsRunning = true; return true; }
        public void Stop() { IsRunning = false; _connections.Clear(); }
        public void Poll() { }

        public void Send(int connectionId, NetWriter message, NetChannel channel) => Send(connectionId, message.Buffer, 0, message.Length, channel);
        public void Send(int connectionId, byte[] data, int offset, int length, NetChannel channel) { }

        public void Broadcast(NetWriter message, NetChannel channel, int exceptConnectionId = -1)
        {
            for (int i = 0; i < _connections.Count; i++)
                if (_connections[i] != exceptConnectionId) Send(_connections[i], message, channel);
        }

        public void Disconnect(int connectionId, DisconnectReason reason) { _connections.Remove(connectionId); }

        /// <summary>Smoothed round trip time in seconds (0 if unknown).</summary>
        public float GetRtt(int connectionId) => 0f;

        public string GetAddress(int connectionId) => "";

        public void Dispose() => Stop();

        void Unused() { ClientConnected?.Invoke(0, default); ClientDisconnected?.Invoke(0, 0); DataReceived?.Invoke(0, null, 0); }
    }
}
