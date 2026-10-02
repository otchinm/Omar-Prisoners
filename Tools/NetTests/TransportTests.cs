using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using PrisonersOfOmar.Net;
using static PrisonersOfOmar.NetTests.T;

namespace PrisonersOfOmar.NetTests
{
    static class TransportTests
    {
        public static void Register(List<(string, Action)> t)
        {
            t.Add(("handshake", Handshake));
            t.Add(("hostname resolve (localhost)", HostnameResolve));
            t.Add(("ipv6 dual-mode", DualMode));
            t.Add(("version mismatch reject", VersionMismatch));
            t.Add(("server full reject + custom Approve", ServerFull));
            t.Add(("duplicate CONNECT re-sends ACCEPT", DuplicateConnect));
            t.Add(("connect failure (no server / bad host)", ConnectFailure));
            t.Add(("reliable order + exactly-once under 30% loss, jitter, dups", ReliableUnderLoss));
            t.Add(("large reliable message fragmentation (40 KB, 64 KB)", LargeMessages));
            t.Add(("unreliable delivery (+ fragmented)", UnreliableDelivery));
            t.Add(("explicit Flush without Poll", ExplicitFlush));
            t.Add(("RTT measurement", RttMeasurement));
            t.Add(("client timeout when server stops polling", ClientTimeout));
            t.Add(("server timeout when client stops polling", ServerTimeout));
            t.Add(("graceful disconnect both directions + Stop", GracefulDisconnect));
            t.Add(("reconnect after disconnect", Reconnect));
            t.Add(("4 clients relaying broadcasts", FourClients));
            t.Add(("pending reliable overflow -> Timeout", PendingOverflow));
            t.Add(("sequence wraparound (70k messages, lossy across 65535)", SequenceWraparound));
            t.Add(("garbage / fuzzed packets never throw", Garbage));
            t.Add(("client drops a server sending garbage (BadData)", ClientBadData));
            t.Add(("event handler exceptions are caught + logged", HandlerExceptions));
        }

        static void Handshake()
        {
            var server = StartServer(4);
            NetClient client = null;
            try
            {
                int connectedId = -1;
                ConnectRequest request = default;
                server.ClientConnected += (id, req) => { connectedId = id; request = req; };
                client = Connect(server, "Alice", new byte[] { 1, 2, 3 });
                Check(connectedId > 0 && connectedId < 255, "ClientConnected not raised with a valid id");
                Equal(connectedId, client.ConnectionId, "client ConnectionId");
                Equal("Alice", request.PlayerName, "player name");
                Equal(3, request.Payload.Length, "payload length");
                Check(request.Payload[0] == 1 && request.Payload[2] == 3, "payload bytes");
                Equal(GameInfo.ProtocolVersion, request.ProtocolVersion, "protocol");
                Equal("127.0.0.1", request.EndPoint.Address.ToString(), "request endpoint");
                Equal(1, server.Connections.Count, "server connection count");
                Equal(connectedId, server.Connections[0], "server connection id");
                Equal("127.0.0.1", server.GetAddress(connectedId), "GetAddress");
                Equal("", server.GetAddress(999), "GetAddress(unknown)");
                PumpFor(0.2, server, client);
                Check(server.Stats.PacketsReceived > 0 && server.Stats.BytesSent > 0, "server stats not counted");
                Check(client.Stats.PacketsSent > 0 && client.Stats.BytesReceived > 0, "client stats not counted");

                // empty name / null payload
                var c2 = Connect(server, null, null);
                Check(server.Connections.Count == 2, "second client");
                c2.Dispose();
            }
            finally { client?.Dispose(); server.Stop(); }
        }

        static void HostnameResolve()
        {
            var server = StartServer(4);
            try
            {
                using var c = Connect(server, "Host", null, "localhost");
                Check(c.ServerEndPoint != null, "resolved endpoint");
            }
            finally { server.Stop(); }
        }

        static void DualMode()
        {
            var server = StartServer(4);
            try
            {
                if (!Socket.OSSupportsIPv6)
                {
                    Check(!server.IsDualMode, "dual mode without IPv6 support");
                    Program.Note("IPv6 unavailable in this environment: server fell back to IPv4 (verified); ::1 connect not tested");
                    using var c4 = Connect(server, "v4");
                    return;
                }
                using var a = Connect(server, "v4", null, "127.0.0.1");
                using var b = Connect(server, "v6", null, "::1");
                Equal("127.0.0.1", server.GetAddress(a.ConnectionId), "IPv4 client address (mapped back)");
                Program.Note("dual mode = " + server.IsDualMode);
            }
            finally { server.Stop(); }
        }

        static void VersionMismatch()
        {
            var server = StartServer(4);
            int approveCalls = 0, connects = 0;
            server.Approve = r => { approveCalls++; return DisconnectReason.None; };
            server.ClientConnected += (id, r) => connects++;
            var c = new NetClient { ProtocolVersionOverride = GameInfo.ProtocolVersion + 1 };
            DisconnectReason reason = DisconnectReason.None;
            c.Disconnected += r => reason = r;
            try
            {
                c.Connect("127.0.0.1", server.Port, "Old");
                Check(Pump(() => reason != DisconnectReason.None, 5, server, c), "no rejection");
                Equal(DisconnectReason.VersionMismatch, reason, "reason");
                Equal(0, approveCalls, "Approve calls");
                Equal(0, connects, "ClientConnected calls");
                Equal(0, server.Connections.Count, "connections");
                Equal(ClientState.Disconnected, c.State, "client state");
            }
            finally { c.Dispose(); server.Stop(); }
        }

