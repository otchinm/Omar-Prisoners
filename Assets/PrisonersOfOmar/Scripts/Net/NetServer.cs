using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// UDP host. Connection ids are small positive ints (1..254) assigned on accept.
    /// All events are raised from <see cref="Poll"/> on the calling (main) thread.
    /// A NetReader passed to an event is only valid during that callback.
    /// <para>Single-threaded and non-blocking: call Poll() once per frame. Poll receives everything waiting,
    /// raises events, detects timeouts and flushes queued messages (call <see cref="Flush"/> to send
    /// messages queued later in the frame without waiting for the next Poll).</para>
    /// <para>Events: ClientDisconnected is raised for remote disconnects, timeouts and protocol errors, exactly once
    /// per connection. It is NOT raised for connections closed locally by <see cref="Disconnect"/> or <see cref="Stop"/>.</para>
    /// </summary>
    public sealed class NetServer : IDisposable
    {
        const int MaxReceivePerPoll = 1024;
        const double IdReuseCooldown = 10.0;
        const double SameEndpointReplaceDelay = 1.0;

        readonly List<int> _connections = new List<int>();
        readonly List<NetConnection> _conns = new List<NetConnection>();
        readonly List<NetConnection> _iter = new List<NetConnection>();
        readonly NetSocket _socket = new NetSocket();
        readonly BufferPool _pool = new BufferPool();
        readonly NetReader _packetReader = new NetReader();
        readonly NetReader _messageReader = new NetReader();
        readonly NetWriter _control = new NetWriter(64);
        readonly byte[] _recvBuf = new byte[NetProtocol.ReceiveBufferSize];
        readonly double[] _idReleasedAt = new double[256];
        readonly NetConnection.MessageHandler _onMessage;
        int _nextId = 1, _runId;
        bool _polling;

        public NetServer(int maxConnections)
        {
            MaxConnections = maxConnections;
            _onMessage = OnMessage;
            for (int i = 0; i < _idReleasedAt.Length; i++) _idReleasedAt[i] = double.NegativeInfinity;
        }

        public int MaxConnections { get; }
        public bool IsRunning { get; private set; }
        /// <summary>The bound port (the real one when Start(0) picked an ephemeral port).</summary>
        public int Port { get; private set; }
        public IReadOnlyList<int> Connections => _connections;
        /// <summary>Socket-level counters since Start.</summary>
        public NetStats Stats => _socket.Stats;
        /// <summary>Seconds without any packet before a client is dropped with Timeout.</summary>
        public float TimeoutSeconds { get; set; } = NetProtocol.DefaultTimeout;
        /// <summary>Try an IPv6 dual-mode socket (accepts IPv4 and IPv6) before falling back to IPv4 only.</summary>
        public bool UseDualMode { get; set; } = true;
        /// <summary>True when the bound socket is IPv6 dual-mode (false: IPv4 only).</summary>
        public bool IsDualMode { get; private set; }

        /// <summary>
        /// Return DisconnectReason.None to accept. Called before ClientConnected. Default accepts while there is room.
        /// The protocol version is checked before (VersionMismatch) and so is capacity (ServerFull): Approve is
        /// only asked when there is room. Exceptions thrown by Approve reject the client with ConnectFailed.
        /// </summary>
        public Func<ConnectRequest, DisconnectReason> Approve;

        public event Action<int, ConnectRequest> ClientConnected;
        public event Action<int, DisconnectReason> ClientDisconnected;
        public event Action<int, NetReader, NetChannel> DataReceived;

        /// <summary>Bind the port (0 = any free port) and start accepting. Returns false (and logs) on failure.</summary>
        public bool Start(int port)
        {
            Stop();
            IsDualMode = false;
            if (UseDualMode && Socket.OSSupportsIPv6)
            {
                try { _socket.Open(AddressFamily.InterNetworkV6, port, true); IsDualMode = true; }
                catch (Exception) { _socket.Close(); }
            }
            if (!_socket.IsOpen)
            {
                try { _socket.Open(AddressFamily.InterNetwork, port, false); }
                catch (Exception e)
                {
                    NetLog.Warn("server could not bind port " + port + ": " + e.Message);
                    return false;
                }
            }
            Port = _socket.LocalPort;
            IsRunning = true;
            _runId++;
            return true;
        }

        /// <summary>Disconnect every client with HostClosed (no ClientDisconnected events) and close the socket.</summary>
        public void Stop()
        {
            if (!IsRunning) return;
            for (int i = _conns.Count - 1; i >= 0; i--) Drop(_conns[i], DisconnectReason.HostClosed, true, false);
            _socket.Close();
            IsRunning = false;
            _runId++;
        }

        public void Poll()
        {
            if (!IsRunning || _polling) return;
            _polling = true;
            try
            {
                int run = _runId;
                double now = NetTime.Now;
                _socket.Pump(now);
                for (int i = 0; i < MaxReceivePerPoll; i++)
                {
                    int n = _socket.Receive(_recvBuf, out EndPoint from);
                    if (n < 0) break;
                    if (n == 0 || n > NetProtocol.MaxPacketSize || !(from is IPEndPoint ep)) continue;
                    HandlePacket(n, ep, now);
                    if (run != _runId) return;
                }

                now = NetTime.Now;
                _iter.Clear();
                _iter.AddRange(_conns);
                for (int i = 0; i < _iter.Count; i++)
                {
                    var c = _iter[i];
                    if (c.Closed) continue;
                    if (c.Fault != DisconnectReason.None) Drop(c, c.Fault, true, true);
                    else if (now - c.LastReceive > TimeoutSeconds) Drop(c, DisconnectReason.Timeout, true, true);
                    if (run != _runId) return;
                }
                _iter.Clear();
                Flush();
            }
            finally { _polling = false; }
        }

        /// <summary>Send everything queued so far (Poll does this automatically at its end).</summary>
        public void Flush()
        {
            if (!IsRunning) return;
            double now = NetTime.Now;
            for (int i = 0; i < _conns.Count; i++) _conns[i].Flush(now);
        }

        public void Send(int connectionId, NetWriter message, NetChannel channel) => Send(connectionId, message.Buffer, 0, message.Length, channel);

        /// <summary>Queue a message (copied). Unknown ids are ignored. Messages over NetProtocol.MaxMessageSize throw.</summary>
        public void Send(int connectionId, byte[] data, int offset, int length, NetChannel channel)
        {
            CheckMessage(data, offset, length);
            var c = Find(connectionId);
            if (c != null) c.Send(data, offset, length, channel);
        }

        public void Broadcast(NetWriter message, NetChannel channel, int exceptConnectionId = -1)
        {
            for (int i = 0; i < _connections.Count; i++)
                if (_connections[i] != exceptConnectionId) Send(_connections[i], message, channel);
        }

        /// <summary>Close a connection: the client is told <paramref name="reason"/> (3 unreliable DISCONNECTs). No ClientDisconnected event.</summary>
        public void Disconnect(int connectionId, DisconnectReason reason)
        {
            var c = Find(connectionId);
            if (c != null) Drop(c, reason == DisconnectReason.None ? DisconnectReason.Kicked : reason, true, false);
        }

        /// <summary>Smoothed round trip time in seconds (0 if unknown).</summary>
        public float GetRtt(int connectionId)
        {
            var c = Find(connectionId);
            return c != null ? c.Rtt : 0f;
        }

        /// <summary>IP address of the client ("192.168.1.20"), "" for unknown ids.</summary>
        public string GetAddress(int connectionId)
        {
            var ep = GetEndPoint(connectionId);
            return ep != null ? ep.Address.ToString() : "";
        }

        /// <summary>Remote endpoint of the client (IPv4-mapped addresses are converted to IPv4), null for unknown ids.</summary>
        public IPEndPoint GetEndPoint(int connectionId)
        {
            var c = Find(connectionId);
            return c != null ? Normalize(c.EndPoint) : null;
        }

        /// <summary>Reliable fragments waiting for an ack from this client (diagnostics).</summary>
        public int GetPendingReliable(int connectionId)
        {
            var c = Find(connectionId);
            return c != null ? c.PendingReliable : 0;
        }

        public void Dispose() => Stop();

        // ------------------------------------------------------------------ internals

        internal static void CheckMessage(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || length < 0 || offset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(length));
            if (length > NetProtocol.MaxMessageSize) throw new ArgumentException("message larger than " + NetProtocol.MaxMessageSize + " bytes");
        }

        internal static IPEndPoint Normalize(IPEndPoint ep)
        {
            return ep.Address.IsIPv4MappedToIPv6 ? new IPEndPoint(ep.Address.MapToIPv4(), ep.Port) : ep;
        }

        NetConnection Find(int id)
        {
            for (int i = 0; i < _conns.Count; i++) if (_conns[i].Id == id) return _conns[i];
            return null;
        }

        NetConnection Find(IPEndPoint ep)
        {
            for (int i = 0; i < _conns.Count; i++) if (_conns[i].EndPoint.Equals(ep)) return _conns[i];
            return null;
        }

        void HandlePacket(int length, IPEndPoint from, double now)
        {
            var r = _packetReader;
            r.SetBuffer(_recvBuf, 0, length);
            NetConnection c = null;
            try
            {
                byte type = r.ReadByte();
                if (type == NetProtocol.PktConnect) { HandleConnect(r, from, now); return; }
                c = Find(from);
                if (c == null || r.ReadUInt() != c.Token) return; // unknown endpoint or stale session
                c.LastReceive = now;
                switch (type)
                {
                    case NetProtocol.PktData:
                        c.ProcessData(r, now);
                        break;
                    case NetProtocol.PktDisconnect:
                        Drop(c, NetProtocol.ToReason(r.ReadByte(), DisconnectReason.LocalClosed), false, true);
                        break;
                    default:
                        throw new NetReadException("unexpected packet type");
                }
            }
            catch (NetReadException)
            {
                if (c != null && !c.Closed) c.ReportBadPacket(now);
            }
        }

        void HandleConnect(NetReader r, IPEndPoint from, double now)
        {
            if (r.ReadUInt() != NetProtocol.Magic) return;
            int protocol = r.ReadInt();
            uint token = r.ReadUInt();
            int run = _runId;

            var existing = Find(from);
            if (existing != null)
            {
                if (existing.Token == token)
                {
                    existing.LastReceive = now;
                    SendControl(from, NetProtocol.PktAccept, token, (byte)existing.Id); // our ACCEPT was lost
                    return;
                }
                // Same endpoint, new attempt: the old session is dead (client restarted on the same port).
                // Ignore while the old session is still talking, so a late duplicate cannot kill a live session.
                if (now - existing.LastReceive < SameEndpointReplaceDelay) return;
                Drop(existing, DisconnectReason.Timeout, false, true);
                if (run != _runId) return;
            }

            if (protocol != GameInfo.ProtocolVersion) { Reject(from, token, DisconnectReason.VersionMismatch); return; }
            string name = r.ReadString();
            byte[] payload = r.ReadByteArray();
            if (_conns.Count >= MaxConnections) { Reject(from, token, DisconnectReason.ServerFull); return; }

            var request = new ConnectRequest { ProtocolVersion = protocol, PlayerName = name, Payload = payload, EndPoint = Normalize(from) };
            var reason = DisconnectReason.None;
            if (Approve != null)
            {
                try { reason = Approve(request); }
                catch (Exception e) { NetLog.Warn("Approve threw: " + e); reason = DisconnectReason.ConnectFailed; }
                if (run != _runId) return; // Approve stopped/restarted the server
            }
            if (reason == DisconnectReason.None && _conns.Count >= MaxConnections) reason = DisconnectReason.ServerFull;
            int id = reason == DisconnectReason.None ? AllocateId(now) : 0;
            if (reason == DisconnectReason.None && id == 0) reason = DisconnectReason.ServerFull;
            if (reason != DisconnectReason.None) { Reject(from, token, reason); return; }

            var c = new NetConnection(_socket, _pool, _onMessage, from, token, now) { Id = id };
            _conns.Add(c);
            _connections.Add(id);
            SendControl(from, NetProtocol.PktAccept, token, (byte)id);

            var h = ClientConnected;
            if (h != null)
            {
                try { h(id, request); }
                catch (Exception e) { NetLog.Warn("ClientConnected handler threw: " + e); }
            }
        }

        int AllocateId(double now)
        {
            for (int n = 0; n < 254; n++)
            {
                int id = _nextId;
                _nextId = _nextId >= 254 ? 1 : _nextId + 1;
                if (Find(id) == null && now - _idReleasedAt[id] >= IdReuseCooldown) return id;
            }
            return 0;
        }

        void Reject(IPEndPoint to, uint token, DisconnectReason reason) => SendControl(to, NetProtocol.PktReject, token, (byte)reason);

        void SendControl(IPEndPoint to, byte type, uint token, byte value)
        {
            NetProtocol.WriteControl(_control, type, token, value);
            _socket.Send(_control.Buffer, _control.Length, to);
        }

        void Drop(NetConnection c, DisconnectReason reason, bool notifyPeer, bool raiseEvent)
        {
            if (c.Closed) return;
            if (notifyPeer)
                for (int i = 0; i < 3; i++) SendControl(c.EndPoint, NetProtocol.PktDisconnect, c.Token, (byte)reason);
            c.Close();
            _conns.Remove(c);
            _connections.Remove(c.Id);
            _idReleasedAt[c.Id] = NetTime.Now;
            if (!raiseEvent) return;
            var h = ClientDisconnected;
            if (h == null) return;
            try { h(c.Id, reason); }
            catch (Exception e) { NetLog.Warn("ClientDisconnected handler threw: " + e); }
        }

        void OnMessage(NetConnection c, byte[] buffer, int offset, int length, NetChannel channel)
        {
            var h = DataReceived;
            if (h == null) return;
            _messageReader.SetBuffer(buffer, offset, length);
            try { h(c.Id, _messageReader, channel); }
            catch (NetReadException e)
            {
                NetLog.Warn("malformed message from connection " + c.Id + ": " + e.Message);
                c.ReportBadPacket(NetTime.Now);
            }
            catch (Exception e) { NetLog.Warn("DataReceived handler threw: " + e); }
        }
    }
}
