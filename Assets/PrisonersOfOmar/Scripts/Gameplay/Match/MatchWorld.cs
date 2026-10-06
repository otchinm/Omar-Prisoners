using System;
using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// One match on this machine: builds the level from the seed, creates every entity and avatar, receives
    /// host events and drives client-side presentation. The host additionally owns a <see cref="MatchHost"/>
    /// (authoritative rules) and the AI Omar.
    /// </summary>
    public sealed partial class MatchWorld : MonoBehaviour
    {
        public static MatchWorld Instance { get; private set; }

        public NetSession Session { get; private set; }
        public MapData Map { get; private set; }
        public MatchSettings Settings { get; private set; }
        public int Seed => Settings.Seed;
        public bool IsHost => Session != null && Session.IsHost;
        public int LocalId => Session != null ? Session.LocalId : -1;
        public MatchHost Host { get; private set; }

        // ---- clock (seconds since MatchBegin, synchronized from host snapshots)
        public bool Running { get; private set; }
        float _clock;
        public float Time => _clock;
        public float NightLength => Settings.NightMinutes * 60f;
        /// <summary>Admin: move the night forward (host; clients follow the snapshots' clock).</summary>
        public void AdminAddTime(float seconds) { if (IsHost) _clock += seconds; }
        public void AdminSetTime(float t) { if (IsHost) _clock = Mathf.Max(_clock, t); }
        /// <summary>0 at 01:00 AM .. 1 at 06:00 AM.</summary>
        public float NightProgress => Mathf.Clamp01(_clock / Mathf.Max(60f, NightLength));

        // ---- entities
        public readonly Dictionary<int, Avatar> Avatars = new Dictionary<int, Avatar>();
        public readonly Dictionary<int, PlayerStatus> Statuses = new Dictionary<int, PlayerStatus>();
        public readonly List<ItemEntity> Items = new List<ItemEntity>();
        public DoorEntity[] Doors = new DoorEntity[0];
        public HidingEntity[] Hiding = new HidingEntity[0];
        public CageEntity[] Cages = new CageEntity[0];
        public readonly List<TrapEntity> Traps = new List<TrapEntity>();
        public NoteEntity[] Notes = new NoteEntity[0];
        public readonly List<ObjectiveTarget> ObjectiveTargets = new List<ObjectiveTarget>();
        public readonly ObjectiveData Objectives = new ObjectiveData();
        public readonly HashSet<int> ChaseTargets = new HashSet<int>();
        public int[] ShelterCode = new int[4];
        public bool CarBatteryStartsDead { get; private set; }
        public bool PowerOn { get; private set; } = true;
        public EndingResult Ending { get; private set; }

        // ---- local
        public Avatar LocalAvatar { get; private set; }
        public PrisonerController LocalPrisoner { get; private set; }
        public OmarController LocalOmar { get; private set; }
        public SpectatorCamera Spectator { get; private set; }
        public LocalInventory Inventory { get; private set; }
        public PlayerStatus LocalStatus => StatusOf(LocalId);
        public PlayerInfo LocalInfo => Session != null ? Session.LocalPlayer : null;
        public bool LocalIsOmar => LocalInfo != null && LocalInfo.IsOmar;
        public ChaseAudio Chase { get; private set; }
        public ProximityFx Proximity { get; private set; }
        public AmbienceController Ambience { get; private set; }

        /// <summary>HUD messages (text, expiry).</summary>
        public readonly List<KeyValuePair<string, float>> Messages = new List<KeyValuePair<string, float>>();
        /// <summary>Omar's HUD pings: (world position, expiry realtime, kind 0 noise / 1 alarm / 2 trap caught / 3 sense / 4 spotted).</summary>
        public readonly List<OmarPing> Pings = new List<OmarPing>();

        public event Action<EndingResult> MatchEnded;

        Transform _dynamicRoot;
        float _sendTimer;
        GameObject _helicopter;
        PsxLight _heliLight;
        AudioSource _heliLoop, _radioLoop, _carIdle;
        GameObject _fireFx;
        readonly List<AudioSource> _emitters = new List<AudioSource>();
        readonly List<bool> _emitterPowered = new List<bool>();
        float _carDriveStart = -1f;
        Vector3 _carStartPos;
        Quaternion _carStartRot;
        int _localTeleportSeen = -1;

        // ================================================================== creation

        public static MatchWorld Create(NetSession session, Transform parent)
        {
            var go = new GameObject("MatchWorld");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<MatchWorld>();
            w.Build(session);
            return w;
        }

        void Build(NetSession session)
        {
            Instance = this;
            Session = session;
            Settings = session.Settings;
            Inventory = new LocalInventory(this);
            _dynamicRoot = new GameObject("Dynamic").transform;
            _dynamicRoot.SetParent(transform, false);

            Tuning.ApplyDifficulty(Settings.Difficulty);
            PsxEnvironment.Brightness = Tuning.Brightness;
            PsxEnvironment.GradeAmount = Tuning.LightGrade;
            AudioManager.ListenerIsOmar = false;
            PsxItemGlow.GlobalStrength = Tuning.GlowStrength;
            AdminState.Reset();
            AnomalySystem.Reset();
            AnomalySystem.Seed = Seed;
            AnomalySystem.TimeSource = () => _clock;
            VhsEffect.ResetTransient();

            try { Map = MapBuilder.Build(Seed, transform); }
            catch (Exception e) { Debug.LogException(e); }
            if (Map == null) Map = new MapData { Root = transform, Nav = new NavGraph(), OmarSpawn = new Pose(Vector3.zero, Quaternion.identity) };
            while (Map.PrisonerSpawns.Count < 4) Map.PrisonerSpawns.Add(new Pose(new Vector3(Map.PrisonerSpawns.Count * 1.5f, 1f, 0), Quaternion.identity));

            // (map anomaly zones are no longer registered: the PS1 warping stays a faint wobble, never a constant distortion)
            if (Map.Nav != null)
                Map.Nav.IsDoorBlocked = code =>
                    code == NavGraph.EdgeMainGate ? !Objectives.GateCut :
                    code == NavGraph.EdgeVehicleGate ? !Objectives.CarGone :
                    code == NavGraph.EdgeShelter ? !Objectives.ShelterOpen :
                    code == NavGraph.EdgeBreach ? !Objectives.Exploded : false;
            AnomalySystem.SetBaseline(0f);

            BuildEntities();
            SpawnItems();
            if (IsHost) Host = new MatchHost(this); // before avatars: the AI Omar needs it
            SpawnAvatars();
            RegisterHandlers();

            Chase = gameObject.AddComponent<ChaseAudio>();
            Proximity = gameObject.AddComponent<ProximityFx>();
            Ambience = gameObject.AddComponent<AmbienceController>();
            StartEmitters();

            Session.MatchBegan += OnMatchBegan;
            Session.PlayerLeft += OnPlayerLeft;
        }

        void OnDestroy()
        {
            if (Session != null)
            {
                Session.MatchBegan -= OnMatchBegan;
                Session.PlayerLeft -= OnPlayerLeft;
                UnregisterHandlers();
            }
            try { Grandma?.Destroy(); } catch { }
            Admin.OnMatchEnded();
            AnomalySystem.Reset();
            VhsEffect.ResetTransient();
            PsxEnvironment.Brightness = 1f;
            PsxEnvironment.GradeAmount = 0f;
            AudioManager.ListenerIsOmar = false;
            PsxItemGlow.GlobalStrength = 1f;
            AdminState.Reset();
            try { PsxFx.ClearBloodDecals(); } catch { }
            AudioManager.StopAllSfx();
            GameInput.SetCursorLocked(false);
            if (Instance == this) Instance = null;
        }

        void BuildEntities()
        {
            var rng = DeterministicRandom.For(Seed, "entities");
            // Only real door leaves stay on the Door layer (Omar passes through those and shoves them);
            // cage doors, gates and the shelter hatch must block him like walls.
            var leaves = new HashSet<Collider>();
            foreach (var di in Map.Doors) if (di.Leaf != null) leaves.Add(di.Leaf);
            if (Map.Root != null)
                foreach (var c in Map.Root.GetComponentsInChildren<Collider>(true))
                    if (c.gameObject.layer == Layers.Door && !leaves.Contains(c)) c.gameObject.layer = Layers.World;
            Doors = new DoorEntity[Map.Doors.Count];
            for (int i = 0; i < Doors.Length; i++) Doors[i] = new DoorEntity(i, Map.Doors[i]);
            Hiding = new HidingEntity[Map.HidingSpots.Count];
            for (int i = 0; i < Hiding.Length; i++) Hiding[i] = new HidingEntity(i, Map.HidingSpots[i]);
            BuildCages();
            try { BuildGrandma(); } catch (Exception e) { Debug.LogException(e); }
            try { BuildKitchen(); } catch (Exception e) { Debug.LogException(e); }
            try { BuildVent(); } catch (Exception e) { Debug.LogException(e); }
            try { BuildDrawers(); } catch (Exception e) { Debug.LogException(e); }
            BuildPuzzles();

            // pre-armed traps: a random subset of the candidate spots
            var wires = new List<TrapSpotInfo>();
            var bears = new List<TrapSpotInfo>();
            foreach (var t in Map.TrapSpots) (t.Kind == TrapKind.Tripwire ? wires : bears).Add(t);
            rng.Shuffle(wires);
            rng.Shuffle(bears);
            int nw = Mathf.Min(wires.Count, Tuning.StartWires), nb = Mathf.Min(bears.Count, Tuning.StartBears);
            for (int i = 0; i < nw; i++) Traps.Add(new TrapEntity(Traps.Count, TrapKind.Tripwire, wires[i].A, wires[i].B, _dynamicRoot));
            for (int i = 0; i < nb; i++) Traps.Add(new TrapEntity(Traps.Count, TrapKind.BearTrap, bears[i].A, bears[i].A, _dynamicRoot));

            // shelter code + notes
            for (int i = 0; i < 4; i++) ShelterCode[i] = rng.Range(i == 0 ? 1 : 0, 10);
            var spots = new List<int>();
            for (int i = 0; i < Map.NoteSpots.Count; i++) spots.Add(i);
            rng.Shuffle(spots);
            // the 4 code digits never go behind the shelter door they open
            var ordered = new List<int>(spots.Count);
            var later = new List<int>();
            foreach (int i in spots) (ordered.Count < 4 && Map.NoteSpots[i].Area != "Tunnel" ? ordered : later).Add(i);
            ordered.AddRange(later);
            spots = ordered;
            var lore = new List<string>(NoteTexts.Lore);
            rng.Shuffle(lore);
            Notes = new NoteEntity[Map.NoteSpots.Count];
            for (int k = 0; k < spots.Count; k++)
            {
                int idx = spots[k];
                var info = Map.NoteSpots[idx];
                string title, text;
                if (k < 4)
                {
                    title = info.OnWall ? "WRITTEN ON THE WALL" : "A TORN NOTE";
                    text = NoteTexts.CodeNote(k, ShelterCode[k], rng, info.OnWall);
                }
                else
                {
                    title = info.OnWall ? "SCRATCHED INTO THE WALL" : "A NOTE";
                    text = lore.Count > 0 ? lore[(k - 4) % lore.Count] : "...";
                }
                Notes[idx] = new NoteEntity(idx, info, title, text, _dynamicRoot);
            }

            CarBatteryStartsDead = rng.Chance(0.5f);
            Objectives.CarBatteryOk = !CarBatteryStartsDead;

            if (Map.MainGate != null) ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.Gate, Map.MainGate.Interact));
            if (Map.Car != null)
            {
                ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.CarFuel, Map.Car.FuelCap));
                ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.CarHood, Map.Car.Hood));
                ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.CarDriver, Map.Car.DriverDoor));
                if (Map.Car.PassengerDoors != null)
                    foreach (var c in Map.Car.PassengerDoors) ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.CarPassenger, c));
                if (Map.Car.Root != null) { _carStartPos = Map.Car.Root.position; _carStartRot = Map.Car.Root.rotation; }
            }
            if (Map.Radio != null)
            {
                ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.FuseBox, Map.Radio.FuseBox));
                ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.Radio, Map.Radio.RadioSet));
            }
            if (Map.FuelDepot != null) ObjectiveTargets.Add(new ObjectiveTarget(ObjectiveKind.Barrels, Map.FuelDepot.Barrels));
            foreach (var l in Map.RadioRoomLights) if (l != null) l.On = false;
        }

        void SpawnItems()
        {
            LockedDrawerSpots(out var keySpots, out var codeSpots);
            var placements = ItemSpawner.Place(Map, Seed, CarBatteryStartsDead, Tuning.SupplyMul, keySpots, codeSpots);
            foreach (var p in placements)
            {
                var e = new ItemEntity { Id = Items.Count, Type = p.Type, Charge = p.Charge };
                Items.Add(e);
                WorldItem.Create(e, p.Position, p.Yaw, _dynamicRoot);
            }
            // one lighter per prisoner (in slot 0), ids after the world items, in roster order
            foreach (var pl in Session.Players)
            {
                if (!pl.IsPrisoner) continue;
                var e = new ItemEntity { Id = Items.Count, Type = ItemType.Lighter, Charge = 1f, Holder = pl.Id, Slot = 0 };
                Items.Add(e);
                if (pl.Id == LocalId) Inventory.Set(0, e.Id);
            }
        }

        void StartEmitters()
        {
            foreach (var e in Map.SoundEmitters)
            {
                var src = AudioManager.Loop3D(e.Clip, e.Position, e.Volume, e.MaxDistance, AudioCategory.Ambience, null, 1.5f);
                _emitters.Add(src);
                _emitterPowered.Add(e.NeedsPower);
            }
        }

        // ================================================================== lookups

        public PlayerStatus StatusOf(int id) => Statuses.TryGetValue(id, out var s) ? s : null;
        public Avatar AvatarOf(int id) => Avatars.TryGetValue(id, out var a) ? a : null;
        public ItemEntity GetItem(int id) => id >= 0 && id < Items.Count ? Items[id] : null;
        public PlayerInfo InfoOf(int id) => Session.Find(id);

        public Avatar OmarAvatar
        {
            get
            {
                foreach (var kv in Avatars) if (kv.Value != null && kv.Value.IsOmar) return kv.Value;
                return null;
            }
        }

        public bool LocalLighterWithFuel()
        {
            int id = Inventory.Find(ItemType.Lighter);
            var it = GetItem(id);
            return it != null && it.Charge > 0.01f;
        }

        public void AddMessage(string text, float seconds = 4f)
        {
            Messages.Add(new KeyValuePair<string, float>(text, UnityEngine.Time.time + seconds));
            if (Messages.Count > 4) Messages.RemoveAt(0);
        }

        // ================================================================== match lifecycle

        void OnMatchBegan()
        {
            Running = true;
            _clock = 0f;
            AudioManager.Play2D(Snd.TapePlay, 0.8f, 1f, AudioCategory.Ui);
            VhsEffect.TriggerGlitch(1f, 0.6f);
            if (LocalIsOmar) AddMessage("YOU ARE OMAR. THEY ARE IN THE PENS. LET THEM RUN.", 6f);
            else AddMessage("YOU WAKE UP IN A CAGE...", 5f);
            Host?.OnBegin();
        }

        void OnPlayerLeft(int id) => Host?.OnPlayerLeft(id);

        void Update()
        {
            float dt = UnityEngine.Time.deltaTime;
            if (Running && Ending == null && !(IsHost && AdminState.ClockPaused)) _clock += dt;

            TickPlayer(dt);
            TickWorld(dt);
            TickPuzzles(dt);
            for (int i = 0; i < Cages.Length; i++) Cages[i].Tick(dt);
            TickMannequins();
            for (int i = 0; i < Traps.Count; i++) Traps[i].Tick(dt);

            for (int i = Messages.Count - 1; i >= 0; i--) if (Messages[i].Value < UnityEngine.Time.time) Messages.RemoveAt(i);
            for (int i = Pings.Count - 1; i >= 0; i--) if (Pings[i].Expire < UnityEngine.Time.time) Pings.RemoveAt(i);

            if (Running) SendLocalState(dt);
            Host?.Tick(dt);
            BloodTrails();
            UpdateObjectiveFx(dt);
            CheckLocalEscape();
        }

        readonly Dictionary<int, Vector3> _lastBlood = new Dictionary<int, Vector3>();

        /// <summary>Injured prisoners leave blood drops (everyone sees them - Omar can track them).</summary>
        void BloodTrails()
        {
            foreach (var kv in Avatars)
            {
                var a = kv.Value;
                if (a == null || a.IsOmar || !a.Visible) continue;
                var st = StatusOf(kv.Key);
                if (st == null || !st.Injured || st.Life != LifeState.Free) continue;
                Vector3 p = a.Position;
                if (!_lastBlood.TryGetValue(kv.Key, out var last) || Vector3.Distance(last, p) > 1.6f)
                {
                    _lastBlood[kv.Key] = p;
                    try { PsxFx.BloodDrop(p + Vector3.up * 0.3f); } catch { }
                }
            }
        }

        void SendLocalState(float dt)
        {
            _sendTimer -= dt;
            if (_sendTimer > 0f) return;
            _sendTimer = 0.05f;
            if (LocalAvatar != null && Session.Online && !IsHost)
            {
                var st = StatusOf(LocalId);
                if (st != null && (st.Life == LifeState.Escaped || st.Life == LifeState.Gone)) return;
                var w = Session.Begin(Msg.AvatarStateReq);
                LocalAvatar.State.Write(w);
                Session.SendToHost(NetChannel.Unreliable);
            }
        }

        // ================================================================== sending (client -> host requests)

        public void SendPickup(int itemId)
        {
            var w = Session.Begin(Msg.PickupReq);
            w.WriteShort((short)itemId);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendDrop(int itemId, Vector3 pos, float yaw, float charge)
        {
            var w = Session.Begin(Msg.DropReq);
            w.WriteShort((short)itemId);
            w.WriteVector3(pos);
            w.WriteAngle(yaw);
            w.WriteUnit(charge);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendThrow(int itemId, Vector3 from, Vector3 velocity)
        {
            var w = Session.Begin(Msg.ThrowReq);
            w.WriteShort((short)itemId);
            w.WriteVector3(from);
            w.WriteVector3(velocity);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendBottleImpact(int itemId, Vector3 pos)
        {
            var w = Session.Begin(Msg.BottleImpact);
            w.WriteShort((short)itemId);
            w.WriteVector3(pos);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendUse(UseTarget target, int targetId, int itemId, float charge = 1f)
        {
            var w = Session.Begin(Msg.UseReq);
            w.WriteByte((byte)target);
            w.WriteShort((short)targetId);
            w.WriteShort((short)itemId);
            w.WriteUnit(charge);
            Session.SendToHost(NetChannel.Reliable);
        }

        float _nextStruggleVoice;

        /// <summary>kind 0 = cage, 1 = bear trap.</summary>
        public void SendStruggle(byte kind)
        {
            var w = Session.Begin(Msg.StruggleReq);
            w.WriteByte(kind);
            Session.SendToHost(NetChannel.Reliable);
            AudioManager.Play2D(kind == 0 ? Snd.CageRattle : Snd.Struggle, 0.55f, UnityEngine.Random.Range(0.9f, 1.1f));
            // straining voice (not on every mash: the clip is longer than a key press)
            if (LocalAvatar != null && UnityEngine.Time.time >= _nextStruggleVoice)
            {
                _nextStruggleVoice = UnityEngine.Time.time + 1.6f;
                AudioManager.Play2D(LocalAvatar.Voice(VoiceLine.Struggle), 0.5f);
            }
        }

        public void SendAction(CharacterAction a)
        {
            var w = Session.Begin(Msg.ActionReq);
            w.WriteByte((byte)a);
            Session.SendToHost(NetChannel.Reliable);
        }

        /// <summary>Noise made by the local prisoner (Omar hears it within radius).</summary>
        public void SendNoise(Vector3 pos, float radius)
        {
            if (radius <= 0.5f) return;
            var w = Session.Begin(Msg.NoiseReq);
            w.WriteVector3(pos);
            w.WriteByte((byte)Mathf.Clamp(Mathf.RoundToInt(radius), 1, 255));
            Session.SendToHost(NetChannel.Unreliable);
        }

        public void SendTrapTrigger(int trapId)
        {
            var w = Session.Begin(Msg.TrapTriggerReq);
            w.WriteShort((short)trapId);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendTrapPlace(TrapKind kind, Vector3 a, Vector3 b)
        {
            var w = Session.Begin(Msg.TrapPlaceReq);
            w.WriteByte((byte)kind);
            w.WriteVector3(a);
            w.WriteVector3(b);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendAttack(int targetId)
        {
            var w = Session.Begin(Msg.AttackReq);
            w.WriteByte((byte)Mathf.Clamp(targetId, 0, 255));
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendDetect(int targetId, bool spotted)
        {
            var w = Session.Begin(Msg.DetectReq);
            w.WriteByte((byte)targetId);
            w.WriteBool(spotted);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendScream()
        {
            Session.Begin(Msg.ScreamReq);
            Session.SendToHost(NetChannel.Reliable);
        }

        public void SendEscape(EscapeRoute route)
        {
            var w = Session.Begin(Msg.EscapeReq);
            w.WriteByte((byte)route);
            Session.SendToHost(NetChannel.Reliable);
        }

        // ================================================================== UI hooks

        public void OpenNote(NoteEntity note) => UI.UIManager.Instance?.Push(new UI.NoteScreen(note.Title, note.Text));

        // ================================================================== escape zones (local)

        float _escapeCooldown;

        void CheckLocalEscape()
        {
            if (LocalAvatar == null || LocalIsOmar || !Running || Ending != null) return;
            var st = LocalStatus;
            if (st == null || st.Life != LifeState.Free) return;
            _escapeCooldown -= UnityEngine.Time.deltaTime;
            if (_escapeCooldown > 0) return;
            Vector3 p = LocalAvatar.Position + Vector3.up * 0.5f;
            EscapeRoute route = EscapeRoute.None;
            if ((Objectives.GateCut || Objectives.CarGone) && Map.MainGate != null && Map.MainGate.ExitZone.Contains(p)) route = EscapeRoute.Road;
            else if (Objectives.ShelterOpen && Map.Shelter != null && Map.Shelter.TunnelExitZone.Contains(p)) route = EscapeRoute.Shelter;
            else if (Objectives.RescuePresent && Map.Radio != null && Map.Radio.LandingZone.Contains(p)) route = EscapeRoute.Radio;
            else if (Objectives.Exploded && Map.FuelDepot != null && Map.FuelDepot.BreachExitZone.Contains(p)) route = EscapeRoute.Fire;
            if (route != EscapeRoute.None)
            {
                SendEscape(route);
                _escapeCooldown = 1f;
            }
        }

        // ================================================================== objective presentation

        void UpdateObjectiveFx(float dt)
        {
            // car escape drive
            if (_carDriveStart >= 0f && Map.Car != null && Map.Car.Root != null && Map.Car.DrivePath != null && Map.Car.DrivePath.Length > 0)
            {
                float t = _clock - _carDriveStart;
                float dist = t < 1.5f ? 0.5f * 2f * t * t : 2.25f + (t - 1.5f) * 9f; // accelerate then cruise ~32 km/h
                Vector3 pos, dir;
                SamplePath(Map.Car.DrivePath, _carStartPos, dist, out pos, out dir);
                if (dir.sqrMagnitude > 0.001f)
                {
                    var target = Quaternion.LookRotation(dir, Vector3.up);
                    Map.Car.Root.SetPositionAndRotation(pos, Quaternion.Slerp(Map.Car.Root.rotation, target, dt * 4f));
                }
                if (Map.Car.VehicleGateLeft != null && Vector3.Distance(pos, Map.Car.VehicleGateLeft.position) < 7f)
                    Map.Car.VehicleGateLeft.localRotation = Quaternion.Slerp(Map.Car.VehicleGateLeft.localRotation, Quaternion.Euler(0, -110, 0), dt * 6f);
                if (Map.Car.VehicleGateRight != null && Vector3.Distance(pos, Map.Car.VehicleGateRight.position) < 7f)
                    Map.Car.VehicleGateRight.localRotation = Quaternion.Slerp(Map.Car.VehicleGateRight.localRotation, Quaternion.Euler(0, 110, 0), dt * 6f);
                if (Map.Car.VehicleGateChain != null && t > 2f) Map.Car.VehicleGateChain.SetActive(false);
                if (t > 1.5f && Map.Car.VehicleGateBlockers != null)
                    foreach (var c in Map.Car.VehicleGateBlockers) if (c != null) c.enabled = false;
            }
            if (_carIdle != null && Map.Car != null && Map.Car.Root != null) _carIdle.transform.position = Map.Car.Root.position;
        }

        static void SamplePath(Vector3[] path, Vector3 start, float dist, out Vector3 pos, out Vector3 dir)
        {
            Vector3 prev = start;
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 next = path[i];
                float seg = Vector3.Distance(prev, next);
                if (dist <= seg || i == path.Length - 1)
                {
                    float t = seg > 0.001f ? Mathf.Clamp01(dist / seg) : 1f;
                    pos = Vector3.Lerp(prev, next, t);
                    dir = next - prev; dir.y = 0;
                    return;
                }
                dist -= seg;
                prev = next;
            }
            pos = prev; dir = Vector3.forward;
        }

        // ================================================================== ending

        internal void ShowEnding(EndingResult result)
        {
            if (Ending != null) return;
            Ending = result;
            Running = false;
            Session.MarkEnding();
            Chase.StopAll(0.5f);
            MatchEnded?.Invoke(result);
        }
    }

    public struct OmarPing
    {
        public Vector3 Position;
        public float Expire;
        public int Kind; // 0 noise, 1 alarm, 2 trap caught, 3 sensed prisoner, 4 spotted
        public float Radius;
    }
}