        static void ServerFull()
        {
            var server = StartServer(1);
            var server2 = StartServer(4);
            var clients = new List<NetClient>();
            try
            {
                clients.Add(Connect(server, "First"));
                var c = new NetClient();
                clients.Add(c);
                DisconnectReason reason = DisconnectReason.None;
                c.Disconnected += r => reason = r;
                c.Connect("127.0.0.1", server.Port, "Second");
                Check(Pump(() => reason != DisconnectReason.None, 5, server, c), "no rejection");
                Equal(DisconnectReason.ServerFull, reason, "reason");
                Equal(1, server.Connections.Count, "connections");

                ConnectRequest seen = default;
                server2.Approve = r => { seen = r; return r.PlayerName == "Late" ? DisconnectReason.MatchInProgress : DisconnectReason.None; };
                var late = new NetClient();
                clients.Add(late);
                DisconnectReason lateReason = DisconnectReason.None;
                late.Disconnected += r => lateReason = r;
                late.Connect("127.0.0.1", server2.Port, "Late", new byte[] { 42 });
                Check(Pump(() => lateReason != DisconnectReason.None, 5, server2, late), "no rejection from Approve");
                Equal(DisconnectReason.MatchInProgress, lateReason, "Approve reason");
                Equal("Late", seen.PlayerName, "Approve saw name");
                Equal(42, (int)seen.Payload[0], "Approve saw payload");
                clients.Add(Connect(server2, "OnTime"));
                Equal(1, server2.Connections.Count, "approved connection");
            }
            finally { foreach (var c in clients) c.Dispose(); server.Stop(); server2.Stop(); }
        }

        static void DuplicateConnect()
        {
            var server = StartServer(4);
            var raw = RawSocket();
            try
            {
                int connects = 0, disconnects = 0;
                DisconnectReason lastReason = DisconnectReason.None;
                server.ClientConnected += (id, r) => connects++;
                server.ClientDisconnected += (id, r) => { disconnects++; lastReason = r; };

                int id1 = RawHandshake(raw, server, 0x1111);
                int id2 = RawHandshake(raw, server, 0x1111); // "ACCEPT was lost": same token again
                Equal(id1, id2, "re-sent ACCEPT id");
                Equal(1, connects, "ClientConnected count");
                Equal(1, server.Connections.Count, "connections");

                // a new token from the same endpoint while the old session is fresh is ignored...
                RawSend(raw, server.Port, ConnectPacket(0x2222));
                Check(RawReceive(raw, server, NetProtocol.PktAccept, 0.3) == null, "fresh session was replaced");
                // ...but once the old session went quiet (client restarted on the same port) it is replaced
                PumpFor(1.1, server);
                int id3 = RawHandshake(raw, server, 0x3333);
                Check(id3 != id1, "replacement got a new id");
                Equal(1, disconnects, "old session disconnect event");
                Equal(DisconnectReason.Timeout, lastReason, "old session reason");
                Equal(1, server.Connections.Count, "connections after replace");
            }
            finally { raw.Close(); server.Stop(); }
        }

        static void ConnectFailure()
        {
            // nothing listening on that port
            var probe = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            probe.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            int deadPort = ((IPEndPoint)probe.LocalEndPoint).Port;
            probe.Close();

            var c = new NetClient { ConnectTimeoutSeconds = 1.5f };
            DisconnectReason reason = DisconnectReason.None;
            int events = 0;
            c.Disconnected += r => { reason = r; events++; };
            var sw = Stopwatch.StartNew();
            c.Connect("127.0.0.1", deadPort, "Nobody");
            Equal(ClientState.Connecting, c.State, "state while connecting");
            Check(Pump(() => reason != DisconnectReason.None, 5, null, c), "no ConnectFailed");
            Equal(DisconnectReason.ConnectFailed, reason, "reason");
            Check(sw.Elapsed.TotalSeconds > 1.3 && sw.Elapsed.TotalSeconds < 3, "connect timeout took " + sw.Elapsed.TotalSeconds);
            Check(c.Stats.PacketsSent >= 3, "CONNECT was not retried (" + c.Stats.PacketsSent + " packets)");

            // unresolvable host / bad port: reported on the next Poll, not thrown
            reason = DisconnectReason.None;
            c.Connect("bad host name", 27015, "X");
            Equal(DisconnectReason.None, reason, "event raised synchronously");
            c.Poll();
            Equal(DisconnectReason.ConnectFailed, reason, "bad host reason");
            reason = DisconnectReason.None;
            c.Connect("127.0.0.1", 0, "X");
            c.Poll();
            Equal(DisconnectReason.ConnectFailed, reason, "bad port reason");
            Equal(ClientState.Disconnected, c.State, "state");
            Equal(3, events, "event count");
            Equal(10f, new NetClient().ConnectTimeoutSeconds, "default connect timeout");
            Equal(10f, new NetClient().TimeoutSeconds, "default timeout");
        }

