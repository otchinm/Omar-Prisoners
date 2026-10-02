using System;
using System.Collections.Generic;
using System.Net;

namespace PrisonersOfOmar.Net
{
    public enum NetChannel : byte
    {
        /// <summary>Fire and forget, may be lost or arrive out of order (state snapshots).</summary>
        Unreliable = 0,
        /// <summary>Guaranteed, in-order delivery (events, RPCs). Messages up to 64 KB (fragmented).</summary>
        Reliable = 1,
    }

    public enum DisconnectReason : byte
    {
        None = 0,
        Timeout,
        Kicked,
        ServerFull,
        VersionMismatch,
        MatchInProgress,
        HostClosed,
        LocalClosed,
        ConnectFailed,
        BadData,
    }

    public enum ClientState : byte
    {
        Disconnected = 0,
        Connecting,
        Connected,
    }

    /// <summary>What a client sent when asking to join.</summary>
    public struct ConnectRequest
    {
        public int ProtocolVersion;
        public string PlayerName;
        /// <summary>Opaque extra bytes from the client (may be empty, never null).</summary>
        public byte[] Payload;
        public IPEndPoint EndPoint;
    }

    public struct NetStats
    {
        public long BytesSent, BytesReceived, PacketsSent, PacketsReceived, Resends;
    }

    /// <summary>A game found on the local network.</summary>
    public sealed class LanSessionInfo
    {
        public string HostName = "";
        public string Address = "";
        public int Port;
        public int Players;
        public int MaxPlayers;
        public bool InLobby = true;
        public int Protocol;
        /// <summary>Realtime (seconds, Stopwatch based) when the last beacon was heard.</summary>
        public double LastSeen;
    }
}
