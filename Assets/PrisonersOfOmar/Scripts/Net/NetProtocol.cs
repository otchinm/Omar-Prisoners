using System;
using System.Diagnostics;

namespace PrisonersOfOmar.Net
{
    /// <summary>
    /// Wire protocol constants and limits.
    /// <para>Datagram layouts (little endian):</para>
    /// <code>
    /// CONNECT    [1][magic u32][protocol i32][token u32][name str][payload bytes]   (prefix up to token is frozen forever)
    /// ACCEPT     [2][token u32][connectionId u8]
    /// REJECT     [3][token u32][reason u8]
    /// DISCONNECT [4][token u32][reason u8]                                           (sent 3x, unreliable)
    /// DATA       [5][token u32][ackNext u16][n u8][n x selectiveAck u16] { entry }*
    ///   entry:   [1 Unreliable][len u16][bytes]
    ///            [2 Reliable | 3 ReliableMoreFragments][seq u16][len u16][bytes]
    ///            [4 UnreliableFragment][group u16][index u8][count u8][len u16][bytes]
    ///            [5 Ping][timeMs u32]  /  [6 Pong][echoed timeMs u32]
    /// </code>
    /// The token is a random number chosen by the client per connection attempt; every packet after CONNECT
    /// carries it, so stale or spoofed packets from the same endpoint are ignored.
    /// </summary>
    public static class NetProtocol
    {
        /// <summary>Largest UDP payload the transport ever sends (safe below every common path MTU).</summary>
        public const int MaxPacketSize = 1200;
        /// <summary>Messages up to this many bytes travel in one piece; bigger ones are fragmented.</summary>
        public const int FragmentSize = 1100;
        /// <summary>Largest message accepted by Send on either channel (64 KB).</summary>
        public const int MaxMessageSize = 64 * 1024;
        /// <summary>Largest connect payload accepted by NetClient.Connect.</summary>
        public const int MaxConnectPayload = 1024;
        /// <summary>Longest player name sent in CONNECT (longer names are truncated).</summary>
        public const int MaxPlayerNameLength = 64;
        /// <summary>If more reliable message fragments than this wait for an ack, the peer is dropped (Timeout).</summary>
        public const int MaxPendingReliable = 2048;
        /// <summary>Seconds between PINGs (keepalive + RTT).</summary>
        public const float PingInterval = 0.5f;
        /// <summary>Default seconds without any packet before a peer is considered gone.</summary>
        public const float DefaultTimeout = 10f;
        /// <summary>Seconds between CONNECT retries while connecting.</summary>
        public const float ConnectRetryInterval = 0.5f;
        /// <summary>Minimum delay before an unacked reliable message is resent (actual: max(this, 1.5 * RTT), doubled for the 2nd and 4x for later resends).</summary>
        public const float MinResendDelay = 0.1f;

        internal const uint Magic = 0x524D4F50;          // "POMR"
        internal const uint DiscoveryMagic = 0x444D4F50; // "POMD"

        internal const byte PktConnect = 1, PktAccept = 2, PktReject = 3, PktDisconnect = 4, PktData = 5;
        internal const byte EntUnreliable = 1, EntReliable = 2, EntReliableMore = 3, EntUnreliableFrag = 4, EntPing = 5, EntPong = 6;

        /// <summary>DATA header: type + token + ackNext + count.</summary>
        internal const int DataHeaderSize = 1 + 4 + 2 + 1;
        internal const int MaxSelectiveAcksPerPacket = 32;
        /// <summary>Unreliable fragment groups have at most this many parts (64 x 1100 B >= 64 KB).</summary>
        internal const int MaxFragments = 64;
        /// <summary>Receive buffer size; anything bigger than MaxPacketSize is dropped as garbage.</summary>
        internal const int ReceiveBufferSize = 2048;

        internal static void WriteConnect(NetWriter w, int protocol, uint token, string name, byte[] payload)
        {
            w.Reset();
            w.WriteByte(PktConnect);
            w.WriteUInt(Magic);
            w.WriteInt(protocol);
            w.WriteUInt(token);
            w.WriteString(name);
            w.WriteByteArray(payload);
        }

        /// <summary>ACCEPT / REJECT / DISCONNECT all share the layout [type][token][byte].</summary>
        internal static void WriteControl(NetWriter w, byte type, uint token, byte value)
        {
            w.Reset();
            w.WriteByte(type);
            w.WriteUInt(token);
            w.WriteByte(value);
        }

        static readonly Random TokenRng = new Random(Environment.TickCount ^ (int)Stopwatch.GetTimestamp() ^ 0x5F3759DF);

        /// <summary>Random non-zero 32-bit value (connection tokens, beacon ids). Not used for gameplay.</summary>
        internal static uint NewToken()
        {
            uint t;
            do t = (uint)TokenRng.Next() ^ ((uint)TokenRng.Next() << 16); while (t == 0);
            return t;
        }

        internal static DisconnectReason ToReason(byte b, DisconnectReason fallback)
        {
            return b == 0 || b > (byte)DisconnectReason.BadData ? fallback : (DisconnectReason)b;
        }
    }

    /// <summary>Monotonic real time in seconds (Stopwatch based, independent of UnityEngine.Time).</summary>
    public static class NetTime
    {
        static readonly long Start = Stopwatch.GetTimestamp();
        static readonly double InvFrequency = 1.0 / Stopwatch.Frequency;

        public static double Now => (Stopwatch.GetTimestamp() - Start) * InvFrequency;
    }

    /// <summary>Logging hook so the transport stays UnityEngine-free. The game sets <c>NetLog.Warning = Debug.LogWarning</c>.</summary>
    public static class NetLog
    {
        /// <summary>Warning sink (default: Console). Set to null to silence.</summary>
        public static Action<string> Warning = DefaultWarning;

        static void DefaultWarning(string message)
        {
            try { Console.WriteLine(message); } catch { }
        }

        internal static void Warn(string message)
        {
            var w = Warning;
            if (w == null) return;
            try { w("[Net] " + message); } catch { }
        }
    }

    /// <summary>
    /// Network condition simulator for tests / debugging. Applied on SEND by every NetServer / NetClient socket
    /// (not by LanDiscovery). All defaults are 0 = off. Main-thread only.
    /// </summary>
    public static class NetSimulation
    {
        /// <summary>Percentage (0..100) of outgoing datagrams silently dropped.</summary>
        public static float PacketLossPercent;
        /// <summary>Percentage (0..100) of outgoing datagrams sent twice.</summary>
        public static float DuplicatePercent;
        /// <summary>Fixed delay added to every outgoing datagram, in milliseconds.</summary>
        public static float LatencyMs;
        /// <summary>Extra random delay 0..JitterMs per datagram (causes reordering), in milliseconds.</summary>
        public static float JitterMs;

        internal static Random Rng = new Random(1234);

        public static bool IsActive => PacketLossPercent > 0f || DuplicatePercent > 0f || LatencyMs > 0f || JitterMs > 0f;

        public static void Reset() { PacketLossPercent = DuplicatePercent = LatencyMs = JitterMs = 0f; }
        public static void SetSeed(int seed) { Rng = new Random(seed); }
    }
}
