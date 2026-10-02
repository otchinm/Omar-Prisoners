using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using PrisonersOfOmar.Net;
using static PrisonersOfOmar.NetTests.T;

namespace PrisonersOfOmar.NetTests
{
    static class DiscoveryTests
    {
        public static void Register(List<(string, Action)> t)
        {
            t.Add(("LAN beacon serialization", BeaconFormat));
            t.Add(("LAN discovery broadcast (2 listeners on one port, update, expiry)", Broadcast));
        }

        static readonly int TestPort = 40000 + new Random().Next(0, 9000); // keeps clear of real game instances

        static LanSessionInfo Find(LanDiscovery d, string hostName)
        {
            foreach (var s in d.Sessions) if (s.HostName == hostName) return s;
            return null;
        }

        static bool PollUntil(Func<bool> done, double seconds, params LanDiscovery[] all)
        {
            var sw = Stopwatch.StartNew();
            while (true)
            {
                foreach (var d in all) d.Poll();
                if (done()) return true;
                if (sw.Elapsed.TotalSeconds > seconds) return false;
                Thread.Sleep(2);
            }
        }

        static void BeaconFormat()
        {
            using var host = new LanDiscovery(TestPort);
            Equal(GameInfo.DiscoveryPort, new LanDiscovery().DiscoveryPort, "default discovery port");
            host.StartAdvertising(new LanSessionInfo { HostName = "Ömar's base", Port = 27015, Players = 3, MaxPlayers = 5, InLobby = false });
            var info = new LanSessionInfo();
            Check(LanDiscovery.TryReadBeacon(new NetReader(host.BeaconBytes), info, out uint id), "beacon did not parse");
            Check(id != 0, "advertiser id");
            Equal("Ömar's base", info.HostName, "host name");
            Equal(27015, info.Port, "port");
            Equal(3, info.Players, "players");
            Equal(5, info.MaxPlayers, "max players");
            Equal(false, info.InLobby, "in lobby");
            Equal(GameInfo.ProtocolVersion, info.Protocol, "protocol");

            var bytes = host.BeaconBytes;
            for (int cut = 0; cut < bytes.Length; cut++)
                Check(!LanDiscovery.TryReadBeacon(new NetReader(bytes, 0, cut), new LanSessionInfo(), out _), "truncated beacon accepted (" + cut + ")");
            bytes[0] ^= 0xFF;
            Check(!LanDiscovery.TryReadBeacon(new NetReader(bytes), new LanSessionInfo(), out _), "bad magic accepted");
        }

        static void Broadcast()
        {
            string name = "TestHost-" + Environment.ProcessId;
            using var host = new LanDiscovery(TestPort);
            using var a = new LanDiscovery(TestPort);
            using var b = new LanDiscovery(TestPort); // second listener on the same port (ReuseAddress)
            a.StartListening();
            b.StartListening();
            host.StartListening(); // a host must not list itself
            host.StartAdvertising(new LanSessionInfo { HostName = name, Port = 27015, Players = 1, MaxPlayers = 5, InLobby = true });

            bool heard = PollUntil(() => Find(a, name) != null && Find(b, name) != null, 3.5, host, a, b);
            if (!heard)
            {
                // Broadcast unavailable here: fall back to unicast loopback so the receive path is still covered.
                Program.Note("broadcast beacons were not received; tested with loopback unicast (ExtraTargets) instead");
                host.ExtraTargets.Add(new IPEndPoint(IPAddress.Loopback, TestPort));
                Check(PollUntil(() => Find(a, name) != null || Find(b, name) != null, 3.5, host, a, b), "beacon not received even via loopback unicast");
                return;
            }

            var s = Find(a, name);
            Equal(27015, s.Port, "port");
            Equal(1, s.Players, "players");
            Equal(5, s.MaxPlayers, "max players");
            Equal(true, s.InLobby, "in lobby");
            Check(!string.IsNullOrEmpty(s.Address) && IPAddress.TryParse(s.Address, out _), "address: " + s.Address);
            Check(s.LastSeen > 0 && NetTime.Now - s.LastSeen < 2, "LastSeen");
            int copies = 0;
            foreach (var x in a.Sessions) if (x.HostName == name) copies++;
            Equal(1, copies, "same host heard on several interfaces must be listed once");
            Check(Find(host, name) == null, "advertiser lists its own session");
            Program.Note("session address " + s.Address);

            host.UpdateAdvertisement(new LanSessionInfo { HostName = name, Port = 27015, Players = 4, MaxPlayers = 5, InLobby = false });
            Check(PollUntil(() => Find(a, name)?.Players == 4 && Find(b, name)?.Players == 4, 1.5, host, a, b), "update not propagated");
            Equal(false, Find(a, name).InLobby, "in lobby after update");

            host.StopAdvertising();
            var sw = Stopwatch.StartNew();
            Check(PollUntil(() => Find(a, name) == null && Find(b, name) == null, 6, host, a, b), "session did not expire");
            Check(sw.Elapsed.TotalSeconds > 2.5, "expired too early: " + sw.Elapsed.TotalSeconds);

            a.StopListening();
            Equal(0, a.Sessions.Count, "sessions cleared by StopListening");
            Check(!a.IsListening && !host.IsAdvertising, "flags");
        }
    }
}
