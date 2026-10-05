using System.Collections.Generic;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Host-side bot that plays Omar when no human does (or after Omar's player disconnects).
    /// Patrols the nav graph, hears noise and alarms, chases what it sees, attacks, screams,
    /// searches hiding spots, smashes boarded doors and strings tripwires.
    /// </summary>
    public sealed class OmarAI : MonoBehaviour
    {
        enum Mode { Waking, Patrol, Investigate, Chase, Search, Chop, Punish }

        MatchWorld W;
        MatchHost H;
        Avatar A;
        CharacterMotor M;
        DetectionSystem D;
        Mode _mode = Mode.Waking;

        List<Vector3> _path = new List<Vector3>();
        int _pathIdx;
        Vector3 _goal;
        bool _running;
        float _repath;
        float _yaw, _pitch;
        float _modeTimer, _waitTimer;
        int _target = -1;
        Vector3 _lastKnown;
        float _attackCooldown, _windup = -1f, _recoverUntil;
        float _nextScreamAt;
        float _stunUntil;
        float _noiseScore;
        Vector3 _noisePos;
        readonly Dictionary<int, int> _sawHide = new Dictionary<int, int>();
        readonly List<int> _searchQueue = new List<int>();
        float _trapTimer = 75f;
        int _pendingTrap = -1;
        float _pendingTrapUntil;
        float _stuckTimer;
        Vector3 _stuckRef;
        float _smashTimer = -1f;
        int _smashDoor = -1;
        float _stepDist, _bob;
        float _speedCur;
        // butcher routine at the kitchen table
        float _nextChopAt = 75f, _chopUntil, _chopNext, _chopLook, _chopApproachT;
        bool _chopApproach;
        bool _chopping;
        int _revenge = -1;
        float _revengeAt;
        // a caged prisoner rattled the lock and he heard it: he goes to kill them in their cage
        int _punishCage = -1;
        float _punishSwingAt = -1f, _punishKillAt = -1f;

        public static OmarAI Attach(Avatar a, MatchWorld w)
        {
            var ai = a.gameObject.AddComponent<OmarAI>();
            ai.W = w;
            ai.H = w.Host;
            ai.A = a;
            ai.M = new CharacterMotor(a.gameObject, 0.36f, 1.95f, 1.2f);
            ai.D = new DetectionSystem(w);
            ai._yaw = a.State.Yaw;
            ai._stuckRef = a.transform.position;
            w.Host?.RegisterAI(ai);
            return ai;
        }

        // ------------------------------------------------------------------ host notifications

        public void Stun(float seconds) => _stunUntil = Mathf.Max(_stunUntil, W.Time + seconds);
        public void ClearStun() => _stunUntil = 0f;

        /// <summary>Admin: go for this prisoner now.</summary>
        public void AdminHunt(int id)
        {
            var av = W.AvatarOf(id);
            var st = W.StatusOf(id);
            if (av == null || st == null || st.Life != LifeState.Free) return;
            D.ForceSpot(id, av.Position, OnDetect);
        }

        /// <summary>Admin: off to the butcher table.</summary>
        public void AdminChop() { if (W.Map.Kitchen != null) StartChop(); }

        /// <summary>Admin: move the body instantly.</summary>
        public void TeleportTo(Vector3 p)
        {
            M.Teleport(p, _yaw);
            _path.Clear();
            _stuckRef = p;
            if (_mode == Mode.Chop) { _mode = Mode.Patrol; PickPatrol(); }
        }

        /// <summary>A prisoner smashed him from behind: once he can see straight again he goes for them.</summary>
        public void OnHitBy(int prisoner, float stunSeconds)
        {
            _revenge = prisoner;
            _revengeAt = W.Time + stunSeconds;
            _chopUntil = 0f;
        }

        public void OnNoise(Vector3 pos, float radius)
        {
            if (_mode == Mode.Chase || _mode == Mode.Waking) return;
            if (_chopping) radius *= 0.5f; // the chopping drowns out small sounds
            if (AdminState.OmarDeaf) return;
            float d = Vector3.Distance(pos, A.Position);
            if (d > radius * 1.3f) return;
            float score = radius / Mathf.Max(1f, d);
            if (_mode == Mode.Investigate && score < _noiseScore * 0.8f) return;
            _noiseScore = score;
            _noisePos = pos;
            StartInvestigate(pos, radius > 12f || d < 12f);
        }

        /// <summary>A caged prisoner clanks the lock: the closer he is, the likelier he notices and comes to deal with them.</summary>
        public void OnCageRattle(int cage, Vector3 pos)
        {
            if (_mode == Mode.Waking || _mode == Mode.Punish || (_mode == Mode.Chase && _target >= 0)) return;
            if (cage < 0 || cage >= W.Cages.Length) return;
            float d = Vector3.Distance(pos, A.Position);
            float chance = Mathf.Lerp(Tuning.CageNoticeNear, 0f, d / 36f) * Tuning.OmarHearingMul * (_chopping ? 0.6f : 1f);
            if (Random.value >= chance) return;
            _mode = Mode.Punish;
            _punishCage = cage;
            _punishSwingAt = _punishKillAt = -1f;
            _running = d > 6f;
            _modeTimer = 45f;
            SetGoal(W.Cages[cage].Info.Outside.position);
        }

        void TickPunish(float dt)
        {
            var c = _punishCage >= 0 && _punishCage < W.Cages.Length ? W.Cages[_punishCage] : null;
            if (c == null) { _mode = Mode.Patrol; PickPatrol(); return; }
            if (_punishKillAt < 0f && (c.Open || c.Occupant < 0))
            {
                // got out before he came: look for them around the pens
                _punishCage = -1;
                StartSearch(c.Info.Outside.position);
                return;
            }
            Vector3 to = c.Info.Outside.position - A.Position; to.y = 0f;
            if (_punishSwingAt < 0f)
            {
                _modeTimer -= dt;
                if (to.magnitude < 1.2f || (Arrived() && to.magnitude < 2.4f))
                {
                    _path.Clear();
                    _punishSwingAt = W.Time + 0.9f;   // stands and looks at them through the bars for a moment
                    H.BroadcastAction(A.Id, CharacterAction.Search);
                }
                else if (_modeTimer <= 0f || (Arrived() && to.magnitude >= 2.4f)) { _punishCage = -1; _mode = Mode.Patrol; PickPatrol(); }
                return;
            }
            Vector3 look = c.Info.Inside.position - A.Position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg, 240f * dt);
            if (_punishKillAt < 0f && W.Time >= _punishSwingAt)
            {
                H.BroadcastAction(A.Id, CharacterAction.Attack);
                _punishKillAt = W.Time + Tuning.AttackWindup;
            }
            if (_punishKillAt > 0f && W.Time >= _punishKillAt)
            {
                H.PunishCage(A.Id, _punishCage);
                _punishCage = -1;
                _punishKillAt = -1f;
                _recoverUntil = W.Time + Tuning.AttackRecover + 0.8f;
                _mode = Mode.Patrol;
                _waitTimer = 2f;
            }
        }

        public void OnAlarm(Vector3 pos)
        {
            if (_mode == Mode.Waking) return;
            if (_mode == Mode.Chase && _target >= 0) return;
            _noiseScore = 99f;
            StartInvestigate(pos, true);
        }

        public void OnSawHide(int prisoner, int spot)
        {
            if (D.IsSpotted(prisoner) || (D.Meter(prisoner) > 0.5f && Vector3.Distance(A.Position, W.Hiding[spot].InteractPoint) < 18f))
            {
                _sawHide[prisoner] = spot;
                if (!_searchQueue.Contains(spot)) _searchQueue.Insert(0, spot);
            }
        }

        float _rushUntil;

        public int OmarId => A != null ? A.Id : -1;

        /// <summary>Host: the bed this AI checked flipped over on a prisoner - after them, faster for a moment.</summary>
        public void OnFlippedBed(int prisoner, Vector3 pos)
        {
            _rushUntil = W.Time + Tuning.OmarRushSeconds;
            D.ForceSpot(prisoner, pos, OnDetect);
        }

        // ------------------------------------------------------------------ main loop

        void Update()
        {
            if (W == null || H == null || !W.Running || W.Ending != null) { Publish(0f); return; }
            float dt = Time.deltaTime;
            if (W.Time < Tuning.OmarIntroSeconds) { _mode = Mode.Waking; Publish(0f); return; }
            if (_mode == Mode.Waking) { _mode = Mode.Patrol; PickPatrol(); }

            Vector3 eye = A.EyePosition;
            Vector3 fwd = Quaternion.Euler(_pitch, _yaw, 0) * Vector3.forward;
            D.SightScale = _chopping ? 0.45f : 1f; // busy with the meat, eyes on the block
            D.Tick(dt, eye, fwd, AdminState.OmarBlind, OnDetect);

            if (W.Time < _stunUntil || AdminState.OmarFrozen || W.Time < AdminState.OmarSleepUntil) { Publish(M.Move(Vector3.zero, dt)); return; }
            if (W.Time < _recoverUntil) { _attackCooldown -= dt; Publish(M.Move(Vector3.zero, dt)); return; } // stands after a swing
            if (_revenge >= 0 && W.Time >= _revengeAt)
            {
                var av = W.AvatarOf(_revenge);
                var rs = W.StatusOf(_revenge);
                if (av != null && rs != null && rs.Life == LifeState.Free && !rs.Hidden) D.ForceSpot(_revenge, av.Position, OnDetect);
                _revenge = -1;
            }

            _attackCooldown -= dt;
            if (_windup >= 0f)
            {
                _windup -= dt;
                if (_windup < 0f) { H.DoAttack(A.Id, _target >= 0 ? _target : 255); _recoverUntil = W.Time + Tuning.AttackRecover; }
            }

            switch (_mode)
            {
                case Mode.Patrol: TickPatrol(dt); break;
                case Mode.Investigate: TickInvestigate(dt); break;
                case Mode.Chase: TickChase(dt); break;
                case Mode.Search: TickSearch(dt); break;
                case Mode.Chop: TickChop(dt); break;
                case Mode.Punish: TickPunish(dt); break;
            }
            if (_chopping && _mode != Mode.Chop) { _chopping = false; H.StopChopping(A.Id); }

            _trapTimer -= dt;
            if (_trapTimer <= 0f) { _trapTimer = Random.Range(70f, 130f); if (!TryPlaceTrap()) _trapTimer = Random.Range(10f, 18f); }
            if (_pendingTrap >= 0 && (_mode != Mode.Patrol || W.Time > _pendingTrapUntil)) _pendingTrap = -1;
            if (_pendingTrap >= 0 && Vector3.Distance(W.Map.TrapSpots[_pendingTrap].A, A.Position) < 4.5f)
            {
                int i = _pendingTrap;
                _pendingTrap = -1;
                PlaceTrapAt(i);
            }

            float speed = FollowPath(dt);
            Publish(speed);
        }

        void OnDetect(int id, bool spotted)
        {
            H.DoDetect(id, spotted);
            if (spotted)
            {
                if (_mode != Mode.Chase || _target < 0)
                {
                    _target = id;
                    _mode = Mode.Chase;
                    _repath = 0f;
                    _nextScreamAt = W.Time + Random.Range(14f, 24f);
                }
            }
            else if (id == _target)
            {
                _lastKnown = D.LastKnown.TryGetValue(id, out var p) ? p : A.Position;
                _target = -1;
                if (!Retarget()) StartSearch(_lastKnown);
            }
        }

        /// <summary>Switch the chase to another prisoner the detector still has spotted (free and not hidden).</summary>
        bool Retarget()
        {
            foreach (var id in D.Spotted)
            {
                var s = W.StatusOf(id);
                if (id == _target || s == null || s.Life != LifeState.Free || s.Hidden || W.AvatarOf(id) == null) continue;
                _target = id;
                _mode = Mode.Chase;
                _repath = 0f;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ modes

        void PickPatrol()
        {
            var nav = W.Map.Nav;
            _running = false;
            // now and then he goes back to the kitchen to butcher meat
            if (W.Map.Kitchen != null && W.Time >= _nextChopAt && Random.value < 0.7f) { StartChop(); return; }
            if (nav == null || nav.Nodes.Count == 0) { SetGoal(A.Position + Random.insideUnitSphere.WithY(0) * 10f); return; }
            Vector3 dest = A.Position;
            // sometimes check on the pens or the objectives
            float r = Random.value;
            if (r < 0.25f && W.Cages.Length > 0)
            {
                // check on one of the occupied cages (they are spread over several rooms)
                int caged = 0, pick = -1;
                for (int i = 0; i < W.Cages.Length; i++)
                    if (W.Cages[i].Occupant >= 0 && Random.Range(0, ++caged) == 0) pick = i;
                if (pick >= 0) { SetGoal(W.Cages[pick].Info.Outside.position); return; }
            }
            if (r < 0.45f)
            {
                var pts = ObjectivePoints();
                if (pts.Count > 0) { SetGoal(pts[Random.Range(0, pts.Count)]); return; }
            }
            for (int tries = 0; tries < 12; tries++)
            {
                var n = nav.Nodes[Random.Range(0, nav.Nodes.Count)];
                float d = Vector3.Distance(n, A.Position);
                if (d > 12f && d < 70f) { dest = n; break; }
                dest = n;
            }
            SetGoal(dest);
        }

        List<Vector3> ObjectivePoints()
        {
            var list = new List<Vector3>();
            var m = W.Map;
            if (m.MainGate != null && m.MainGate.Interact != null) list.Add(m.MainGate.Interact.bounds.center);
            if (m.Car != null && m.Car.Root != null) list.Add(m.Car.Root.position);
            if (m.Radio != null && m.Radio.RadioSet != null) list.Add(m.Radio.RadioSet.bounds.center);
            if (m.Radio != null && m.Radio.FuseBox != null) list.Add(m.Radio.FuseBox.bounds.center);
            if (m.FuelDepot != null && m.FuelDepot.Barrels != null) list.Add(m.FuelDepot.Barrels.bounds.center);
            if (m.Shelter != null && m.Shelter.Keypad != null) list.Add(m.Shelter.Keypad.bounds.center);
            return list;
        }

        void TickPatrol(float dt)
        {
            if (_waitTimer > 0f) { _waitTimer -= dt; if (_waitTimer <= 0f) PickPatrol(); return; }
            if (Arrived())
            {
                _waitTimer = Random.Range(0.8f, 3f);
                if (Random.value < 0.35f) H.BroadcastAction(A.Id, CharacterAction.Search);
                // peek into a close hiding spot now and then
                int spot = NearestHidingSpot(5f);
                if (spot >= 0 && Random.value < 0.25f) { _searchQueue.Add(spot); StartSearch(A.Position); }
            }
        }

        void StartChop()
        {
            _mode = Mode.Chop;
            _running = false;
            _chopping = false;
            _chopApproach = false;
            _nextChopAt = W.Time + Random.Range(70f, 120f);
            SetGoal(W.Map.Kitchen.ChopPose.position);
        }

        void TickChop(float dt)
        {
            var k = W.Map.Kitchen;
            Vector3 to = k.ChopPose.position - A.Position; to.y = 0f;
            if (!_chopping)
            {
                // the nav arrival radius would leave him a metre short of the table: the last steps go straight to the block
                if (!_chopApproach && to.magnitude < 2.6f && Mathf.Abs(k.ChopPose.position.y - A.Position.y) < 1f)
                {
                    _chopApproach = true;
                    _chopApproachT = 0f;
                    _path.Clear();
                }
                if (_chopApproach)
                {
                    _chopApproachT += dt;
                    if (to.magnitude < 0.1f || _chopApproachT > 3.5f)
                    {
                        _chopApproach = false;
                        _chopping = true;
                        H.StartChopping(A.Id);
                        _chopUntil = W.Time + Random.Range(30f, 50f);
                        _chopNext = W.Time + 0.8f;
                        _chopLook = W.Time + Random.Range(6f, 11f);
                    }
                }
                else if (Arrived()) { _mode = Mode.Patrol; PickPatrol(); }
                return;
            }
            float tableYaw = k.ChopPose.rotation.eulerAngles.y;
            if (W.Time < _chopLook - 1.8f || W.Time > _chopLook) _yaw = Mathf.MoveTowardsAngle(_yaw, tableYaw, 220f * dt);
            if (W.Time > _chopLook)
            {
                // straightens up and looks around the kitchen for a moment
                _chopLook = W.Time + Random.Range(7f, 13f);
                _chopNext = W.Time + 2.2f;
                H.BroadcastAction(A.Id, CharacterAction.Search);
            }
            if (W.Time >= _chopNext)
            {
                _chopNext = W.Time + Random.Range(1.05f, 1.6f);
                H.Chop(A.Id);
            }
            if (W.Time >= _chopUntil) { _mode = Mode.Patrol; PickPatrol(); }
        }

        void StartInvestigate(Vector3 pos, bool run)
        {
            _mode = Mode.Investigate;
            _running = run;
            _modeTimer = 25f;
            SetGoal(pos);
        }

        void TickInvestigate(float dt)
        {
            _modeTimer -= dt;
            if (Arrived() || _modeTimer <= 0f)
            {
                _noiseScore = 0f;
                H.BroadcastAction(A.Id, CharacterAction.Search);
                foreach (var s in HidingSpotsWithin(_noisePos, 7f)) if (Random.value < 0.45f && !_searchQueue.Contains(s)) _searchQueue.Add(s);
                StartSearch(A.Position);
            }
        }

        void StartSearch(Vector3 around)
        {
            _mode = Mode.Search;
            _modeTimer = 14f;
            _running = false;
            foreach (var s in HidingSpotsWithin(around, 9f))
                if (!_searchQueue.Contains(s) && Random.value < 0.35f) _searchQueue.Add(s);
            NextSearchStep(around);
        }

        void NextSearchStep(Vector3 around)
        {
            while (_searchQueue.Count > 0)
            {
                SetGoal(W.Hiding[_searchQueue[0]].Info.ExitPose.position);
                if (_path.Count > 0) return;
                _searchQueue.RemoveAt(0);   // can't reach that spot right now
            }
            var nav = W.Map.Nav;
            Vector3 p = around;
            for (int tries = 0; tries < 6; tries++)
            {
                p = around + new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
                if (nav == null || nav.Nodes.Count == 0) break;
                p = nav.Nodes[nav.Nearest(p)];
                if (nav.FindPath(A.Position, p).Count > 0) break;   // skip points behind closed gates / the shelter door
                p = A.Position;
            }
            SetGoal(p);
        }

        void TickSearch(float dt)
        {
            _modeTimer -= dt;
            if (_searchQueue.Count > 0)
            {
                int s = _searchQueue[0];
                var h = W.Hiding[s];
                if (Vector3.Distance(A.Position, h.InteractPoint) < 2.6f)
                {
                    FaceTowards(h.InteractPoint);
                    _searchQueue.RemoveAt(0);
                    H.BroadcastAction(A.Id, CharacterAction.Search);
                    int found = H.DoSearch(A.Id, s);
                    // a bed: he drops to look under it, and if someone is there he stays busy flipping it
                    if (h.IsBed) _recoverUntil = W.Time + (found >= 0 ? Tuning.BedFlipAt + Tuning.BedEscapeGrace : Tuning.BedPeekTime);
                    foreach (var kv in new List<KeyValuePair<int, int>>(_sawHide)) if (kv.Value == s) _sawHide.Remove(kv.Key);
                    if (found >= 0)
                    {
                        D.ForceSpot(found, A.Position, OnDetect);
                        return;
                    }
                    _waitTimer = 0.8f;
                    NextSearchStep(A.Position);
                    return;
                }
            }
            if (_waitTimer > 0f) { _waitTimer -= dt; return; }
            if (_modeTimer < -8f) { _searchQueue.Clear(); _mode = Mode.Patrol; PickPatrol(); return; }
            if (Arrived())
            {
                if (_modeTimer <= 0f && _searchQueue.Count == 0) { _mode = Mode.Patrol; PickPatrol(); }
                else { _waitTimer = Random.Range(0.6f, 1.6f); NextSearchStep(A.Position); }
            }
        }

        void TickChase(float dt)
        {
            var target = _target >= 0 ? W.AvatarOf(_target) : null;
            var st = _target >= 0 ? W.StatusOf(_target) : null;
            if (target == null || st == null || st.Life != LifeState.Free)
            {
                _target = -1;
                if (Retarget()) return;
                _mode = Mode.Patrol;
                PickPatrol();
                return;
            }
            if (st.Hidden)
            {
                _sawHide[_target] = st.HidingSpot;
                if (!_searchQueue.Contains(st.HidingSpot)) _searchQueue.Insert(0, st.HidingSpot);
                int spot = st.HidingSpot;
                _target = -1;
                if (Retarget()) return;
                StartSearch(W.Hiding[spot].InteractPoint);
                return;
            }
            _running = true;
            Vector3 tp = target.Position;
            float d = Vector3.Distance(A.Position, tp);
            bool los = !Physics.Linecast(A.EyePosition, target.ChestPosition, Layers.SightBlockers, QueryTriggerInteraction.Ignore);
            if (los && d < 8f)
            {
                _path.Clear();
                _goal = tp;
            }
            else
            {
                _repath -= dt;
                if (_repath <= 0f) { _repath = 0.6f; SetGoal(tp); }
            }
            if (d < Tuning.AttackRange - 0.1f && _attackCooldown <= 0f && _windup < 0f)
            {
                FaceTowards(tp);
                _attackCooldown = Tuning.AttackCooldown + Random.Range(0f, 0.4f);
                _windup = Tuning.AttackWindup;
                H.BroadcastAction(A.Id, CharacterAction.Attack);
            }
            if (W.Time >= _nextScreamAt)
            {
                _nextScreamAt = W.Time + Random.Range(15f, 26f);
                TryScream();
            }
        }

        void TryScream()
        {
            if (H.DoScream(A.Id)) { }
        }

        /// <summary>Picks the nearest free trap spot: places it right away when close, otherwise walks there (patrol) and places it on arrival.</summary>
        bool TryPlaceTrap()
        {
            var spots = W.Map.TrapSpots;
            if (spots.Count == 0) return false;
            int best = -1; float bestD = 22f;
            for (int i = 0; i < spots.Count; i++)
            {
                float d = Vector3.Distance(spots[i].A, A.Position);
                if (d < bestD && !TrapExistsNear(spots[i].A)) { bestD = d; best = i; }
            }
            if (best < 0) return false;
            if (bestD < 4.5f) return PlaceTrapAt(best);
            if (_mode != Mode.Patrol) return false;
            _pendingTrap = best;
            _pendingTrapUntil = W.Time + 25f;
            SetGoal(spots[best].A);
            return _path.Count > 0;
        }

        bool PlaceTrapAt(int spot)
        {
            var s = W.Map.TrapSpots[spot];
            if (TrapExistsNear(s.A) || !H.PlaceTrap(A.Id, s.Kind, s.A, s.Kind == TrapKind.Tripwire ? s.B : s.A)) return false;
            FaceTowards(s.A);
            H.BroadcastAction(A.Id, CharacterAction.PlaceTrap);
            return true;
        }

        bool TrapExistsNear(Vector3 p)
        {
            foreach (var t in W.Traps) if (t.State == TrapState.Armed && Vector3.Distance(t.A, p) < 1.5f) return true;
            return false;
        }

        // ------------------------------------------------------------------ movement

        void SetGoal(Vector3 goal)
        {
            _goal = goal;
            _path.Clear();
            _pathIdx = 0;
            var nav = W.Map.Nav;
            if (nav != null && nav.Nodes.Count > 1)
            {
                var p = nav.FindPath(A.Position, goal);
                if (p != null) _path = p;
                if (_path.Count == 0 && _mode != Mode.Chase)
                {
                    // unreachable right now (behind a closed gate / the shelter door): stay put, the mode picks something else
                    _goal = A.Position;
                    _waitTimer = 0.5f;
                    return;
                }
            }
            if (_path.Count == 0) _path.Add(goal);
        }

        bool Arrived() => _path.Count == 0 || (_pathIdx >= _path.Count) || (GeoUtil.FlatDistance(A.Position, _goal) < 1.0f && Mathf.Abs(A.Position.y - _goal.y) < 1.8f);

        float FollowPath(float dt)
        {
            if (_mode == Mode.Chop && (_chopApproach || _chopping) && W.Map.Kitchen != null) return SettleAtTable(dt);
            Vector3 target = _goal;
            if (_path.Count > 0 && _pathIdx < _path.Count)
            {
                target = _path[_pathIdx];
                if (GeoUtil.FlatDistance(A.Position, target) < 0.75f && Mathf.Abs(A.Position.y - target.y) < 1.8f)
                {
                    _pathIdx++;
                    if (_pathIdx < _path.Count) target = _path[_pathIdx];
                }
            }
            Vector3 to = target - A.Position; to.y = 0;
            bool moving = to.magnitude > 0.4f && !(Arrived() && _mode != Mode.Chase) && _windup < 0f && _smashTimer < 0f;
            Vector3 vel = Vector3.zero;
            if (moving)
            {
                float targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                _yaw = Mathf.MoveTowardsAngle(_yaw, targetYaw, 360f * dt);
                // no human sprint: the heavy walk just speeds up (ramps to full run over ~1.2 s, slows down faster)
                float want = _running ? Tuning.OmarRunSpeed * 0.93f : Tuning.OmarWalkSpeed;
                if (W.Time < _rushUntil) want *= Tuning.OmarRushMul;
                _speedCur = Mathf.MoveTowards(Mathf.Max(_speedCur, Tuning.OmarWalkSpeed * 0.6f), want, (want > _speedCur ? Tuning.OmarAcceleration : Tuning.OmarAcceleration * 2f) * dt);
                float speed = _speedCur;
                float align = Mathf.Clamp01(1f - Mathf.Abs(Mathf.DeltaAngle(_yaw, targetYaw)) / 90f);
                vel = Quaternion.Euler(0, _yaw, 0) * Vector3.forward * speed * Mathf.Max(0.25f, align);
                HandleDoors(vel);
            }
            if (_smashTimer >= 0f)
            {
                _smashTimer -= dt;
                if (_smashTimer < 0f && _smashDoor >= 0) { H.SmashBoards(A.Id, _smashDoor); _smashDoor = -1; }
            }
            float moved = M.Move(vel, dt);
            A.transform.rotation = Quaternion.Euler(0, _yaw, 0);

            // stuck handling
            _stuckTimer += dt;
            if (_stuckTimer > 1.6f)
            {
                if (moving && Vector3.Distance(_stuckRef, A.Position) < 0.35f)
                {
                    if (_pathIdx < _path.Count - 1) _pathIdx++;
                    else if (_mode == Mode.Patrol) PickPatrol();
                    else if (_mode == Mode.Search)
                    {
                        if (_searchQueue.Count > 0) _searchQueue.RemoveAt(0);
                        NextSearchStep(A.Position);
                    }
                    else if (_mode == Mode.Investigate) _modeTimer = 0f;
                    else SetGoal(_goal);
                }
                _stuckTimer = 0f;
                _stuckRef = A.Position;
            }

            // footsteps (host local audio is handled by the avatar's animator footstep events)
            _stepDist += moved * dt;
            _bob += moved * dt;
            return moved;
        }

        /// <summary>Walks the last steps right up to the butcher block and keeps him there (pressed against the table) while he chops.</summary>
        float SettleAtTable(float dt)
        {
            Vector3 to = W.Map.Kitchen.ChopPose.position - A.Position; to.y = 0f;
            float d = to.magnitude;
            Vector3 vel = Vector3.zero;
            if (d > 0.05f)
            {
                float speed = _chopping ? Mathf.Min(0.5f, d * 3f) : Mathf.Min(Tuning.OmarWalkSpeed * 0.8f, 0.35f + d * 1.5f);
                vel = to / d * speed;
                if (!_chopping) _yaw = Mathf.MoveTowardsAngle(_yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 300f * dt);
            }
            _speedCur = Tuning.OmarWalkSpeed * 0.6f;
            float moved = M.Move(vel, dt);
            A.transform.rotation = Quaternion.Euler(0, _yaw, 0);
            _stepDist += moved * dt;
            _bob += moved * dt;
            return moved;
        }

        void HandleDoors(Vector3 vel)
        {
            if (vel.sqrMagnitude < 0.01f) return;
            Vector3 dir = vel.normalized;
            for (int i = 0; i < W.Doors.Length; i++)
            {
                var d = W.Doors[i];
                Vector3 c = d.Info.Center;
                Vector3 to = c - A.Position;
                if (Mathf.Abs(to.y) > 1.6f) continue;
                to.y = 0;
                float dist = to.magnitude;
                if (dist > 1.9f) continue;
                if (dist > 0.3f && Vector3.Dot(to / dist, dir) < 0.2f) continue;
                if (d.Boarded)
                {
                    if (_smashTimer < 0f) { _smashTimer = 1.6f; _smashDoor = i; H.BroadcastAction(A.Id, CharacterAction.Attack); }
                }
                // unlocked leaves are simply shoved aside by his body (MatchHost door simulation); locked ones he unlocks
                else if (d.Locked) H.RequestDoor(A.Id, i, true);
            }
        }

        void FaceTowards(Vector3 p)
        {
            Vector3 to = p - A.Position; to.y = 0;
            if (to.sqrMagnitude > 0.001f) _yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
        }

        int NearestHidingSpot(float max)
        {
            int best = -1; float bd = max;
            for (int i = 0; i < W.Hiding.Length; i++)
            {
                float d = Vector3.Distance(W.Hiding[i].InteractPoint, A.Position);
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        List<int> HidingSpotsWithin(Vector3 p, float r)
        {
            var list = new List<int>();
            for (int i = 0; i < W.Hiding.Length; i++)
                if (Vector3.Distance(W.Hiding[i].InteractPoint, p) < r) list.Add(i);
            return list;
        }

        void Publish(float speed)
        {
            var s = A.State;
            s.Position = A.transform.position;
            s.Yaw = _yaw;
            s.Pitch = _pitch;
            AvatarFlags f = AvatarFlags.Grounded;
            if (_running && speed > 2.5f) f |= AvatarFlags.Sprint;
            if (W != null && W.Time < _stunUntil) f |= AvatarFlags.Stunned;
            s.Flags = f;
            s.Held = ItemType.None;
            A.SetLocalState(s);
        }
    }

    static class VectorExt
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}
