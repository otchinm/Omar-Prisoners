using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// LAN game discovery over UDP broadcast on <see cref="GameInfo.DiscoveryPort"/>.
    /// Host: StartAdvertising + UpdateAdvertisement. Clients: StartListening and read Sessions.
    /// Call Poll() every frame.
    /// <para>The host sends a beacon every second to 255.255.255.255, to the subnet broadcast address of every
    /// IPv4 interface and to 127.255.255.255 (other instances on the same PC). Listeners share the port
    /// (ReuseAddress), so several game instances on one machine can browse at once. Beacons carry a random
    /// per-advertiser id so the same host heard on several interfaces shows up once (non-loopback address
    /// preferred). Sessions expire 4 s after their last beacon. All socket / network errors are swallowed and
    /// sockets are re-opened automatically every 2 s while they fail.</para>
    /// <para>Beacon: [magic u32][protocol i32][advertiser id u32][host name str][game port u16][players u8][max players u8][in lobby u8]</para>
    /// </summary>
    public sealed class LanDiscovery : IDisposable
    {
        // Interface enumeration can take tens of ms on Windows/Mono: do it on StartAdvertising and then rarely.
        const double BeaconInterval = 1.0, SessionLifetime = 4.0, RetryInterval = 2.0, TargetRefreshInterval = 60.0;
        const int MaxHostNameLength = 32;

        readonly List<LanSessionInfo> _sessions = new List<LanSessionInfo>();
        readonly List<uint> _sessionIds = new List<uint>(); // advertiser id per session (parallel list)
        readonly List<IPEndPoint> _targets = new List<IPEndPoint>();
        static readonly EndPoint AnyEndPoint = new IPEndPoint(IPAddress.Any, 0);
        NetWriter _beacon = new NetWriter(128), _spare = new NetWriter(128);
        readonly NetReader _reader = new NetReader();
        readonly byte[] _recvBuf = new byte[1024];
        readonly uint _advertiserId = NetProtocol.NewToken();
        readonly int _port;
        Socket _sendSocket, _listenSocket;
        double _nextBeacon, _lastBeacon = double.NegativeInfinity, _nextTargetRefresh, _nextSendOpen, _nextListenOpen;

        /// <summary>Extra unicast beacon destinations (tests, or known hosts on networks without broadcast).</summary>
        public readonly List<IPEndPoint> ExtraTargets = new List<IPEndPoint>();

        public LanDiscovery() : this(GameInfo.DiscoveryPort) { }
        /// <summary>Use a custom discovery port (tests).</summary>
        public LanDiscovery(int discoveryPort) { _port = discoveryPort; }

        /// <summary>Sessions heard during the last few seconds.</summary>
        public IReadOnlyList<LanSessionInfo> Sessions => _sessions;
        public bool IsAdvertising { get; private set; }
        public bool IsListening { get; private set; }
        public int DiscoveryPort => _port;

        public void StartAdvertising(LanSessionInfo info)
        {
            IsAdvertising = true;
            WriteBeacon(info, _beacon);
            _nextBeacon = NetTime.Now;
            _nextSendOpen = 0;
            _nextTargetRefresh = 0;
        }

        /// <summary>Change the advertised data; a changed beacon goes out early (at most 4 per second).</summary>
        public void UpdateAdvertisement(LanSessionInfo info)
        {
            WriteBeacon(info, _spare);
            bool changed = _spare.Length != _beacon.Length;
            for (int i = 0; !changed && i < _spare.Length; i++) changed = _spare.Buffer[i] != _beacon.Buffer[i];
            if (!changed) return;
            var t = _beacon; _beacon = _spare; _spare = t;
            _nextBeacon = Math.Min(_nextBeacon, _lastBeacon + 0.25);
        }

        public void StopAdvertising()
        {
            IsAdvertising = false;
            Close(ref _sendSocket);
        }

        public void StartListening()
        {
            IsListening = true;
            _nextListenOpen = 0;
        }

        public void StopListening()
        {
            IsListening = false;
            Close(ref _listenSocket);
            _sessions.Clear();
            _sessionIds.Clear();
        }

        public void Poll()
        {
            double now = NetTime.Now;
            // Never let discovery break the menu: an unexpected error closes the socket, which is re-opened 2 s later.
            if (IsAdvertising)
            {
                try
                {
                    if (_sendSocket == null && now >= _nextSendOpen) OpenSender(now);
                    if (_sendSocket != null && now >= _nextBeacon) SendBeacon(now);
                }
                catch (Exception e) { NetLog.Warn("LAN advertise: " + e.Message); Close(ref _sendSocket); _nextSendOpen = now + RetryInterval; }
            }
            if (IsListening)
            {
                try
                {
                    if (_listenSocket == null && now >= _nextListenOpen) OpenListener(now);
                    if (_listenSocket != null) Receive(now);
                }
                catch (Exception e) { NetLog.Warn("LAN listen: " + e.Message); Close(ref _listenSocket); _nextListenOpen = now + RetryInterval; }
                for (int i = _sessions.Count - 1; i >= 0; i--)
                    if (now - _sessions[i].LastSeen > SessionLifetime) { _sessions.RemoveAt(i); _sessionIds.RemoveAt(i); }
            }
        }

        public void Dispose() { StopAdvertising(); StopListening(); }

        // ------------------------------------------------------------------ beacon format

        void WriteBeacon(LanSessionInfo info, NetWriter w)
        {
            string name = info.HostName ?? "";
            if (name.Length > MaxHostNameLength) name = name.Substring(0, MaxHostNameLength);
            w.Reset();
            w.WriteUInt(NetProtocol.DiscoveryMagic);
            w.WriteInt(GameInfo.ProtocolVersion);
            w.WriteUInt(_advertiserId);
            w.WriteString(name);
            w.WriteUShort((ushort)(info.Port > 0 && info.Port <= 65535 ? info.Port : GameInfo.DefaultPort));
            w.WriteByte((byte)Math.Max(0, Math.Min(255, info.Players)));
            w.WriteByte((byte)Math.Max(0, Math.Min(255, info.MaxPlayers)));
            w.WriteBool(info.InLobby);
        }

        /// <summary>Parse a beacon into <paramref name="into"/>. Returns false for anything that is not a valid beacon.</summary>
        internal static bool TryReadBeacon(NetReader r, LanSessionInfo into, out uint advertiserId)
        {
            advertiserId = 0;
            try
            {
                if (r.ReadUInt() != NetProtocol.DiscoveryMagic) return false;
                into.Protocol = r.ReadInt();
                advertiserId = r.ReadUInt();
                string name = r.ReadString();
                into.HostName = name.Length > MaxHostNameLength ? name.Substring(0, MaxHostNameLength) : name;
                into.Port = r.ReadUShort();
                into.Players = r.ReadByte();
                into.MaxPlayers = r.ReadByte();
                into.InLobby = r.ReadBool();
                return into.Port != 0;
            }
            catch (NetReadException) { return false; }
        }

        /// <summary>The current beacon bytes (tests).</summary>
        internal byte[] BeaconBytes => _beacon.ToArray();

        // ------------------------------------------------------------------ sockets

        void OpenSender(double now)
        {
            _nextSendOpen = now + RetryInterval;
            Socket s = null;
            try
            {
                s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                s.EnableBroadcast = true;
                s.Blocking = false;
                s.Bind(new IPEndPoint(IPAddress.Any, 0));
                _sendSocket = s;
            }
            catch (Exception)
            {
                if (s != null) s.Close();
            }
        }

        void OpenListener(double now)
        {
            _nextListenOpen = now + RetryInterval;
            Socket s = null;
            try
            {
                s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                try { s.ExclusiveAddressUse = false; } catch { }
                s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                try { s.EnableBroadcast = true; } catch { }
                s.Blocking = false;
                s.Bind(new IPEndPoint(IPAddress.Any, _port));
                _listenSocket = s;
            }
            catch (Exception)
            {
                if (s != null) s.Close();
            }
        }

        static void Close(ref Socket s)
        {
            if (s == null) return;
            try { s.Close(); } catch { }
            s = null;
        }

        void SendBeacon(double now)
        {
            _nextBeacon = now + BeaconInterval;
            _lastBeacon = now;
            if (now >= _nextTargetRefresh) RefreshTargets(now);
            for (int i = 0; i < _targets.Count + ExtraTargets.Count; i++)
            {
                var to = i < _targets.Count ? _targets[i] : ExtraTargets[i - _targets.Count];
                try { _sendSocket.SendTo(_beacon.Buffer, 0, _beacon.Length, SocketFlags.None, to); }
                catch (Exception) { } // no network, no route, firewall...: try again next beacon
            }
        }

        void RefreshTargets(double now)
        {
            _nextTargetRefresh = now + TargetRefreshInterval;
            _targets.Clear();
            AddTarget(IPAddress.Broadcast);
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    var status = ni.OperationalStatus;
                    if (status != OperationalStatus.Up && status != OperationalStatus.Unknown) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        IPAddress mask = null;
                        try { mask = ua.IPv4Mask; } catch { } // not implemented on some platforms
                        if (mask == null) continue;
                        byte[] a = ua.Address.GetAddressBytes(), m = mask.GetAddressBytes();
                        if (a.Length != 4 || m.Length != 4) continue;
                        for (int k = 0; k < 4; k++) a[k] = (byte)(a[k] | ~m[k]);
                        AddTarget(new IPAddress(a));
                    }
                }
            }
            catch (Exception) { } // interface enumeration unsupported: plain broadcast only
            AddTarget(IPAddress.Parse("127.255.255.255"));
        }

        void AddTarget(IPAddress address)
        {
            var ep = new IPEndPoint(address, _port);
            for (int i = 0; i < _targets.Count; i++) if (_targets[i].Equals(ep)) return;
            _targets.Add(ep);
        }

        // ------------------------------------------------------------------ receiving

        readonly LanSessionInfo _scratch = new LanSessionInfo();

        void Receive(double now)
        {
            for (int n = 0; n < 64; n++)
            {
                int length;
                EndPoint from = AnyEndPoint;
                try
                {
                    if (!_listenSocket.Poll(0, SelectMode.SelectRead)) return;
                    length = _listenSocket.ReceiveFrom(_recvBuf, 0, _recvBuf.Length, SocketFlags.None, ref from);
                }
                catch (SocketException e)
                {
                    if (e.SocketErrorCode == SocketError.WouldBlock) return;
                    continue; // ConnectionReset / MessageSize / ...: skip that datagram
                }
                _reader.SetBuffer(_recvBuf, 0, length);
                if (!TryReadBeacon(_reader, _scratch, out uint id) || !(from is IPEndPoint ep)) continue;
                if (IsAdvertising && id == _advertiserId) continue; // our own beacon
                string address = (ep.Address.IsIPv4MappedToIPv6 ? ep.Address.MapToIPv4() : ep.Address).ToString();
                Upsert(id, address, IPAddress.IsLoopback(ep.Address), now);
            }
        }

        void Upsert(uint id, string address, bool loopback, double now)
        {
            int index = -1;
            for (int i = 0; i < _sessions.Count && index < 0; i++)
                if (_sessionIds[i] == id || (_sessions[i].Address == address && _sessions[i].Port == _scratch.Port)) index = i;
            LanSessionInfo s;
            if (index < 0)
            {
                s = new LanSessionInfo { Address = address };
                _sessions.Add(s);
                _sessionIds.Add(id);
            }
            else
            {
                s = _sessions[index];
                _sessionIds[index] = id;
                if (s.Address != address && !loopback && IsLoopbackString(s.Address)) s.Address = address;
            }
            s.HostName = _scratch.HostName;
            s.Port = _scratch.Port;
            s.Players = _scratch.Players;
            s.MaxPlayers = _scratch.MaxPlayers;
            s.InLobby = _scratch.InLobby;
            s.Protocol = _scratch.Protocol;
            s.LastSeen = now;
        }

        static bool IsLoopbackString(string address) => address.StartsWith("127.", StringComparison.Ordinal) || address == "::1";
    }
}