        static void ReliableUnderLoss()
        {
            var server = StartServer(4);
            var client = Connect(server, "Lossy");
            try
            {
                const int N = 1500;
                int id = client.ConnectionId;
                var atServer = new List<int>();
                var atClient = new List<int>();
                int unreliableAtServer = 0;
                server.DataReceived += (cid, r, ch) =>
                {
                    var (tag, index) = ReadMessage(r);
                    if (ch == NetChannel.Unreliable) { unreliableAtServer++; return; }
                    Equal(1, tag, "tag at server");
                    atServer.Add(index);
                };
                client.DataReceived += (r, ch) =>
                {
                    var (tag, index) = ReadMessage(r);
                    if (ch == NetChannel.Reliable) atClient.Add(index);
                };

                NetSimulation.PacketLossPercent = 30;
                NetSimulation.DuplicatePercent = 10;
                NetSimulation.LatencyMs = 20;
                NetSimulation.JitterMs = 40; // reorders packets

                var w = new NetWriter();
                int sent = 0;
                var sw = Stopwatch.StartNew();
                double nextBatch = 0;
                Pump(() =>
                {
                    if (sent < N && sw.Elapsed.TotalSeconds >= nextBatch)
                    {
                        nextBatch += 0.01;
                        for (int k = 0; k < 5 && sent < N; k++, sent++)
                        {
                            int len = sent % 97 == 0 ? 2500 : (sent * 7919) % 300; // some multi-fragment messages
                            WriteMessage(w, 1, sent, len);
                            client.Send(w, NetChannel.Reliable);
                            WriteMessage(w, 2, sent, len);
                            server.Send(id, w, NetChannel.Reliable);
                            WriteMessage(w, 1, sent, 20);
                            client.Send(w, NetChannel.Unreliable);
                        }
                    }
                    return atServer.Count >= N && atClient.Count >= N;
                }, 60, server, client);

                Equal(N, atServer.Count, "reliable messages at server");
                Equal(N, atClient.Count, "reliable messages at client");
                for (int i = 0; i < N; i++)
                {
                    if (atServer[i] != i) throw new TestFailure($"server got message {atServer[i]} at position {i}");
                    if (atClient[i] != i) throw new TestFailure($"client got message {atClient[i]} at position {i}");
                }
                Check(client.Stats.Resends > 0 && server.Stats.Resends > 0, "no resends under loss?");
                Check(unreliableAtServer < N, "unreliable messages were not affected by 30% loss?");
                PumpFor(1.0, server, client); // late duplicates must not be delivered again
                Equal(N, atServer.Count, "server count after settling (exactly once)");
                Equal(N, atClient.Count, "client count after settling (exactly once)");
                Equal(ClientState.Connected, client.State, "still connected");
                Program.Note($"{sw.Elapsed.TotalSeconds:0.0}s, resends client={client.Stats.Resends} server={server.Stats.Resends}, unreliable {unreliableAtServer}/{N}");
            }
            finally { NetSimulation.Reset(); client.Dispose(); server.Stop(); }
        }

        static byte[] Pattern(int length, int seed)
        {
            var b = new byte[length];
            var rng = new Random(seed);
            rng.NextBytes(b);
            return b;
        }

        static bool Same(byte[] a, NetReader r)
        {
            if (r.Remaining != a.Length) return false;
            var got = new byte[a.Length];
            r.ReadBytes(got, 0, got.Length);
            for (int i = 0; i < a.Length; i++) if (a[i] != got[i]) return false;
            return true;
        }

        static void LargeMessages()
        {
            var server = StartServer(4);
            var client = Connect(server);
            try
            {
                int id = client.ConnectionId;
                var big = Pattern(40 * 1024, 1);
                var max = Pattern(NetProtocol.MaxMessageSize, 2);
                var small = Pattern(10, 3);
                var serverGot = new List<bool>();
                var clientGot = new List<bool>();
                server.DataReceived += (c, r, ch) => serverGot.Add(serverGot.Count switch { 0 => Same(big, r), 1 => Same(small, r), _ => Same(max, r) });
                client.DataReceived += (r, ch) => clientGot.Add(clientGot.Count switch { 0 => Same(big, r), 1 => Same(small, r), _ => Same(max, r) });

                client.Send(big, 0, big.Length, NetChannel.Reliable);
                client.Send(small, 0, small.Length, NetChannel.Reliable);
                server.Send(id, big, 0, big.Length, NetChannel.Reliable);
                server.Send(id, small, 0, small.Length, NetChannel.Reliable);
                Check(Pump(() => serverGot.Count == 2 && clientGot.Count == 2, 5, server, client), "40 KB message not delivered");
                Check(serverGot.TrueForAll(x => x) && clientGot.TrueForAll(x => x), "40 KB message corrupted / out of order");

                // again with loss + reordering, and the 64 KB maximum
                NetSimulation.PacketLossPercent = 20;
                NetSimulation.JitterMs = 30;
                client.Send(max, 0, max.Length, NetChannel.Reliable);
                server.Send(id, max, 0, max.Length, NetChannel.Reliable);
                Check(Pump(() => serverGot.Count == 3 && clientGot.Count == 3, 20, server, client), "64 KB message not delivered under loss");
                Check(serverGot[2] && clientGot[2], "64 KB message corrupted");
                NetSimulation.Reset();

                bool threw = false;
                try { client.Send(new byte[NetProtocol.MaxMessageSize + 1], 0, NetProtocol.MaxMessageSize + 1, NetChannel.Reliable); }
                catch (ArgumentException) { threw = true; }
                Check(threw, "oversized message did not throw");
            }
            finally { NetSimulation.Reset(); client.Dispose(); server.Stop(); }
        }

