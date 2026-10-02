using System;
using System.Net;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// One established peer: reliable/unreliable channels, acks, fragmentation, ping/RTT.
    /// Shared by NetServer (one per client) and NetClient (one for the server).
    /// <para>Reliable channel: every message (or fragment of a large message) gets a 16-bit sequence number.
    /// The sender keeps it until acked and resends it after max(0.1 s, 1.5 * RTT) (x2, x4 for later attempts).
    /// Each DATA packet carries a cumulative ack ("everything before ackNext arrived") plus selective acks for
    /// messages received ahead of a gap. The receiver buffers early messages and delivers strictly in order,
    /// exactly once.
    /// Fragments of a large reliable message are consecutive sequence numbers flagged "more follows",
    /// so in-order delivery reassembles them for free.</para>
    /// <para>Unreliable channel: messages are staged and packed into the next flush; large ones are split into
    /// a fragment group that is delivered only if every part arrives within 1 s.</para>
    /// </summary>
    internal sealed class NetConnection
    {
        public delegate void MessageHandler(NetConnection connection, byte[] buffer, int offset, int length, NetChannel channel);

        const int RingSize = 4096, RingMask = RingSize - 1; // > MaxPendingReliable: sequence windows never overlap
        const int MaxQueuedSelectiveAcks = 512;
        const int MaxPacketsPerFlush = 128;                  // ~150 KB burst cap per peer per flush (reliable data)
        const int MaxStagedUnreliable = 2 * NetProtocol.MaxMessageSize;
        const int FragmentSlots = 4;
        const double FragmentTimeout = 1.0;
        const int BadPacketLimit = 10;
        const double BadPacketWindow = 5.0;

        struct OutSlot { public byte[] Buf; public int Len; public bool More, Used; public int Sends; public double LastSend; }
        struct InSlot { public byte[] Buf; public int Len; public bool More, Used; }
        struct FragmentGroup { public bool Active; public ushort Group; public int Count, TotalLen; public ulong Mask; public double Started; public byte[] Buf; }

        readonly NetSocket _socket;
        readonly BufferPool _pool;
        readonly MessageHandler _onMessage;

        public readonly IPEndPoint EndPoint;
        public readonly uint Token;
        public int Id;
        public double LastReceive;
        public bool Closed;
        /// <summary>Set when the connection must be dropped (overflow, bad data); the owner acts on it in Poll.</summary>
        public DisconnectReason Fault;

        // reliable send
        readonly OutSlot[] _out = new OutSlot[RingSize];
        ushort _sendSeq, _oldest;
        // reliable receive
        readonly InSlot[] _in = new InSlot[RingSize];
        ushort _recvNext;
        byte[] _assembly;
        int _assemblyLen;
        bool _assembling;
        // acks to send
        readonly ushort[] _selAcks = new ushort[MaxQueuedSelectiveAcks];
        int _selCount;
        bool _ackDirty;
        // unreliable send staging: [entry length u16][entry bytes in wire format]...
        byte[] _staged = new byte[2048];
        int _stagedLen;
        ushort _fragGroup;
        readonly FragmentGroup[] _frags = new FragmentGroup[FragmentSlots];
        // ping / rtt
        double _nextPing;
        bool _pongDue, _hasRtt;
        uint _pongStamp;
        float _rtt;
        // garbage accounting
        int _badCount;
        double _badWindowStart = double.NegativeInfinity;

        public NetConnection(NetSocket socket, BufferPool pool, MessageHandler onMessage, IPEndPoint endPoint, uint token, double now)
        {
            _socket = socket;
            _pool = pool;
            _onMessage = onMessage;
            EndPoint = endPoint;
            Token = token;
            LastReceive = now;
            _nextPing = now; // ping right away: RTT is known after the first round trip
        }

        /// <summary>Smoothed round trip time in seconds (0 until the first PONG).</summary>
        public float Rtt => _hasRtt ? _rtt : 0f;
        /// <summary>Reliable fragments sent but not yet acknowledged.</summary>
        public int PendingReliable => (ushort)(_sendSeq - _oldest);

        // ------------------------------------------------------------------ sending

        public void Send(byte[] data, int offset, int length, NetChannel channel)
        {
            if (Closed || Fault != DisconnectReason.None) return;
            if (channel == NetChannel.Unreliable) StageUnreliable(data, offset, length);
            else QueueReliable(data, offset, length);
        }

        void QueueReliable(byte[] data, int offset, int length)
        {
            int chunks = length <= NetProtocol.FragmentSize ? 1 : (length + NetProtocol.FragmentSize - 1) / NetProtocol.FragmentSize;
            if (PendingReliable + chunks > NetProtocol.MaxPendingReliable)
            {
                Fault = DisconnectReason.Timeout; // peer is not acking: drop it instead of growing without bound
                return;
            }
            for (int i = 0; i < chunks; i++)
            {
                int off = i * NetProtocol.FragmentSize;
                int len = Math.Min(NetProtocol.FragmentSize, length - off);
                ref OutSlot s = ref _out[_sendSeq & RingMask];
                s.Buf = _pool.Rent();
                Buffer.BlockCopy(data, offset + off, s.Buf, 0, len);
                s.Len = len;
                s.More = i < chunks - 1;
                s.Used = true;
                s.Sends = 0;
                s.LastSend = 0;
                _sendSeq++;
            }
        }

        void StageUnreliable(byte[] data, int offset, int length)
        {
            if (length <= NetProtocol.FragmentSize)
            {
                if (!ReserveStage(3 + length)) return;
                StageByte(NetProtocol.EntUnreliable);
                StageUShort((ushort)length);
                StageBytes(data, offset, length);
                return;
            }
            int count = (length + NetProtocol.FragmentSize - 1) / NetProtocol.FragmentSize;
            if (_stagedLen + count * (2 + 7) + length > MaxStagedUnreliable) return; // drop whole message
            ushort group = _fragGroup++;
            for (int i = 0; i < count; i++)
            {
                int off = i * NetProtocol.FragmentSize;
                int len = Math.Min(NetProtocol.FragmentSize, length - off);
                ReserveStage(7 + len);
                StageByte(NetProtocol.EntUnreliableFrag);
                StageUShort(group);
                StageByte((byte)i);
                StageByte((byte)count);
                StageUShort((ushort)len);
                StageBytes(data, offset + off, len);
            }
        }

        /// <summary>Start a staged entry of <paramref name="entryLength"/> bytes (writes its length prefix).</summary>
        bool ReserveStage(int entryLength)
        {
            if (_stagedLen + 2 + entryLength > MaxStagedUnreliable) return false; // too much unsent unreliable data: drop
            if (_stagedLen + 2 + entryLength > _staged.Length)
            {
                int n = _staged.Length * 2;
                while (n < _stagedLen + 2 + entryLength) n *= 2;
                Array.Resize(ref _staged, n);
            }
            StageUShort((ushort)entryLength);
            return true;
        }

        void StageByte(byte v) { _staged[_stagedLen++] = v; }
        void StageUShort(ushort v) { _staged[_stagedLen++] = (byte)v; _staged[_stagedLen++] = (byte)(v >> 8); }
        void StageBytes(byte[] src, int offset, int count) { Buffer.BlockCopy(src, offset, _staged, _stagedLen, count); _stagedLen += count; }

        // ------------------------------------------------------------------ flushing

        NetWriter _pkt;
        bool _pktOpen;
        int _pktCount;

        /// <summary>Write pings, acks, due reliable messages and staged unreliable messages into datagrams.</summary>
        public void Flush(double now)
        {
            if (Closed) return;
            _pkt = _socket.Writer;
            _pktOpen = false;
            _pktCount = 0;

            if (now >= _nextPing)
            {
                _nextPing = now + NetProtocol.PingInterval;
                Room(5);
                _pkt.WriteByte(NetProtocol.EntPing);
                _pkt.WriteUInt(Millis(now));
            }
            if (_pongDue)
            {
                _pongDue = false;
                Room(5);
                _pkt.WriteByte(NetProtocol.EntPong);
                _pkt.WriteUInt(_pongStamp);
            }

            // Resend after max(0.1 s, 1.5 RTT), doubling per attempt up to 4x so a big burst on a slow link
            // does not turn into a retransmission storm.
            double resendDelay = Math.Max(NetProtocol.MinResendDelay, 1.5 * Rtt);
            for (ushort seq = _oldest; seq != _sendSeq; seq++)
            {
                ref OutSlot s = ref _out[seq & RingMask];
                if (!s.Used || (s.Sends > 0 && now - s.LastSend < resendDelay * (1 << Math.Min(s.Sends - 1, 2)))) continue;
                if (!Room(5 + s.Len, true)) break; // burst cap reached: the rest goes next flush
                _pkt.WriteByte(s.More ? NetProtocol.EntReliableMore : NetProtocol.EntReliable);
                _pkt.WriteUShort(seq);
                _pkt.WriteUShort((ushort)s.Len);
                _pkt.WriteBytes(s.Buf, 0, s.Len);
                if (s.Sends > 0) _socket.Stats.Resends++;
                s.Sends++;
                s.LastSend = now;
            }

            for (int p = 0; p < _stagedLen;)
            {
                int len = _staged[p] | (_staged[p + 1] << 8);
                Room(len);
                _pkt.WriteBytes(_staged, p + 2, len);
                p += 2 + len;
            }
            _stagedLen = 0;

            if (_pktOpen) SendPacket();
            else if (_ackDirty || _selCount > 0) { BeginPacket(); SendPacket(); }
            while (_selCount > 0) { BeginPacket(); SendPacket(); } // leftover selective acks
            _pkt = null;
        }

        /// <summary>Make sure the open packet has room for <paramref name="bytes"/> more (starting a new one if needed).</summary>
        bool Room(int bytes, bool capped = false)
        {
            if (_pktOpen && _pkt.Length + bytes <= NetProtocol.MaxPacketSize) return true;
            if (capped && _pktCount >= MaxPacketsPerFlush) return false;
            if (_pktOpen) SendPacket();
            BeginPacket();
            return true;
        }

        void BeginPacket()
        {
            _pkt.Reset();
            _pkt.WriteByte(NetProtocol.PktData);
            _pkt.WriteUInt(Token);
            _pkt.WriteUShort(_recvNext);
            int n = Math.Min(_selCount, NetProtocol.MaxSelectiveAcksPerPacket);
            _pkt.WriteByte((byte)n);
            for (int i = 0; i < n; i++) _pkt.WriteUShort(_selAcks[--_selCount]);
            _ackDirty = false;
            _pktOpen = true;
        }

        void SendPacket()
        {
            _socket.Send(_pkt.Buffer, _pkt.Length, EndPoint);
            _pktOpen = false;
            _pktCount++;
        }

        static uint Millis(double t) => (uint)(long)(t * 1000.0);

        // ------------------------------------------------------------------ receiving

        /// <summary>Handle a DATA packet; <paramref name="r"/> is positioned after the token. Throws NetReadException on garbage.</summary>
        public void ProcessData(NetReader r, double now)
        {
            ushort ackNext = r.ReadUShort();
            int sel = r.ReadByte();
            if (sel > NetProtocol.MaxSelectiveAcksPerPacket) throw new NetReadException("too many acks");
            AckUpTo(ackNext);
            for (int i = 0; i < sel; i++) AckOne(r.ReadUShort());
            while (_oldest != _sendSeq && !_out[_oldest & RingMask].Used) _oldest++;

            byte[] buf = r.Data;
            while (r.Remaining > 0)
            {
                if (Closed || Fault != DisconnectReason.None) return; // a handler disconnected us
                byte kind = r.ReadByte();
                switch (kind)
                {
                    case NetProtocol.EntUnreliable:
                    {
                        int len = r.ReadUShort();
                        int p = r.Position;
                        r.Skip(len);
                        _onMessage(this, buf, p, len, NetChannel.Unreliable);
                        break;
                    }
                    case NetProtocol.EntReliable:
                    case NetProtocol.EntReliableMore:
                    {
                        ushort seq = r.ReadUShort();
                        int len = r.ReadUShort();
                        int p = r.Position;
                        r.Skip(len);
                        if (len > NetProtocol.FragmentSize) throw new NetReadException("reliable chunk too big");
                        OnReliable(seq, buf, p, len, kind == NetProtocol.EntReliableMore);
                        break;
                    }
                    case NetProtocol.EntUnreliableFrag:
                    {
                        ushort group = r.ReadUShort();
                        int index = r.ReadByte();
                        int count = r.ReadByte();
                        int len = r.ReadUShort();
                        int p = r.Position;
                        r.Skip(len);
                        OnFragment(group, index, count, buf, p, len, now);
                        break;
                    }
                    case NetProtocol.EntPing:
                        _pongStamp = r.ReadUInt();
                        _pongDue = true;
                        break;
                    case NetProtocol.EntPong:
                        OnPong(r.ReadUInt(), now);
                        break;
                    default:
                        throw new NetReadException("unknown entry");
                }
            }
        }

        void AckUpTo(ushort ackNext)
        {
            if ((short)(ackNext - _sendSeq) > 0) return; // acks something never sent: ignore
            while (_oldest != _sendSeq && (short)(_oldest - ackNext) < 0)
            {
                Release(_oldest);
                _oldest++;
            }
        }

        void AckOne(ushort seq)
        {
            if ((short)(seq - _oldest) < 0 || (short)(seq - _sendSeq) >= 0) return;
            Release(seq);
        }

        void Release(ushort seq)
        {
            ref OutSlot s = ref _out[seq & RingMask];
            if (!s.Used) return;
            _pool.Return(s.Buf);
            s.Buf = null;
            s.Used = false;
        }

        void OnReliable(ushort seq, byte[] buf, int offset, int length, bool more)
        {
            _ackDirty = true;
            int d = (short)(seq - _recvNext);
            if (d < 0) return; // already delivered: the cumulative ack tells the sender
            if (d >= RingSize) throw new NetReadException("reliable seq outside window");
            if (d > 0)
            {
                ref InSlot s = ref _in[seq & RingMask];
                if (!s.Used)
                {
                    s.Buf = _pool.Rent();
                    Buffer.BlockCopy(buf, offset, s.Buf, 0, length);
                    s.Len = length;
                    s.More = more;
                    s.Used = true;
                }
                if (_selCount < MaxQueuedSelectiveAcks) _selAcks[_selCount++] = seq;
                return;
            }

            _recvNext++;
            DeliverReliable(buf, offset, length, more);
            while (!Closed && Fault == DisconnectReason.None)
            {
                ref InSlot s = ref _in[_recvNext & RingMask];
                if (!s.Used) break;
                byte[] b = s.Buf;
                int len = s.Len;
                bool m = s.More;
                s.Buf = null;
                s.Used = false;
                _recvNext++;
                DeliverReliable(b, 0, len, m);
                _pool.Return(b);
            }
        }

        void DeliverReliable(byte[] buf, int offset, int length, bool more)
        {
            if (!_assembling && !more)
            {
                _onMessage(this, buf, offset, length, NetChannel.Reliable);
                return;
            }
            if (_assemblyLen + length > NetProtocol.MaxMessageSize) { Fault = DisconnectReason.BadData; return; }
            if (_assembly == null || _assembly.Length < _assemblyLen + length)
            {
                int n = _assembly == null ? 8192 : _assembly.Length * 2;
                while (n < _assemblyLen + length) n *= 2;
                Array.Resize(ref _assembly, Math.Min(n, NetProtocol.MaxMessageSize));
            }
            Buffer.BlockCopy(buf, offset, _assembly, _assemblyLen, length);
            _assemblyLen += length;
            _assembling = more;
            if (more) return;
            int total = _assemblyLen;
            _assemblyLen = 0;
            _onMessage(this, _assembly, 0, total, NetChannel.Reliable);
        }

        void OnFragment(ushort group, int index, int count, byte[] buf, int offset, int length, double now)
        {
            const int F = NetProtocol.FragmentSize;
            if (count < 2 || count > NetProtocol.MaxFragments || index >= count) throw new NetReadException("bad fragment header");
            bool last = index == count - 1;
            if (last ? (length == 0 || length > F) : length != F) throw new NetReadException("bad fragment size");

            int slot = -1;
            for (int i = 0; i < FragmentSlots; i++)
            {
                if (_frags[i].Active && now - _frags[i].Started > FragmentTimeout) _frags[i].Active = false;
                if (_frags[i].Active && _frags[i].Group == group && _frags[i].Count == count) { slot = i; break; }
            }
            if (slot < 0)
            {
                slot = 0; // first free slot, else evict the oldest group
                for (int i = 0; i < FragmentSlots; i++)
                {
                    if (!_frags[i].Active) { slot = i; break; }
                    if (_frags[i].Started < _frags[slot].Started) slot = i;
                }
                ref FragmentGroup g0 = ref _frags[slot];
                g0.Active = true;
                g0.Group = group;
                g0.Count = count;
                g0.Mask = 0;
                g0.TotalLen = 0;
                g0.Started = now;
                if (g0.Buf == null || g0.Buf.Length < count * F) g0.Buf = new byte[count * F];
            }
            ref FragmentGroup g = ref _frags[slot];
            ulong bit = 1UL << index;
            if ((g.Mask & bit) != 0) return; // duplicate
            g.Mask |= bit;
            Buffer.BlockCopy(buf, offset, g.Buf, index * F, length);
            if (last) g.TotalLen = index * F + length;
            ulong full = count == 64 ? ulong.MaxValue : (1UL << count) - 1;
            if (g.Mask != full) return;
            g.Active = false;
            _onMessage(this, g.Buf, 0, g.TotalLen, NetChannel.Unreliable);
        }

        void OnPong(uint stamp, double now)
        {
            uint ms = Millis(now) - stamp;
            if (ms > 10000) return; // bogus or ancient
            float sample = ms * 0.001f;
            if (!_hasRtt) { _rtt = sample; _hasRtt = true; }
            else _rtt += (sample - _rtt) * 0.25f; // pings come at 2 Hz: adapt within a few seconds
        }

        /// <summary>Count a malformed packet; too many in a short window marks the connection BadData.</summary>
        public void ReportBadPacket(double now)
        {
            if (now - _badWindowStart > BadPacketWindow) { _badWindowStart = now; _badCount = 0; }
            if (++_badCount >= BadPacketLimit && Fault == DisconnectReason.None) Fault = DisconnectReason.BadData;
        }

        /// <summary>Release pooled buffers. Safe to call more than once.</summary>
        public void Close()
        {
            if (Closed) return;
            Closed = true;
            for (int i = 0; i < RingSize; i++)
            {
                if (_out[i].Used) { _pool.Return(_out[i].Buf); _out[i] = default; }
                if (_in[i].Used) { _pool.Return(_in[i].Buf); _in[i] = default; }
            }
            _stagedLen = 0;
            _selCount = 0;
        }
    }
}
