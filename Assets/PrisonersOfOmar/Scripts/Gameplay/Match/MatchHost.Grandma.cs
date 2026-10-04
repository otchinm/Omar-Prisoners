using System.Collections.Generic;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Host rules for the grandmother.
    //  Stage 1: she watches TV in her room and only sees what is in front of her (or right next to her).
    //  Stage 2 (enough progress / late in the night): she rolls around the ground floor between the roam nodes.
    //  Seeing a prisoner: a shriek, then screams every few seconds while she still sees them - each one tells Omar
    //  where she is. Only the revolver kills her.
    public sealed partial class MatchHost
    {
        const float GrandmaRollSpeed = 0.95f, GrandmaFollowSpeed = 0.55f, GrandmaTurnSpeed = 110f;

        float _gThink, _gVoiceAt, _gLostAt = -1f, _gSnapTimer, _gPauseUntil, _gSitUntil = -1f, _gNoiseUntil, _gRepath;
        int _gTarget = -1;
        bool _gRoaming, _gSeesTarget;
        Vector3 _gNoise;
        List<Vector3> _gPath;
        int _gPathIdx;
        HashSet<int> _gRoam;
        Vector3 _gLastSentPos;
        float _gLastSentYaw;

        partial void TickGrandma(float dt)
        {
            var g = W.Grandma;
            if (g == null || g.Dead) return;
            float now = W.Time;
            if (_gRoam == null) BuildRoamSet(g);

            if (AdminState.GrandmaDisabled)
            {
                if (g.Mode == GrandmaMode.Screaming) SetGrandma(_gRoaming ? GrandmaMode.Roaming : GrandmaMode.WatchingTv, GrandmaEntity.VoiceNone, -1);
                return;
            }

            if (!_gRoaming && GrandmaShouldRoam())
            {
                _gRoaming = true;
                g.Stage = 1;
                _gPath = null;
                _gPauseUntil = now + 2f;
                SetGrandma(GrandmaMode.WatchingTv, GrandmaEntity.VoiceNone, -1);
            }

            _gThink -= dt;
            if (_gThink <= 0f) { _gThink = 0.12f; GrandmaSense(g, now); }

            if (g.Mode == GrandmaMode.Screaming) GrandmaScreamStep(g, dt, now);
            else if (_gRoaming) GrandmaRoamStep(g, dt, now);
            else GrandmaChairStep(g, dt, now);

            // stream the pose while she moves / turns
            _gSnapTimer -= dt;
            if (_gSnapTimer <= 0f)
            {
                _gSnapTimer = 0.1f;
                if ((g.Position - _gLastSentPos).sqrMagnitude > 0.0004f || Mathf.Abs(Mathf.DeltaAngle(g.Yaw, _gLastSentYaw)) > 0.5f || g.Target >= 0)
                {
                    _gLastSentPos = g.Position; _gLastSentYaw = g.Yaw;
                    var w = S.Begin(Msg.GrandmaSnap);
                    w.WriteVector3(g.Position);
                    w.WriteFloat(g.Yaw);
                    w.WriteByte((byte)(g.Target < 0 ? 255 : g.Target));
                    S.SendToAll(NetChannel.Unreliable, false);
                }
            }
        }

        void SetGrandma(GrandmaMode mode, byte voice, int target)
        {
            var g = W.Grandma;
            if (g == null) return;
            var w = S.Begin(Msg.GrandmaState);
            w.WriteByte((byte)mode);
            w.WriteByte(g.Stage);
            w.WriteVector3(g.Position);
            w.WriteFloat(g.Yaw);
            w.WriteByte(voice);
            w.WriteByte((byte)(target < 0 ? 255 : target));
            S.SendToAll(NetChannel.Reliable);
        }

        bool GrandmaShouldRoam()
        {
            var o = W.Objectives;
            int done = 0;
            if (o.GateCut) done++;
            if (o.CarFueled) done++;
            if (W.CarBatteryStartsDead && o.CarBatteryOk) done++;
            if (o.FuseIn) done++;
            if (o.RadioCalled) done++;
            if (o.BarrelsPoured) done++;
            if (o.ShelterOpen) done++;
            return done >= Tuning.GrandmaRoamProgress || W.NightProgress >= Tuning.GrandmaRoamNight;
        }

        /// <summary>Nav nodes she may use: the ground floor of the house (no stairs), her room included.</summary>
        void BuildRoamSet(GrandmaEntity g)
        {
            _gRoam = new HashSet<int>(g.Info.RoamNodes);
            var nav = W.Map.Nav;
            if (_gRoam.Count == 0 && nav != null)
            {
                float y = g.Info.ChairPose.position.y;
                for (int i = 0; i < nav.Nodes.Count; i++)
                {
                    string a = i < nav.NodeAreas.Count ? nav.NodeAreas[i] : "";
                    if (a == null || !a.StartsWith("House.")) continue;
                    if (a == "House.Bathroom") continue;
                    if (Mathf.Abs(nav.Nodes[i].y - y) > 0.45f) continue;
                    _gRoam.Add(i);
                }
            }
        }

        // ------------------------------------------------------------------ senses

        void GrandmaSense(GrandmaEntity g, float now)
        {
            Vector3 eye = g.Eye;
            Vector3 fwd = g.Forward;
            int best = -1;
            float bestD = float.MaxValue;
            foreach (var kv in W.Avatars)
            {
                var a = kv.Value;
                if (a == null || a.IsOmar || !a.Visible) continue;
                var st = W.StatusOf(a.Id);
                if (st == null || st.Life != LifeState.Free || st.Hidden || st.InCar) continue;
                if (AdminState.Invisible.Contains(a.Id)) continue;
                Vector3 chest = a.ChestPosition;
                Vector3 to = chest - eye;
                float d = to.magnitude;
                bool close = d < 2.5f;
                if (!close)
                {
                    // old eyes in a dark room: light gives you away, the TV glare doesn't help her
                    float range = DetectionSystem.VisibilityRange(a) * 0.8f;
                    if (d > range) continue;
                    to.y = 0f;
                    if (Vector3.Angle(fwd, to) > 60f) continue;
                }
                if (Physics.Linecast(eye, chest, Layers.SightBlockers, QueryTriggerInteraction.Ignore) &&
                    Physics.Linecast(eye, a.EyePosition, Layers.SightBlockers, QueryTriggerInteraction.Ignore)) continue;
                if (d < bestD) { bestD = d; best = a.Id; }
            }

            _gSeesTarget = best >= 0;
            if (best >= 0)
            {
                _gLostAt = -1f;
                if (g.Mode != GrandmaMode.Screaming)
                {
                    _gTarget = best;
                    _gVoiceAt = now + Random.Range(1.3f, 1.8f);
                    g.Target = best;
                    SetGrandma(GrandmaMode.Screaming, GrandmaEntity.VoiceSpot, best);
                    GrandmaAlert(g);
                }
                else if (best != _gTarget)
                {
                    _gTarget = best;
                    g.Target = best;
                }
            }
            else if (g.Mode == GrandmaMode.Screaming)
            {
                if (_gLostAt < 0f) _gLostAt = now;
                else if (now - _gLostAt > 2.5f)
                {
                    _gTarget = -1;
                    g.Target = -1;
                    _gPath = null;
                    _gPauseUntil = now + 2f;
                    SetGrandma(_gRoaming ? GrandmaMode.Roaming : GrandmaMode.WatchingTv, GrandmaEntity.VoiceNone, -1);
                }
            }
        }

        /// <summary>Her screams carry through the house: Omar comes running.</summary>
        void GrandmaAlert(GrandmaEntity g)
        {
            Vector3 p = g.Position + Vector3.up;
            DeliverNoise(p, 35f);
            foreach (var ai in _ais) if (ai != null) ai.OnAlarm(p);
        }

        partial void GrandmaHeard(Vector3 pos, float radius)
        {
            var g = W.Grandma;
            if (g == null || g.Dead || AdminState.GrandmaDisabled) return;
            float d = Vector3.Distance(pos, g.Position);
            if (d < 1.2f || d > radius) return; // her own voice / out of earshot
            if (g.Mode == GrandmaMode.Screaming) return;
            _gNoise = pos;
            _gNoiseUntil = W.Time + 5f;
        }

        // ------------------------------------------------------------------ behaviour

        void GrandmaChairStep(GrandmaEntity g, float dt, float now)
        {
            float tvYaw = g.Info.ChairPose.rotation.eulerAngles.y;
            float want = tvYaw;
            if (now < _gNoiseUntil)
            {
                // she turns the chair towards a sound, then back to her programme
                Vector3 to = _gNoise - g.Position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) want = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            }
            g.Yaw = Mathf.MoveTowardsAngle(g.Yaw, want, GrandmaTurnSpeed * 0.5f * dt);
            if (g.Mode != GrandmaMode.WatchingTv) SetGrandma(GrandmaMode.WatchingTv, GrandmaEntity.VoiceNone, -1);
        }

        void GrandmaScreamStep(GrandmaEntity g, float dt, float now)
        {
            var target = W.AvatarOf(_gTarget);
            if (target != null)
            {
                Vector3 to = target.Position - g.Position; to.y = 0f;
                if (to.sqrMagnitude > 0.01f) g.Yaw = Mathf.MoveTowardsAngle(g.Yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, GrandmaTurnSpeed * dt);
                // once she roams she rolls after them while she can see them
                if (_gRoaming && _gSeesTarget && to.magnitude > 2f)
                {
                    _gRepath -= dt;
                    if (_gRepath <= 0f || _gPath == null) { _gRepath = 1f; GrandmaPathTo(g, target.Position); }
                    GrandmaFollowPath(g, dt, GrandmaFollowSpeed, false);
                }
            }
            if (now >= _gVoiceAt)
            {
                _gVoiceAt = now + Random.Range(3f, 4f);
                SetGrandma(GrandmaMode.Screaming, GrandmaEntity.VoiceScream, _gTarget);
                GrandmaAlert(g);
            }
        }

        void GrandmaRoamStep(GrandmaEntity g, float dt, float now)
        {
            if (_gSitUntil > 0f)
            {
                // back in front of her TV for a while
                if (now < _gSitUntil) { GrandmaChairStep(g, dt, now); return; }
                _gSitUntil = -1f;
                _gPath = null;
            }
            if (_gPath == null || _gPathIdx >= _gPath.Count)
            {
                if (now < _gPauseUntil)
                {
                    if (now < _gNoiseUntil)
                    {
                        Vector3 to = _gNoise - g.Position; to.y = 0f;
                        if (to.sqrMagnitude > 0.01f) g.Yaw = Mathf.MoveTowardsAngle(g.Yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, GrandmaTurnSpeed * 0.6f * dt);
                    }
                    if (g.Mode != GrandmaMode.Roaming) SetGrandma(GrandmaMode.Roaming, GrandmaEntity.VoiceNone, -1);
                    return;
                }
                bool home = _gRng() < 0.22f;
                if (home) GrandmaPathTo(g, g.Info.ChairPose.position);
                else
                {
                    var nodes = new List<int>(_gRoam);
                    if (nodes.Count == 0) { _gPauseUntil = now + 5f; return; }
                    var nav = W.Map.Nav;
                    int pick = nodes[Mathf.Clamp((int)(_gRng() * nodes.Count), 0, nodes.Count - 1)];
                    GrandmaPathTo(g, nav.Nodes[pick]);
                }
                _gGoingHome = home;
                if (g.Mode != GrandmaMode.Roaming) SetGrandma(GrandmaMode.Roaming, GrandmaEntity.VoiceNone, -1);
            }
            if (GrandmaFollowPath(g, dt, GrandmaRollSpeed, true))
            {
                _gPauseUntil = now + Random.Range(2f, 6f);
                if (_gGoingHome) _gSitUntil = now + Random.Range(18f, 40f);
            }
        }

        bool _gGoingHome;
        float _gRng() => Random.value;

        /// <summary>Path over her roam nodes only (doors that are locked or boarded stop her).</summary>
        void GrandmaPathTo(GrandmaEntity g, Vector3 goal)
        {
            var nav = W.Map.Nav;
            _gPath = new List<Vector3>();
            _gPathIdx = 0;
            if (nav == null || _gRoam.Count == 0) { _gPath.Add(goal); return; }
            int a = NearestRoam(nav, g.Position), b = NearestRoam(nav, goal);
            if (a < 0 || b < 0) { _gPath.Add(goal); return; }
            var prev = new Dictionary<int, int> { [a] = -1 };
            var q = new Queue<int>();
            q.Enqueue(a);
            while (q.Count > 0)
            {
                int n = q.Dequeue();
                if (n == b) break;
                foreach (int m in nav.Edges[n])
                {
                    if (!_gRoam.Contains(m) || prev.ContainsKey(m)) continue;
                    int door = nav.DoorOnEdge(n, m);
                    if (door >= 0 && door < W.Doors.Length && (W.Doors[door].Locked || W.Doors[door].Boarded)) continue;
                    if (door < -1 && nav.IsDoorBlocked != null && nav.IsDoorBlocked(door)) continue;
                    prev[m] = n;
                    q.Enqueue(m);
                }
            }
            if (!prev.ContainsKey(b)) { _gPath.Add(goal); return; }
            var rev = new List<Vector3>();
            for (int n = b; n >= 0; n = prev[n]) rev.Add(nav.Nodes[n]);
            rev.Reverse();
            if (rev.Count > 1) rev.RemoveAt(0); // skip the node she starts next to
            _gPath.AddRange(rev);
            _gPath.Add(goal);
        }

        int NearestRoam(NavGraph nav, Vector3 p)
        {
            int best = -1; float bd = float.MaxValue;
            foreach (int i in _gRoam)
            {
                float d = (nav.Nodes[i] - p).sqrMagnitude;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Roll along the current path. Returns true when the path is finished.</summary>
        bool GrandmaFollowPath(GrandmaEntity g, float dt, float speed, bool turnFirst)
        {
            if (_gPath == null || _gPathIdx >= _gPath.Count) return true;
            Vector3 target = _gPath[_gPathIdx];
            Vector3 to = target - g.Position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.3f)
            {
                _gPathIdx++;
                return _gPathIdx >= _gPath.Count;
            }
            float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float diff = Mathf.Abs(Mathf.DeltaAngle(g.Yaw, wantYaw));
            g.Yaw = Mathf.MoveTowardsAngle(g.Yaw, wantYaw, GrandmaTurnSpeed * dt);
            // a wheelchair turns before it rolls on
            float k = diff > 60f && turnFirst ? 0.15f : Mathf.Clamp01(1.2f - diff / 90f);
            Vector3 step = g.Forward * Mathf.Min(dist, speed * k * dt);
            Vector3 next = g.Position + step;
            // keep her on the floor
            if (Physics.Raycast(next + Vector3.up * 0.6f, Vector3.down, out var hit, 1.5f, Layers.Solid, QueryTriggerInteraction.Ignore)) next.y = hit.point.y;
            Vector3 vel = dt > 0f ? (next - g.Position) / dt : Vector3.zero;
            g.Position = next;
            ShoveDoorsWith(g.Position, vel);
            return false;
        }

        partial void AdminGrandma(AdminCmd cmd)
        {
            var g = W.Grandma;
            if (g == null || g.Dead) return;
            switch (cmd)
            {
                case AdminCmd.GrandmaRoam:
                    _gRoaming = true; g.Stage = 1; _gSitUntil = -1f; _gPath = null; _gPauseUntil = 0f;
                    SetGrandma(GrandmaMode.Roaming, GrandmaEntity.VoiceNone, -1);
                    break;
                case AdminCmd.GrandmaReturn:
                    GrandmaPathTo(g, g.Info.ChairPose.position);
                    _gGoingHome = true;
                    if (!_gRoaming) { g.Position = g.Info.ChairPose.position; g.Yaw = g.Info.ChairPose.rotation.eulerAngles.y; _gPath = null; }
                    SetGrandma(_gRoaming ? GrandmaMode.Roaming : GrandmaMode.WatchingTv, GrandmaEntity.VoiceNone, -1);
                    break;
                case AdminCmd.GrandmaKill:
                    g.Target = -1; _gTarget = -1;
                    SetGrandma(GrandmaMode.Dead, GrandmaEntity.VoiceDeath, -1);
                    break;
                case AdminCmd.GrandmaScream:
                    SetGrandma(g.Mode, GrandmaEntity.VoiceScream, g.Target);
                    GrandmaAlert(g);
                    break;
            }
        }

        /// <summary>A shot hit the grandmother: she dies (only the revolver can do it).</summary>
        partial void OnShotHit(int shooter, Collider c, Vector3 point, ref byte kind)
        {
            var g = W.Grandma;
            if (g == null || g.Dead || c == null || c != g.Body) return;
            kind = MatchWorld.ShotGrandma;
            g.Target = -1;
            _gTarget = -1;
            SetGrandma(GrandmaMode.Dead, GrandmaEntity.VoiceDeath, -1);
            Message("THE OLD WOMAN IS SILENT NOW", 3f, shooter);
        }
    }
}