        static void UnreliableDelivery()
        {
            var server = StartServer(4);
            var client = Connect(server);
            try
            {
                int id = client.ConnectionId;
                int small = 0, big = 0, wrongChannel = 0;
                var bigMsg = Pattern(5000, 9);
                server.DataReceived += (c, r, ch) =>
                {
                    if (ch != NetChannel.Unreliable) wrongChannel++;
                    if (r.Remaining == bigMsg.Length) { if (Same(bigMsg, r)) big++; }
                    else { ReadMessage(r); small++; }
                };
                client.DataReceived += (r, ch) => { if (ch == NetChannel.Unreliable && Same(bigMsg, r)) big++; };
                var w = new NetWriter();
                int sent = 0;
                Pump(() =>
                {
                    if (sent < 200) { WriteMessage(w, 5, sent++, 50); client.Send(w, NetChannel.Unreliable); }
                    if (sent == 100) { client.Send(bigMsg, 0, bigMsg.Length, NetChannel.Unreliable); server.Send(id, bigMsg, 0, bigMsg.Length, NetChannel.Unreliable); sent++; }
                    return small >= 199 && big >= 2;
                }, 5, server, client);
                Check(small >= 190, "unreliable messages received: " + small + "/199");
                Equal(2, big, "fragmented unreliable 5 KB messages (both directions)");
                Equal(0, wrongChannel, "channel reported");
            }
            finally { client.Dispose(); server.Stop(); }
        }

        static void ExplicitFlush()
        {
            var server = StartServer(4);
            var client = Connect(server);
            try
            {
                int got = 0;
                server.DataReceived += (c, r, ch) => got++;
                var w = new NetWriter();
                w.WriteInt(7);
                client.Send(w, NetChannel.Reliable);
                client.Flush(); // no client.Poll() from here on
                var sw = Stopwatch.StartNew();
                while (got == 0 && sw.Elapsed.TotalSeconds < 2) { server.Poll(); Thread.Sleep(1); }
                Equal(1, got, "message delivered after Flush");
            }
            finally { client.Dispose(); server.Stop(); }
        }

        static void RttMeasurement()
        {
            var server = StartServer(4);
            var client = Connect(server);
            try
            {
                PumpFor(1.2, server, client);
                float fast = client.Rtt;
                Check(fast > 0f && fast < 0.03f, "loopback RTT " + fast);
                Check(server.GetRtt(client.ConnectionId) > 0f && server.GetRtt(client.ConnectionId) < 0.03f, "server loopback RTT " + server.GetRtt(client.ConnectionId));

                NetSimulation.LatencyMs = 50; // each way -> ~100 ms round trip
                PumpFor(5.0, server, client);
                float c = client.Rtt, s = server.GetRtt(client.ConnectionId);
                Check(c > 0.08f && c < 0.16f, "client RTT with 100 ms simulated round trip: " + c);
                Check(s > 0.08f && s < 0.16f, "server RTT with 100 ms simulated round trip: " + s);
                Equal(0f, server.GetRtt(999), "unknown id RTT");
                Program.Note($"loopback {fast * 1000:0.0} ms, simulated: client {c * 1000:0} ms, server {s * 1000:0} ms");
            }
            finally { NetSimulation.Reset(); client.Dispose(); server.Stop(); }
        }

        static void ClientTimeout()
        {
            var server = StartServer(4);
            var client = Connect(server);
            try
            {
                client.TimeoutSeconds = 2f;
                DisconnectReason reason = DisconnectReason.None;
                client.Disconnected += r => reason = r;
                PumpFor(0.3, server, client);
                var sw = Stopwatch.StartNew();
                Check(Pump(() => reason != DisconnectReason.None, 6, null, client), "client never timed out"); // server not polled
                Equal(DisconnectReason.Timeout, reason, "reason");
                Check(sw.Elapsed.TotalSeconds > 1.5 && sw.Elapsed.TotalSeconds < 3.0, "timeout after " + sw.Elapsed.TotalSeconds + " s");
                Equal(ClientState.Disconnected, client.State, "state");
            }
            finally { client.Dispose(); server.Stop(); }
        }

