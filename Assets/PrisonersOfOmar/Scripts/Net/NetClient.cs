using System;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// UDP client. Events are raised from <see cref="Poll"/> on the calling (main) thread.
    /// Connect is non-blocking: Connected or Disconnected(ConnectFailed/...) fires later.
    /// </summary>
    public sealed class NetClient : IDisposable
    {
        public ClientState State { get; private set; }
        /// <summary>Id the server assigned to this connection (valid when Connected).</summary>
        public int ConnectionId { get; private set; }
        /// <summary>Smoothed round trip time in seconds.</summary>
        public float Rtt { get; private set; }
        public NetStats Stats { get; private set; }

        public event Action Connected;
        public event Action<DisconnectReason> Disconnected;
        public event Action<NetReader, NetChannel> DataReceived;

        /// <summary>Start connecting. <paramref name="host"/> is an IPv4/IPv6 literal or a host name.</summary>
        public void Connect(string host, int port, string playerName, byte[] payload = null) { State = ClientState.Connecting; }

        public void Disconnect() { State = ClientState.Disconnected; }
        public void Poll() { }

        public void Send(NetWriter message, NetChannel channel) => Send(message.Buffer, 0, message.Length, channel);
        public void Send(byte[] data, int offset, int length, NetChannel channel) { }

        public void Dispose() => Disconnect();

        void Unused() { Connected?.Invoke(); Disconnected?.Invoke(0); DataReceived?.Invoke(null, 0); }
    }
}
