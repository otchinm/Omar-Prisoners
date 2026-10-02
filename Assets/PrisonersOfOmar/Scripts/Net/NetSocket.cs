using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// Non-blocking UDP socket used by NetServer / NetClient. Handles platform quirks, swallows non-fatal
    /// socket errors, counts traffic into <see cref="Stats"/> and applies <see cref="NetSimulation"/> on send.
    /// </summary>
    internal sealed class NetSocket
    {
        const int SioUdpConnReset = -1744830452; // SIO_UDP_CONNRESET: stop Windows reporting ICMP port-unreachable as 10054
        const int BufferBytes = 256 * 1024;

        struct DelayedPacket { public double Due; public byte[] Data; public EndPoint To; }

        Socket _socket;
        EndPoint _anyEndPoint;
        readonly List<DelayedPacket> _delayed = new List<DelayedPacket>();
        double _lastSendWarning = double.NegativeInfinity;

        public NetStats Stats;
        /// <summary>Scratch writer for building outgoing packets (main thread only).</summary>
        public readonly NetWriter Writer = new NetWriter(NetProtocol.MaxPacketSize);

        public bool IsOpen => _socket != null;
        public int LocalPort { get; private set; }
        public AddressFamily Family { get; private set; }

        /// <summary>Open and bind. Throws on failure (caller decides how to report). Resets Stats.</summary>
        public void Open(AddressFamily family, int port, bool dualMode)
        {
            Close();
            var s = new Socket(family, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                if (family == AddressFamily.InterNetworkV6) s.DualMode = dualMode;
                s.Blocking = false;
                try { s.ReceiveBufferSize = BufferBytes; s.SendBufferSize = BufferBytes; } catch { }
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    try { s.IOControl(SioUdpConnReset, new byte[] { 0 }, null); } catch { }
                }
                var any = family == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any;
                s.Bind(new IPEndPoint(any, port));
                _anyEndPoint = new IPEndPoint(any, 0);
                LocalPort = ((IPEndPoint)s.LocalEndPoint).Port;
                Family = family;
            }
            catch
            {
                s.Close();
                throw;
            }
            _socket = s;
            Stats = default;
        }

        /// <summary>Send pending simulated packets immediately, then close.</summary>
        public void Close()
        {
            if (_socket == null) return;
            for (int i = 0; i < _delayed.Count; i++) RawSend(_delayed[i].Data, _delayed[i].Data.Length, _delayed[i].To);
            _delayed.Clear();
            try { _socket.Close(); } catch { }
            _socket = null;
        }

        /// <summary>Read one datagram. Returns its length, or -1 when nothing is waiting.</summary>
        public int Receive(byte[] buffer, out EndPoint from)
        {
            from = null;
            for (int attempt = 0; attempt < 32 && _socket != null; attempt++)
            {
                try
                {
                    if (!_socket.Poll(0, SelectMode.SelectRead)) return -1;
                    EndPoint ep = _anyEndPoint;
                    int n = _socket.ReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref ep);
                    from = ep;
                    Stats.PacketsReceived++;
                    Stats.BytesReceived += n;
                    return n;
                }
                catch (SocketException e)
                {
                    switch (e.SocketErrorCode)
                    {
                        case SocketError.WouldBlock:
                        case SocketError.TryAgain:
                        case SocketError.Interrupted:
                            return -1;
                        case SocketError.ConnectionReset:   // ICMP port unreachable (Windows)
                        case SocketError.MessageSize:       // oversized datagram (truncated & consumed)
                        case SocketError.NetworkReset:
                        case SocketError.HostUnreachable:
                        case SocketError.NetworkUnreachable:
                        case SocketError.ConnectionRefused:
                            continue;
                        default:
                            NetLog.Warn("receive error " + e.SocketErrorCode);
                            return -1;
                    }
                }
                catch (ObjectDisposedException) { return -1; }
            }
            return -1;
        }

        public void Send(byte[] data, int length, EndPoint to)
        {
            if (_socket == null) return;
            Stats.PacketsSent++;
            Stats.BytesSent += length;
            if (!NetSimulation.IsActive) { RawSend(data, length, to); return; }

            var rng = NetSimulation.Rng;
            if (rng.NextDouble() * 100.0 < NetSimulation.PacketLossPercent) return;
            int copies = rng.NextDouble() * 100.0 < NetSimulation.DuplicatePercent ? 2 : 1;
            for (int c = 0; c < copies; c++)
            {
                double delay = (NetSimulation.LatencyMs + rng.NextDouble() * NetSimulation.JitterMs) * 0.001;
                if (delay <= 0) { RawSend(data, length, to); continue; }
                var copy = new byte[length];
                Buffer.BlockCopy(data, 0, copy, 0, length);
                _delayed.Add(new DelayedPacket { Due = NetTime.Now + delay, Data = copy, To = to });
            }
        }

        /// <summary>Send simulated-latency packets that are due.</summary>
        public void Pump(double now)
        {
            for (int i = 0; i < _delayed.Count; i++)
            {
                if (_delayed[i].Due > now) continue;
                RawSend(_delayed[i].Data, _delayed[i].Data.Length, _delayed[i].To);
                _delayed.RemoveAt(i--);
            }
        }

        void RawSend(byte[] data, int length, EndPoint to)
        {
            if (_socket == null) return;
            try
            {
                _socket.SendTo(data, 0, length, SocketFlags.None, to);
            }
            catch (SocketException e)
            {
                // Full send buffer or a transient routing error: the packet is simply lost (reliable layer resends).
                if (e.SocketErrorCode == SocketError.WouldBlock || e.SocketErrorCode == SocketError.NoBufferSpaceAvailable) return;
                double now = NetTime.Now;
                if (now - _lastSendWarning > 5.0) { _lastSendWarning = now; NetLog.Warn("send to " + to + " failed: " + e.SocketErrorCode); }
            }
            catch (ObjectDisposedException) { }
        }
    }

    /// <summary>Pool of FragmentSize byte arrays for queued reliable messages (avoids per-message garbage).</summary>
    internal sealed class BufferPool
    {
        readonly Stack<byte[]> _free = new Stack<byte[]>();

        public byte[] Rent() => _free.Count > 0 ? _free.Pop() : new byte[NetProtocol.FragmentSize];

        public void Return(byte[] b)
        {
            if (b != null && b.Length == NetProtocol.FragmentSize && _free.Count < 4096) _free.Push(b);
        }
    }
}