        static void ServerTimeout()
        {
            var server = StartServer(4);
            server.TimeoutSeconds = 2f;
            var client = Connect(server);
            try
            {
                int id = client.ConnectionId, events = 0;
                DisconnectReason reason = DisconnectReason.None;
                server.ClientDisconnected += (cid, r) => { if (cid == id) reason = r; events++; };
                PumpFor(0.3, server, client);
                var sw = Stopwatch.StartNew();
                Check(Pump(() => reason != DisconnectReason.None, 6, server), "server never timed the client out");
                Equal(DisconnectReason.Timeout, reason, "reason");
                Check(sw.Elapsed.TotalSeconds > 1.5 && sw.Elapsed.TotalSeconds < 3.0, "timeout after " + sw.Elapsed.TotalSeconds + " s");
                Equal(0, server.Connections.Count, "connections");
                // the client resumes: it was told (DISCONNECT Timeout) and must not raise a second event later
                DisconnectReason clientReason = DisconnectReason.None;
                client.Disconnected += r => clientReason = r;
                PumpFor(0.5, server, client);
                Equal(DisconnectReason.Timeout, clientReason, "client told about the timeout");
                Equal(1, events, "server events");
            }
            finally { client.Dispose(); server.Stop(); }
        }

        static void GracefulDisconnect()
        {
            var server = StartServer(4);
            try
            {
                var events = new List<(int, DisconnectReason)>();
                server.ClientDisconnected += (id, r) => events.Add((id, r));

                // client -> server
                var a = Connect(server, "A");
                int aEvents = 0;
                a.Disconnected += r => aEvents++;
                int aId = a.ConnectionId;
                a.Disconnect();
                Equal(ClientState.Disconnected, a.State, "client state after Disconnect");
                Check(Pump(() => events.Count > 0, 2, server), "server did not see the disconnect");
                Equal((aId, DisconnectReason.LocalClosed), events[0], "server event");
                Equal(0, aEvents, "local Disconnect raised an event");
                Equal(0, server.Connections.Count, "connections");

                // server -> client (kick)
                var b = Connect(server, "B");
                DisconnectReason bReason = DisconnectReason.None;
                b.Disconnected += r => bReason = r;
                server.Disconnect(b.ConnectionId, DisconnectReason.Kicked);
                Equal(0, server.Connections.Count, "connections after kick");
                Check(Pump(() => bReason != DisconnectReason.None, 2, server, b), "client did not see the kick");
                Equal(DisconnectReason.Kicked, bReason, "kick reason");
                PumpFor(0.3, server, b);
                Equal(1, events.Count, "server raised an event for a local kick");

                // disconnect while still connecting: the server must not keep a half-open connection
                var c = new NetClient();
                c.Connect("127.0.0.1", server.Port, "C");
                c.Disconnect();
                PumpFor(0.3, server, c);
                Equal(0, server.Connections.Count, "half-open connection left behind");

                // server Stop -> HostClosed
                var d = Connect(server, "D");
                DisconnectReason dReason = DisconnectReason.None;
                d.Disconnected += r => dReason = r;
                int before = events.Count;
                server.Stop();
                Check(!server.IsRunning, "IsRunning after Stop");
                Check(Pump(() => dReason != DisconnectReason.None, 2, null, d), "client did not see Stop");
                Equal(DisconnectReason.HostClosed, dReason, "Stop reason");
                Equal(before, events.Count, "Stop raised server events");
                server.Poll(); // harmless when stopped
            }
            finally { server.Stop(); }
        }

        static void Reconnect()
        {
            var server = StartServer(4);
            var client = new NetClient();
            try
            {
                int connects = 0;
                client.Connected += () => connects++;
                var received = new List<int>();
                server.DataReceived += (id, r, ch) => received.Add(r.ReadInt());
                var echoed = new List<int>();
                client.DataReceived += (r, ch) => echoed.Add(r.ReadInt());
                var w = new NetWriter();
                var ids = new List<int>();

                for (int round = 0; round < 3; round++)
                {
                    client.Connect("127.0.0.1", server.Port, "Again");
                    Check(Pump(() => connects == round + 1, 3, server, client), "connect round " + round);
                    int id = client.ConnectionId;
                    w.Reset(); w.WriteInt(round);
                    client.Send(w, NetChannel.Reliable);
                    server.Send(id, w, NetChannel.Reliable);
                    Check(Pump(() => received.Count == round + 1 && echoed.Count == round + 1, 3, server, client), "exchange round " + round);
                    Equal(round, received[round], "server message");
                    Equal(round, echoed[round], "client message");
                    Check(!ids.Contains(id), "connection id reused immediately: " + id);
                    ids.Add(id);
                    // round 0: explicit Disconnect; later rounds: Connect again while connected (drops the old one)
                    if (round == 0) client.Disconnect();
                    Check(Pump(() => server.Connections.Count == (round == 0 ? 0 : 1), 2, server, client), "old connection not removed");
                }
                Equal(1, server.Connections.Count, "one connection at the end");
                Equal(ids[2], server.Connections[0], "the live connection is the latest");
            }
            finally { client.Dispose(); server.Stop(); }
        }

