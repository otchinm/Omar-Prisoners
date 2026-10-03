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
        enum Mode { Waking, Patrol, Investigate, Chase, Search }

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
        float _attackCooldown, _windup = -1f;
        float _nextScreamAt;
        float _stunUntil;
        float _noiseScore;
        Vector3 _noisePos;
        readonly Dictionary<int, int> _sawHide = new Dictionary<int, int>();
        readonly List<int> _searchQueue = new List<int>();
        float _trapTimer = 75f;
        float _stuckTimer;
        Vector3 _stuckRef;
        float _smashTimer = -1f;
        int _smashDoor = -1;
        float _stepDist, _bob;

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

        public void OnNoise(Vector3 pos, float radius)
        {
            if (_mode == Mode.Chase || _mode == Mode.Waking) return;
            float d = Vector3.Distance(pos, A.Position);
            if (d > radius * 1.3f) return;
            float score = radius / Mathf.Max(1f, d);
            if (_mode == Mode.Investigate && score < _noiseScore * 0.8f) return;
            _noiseScore = score;
            _noisePos = pos;
            StartInvestigate(pos, radius > 12f || d < 12f);
        }

        public void OnAlarm(Vector3 pos)
        {
            if (_mode == Mode.Waking) return;
            if (_mode == Mode.Chase && _target >= 0) return;
            _noiseScore = 99f;
            StartInvestigate(pos, true);
            if (Random.value < 0.5f) TryScream();
        }

        public void OnSawHide(int prisoner, int spot)
        {
            if (D.IsSpotted(prisoner) || (D.Meter(prisoner) > 0.5f && Vector3.Distance(A.Position, W.Hiding[spot].InteractPoint) < 18f))
            {
                _sawHide[prisoner] = spot;
                if (!_searchQueue.Contains(spot)) _searchQueue.Insert(0, spot);
            }
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
            D.Tick(dt, eye, fwd, false, OnDetect);

            if (W.Time < _stunUntil) { Publish(M.Move(Vector3.zero, dt)); return; }

            _attackCooldown -= dt;
            if (_windup >= 0f)
            {
                _windup -= dt;
                if (_windup < 0f) H.DoAttack(A.Id, _target >= 0 ? _target : 255);
            }

            switch (_mode)
            {
                case Mode.Patrol: TickPatrol(dt); break;
                case Mode.Investigate: TickInvestigate(dt); break;
                case Mode.Chase: TickChase(dt); break;
                case Mode.Search: TickSearch(dt); break;
            }

            _trapTimer -= dt;
            if (_trapTimer <= 0f) { _trapTimer = Random.Range(70f, 130f); TryPlaceTrap(); }

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
                    if (Random.value < 0.45f) TryScream();
                    _nextScreamAt = W.Time + Random.Range(14f, 24f);
                }
            }
            else if (id == _target)
            {
                _lastKnown = D.LastKnown.TryGetValue(id, out var p) ? p : A.Position;
                _target = -1;
                StartSearch(_lastKnown);
            }
        }

        // ------------------------------------------------------------------ modes

        void PickPatrol()
        {
            var nav = W.Map.Nav;
            _running = false;
            if (nav == null || nav.Nodes.Count == 0) { SetGoal(A.Position + Random.insideUnitSphere.WithY(0) * 10f); return; }
            Vector3 dest = A.Position;
            // sometimes check on the pens or the objectives
            float r = Random.value;
            if (r < 0.25f && W.Cages.Length > 0)
            {
                bool anyCaged = false;
                foreach (var c in W.Cages) if (c.Occupant >= 0) anyCaged = true;
                if (anyCaged) { SetGoal(W.Cages[Random.Range(0, W.Cages.Length)].Info.Outside.position); return; }
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
            if (_searchQueue.Count > 0)
            {
                int s = _searchQueue[0];
                var h = W.Hiding[s];
                SetGoal(h.Info.ExitPose.position);
            }
            else
            {
                Vector3 p = around + new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-6f, 6f));
                var nav = W.Map.Nav;
                if (nav != null && nav.Nodes.Count > 0) p = nav.Nodes[nav.Nearest(p)];
                SetGoal(p);
            }
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
                _mode = Mode.Patrol;
                PickPatrol();
                return;
            }
            if (st.Hidden)
            {
                _sawHide[_target] = st.HidingSpot;
                if (!_searchQueue.Contains(st.HidingSpot)) _searchQueue.Insert(0, st.HidingSpot);
                _target = -1;
                StartSearch(W.Hiding[st.HidingSpot].InteractPoint);
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

        void TryPlaceTrap()
        {
            var spots = W.Map.TrapSpots;
            if (spots.Count == 0) return;
            int best = -1; float bestD = 18f;
            for (int i = 0; i < spots.Count; i++)
            {
                float d = Vector3.Distance(spots[i].A, A.Position);
                if (d < bestD && !TrapExistsNear(spots[i].A)) { bestD = d; best = i; }
            }
            if (best < 0) return;
            var s = spots[best];
            if (H.PlaceTrap(A.Id, s.Kind, s.A, s.Kind == TrapKind.Tripwire ? s.B : s.A)) H.BroadcastAction(A.Id, CharacterAction.PlaceTrap);
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
                if (_path.Count == 0 && _mode == Mode.Patrol)
                {
                    // unreachable right now (behind a closed gate): patrol somewhere else
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
                float speed = _running ? Tuning.OmarRunSpeed * 0.93f : Tuning.OmarWalkSpeed;
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
                else if (!d.Open) H.RequestDoor(A.Id, i, true);
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
