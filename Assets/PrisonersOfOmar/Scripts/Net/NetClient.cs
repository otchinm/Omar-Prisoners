using System;
using System.Net;
using System.Net.Sockets;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// UDP client. Events are raised from <see cref="Poll"/> on the calling (main) thread.
    /// Connect is non-blocking: Connected or Disconnected(ConnectFailed/...) fires later.
    /// <para>Disconnected is raised when the connection ends for any reason other than a local
    /// <see cref="Disconnect"/> / <see cref="Connect"/> / <see cref="Dispose"/> call: rejected (ServerFull,
    /// VersionMismatch, ...), kicked, host closed, timeout, connect failure, bad data.</para>
    /// </summary>
    public sealed class NetClient : IDisposable
    {
        const int MaxReceivePerPoll = 1024;

        readonly NetSocket _socket = new NetSocket();
        readonly BufferPool _pool = new BufferPool();
        readonly NetReader _packetReader = new NetReader();
        readonly NetReader _messageReader = new NetReader();
        readonly NetWriter _connectPacket = new NetWriter(256);
        readonly NetWriter _control = new NetWriter(64);
        readonly byte[] _recvBuf = new byte[NetProtocol.ReceiveBufferSize];
        readonly NetConnection.MessageHandler _onMessage;
        NetConnection _conn;
        IPEndPoint _server;
        uint _token;
        double _connectStarted, _nextConnectSend;
        bool _failPending, _polling;
        int _generation;

        /// <summary>Protocol version sent in CONNECT (tests use it to provoke VersionMismatch).</summary>
        internal int ProtocolVersionOverride = GameInfo.ProtocolVersion;
        /// <summary>Token of the current attempt (tests).</summary>
        internal uint Token => _token;

        public NetClient() { _onMessage = OnMessage; }

        public ClientState State { get; private set; }
        /// <summary>Id the server assigned to this connection (valid when Connected).</summary>
        public int ConnectionId { get; private set; }
        /// <summary>Smoothed round trip time in seconds.</summary>
        public float Rtt => _conn != null ? _conn.Rtt : 0f;
        /// <summary>Socket-level counters since the last Connect.</summary>
        public NetStats Stats => _socket.Stats;
        /// <summary>Seconds without any packet from the server before Disconnected(Timeout).</summary>
        public float TimeoutSeconds { get; set; } = NetProtocol.DefaultTimeout;
        /// <summary>Seconds of CONNECT retries (every 0.5 s) before Disconnected(ConnectFailed).</summary>
        public float ConnectTimeoutSeconds { get; set; } = NetProtocol.DefaultTimeout;
        /// <summary>Resolved server endpoint of the current / last attempt (null if resolution failed).</summary>
        public IPEndPoint ServerEndPoint => _server;
        /// <summary>Local UDP port of the current attempt (0 when no socket is open).</summary>
        public int LocalPort => _socket.IsOpen ? _socket.LocalPort : 0;
        /// <summary>Reliable fragments waiting for an ack from the server (diagnostics).</summary>
        public int PendingReliable => _conn != null ? _conn.PendingReliable : 0;

        public event Action Connected;
        public event Action<DisconnectReason> Disconnected;
        public event Action<NetReader, NetChannel> DataReceived;

        /// <summary>
        /// Start connecting (drops any current connection silently). <paramref name="host"/> is an IPv4/IPv6 literal
        /// or a host name (resolved with DNS, IPv4 preferred). Resolution / socket errors are reported as
        /// Disconnected(ConnectFailed) on the next Poll. Payload is limited to NetProtocol.MaxConnectPayload bytes.
        /// </summary>
        public void Connect(string host, int port, string playerName, byte[] payload = null)
        {
            if (payload != null && payload.Length > NetProtocol.MaxConnectPayload)
                throw new ArgumentException("connect payload larger than " + NetProtocol.MaxConnectPayload + " bytes", nameof(payload));
            Shutdown(DisconnectReason.LocalClosed, true);

            string name = playerName ?? "";
            if (name.Length > NetProtocol.MaxPlayerNameLength) name = name.Substring(0, NetProtocol.MaxPlayerNameLength);
            _token = NetProtocol.NewToken();
            NetProtocol.WriteConnect(_connectPacket, ProtocolVersionOverride, _token, name, payload);
            State = ClientState.Connecting;
            _connectStarted = NetTime.Now;
            _nextConnectSend = _connectStarted;
            _server = null;
            try
            {
                if (port <= 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
                var address = Resolve(host);
                _server = new IPEndPoint(address, port);
                _socket.Open(address.AddressFamily, 0, false);
                SendConnect(_connectStarted);
            }
            catch (Exception e)
            {
                NetLog.Warn("connect to " + host + ":" + port + " failed: " + e.Message);
                _socket.Close();
                _failPending = true;
            }
        }

        /// <summary>Leave the server (it is told LocalClosed) and reset. Does not raise Disconnected.</summary>
        public void Disconnect() => Shutdown(DisconnectReason.LocalClosed, true);

        public void Poll()
        {
            if (State == ClientState.Disconnected || _polling) return;
            _polling = true;
            try
            {
                int gen = _generation;
                if (_failPending) { Fail(DisconnectReason.ConnectFailed, false); return; }
                double now = NetTime.Now;
                _socket.Pump(now);
                for (int i = 0; i < MaxReceivePerPoll; i++)
                {
                    int n = _socket.Receive(_recvBuf, out EndPoint _);
                    if (n < 0) break;
                    if (n == 0 || n > NetProtocol.MaxPacketSize) continue;
                    HandlePacket(n, now);
                    if (gen != _generation) return;
                }

                now = NetTime.Now;
                if (State == ClientState.Connecting)
                {
                    if (now - _connectStarted > ConnectTimeoutSeconds) { Fail(DisconnectReason.ConnectFailed, true); return; }
                    if (now >= _nextConnectSend) SendConnect(now);
                }
                else if (State == ClientState.Connected)
                {
                    if (_conn.Fault != DisconnectReason.None) { Fail(_conn.Fault, true); return; }
                    if (now - _conn.LastReceive > TimeoutSeconds) { Fail(DisconnectReason.Timeout, true); return; }
                    _conn.Flush(now);
                }
            }
            finally { _polling = false; }
        }

        /// <summary>Send everything queued so far (Poll does this automatically at its end).</summary>
        public void Flush()
        {
            if (State == ClientState.Connected) _conn.Flush(NetTime.Now);
        }

        public void Send(NetWriter message, NetChannel channel) => Send(message.Buffer, 0, message.Length, channel);

        /// <summary>Queue a message (copied). Ignored unless Connected. Messages over NetProtocol.MaxMessageSize throw.</summary>
        public void Send(byte[] data, int offset, int length, NetChannel channel)
        {
            NetServer.CheckMessage(data, offset, length);
            if (State == ClientState.Connected) _conn.Send(data, offset, length, channel);
        }

        public void Dispose() => Disconnect();

        // ------------------------------------------------------------------ internals

        static IPAddress Resolve(string host)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentException("empty host name");
            host = host.Trim();
            if (IPAddress.TryParse(host, out var ip)) return ip;
            IPAddress v6 = null;
            foreach (var a in Dns.GetHostAddresses(host))
            {
                if (a.AddressFamily == AddressFamily.InterNetwork) return a;
                if (v6 == null && a.AddressFamily == AddressFamily.InterNetworkV6) v6 = a;
            }
            if (v6 != null) return v6;
            throw new SocketException((int)SocketError.HostNotFound);
        }

        void SendConnect(double now)
        {
            _nextConnectSend = now + NetProtocol.ConnectRetryInterval;
            _socket.Send(_connectPacket.Buffer, _connectPacket.Length, _server);
        }

        void HandlePacket(int length, double now)
        {
            var r = _packetReader;
            r.SetBuffer(_recvBuf, 0, length);
            try
            {
                byte type = r.ReadByte();
                // Filter by token, not by source address: a multi-homed host may answer from another of its IPs.
                if (type == NetProtocol.PktConnect || r.ReadUInt() != _token) return;
                switch (type)
                {
                    case NetProtocol.PktAccept:
                    {
                        int id = r.ReadByte();
                        if (State == ClientState.Connected) { _conn.LastReceive = now; return; }
                        if (id == 0 || id == 255) throw new NetReadException("bad connection id");
                        State = ClientState.Connected;
                        ConnectionId = id;
                        _conn = new NetConnection(_socket, _pool, _onMessage, _server, _token, now) { Id = id };
                        Raise(Connected, "Connected");
                        return;
                    }
                    case NetProtocol.PktReject:
                        if (State == ClientState.Connecting) Fail(NetProtocol.ToReason(r.ReadByte(), DisconnectReason.ConnectFailed), false);
                        return;
                    case NetProtocol.PktDisconnect:
                        Fail(NetProtocol.ToReason(r.ReadByte(), DisconnectReason.HostClosed), false);
                        return;
                    case NetProtocol.PktData:
                        if (State != ClientState.Connected) return; // data overtook a lost ACCEPT: the server resends
                        _conn.LastReceive = now;
                        _conn.ProcessData(r, now);
                        return;
                    default:
                        throw new NetReadException("unexpected packet type");
                }
            }
            catch (NetReadException)
            {
                if (State == ClientState.Connected && _conn != null) _conn.ReportBadPacket(now);
            }
        }

        /// <summary>Close everything; optionally tell the server why. Never raises events.</summary>
        void Shutdown(DisconnectReason reason, bool notifyServer)
        {
            if (notifyServer && State != ClientState.Disconnected && _socket.IsOpen && _server != null)
            {
                NetProtocol.WriteControl(_control, NetProtocol.PktDisconnect, _token, (byte)reason);
                for (int i = 0; i < 3; i++) _socket.Send(_control.Buffer, _control.Length, _server);
            }
            if (_conn != null) { _conn.Close(); _conn = null; }
            _socket.Close();
            State = ClientState.Disconnected;
            ConnectionId = 0;
            _failPending = false;
            _generation++;
        }

        void Fail(DisconnectReason reason, bool notifyServer)
        {
            Shutdown(reason, notifyServer);
            var h = Disconnected;
            if (h == null) return;
            try { h(reason); }
            catch (Exception e) { NetLog.Warn("Disconnected handler threw: " + e); }
        }

        static void Raise(Action h, string name)
        {
            if (h == null) return;
            try { h(); }
            catch (Exception e) { NetLog.Warn(name + " handler threw: " + e); }
        }

        void OnMessage(NetConnection c, byte[] buffer, int offset, int length, NetChannel channel)
        {
            var h = DataReceived;
            if (h == null) return;
            _messageReader.SetBuffer(buffer, offset, length);
            try { h(_messageReader, channel); }
            catch (NetReadException e)
            {
                NetLog.Warn("malformed message from server: " + e.Message);
                c.ReportBadPacket(NetTime.Now);
            }
            catch (Exception e) { NetLog.Warn("DataReceived handler threw: " + e); }
        }
    }
}