        static void FourClients()
        {
            var server = StartServer(4);
            var clients = new List<NetClient>();
            try
            {
                var relay = new NetWriter();
                server.DataReceived += (id, r, ch) =>
                {
                    // relay the message to everyone else, prefixed with the sender id
                    relay.Reset();
                    relay.WriteInt(id);
                    int n = r.Remaining;
                    var tmp = new byte[n];
                    r.ReadBytes(tmp, 0, n);
                    relay.WriteBytes(tmp, 0, n);
                    server.Broadcast(relay, ch, id);
                };
                for (int i = 0; i < 4; i++) clients.Add(Connect(server, "P" + i));
                Equal(4, server.Connections.Count, "connections");

                const int M = 100;
                var nextExpected = new Dictionary<int, int>[4];
                int[] reliableGot = new int[4], unreliableGot = new int[4];
                for (int i = 0; i < 4; i++)
                {
                    int me = i;
                    nextExpected[i] = new Dictionary<int, int>();
                    clients[i].DataReceived += (r, ch) =>
                    {
                        int from = r.ReadInt();
                        var (tag, index) = ReadMessage(r);
                        Check(from != clients[me].ConnectionId, "got own message back");
                        if (ch == NetChannel.Unreliable) { unreliableGot[me]++; return; }
                        nextExpected[me].TryGetValue(from, out int expect);
                        if (index != expect) throw new TestFailure($"client {me} got {index} from {from}, expected {expect}");
                        nextExpected[me][from] = expect + 1;
                        reliableGot[me]++;
                    };
                }

                var w = new NetWriter();
                int sent = 0;
                var lateClient = new NetClient();
                DisconnectReason lateReason = DisconnectReason.None;
                lateClient.Disconnected += r => lateReason = r;
                lateClient.Connect("127.0.0.1", server.Port, "Fifth");
                var all = new List<NetClient>(clients) { lateClient };
                Pump(() =>
                {
                    if (sent < M)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            WriteMessage(w, i, sent, 40 + i);
                            clients[i].Send(w, NetChannel.Reliable);
                            clients[i].Send(w, NetChannel.Unreliable);
                        }
                        sent++;
                    }
                    for (int i = 0; i < 4; i++) if (reliableGot[i] < 3 * M) return false;
                    return lateReason != DisconnectReason.None;
                }, 15, server, all.ToArray());

