using System;
using System.Collections.Generic;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    public enum SessionState
    {
        Offline = 0,
        Connecting,
        Lobby,
        Loading,
        InMatch,
        Ending,
    }

    /// <summary>
    /// Lobby + message routing on top of the UDP transport. The host is authoritative; it is also a player
    /// (id 0). Gameplay registers handlers per <see cref="Msg"/>; handlers receive (senderId, reader).
    /// Messages the host sends to "all" are dispatched to the host itself too (loopback), so every peer
    /// runs the same client-side code path.
    /// </summary>
    public sealed class NetSession : MonoBehaviour
    {
        public static NetSession Instance { get; private set; }

        NetServer _server;
        NetClient _client;
        readonly LanDiscovery _discovery = new LanDiscovery();
        readonly Action<int, NetReader>[] _handlers = new Action<int, NetReader>[256];
        readonly NetWriter _w = new NetWriter(2048);
        readonly NetReader _loop = new NetReader();
        readonly HashSet<int> _loaded = new HashSet<int>();
        float _loadingSince;
        float _advertiseTimer;
        string _pendingShutdown;

        public bool IsHost { get; private set; }
        public bool IsClient => _client != null && _client.State != ClientState.Disconnected;
        public bool Online => IsHost || (_client != null && _client.State == ClientState.Connected);
        public int LocalId { get; private set; } = -1;
        public SessionState State { get; private set; }
        public readonly List<PlayerInfo> Players = new List<PlayerInfo>();
        public MatchSettings Settings = new MatchSettings();
        public string LastError = "";
        public LanDiscovery Discovery => _discovery;
        /// <summary>True when hosting a solo practice game (no advertising).</summary>
        public bool Practice { get; private set; }

        public event Action RosterChanged;
        public event Action<SessionState> StateChanged;
        /// <summary>Raised on every peer when the host starts a match (map must be built, then call <see cref="NotifyLoaded"/>).</summary>
        public event Action MatchStarting;
        /// <summary>Raised on every peer when all players finished loading.</summary>
        public event Action MatchBegan;
        public event Action ReturnedToLobby;
        /// <summary>Session ended (disconnect / host closed). Argument: human readable reason.</summary>
        public event Action<string> SessionEnded;
        /// <summary>Host only: a client left during a match.</summary>
        public event Action<int> PlayerLeft;

        public PlayerInfo LocalPlayer => Find(LocalId);

        public static NetSession Create(Transform parent)
        {
            var go = new GameObject("NetSession");
            go.transform.SetParent(parent, false);
            return go.AddComponent<NetSession>();
        }

        void Awake()
        {
            Instance = this;
            NetLogBridge.Install();
            On(Msg.Roster, OnRoster);
            On(Msg.LobbyReq, OnLobbyReq);
            On(Msg.StartMatch, OnStartMatch);
            On(Msg.LoadedReq, OnLoadedReq);
            On(Msg.MatchBegin, OnMatchBegin);
            On(Msg.ReturnToLobby, OnReturnToLobby);
            On(Msg.Kick, OnKick);
        }

        void OnDestroy()
        {
            Shutdown("");
            _discovery.Dispose();
            if (Instance == this) Instance = null;
        }

        // ================================================================== public API

        public PlayerInfo Find(int id)
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].Id == id) return Players[i];
            return null;
        }

        public PlayerInfo FindOmar()
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].Role == PlayerRole.Omar) return Players[i];
            return null;
        }

        public int PrisonerCount
        {
            get { int n = 0; foreach (var p in Players) if (p.IsPrisoner) n++; return n; }
        }

        /// <summary>Start hosting a lobby. Returns false if the port could not be opened.</summary>
        public bool Host(string playerName, int port, bool practice = false)
        {
            Shutdown("");
            Practice = practice;
            if (!practice)
            {
                _server = new NetServer(GameInfo.MaxPlayers - 1);
                _server.Approve = Approve;
                _server.ClientConnected += OnClientConnected;
                _server.ClientDisconnected += OnClientDisconnected;
                _server.DataReceived += OnServerData;
                bool ok;
                try { ok = _server.Start(port); }
                catch (Exception e) { ok = false; LastError = e.Message; }
                if (!ok)
                {
                    LastError = "COULD NOT OPEN PORT " + port + ". IS ANOTHER GAME RUNNING?";
                    _server = null;
                    return false;
                }
            }
            IsHost = true;
            LocalId = 0;
            Players.Clear();
            Players.Add(new PlayerInfo { Id = 0, Name = Settings_PlayerName(playerName), Role = PlayerRole.Prisoner, Skin = CharacterSkin.Prisoner1, Ready = true });
            Settings = new MatchSettings { NightMinutes = Gameplay.Settings.NightMinutes, AiOmar = Gameplay.Settings.AiOmar };
            SetState(SessionState.Lobby);
            if (!practice)
            {
                try { _discovery.StartAdvertising(BuildLanInfo()); } catch (Exception e) { Debug.LogWarning("[Session] LAN advertising failed: " + e.Message); }
            }
            BroadcastRoster();
            return true;
        }

        static string Settings_PlayerName(string n) => Gameplay.Settings.CleanName(n);

        /// <summary>Start connecting to a host.</summary>
        public void Join(string address, int port, string playerName)
        {
            Shutdown("");
            Practice = false;
            _client = new NetClient();
            _client.Connected += OnClientConnectedToHost;
            _client.Disconnected += OnClientDisconnectedFromHost;
            _client.DataReceived += OnClientData;
            LocalId = -1;
            Players.Clear();
            SetState(SessionState.Connecting);
            try { _client.Connect(address.Trim(), port, Settings_PlayerName(playerName)); }
            catch (Exception e) { LastError = "CONNECT FAILED: " + e.Message; Shutdown(LastError); }
        }

        /// <summary>Leave / close the session (host closes it for everyone).</summary>
        public void Leave() => Shutdown("");

        void Shutdown(string reason)
        {
            _pendingShutdown = null;
            bool wasActive = State != SessionState.Offline;
            if (_server != null)
            {
                try { _server.Stop(); } catch { }
                _server.Dispose();
                _server = null;
            }
            if (_client != null)
            {
                var c = _client;
                _client = null;
                try { c.Disconnect(); } catch { }
                c.Dispose();
            }
            try { _discovery.StopAdvertising(); } catch { }
            IsHost = false;
            LocalId = -1;
            Players.Clear();
            _loaded.Clear();
            SetState(SessionState.Offline);
            if (wasActive) SessionEnded?.Invoke(reason);
        }

        /// <summary>Lobby: request a role / skin / ready state.</summary>
        public void RequestLobby(PlayerRole role, CharacterSkin skin, bool ready)
        {
            var w = Begin(Msg.LobbyReq);
            w.WriteByte((byte)role);
            w.WriteByte((byte)skin);
            w.WriteBool(ready);
            SendToHost(NetChannel.Reliable);
        }

        /// <summary>Host: can the match start right now?</summary>
        public bool CanStart(out string why)
        {
            why = "";
            if (!IsHost) { why = "ONLY THE HOST CAN START"; return false; }
            if (State != SessionState.Lobby) { why = "NOT IN LOBBY"; return false; }
            if (PrisonerCount == 0) { why = "NEED AT LEAST ONE PRISONER"; return false; }
            if (FindOmar() == null && !Settings.AiOmar) { why = "NOBODY PLAYS OMAR (ENABLE AI OMAR)"; return false; }
            foreach (var p in Players) if (!p.Ready && !p.IsBot) { why = "WAITING FOR " + p.Name; return false; }
            return true;
        }

        /// <summary>Host: start the match for everybody.</summary>
        public bool HostStartMatch()
        {
            if (!CanStart(out var why)) { LastError = why; return false; }
            Players.RemoveAll(p => p.IsBot);
            if (FindOmar() == null)
            {
                int botId = 253;
                while (botId > 1 && Find(botId) != null) botId--;
                Players.Add(new PlayerInfo { Id = botId, Name = "OMAR", Role = PlayerRole.Omar, Skin = CharacterSkin.Omar, Ready = true, IsBot = true });
            }
            Settings.Seed = Environment.TickCount ^ (int)(DateTime.UtcNow.Ticks & 0x7FFFFFFF);
            var w = Begin(Msg.StartMatch);
            Settings.Write(w);
            w.WriteByte((byte)Players.Count);
            foreach (var p in Players) p.Write(w);
            SendToAll(NetChannel.Reliable);
            return true;
        }

        /// <summary>Every peer: call when the map is built and the match objects exist.</summary>
        public void NotifyLoaded()
        {
            Begin(Msg.LoadedReq);
            SendToHost(NetChannel.Reliable);
        }

        /// <summary>Host: back to the lobby after the ending screen.</summary>
        public void HostReturnToLobby()
        {
            if (!IsHost) return;
            Begin(Msg.ReturnToLobby);
            SendToAll(NetChannel.Reliable);
        }

        public float GetRtt(int playerId)
        {
            var p = Find(playerId);
            if (IsHost && _server != null && playerId != LocalId && (p == null || !p.IsBot)) return _server.GetRtt(playerId);
            if (!IsHost && _client != null) return _client.Rtt;
            return 0f;
        }

        // ================================================================== messaging

        public void On(Msg msg, Action<int, NetReader> handler) => _handlers[(int)msg] = handler;
        public void Off(Msg msg) => _handlers[(int)msg] = null;

        /// <summary>Start writing a message into the shared writer. Send it with one of the Send* methods right away.</summary>
        public NetWriter Begin(Msg msg)
        {
            _w.Reset();
            _w.WriteByte((byte)msg);
            return _w;
        }

        /// <summary>Send the current message to the host (handled locally when we are the host).</summary>
        public void SendToHost(NetChannel ch)
        {
            if (IsHost) { DispatchLocal(LocalId); return; }
            if (_client != null && _client.State == ClientState.Connected) _client.Send(_w, ch);
        }

        /// <summary>Host: send the current message to every client (+ handle it locally).</summary>
        public void SendToAll(NetChannel ch, bool includeSelf = true)
        {
            if (!IsHost) return;
            if (_server != null && _server.IsRunning) _server.Broadcast(_w, ch);
            if (includeSelf) DispatchLocal(0);
        }

        /// <summary>Host: send to every client except one (+ locally unless the excluded one is the host).</summary>
        public void SendToAllExcept(int playerId, NetChannel ch)
        {
            if (!IsHost) return;
            if (_server != null && _server.IsRunning) _server.Broadcast(_w, ch, playerId);
            if (playerId != LocalId) DispatchLocal(0);
        }

        /// <summary>Host: send to one player (locally if it is the host itself; bots are ignored).</summary>
        public void SendTo(int playerId, NetChannel ch)
        {
            if (!IsHost) return;
            if (playerId == LocalId) { DispatchLocal(0); return; }
            var target = Find(playerId);
            if (target != null && target.IsBot) return;
            if (_server != null && _server.IsRunning) _server.Send(playerId, _w, ch);
        }

        void DispatchLocal(int sender)
        {
            var copy = _w.ToArray();
            _loop.SetBuffer(copy, 0, copy.Length);
            Dispatch(sender, _loop);
        }

        void Dispatch(int sender, NetReader r)
        {
            if (r.Remaining < 1) return;
            int type = r.ReadByte();
            var h = _handlers[type];
            if (h == null) return;
            try { h(sender, r); }
            catch (NetReadException) { Debug.LogWarning("[Session] malformed message " + (Msg)type + " from " + sender); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ================================================================== transport callbacks

        DisconnectReason Approve(ConnectRequest req)
        {
            if (req.ProtocolVersion != GameInfo.ProtocolVersion) return DisconnectReason.VersionMismatch;
            if (State != SessionState.Lobby) return DisconnectReason.MatchInProgress;
            int humans = 0;
            foreach (var p in Players) if (!p.IsBot) humans++;
            if (humans >= GameInfo.MaxPlayers) return DisconnectReason.ServerFull;
            return DisconnectReason.None;
        }

        void OnClientConnected(int connId, ConnectRequest req)
        {
            var p = new PlayerInfo { Id = connId, Name = Gameplay.Settings.CleanName(req.PlayerName) };
            // unique name
            string baseName = p.Name; int n = 2;
            while (Players.Exists(o => o.Name == p.Name)) p.Name = baseName + n++;
            AssignDefaultRole(p);
            Players.Add(p);
            BroadcastRoster();
        }

        void AssignDefaultRole(PlayerInfo p)
        {
            for (int s = 0; s < 4; s++)
            {
                var skin = (CharacterSkin)s;
                if (!Players.Exists(o => o.IsPrisoner && o.Skin == skin && o.Id != p.Id)) { p.Role = PlayerRole.Prisoner; p.Skin = skin; return; }
            }
            if (FindOmar() == null) { p.Role = PlayerRole.Omar; p.Skin = CharacterSkin.Omar; return; }
            p.Role = PlayerRole.Spectator;
        }

        void OnClientDisconnected(int connId, DisconnectReason reason)
        {
            var p = Find(connId);
            if (p == null) return;
            if (State == SessionState.Lobby || State == SessionState.Connecting)
            {
                Players.Remove(p);
            }
            else
            {
                p.Connected = false;
                _loaded.Remove(connId);
                PlayerLeft?.Invoke(connId);
                CheckAllLoaded();
            }
            BroadcastRoster();
        }

        void OnServerData(int connId, NetReader r, NetChannel ch) => Dispatch(connId, r);

        void OnClientConnectedToHost()
        {
            LocalId = _client.ConnectionId;
            SetState(SessionState.Lobby);
        }

        void OnClientDisconnectedFromHost(DisconnectReason reason)
        {
            string text;
            switch (reason)
            {
                case DisconnectReason.ServerFull: text = "THE GAME IS FULL"; break;
                case DisconnectReason.VersionMismatch: text = "VERSION MISMATCH (HOST RUNS A DIFFERENT BUILD)"; break;
                case DisconnectReason.MatchInProgress: text = "A MATCH IS ALREADY IN PROGRESS"; break;
                case DisconnectReason.HostClosed: text = "THE HOST CLOSED THE GAME"; break;
                case DisconnectReason.Kicked: text = "YOU WERE REMOVED FROM THE GAME"; break;
                case DisconnectReason.ConnectFailed: text = "COULD NOT REACH THE HOST"; break;
                case DisconnectReason.Timeout: text = "CONNECTION LOST"; break;
                default: text = "DISCONNECTED"; break;
            }
            LastError = text;
            _pendingShutdown = text; // deferred: we are inside the client's Poll()
        }

        void OnClientData(NetReader r, NetChannel ch) => Dispatch(0, r);

        // ================================================================== lobby messages

        void BroadcastRoster()
        {
            if (!IsHost) return;
            var w = Begin(Msg.Roster);
            w.WriteByte((byte)State);
            Settings.Write(w);
            w.WriteByte((byte)Players.Count);
            foreach (var p in Players) p.Write(w);
            SendToAll(NetChannel.Reliable);
            UpdateLan();
        }

        /// <summary>Host: push settings changes (night length, AI Omar) to everybody.</summary>
        public void HostSettingsChanged()
        {
            Gameplay.Settings.NightMinutes = Settings.NightMinutes;
            Gameplay.Settings.AiOmar = Settings.AiOmar;
            BroadcastRoster();
        }

        void OnRoster(int sender, NetReader r)
        {
            var state = (SessionState)r.ReadByte();
            var settings = MatchSettings.Read(r);
            int n = r.ReadByte();
            var list = new List<PlayerInfo>(n);
            for (int i = 0; i < n; i++) list.Add(PlayerInfo.Read(r));
            if (!IsHost)
            {
                Settings = settings;
                Players.Clear();
                Players.AddRange(list);
                if (State == SessionState.Connecting) SetState(SessionState.Lobby);
            }
            RosterChanged?.Invoke();
        }

        void OnLobbyReq(int sender, NetReader r)
        {
            if (!IsHost) return;
            var role = (PlayerRole)r.ReadByte();
            var skin = (CharacterSkin)r.ReadByte();
            bool ready = r.ReadBool();
            var p = Find(sender);
            if (p == null || State != SessionState.Lobby) return;
            if (role == PlayerRole.Omar)
            {
                var omar = FindOmar();
                if (omar == null || omar.Id == p.Id) { p.Role = PlayerRole.Omar; p.Skin = CharacterSkin.Omar; }
            }
            else if (role == PlayerRole.Prisoner)
            {
                if (skin > CharacterSkin.Prisoner4) skin = CharacterSkin.Prisoner1;
                bool taken = Players.Exists(o => o.Id != p.Id && o.IsPrisoner && o.Skin == skin);
                int prisoners = 0;
                foreach (var o in Players) if (o.IsPrisoner && o.Id != p.Id) prisoners++;
                if (!taken && prisoners < GameInfo.MaxPrisoners) { p.Role = PlayerRole.Prisoner; p.Skin = skin; }
            }
            else
            {
                p.Role = PlayerRole.Spectator;
            }
            p.Ready = ready || p.Id == 0;
            BroadcastRoster();
        }

        void OnStartMatch(int sender, NetReader r)
        {
            var settings = MatchSettings.Read(r);
            int n = r.ReadByte();
            var list = new List<PlayerInfo>(n);
            for (int i = 0; i < n; i++) list.Add(PlayerInfo.Read(r));
            Settings = settings;
            if (!IsHost) { Players.Clear(); Players.AddRange(list); }
            _loaded.Clear();
            _loadingSince = Time.unscaledTime;
            SetState(SessionState.Loading);
            try { _discovery.StopAdvertising(); } catch { }
            MatchStarting?.Invoke();
        }

        void OnLoadedReq(int sender, NetReader r)
        {
            if (!IsHost) return;
            _loaded.Add(sender);
            CheckAllLoaded();
        }

        void CheckAllLoaded()
        {
            if (!IsHost || State != SessionState.Loading) return;
            bool all = true;
            foreach (var p in Players)
                if (!p.IsBot && p.Connected && !_loaded.Contains(p.Id)) { all = false; break; }
            if (!all && Time.unscaledTime - _loadingSince < 45f) return;
            Begin(Msg.MatchBegin);
            SendToAll(NetChannel.Reliable);
        }

        void OnMatchBegin(int sender, NetReader r)
        {
            if (State != SessionState.Loading) return;
            SetState(SessionState.InMatch);
            MatchBegan?.Invoke();
        }

        /// <summary>Called by the match when the ending screen starts.</summary>
        public void MarkEnding() { if (State == SessionState.InMatch) SetState(SessionState.Ending); }

        void OnReturnToLobby(int sender, NetReader r)
        {
            if (IsHost)
            {
                Players.RemoveAll(p => p.IsBot || !p.Connected);
                foreach (var p in Players) p.Ready = p.Id == 0;
            }
            SetState(SessionState.Lobby);
            ReturnedToLobby?.Invoke();
            if (IsHost)
            {
                if (!Practice) { try { _discovery.StartAdvertising(BuildLanInfo()); } catch { } }
                BroadcastRoster();
            }
        }

        void OnKick(int sender, NetReader r)
        {
            string reason = r.ReadString();
            if (!IsHost) { LastError = reason; _pendingShutdown = reason; }
        }

        void SetState(SessionState s)
        {
            if (State == s) return;
            State = s;
            StateChanged?.Invoke(s);
        }

        // ================================================================== LAN

        LanSessionInfo BuildLanInfo()
        {
            int humans = 0;
            foreach (var p in Players) if (!p.IsBot) humans++;
            var host = Find(0);
            return new LanSessionInfo
            {
                HostName = host != null ? host.Name : "HOST",
                Port = _server != null ? _server.Port : Gameplay.Settings.Port,
                Players = humans,
                MaxPlayers = GameInfo.MaxPlayers,
                InLobby = State == SessionState.Lobby,
                Protocol = GameInfo.ProtocolVersion,
            };
        }

        void UpdateLan()
        {
            if (!IsHost || Practice || !_discovery.IsAdvertising) return;
            try { _discovery.UpdateAdvertisement(BuildLanInfo()); } catch { }
        }

        /// <summary>Local IPv4 addresses to show in the lobby so friends can connect.</summary>
        public static string LocalAddresses()
        {
            try
            {
                var list = new List<string>();
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            list.Add(ua.Address.ToString());
                }
                return list.Count > 0 ? string.Join("  ", list.ToArray()) : "127.0.0.1";
            }
            catch { return "127.0.0.1"; }
        }

        // ================================================================== loop

        void Update()
        {
            try
            {
                if (_server != null && _server.IsRunning) _server.Poll();
                if (_client != null) _client.Poll();
                _discovery.Poll();
            }
            catch (Exception e) { Debug.LogException(e); }

            if (_pendingShutdown != null)
            {
                string reason = _pendingShutdown;
                _pendingShutdown = null;
                Shutdown(reason);
                return;
            }

            if (IsHost && State == SessionState.Loading) CheckAllLoaded();
            if (IsHost && State == SessionState.Lobby && !Practice)
            {
                _advertiseTimer -= Time.unscaledDeltaTime;
                if (_advertiseTimer <= 0f) { _advertiseTimer = 2f; UpdateLan(); }
            }
        }
    }

    /// <summary>Routes transport warnings to the Unity console.</summary>
    static class NetLogBridge
    {
        static bool _done;
        public static void Install()
        {
            if (_done) return;
            _done = true;
            NetLogHook.Install(msg => Debug.LogWarning(msg));
        }
    }
}
