using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PrisonersOfOmar.Net;

namespace PrisonersOfOmar.NetTests
{
    /// <summary>Tiny test runner: every test is a method that throws on failure. Prints PASS/FAIL, exits 1 on any failure.</summary>
    static class Program
    {
        static bool _verbose;
        static readonly List<string> Notes = new List<string>();
        public static readonly List<string> Warnings = new List<string>();

        static int Main(string[] args)
        {
            var filters = new List<string>();
            foreach (var a in args)
                if (a == "-v") _verbose = true; else filters.Add(a.ToLowerInvariant());

            NetLog.Warning = m => { Warnings.Add(m); if (_verbose) Console.WriteLine("      " + m); };

            var tests = new List<(string, Action)>();
            TransportTests.Register(tests);
            DiscoveryTests.Register(tests);

            int pass = 0, fail = 0, seed = 1;
            var total = Stopwatch.StartNew();
            foreach (var (name, test) in tests)
            {
                seed++;
                if (filters.Count > 0 && !filters.Exists(f => name.ToLowerInvariant().Contains(f))) continue;
                NetSimulation.Reset();
                NetSimulation.SetSeed(seed);
                Warnings.Clear();
                Notes.Clear();
                var sw = Stopwatch.StartNew();
                try
                {
                    test();
                    pass++;
                    Console.WriteLine($"PASS  {name}  ({sw.Elapsed.TotalSeconds:0.0}s)");
                }
                catch (Exception e)
                {
                    fail++;
                    Console.WriteLine($"FAIL  {name}  ({sw.Elapsed.TotalSeconds:0.0}s): {e.Message}");
                    if (!(e is TestFailure)) Console.WriteLine(e);
                }
                finally { NetSimulation.Reset(); }
                foreach (var n in Notes) Console.WriteLine("      note: " + n);
            }
            Console.WriteLine($"---- {pass} passed, {fail} failed ({total.Elapsed.TotalSeconds:0.0}s)");
            return fail == 0 ? 0 : 1;
        }

        public static void Note(string text) => Notes.Add(text);
    }

    sealed class TestFailure : Exception
    {
        public TestFailure(string message) : base(message) { }
    }

    /// <summary>Assertions and Poll-loop helpers shared by the tests.</summary>
    static class T
    {
        public static void Check(bool condition, string message)
        {
            if (!condition) throw new TestFailure(message);
        }

        public static void Equal<TV>(TV expected, TV actual, string what)
        {
            if (!EqualityComparer<TV>.Default.Equals(expected, actual)) throw new TestFailure($"{what}: expected {expected}, got {actual}");
        }

        /// <summary>Poll everything (≈1 ms per iteration) until <paramref name="done"/> or the timeout. Returns done().</summary>
        public static bool Pump(Func<bool> done, double seconds, NetServer server, params NetClient[] clients)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                server?.Poll();
                foreach (var c in clients) c.Poll();
                if (done()) return true;
                if (sw.Elapsed.TotalSeconds > seconds) return false;
                Thread.Sleep(1);
            }
        }

        /// <summary>Poll for a fixed time.</summary>
        public static void PumpFor(double seconds, NetServer server, params NetClient[] clients) => Pump(() => false, seconds, server, clients);

        public static NetServer StartServer(int maxConnections)
        {
            var s = new NetServer(maxConnections);
            Check(s.Start(0), "server failed to start");
            Check(s.Port > 0, "server port not assigned");
            return s;
        }

        public static NetClient Connect(NetServer server, string name = "Player", byte[] payload = null, string host = "127.0.0.1")
        {
            var c = new NetClient();
            bool connected = false;
            DisconnectReason failed = DisconnectReason.None;
            c.Connected += () => connected = true;
            c.Disconnected += r => failed = r;
            c.Connect(host, server.Port, name, payload);
            Check(Pump(() => connected || failed != DisconnectReason.None, 5, server, c), "connect timed out");
            Check(connected, "connect failed: " + failed);
            Equal(ClientState.Connected, c.State, "client state");
            return c;
        }

        /// <summary>Deterministic test message: [int tag][int index][ushort length][bytes f(tag,index)].</summary>
        public static void WriteMessage(NetWriter w, int tag, int index, int length)
        {
            w.Reset();
            w.WriteInt(tag);
            w.WriteInt(index);
            w.WriteUShort((ushort)length);
            for (int i = 0; i < length; i++) w.WriteByte((byte)(index * 31 + tag * 7 + i));
        }

        /// <summary>Read and verify a <see cref="WriteMessage"/> message; returns (tag, index).</summary>
        public static (int tag, int index) ReadMessage(NetReader r)
        {
            int tag = r.ReadInt();
            int index = r.ReadInt();
            int length = r.ReadUShort();
            for (int i = 0; i < length; i++)
                if (r.ReadByte() != (byte)(index * 31 + tag * 7 + i)) throw new TestFailure($"corrupt payload in message {tag}/{index} at byte {i}");
            if (r.Remaining != 0) throw new TestFailure($"message {tag}/{index} has {r.Remaining} trailing bytes");
            return (tag, index);
        }

        // ------------------------------------------------------------ raw UDP peer (to craft packets by hand)

        public static Socket RawSocket()
        {
            var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            s.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            s.Blocking = false;
            return s;
        }

        public static void RawSend(Socket s, int port, byte[] data, int length = -1)
        {
            s.SendTo(data, 0, length < 0 ? data.Length : length, SocketFlags.None, new IPEndPoint(IPAddress.Loopback, port));
        }

        public static byte[] ConnectPacket(uint token, int protocol = GameInfo.ProtocolVersion, string name = "raw")
        {
            var w = new NetWriter();
            NetProtocol.WriteConnect(w, protocol, token, name, null);
            return w.ToArray();
        }

        /// <summary>Poll the server until the raw socket gets a packet of <paramref name="type"/> (other types are skipped).</summary>
        public static byte[] RawReceive(Socket s, NetServer server, byte type, double seconds)
        {
            var buf = new byte[2048];
            byte[] result = null;
            Pump(() =>
            {
                while (result == null && s.Poll(0, SelectMode.SelectRead))
                {
                    EndPoint ep = new IPEndPoint(IPAddress.Any, 0);
                    int n = s.ReceiveFrom(buf, ref ep);
                    if (n > 0 && buf[0] == type) { result = new byte[n]; Buffer.BlockCopy(buf, 0, result, 0, n); }
                }
                return result != null;
            }, seconds, server);
            return result;
        }

        /// <summary>Raw handshake; returns the connection id the server assigned.</summary>
        public static int RawHandshake(Socket s, NetServer server, uint token)
        {
            RawSend(s, server.Port, ConnectPacket(token));
            var accept = RawReceive(s, server, NetProtocol.PktAccept, 2);
            Check(accept != null, "raw handshake: no ACCEPT");
            var r = new NetReader(accept);
            r.ReadByte();
            Equal(token, r.ReadUInt(), "ACCEPT token");
            return r.ReadByte();
        }
    }
}