                for (int i = 0; i < 4; i++)
                {
                    Equal(3 * M, reliableGot[i], $"client {i} reliable relayed messages");
                    Check(unreliableGot[i] >= 3 * M * 9 / 10, $"client {i} unreliable relayed messages: {unreliableGot[i]}");
                }
                Equal(DisconnectReason.ServerFull, lateReason, "fifth client");
                lateClient.Dispose();
            }
            finally { foreach (var c in clients) c.Dispose(); server.Stop(); }
        }

        static void PendingOverflow()
        {
            var server = StartServer(4);
            var client = Connect(server);
            var client2 = Connect(server);
            try
            {
                var w = new NetWriter();
                w.WriteInt(1);
                // client side: the server stops acking (not polled)
                DisconnectReason reason = DisconnectReason.None;
                client.Disconnected += r => reason = r;
                for (int i = 0; i < NetProtocol.MaxPendingReliable + 10; i++) client.Send(w, NetChannel.Reliable);
                client.Poll();
                Equal(DisconnectReason.Timeout, reason, "client overflow reason");

                // server side: client2 stops acking
                int id2 = client2.ConnectionId;
                DisconnectReason serverReason = DisconnectReason.None;
                server.ClientDisconnected += (id, r) => { if (id == id2) serverReason = r; };
                for (int i = 0; i < NetProtocol.MaxPendingReliable + 10; i++) server.Send(id2, w, NetChannel.Reliable);
                Check(Pump(() => serverReason != DisconnectReason.None, 2, server), "server did not drop the overflowing client");
                Equal(DisconnectReason.Timeout, serverReason, "server overflow reason");
            }
            finally { client.Dispose(); client2.Dispose(); server.Stop(); }
        }

        static void Garbage()
        {
            var server = StartServer(16);
            var good = Connect(server, "Good");
            var raw = RawSocket();
            try
            {
                int delivered = 0, echoed = 0;
                server.DataReceived += (id, r, ch) =>
                {
                    if (id == good.ConnectionId) { server.Send(id, new byte[] { 1 }, 0, 1, NetChannel.Reliable); return; }
                    delivered++;
                    bool overRead = r.Remaining > 0 && r.ReadByte() == 0xEE; // sometimes read past the end: Poll must swallow it
                    while (r.Remaining > 0) r.ReadByte();
                    if (overRead) r.ReadInt();
                };
                var events = new List<DisconnectReason>();
                server.ClientDisconnected += (id, r) => events.Add(r);
                var rng = new Random(77);
                var buf = new byte[1600];

                // 1) random datagrams from an unknown endpoint (some look like CONNECT / DATA)
                for (int i = 0; i < 2000; i++)
                {
                    int n = rng.Next(0, buf.Length);
                    rng.NextBytes(buf);
                    if (i % 3 == 0 && n > 0) buf[0] = (byte)rng.Next(0, 7);
                    if (i % 7 == 0 && n >= 5) { buf[0] = NetProtocol.PktConnect; buf[1] = 0x50; buf[2] = 0x4F; buf[3] = 0x4D; buf[4] = 0x52; } // magic, random rest
                    RawSend(raw, server.Port, buf, n);
                    RawSend(raw, good.LocalPort, buf, n); // and at the client
                    if (i % 50 == 0) { server.Poll(); good.Poll(); }
                }
                PumpFor(0.3, server, good);
                Equal(ClientState.Connected, good.State, "good client still connected");
                Check(server.Connections.Count >= 1, "good client dropped");

                // 2) a connected raw peer sending malformed DATA packets -> BadData
                int connectionsBefore = server.Connections.Count;
                uint token = 0xABCDEF;
                RawHandshake(raw, server, token);
                var w = new NetWriter();
                for (int i = 0; i < 12; i++)
                {
                    w.Reset();
                    w.WriteByte(NetProtocol.PktData);
                    w.WriteUInt(token);
                    w.WriteUShort(0);
                    w.WriteByte(200); // more selective acks than allowed
                    RawSend(raw, server.Port, w.ToArray());
                }
                Check(Pump(() => events.Contains(DisconnectReason.BadData), 2, server, good), "garbage peer not dropped with BadData");
                var disc = RawReceive(raw, server, NetProtocol.PktDisconnect, 1);
                Check(disc != null && disc[5] == (byte)DisconnectReason.BadData, "garbage peer was not told BadData");
                Equal(connectionsBefore, server.Connections.Count, "connections after BadData drop");

                // 3) fuzz: structurally plausible DATA packets (random entries, seqs, fragments), some mutated or truncated
                int badDrops = 0;
                server.ClientDisconnected += (id, r) => { if (r == DisconnectReason.BadData) badDrops++; };
                for (int round = 0; round < 25; round++)
                {
                    uint t = (uint)(0x100000 + round);
                    int rawId = RawHandshake(raw, server, t);
                    for (int i = 0; i < 80; i++)
                    {
                        var pkt = FuzzPacket(rng, t);
                        RawSend(raw, server.Port, pkt, rng.Next(10) == 0 ? rng.Next(5, pkt.Length + 1) : pkt.Length);
                        if (i % 10 == 0) { server.Poll(); good.Poll(); }
                    }
                    PumpFor(0.02, server, good);
                    server.Disconnect(rawId, DisconnectReason.Kicked); // no-op if it was already dropped for BadData
                    PumpFor(0.01, server, good);
                }
                Check(delivered > 100, "fuzzing barely reached message delivery: " + delivered);
                PumpFor(0.3, server, good);
                Equal(ClientState.Connected, good.State, "good client survived the fuzzing");

                // the good client still works end to end
                good.DataReceived += (r, ch) => echoed++;
                var m = new NetWriter();
                m.WriteInt(5);
                good.Send(m, NetChannel.Reliable);
                Check(Pump(() => echoed > 0, 2, server, good), "good client no longer served");
                Program.Note($"fuzz delivered {delivered} random messages to the handler, {badDrops} fuzz peers dropped for BadData");
            }
            finally { raw.Close(); good.Dispose(); server.Stop(); }
        }

        /// <summary>A DATA packet with a valid header and random but mostly well-formed entries.</summary>
        static byte[] FuzzPacket(Random rng, uint token)
        {
            var w = new NetWriter(NetProtocol.MaxPacketSize);
            w.WriteByte(NetProtocol.PktData);
            w.WriteUInt(token);
            w.WriteUShort((ushort)rng.Next(0, 8));
            int acks = rng.Next(0, 4);
            w.WriteByte((byte)acks);
            for (int i = 0; i < acks; i++) w.WriteUShort((ushort)rng.Next(0, 64));
            int entries = rng.Next(1, 7);
            for (int e = 0; e < entries && w.Length < 1000; e++)
            {
                int kind = rng.Next(0, 20) == 0 ? rng.Next(0, 256) : rng.Next(1, 7);
                w.WriteByte((byte)kind);
                switch (kind)
                {
                    case NetProtocol.EntUnreliable: RandomBytes(w, rng, rng.Next(0, 40)); break;
                    case NetProtocol.EntReliable:
                    case NetProtocol.EntReliableMore:
                        w.WriteUShort((ushort)(rng.Next(0, 10) == 0 ? rng.Next(0, 65536) : rng.Next(0, 48)));
                        RandomBytes(w, rng, rng.Next(0, 60));
                        break;
                    case NetProtocol.EntUnreliableFrag:
                    {
                        int count = rng.Next(0, 6), index = rng.Next(0, Math.Max(1, count + 1));
                        w.WriteUShort((ushort)rng.Next(0, 3));
                        w.WriteByte((byte)index);
                        w.WriteByte((byte)count);
                        int len = index < count - 1 && rng.Next(4) != 0 ? NetProtocol.FragmentSize : rng.Next(0, 100);
                        if (w.Length + len + 2 > NetProtocol.MaxPacketSize) len = 10;
                        RandomBytes(w, rng, len);
                        break;
                    }
                    default: w.WriteUInt((uint)rng.Next()); break; // ping / pong / junk
                }
            }
            var bytes = w.ToArray();
            if (rng.Next(4) == 0)
                for (int k = rng.Next(1, 4); k > 0; k--) bytes[rng.Next(5, bytes.Length)] = (byte)rng.Next(256); // mutate (not the token)
            return bytes;
        }

        static void RandomBytes(NetWriter w, Random rng, int length)
        {
            w.WriteUShort((ushort)length);
            for (int i = 0; i < length; i++) w.WriteByte((byte)rng.Next(256));
        }

        static void ClientBadData()
        {
            var server = StartServer(4);
            var client = Connect(server);
            var raw = RawSocket();
            try
            {
                int id = client.ConnectionId;
                DisconnectReason clientReason = DisconnectReason.None, serverReason = DisconnectReason.None;
                client.Disconnected += r => clientReason = r;
                server.ClientDisconnected += (cid, r) => { if (cid == id) serverReason = r; };
                var w = new NetWriter();
                for (int i = 0; i < 12; i++)
                {
                    w.Reset();
                    w.WriteByte(NetProtocol.PktData);
                    w.WriteUInt(client.Token); // right token (as if the server sent it), broken body
                    w.WriteUShort(0);
                    w.WriteByte(0);
                    w.WriteByte(99); // unknown entry kind
                    RawSend(raw, client.LocalPort, w.ToArray());
                }
                Check(Pump(() => clientReason != DisconnectReason.None && serverReason != DisconnectReason.None, 2, server, client), "client did not drop the garbage connection");
                Equal(DisconnectReason.BadData, clientReason, "client reason");
                Equal(DisconnectReason.BadData, serverReason, "server was told BadData");
            }
            finally { raw.Close(); client.Dispose(); server.Stop(); }
        }

        static void SequenceWraparound()
        {
            var server = StartServer(4);
            var client = Connect(server);
            try
            {
                const int Total = 70000, LossFrom = 64000, LossTo = 67500; // lossy, reordered stretch across seq 65535 -> 0
                int id = client.ConnectionId;
                int atServer = 0, atClient = 0;
                server.DataReceived += (c, r, ch) => { int v = r.ReadInt(); if (v != atServer) throw new TestFailure($"server got {v}, expected {atServer}"); atServer++; };
                client.DataReceived += (r, ch) => { int v = r.ReadInt(); if (v != atClient) throw new TestFailure($"client got {v}, expected {atClient}"); atClient++; };
                var w = new NetWriter();
                int sent = 0;
                var sw = Stopwatch.StartNew();
                while ((atServer < Total || atClient < Total) && sw.Elapsed.TotalSeconds < 60)
                {
                    bool lossy = sent >= LossFrom && sent < LossTo;
                    NetSimulation.PacketLossPercent = lossy ? 20 : 0;
                    NetSimulation.JitterMs = lossy ? 15 : 0;
                    NetSimulation.DuplicatePercent = lossy ? 10 : 0;
                    for (int k = 0; k < 400 && sent < Total && client.PendingReliable < 1500 && server.GetPendingReliable(id) < 1500; k++, sent++)
                    {
                        w.Reset();
                        w.WriteInt(sent);
                        client.Send(w, NetChannel.Reliable);
                        server.Send(id, w, NetChannel.Reliable);
                    }
                    client.Poll();
                    server.Poll();
                    if (NetSimulation.IsActive || sent >= Total) Thread.Sleep(1);
                }
                Equal(Total, atServer, "messages at server");
                Equal(Total, atClient, "messages at client");
                Equal(ClientState.Connected, client.State, "still connected");
                Program.Note($"{Total} messages each way in {sw.Elapsed.TotalSeconds:0.0}s, resends {client.Stats.Resends}/{server.Stats.Resends}");
            }
            finally { NetSimulation.Reset(); client.Dispose(); server.Stop(); }
        }

        static void HandlerExceptions()
        {
            var server = StartServer(4);
            int warningsBefore = Program.Warnings.Count;
            server.ClientConnected += (id, r) => throw new InvalidOperationException("boom: connected");
            server.DataReceived += (id, r, ch) => throw new InvalidOperationException("boom: data");
            var client = new NetClient();
            bool connected = false;
            client.Connected += () => { connected = true; throw new InvalidOperationException("boom: client connected"); };
            client.DataReceived += (r, ch) => throw new InvalidOperationException("boom: client data");
            try
            {
                client.Connect("127.0.0.1", server.Port, "Thrower");
                Check(Pump(() => connected, 3, server, client), "connect");
                var w = new NetWriter();
                w.WriteInt(1);
                for (int i = 0; i < 5; i++) { client.Send(w, NetChannel.Reliable); server.Send(client.ConnectionId, w, NetChannel.Reliable); }
                PumpFor(0.5, server, client);
                Equal(ClientState.Connected, client.State, "client still connected");
                Equal(1, server.Connections.Count, "server still has the client");
                int logged = Program.Warnings.FindAll(m => m.Contains("boom")).Count;
                Check(logged >= 12, "handler exceptions logged: " + logged);
                Check(Program.Warnings.Count > warningsBefore, "NetLog.Warning hook not used");
            }
            finally { client.Dispose(); server.Stop(); }
        }
    }
}
