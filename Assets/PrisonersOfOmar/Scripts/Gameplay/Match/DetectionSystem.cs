using System;
using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Omar's eyes. Runs where Omar is simulated (the human Omar's machine, or the host for the AI).
    /// A prisoner is visible when inside Omar's view cone, within a range that depends on how lit they are
    /// (their own lighter / flashlight, nearby lights, crouching), and in line of sight. Sustained visibility
    /// fills a meter; a full meter = SPOTTED (find sound + chase). The chase is reported as over only after
    /// the prisoner stayed out of sight for <see cref="GameInfo.ChaseSoundLinger"/> seconds.
    /// </summary>
    public sealed class DetectionSystem
    {
        readonly MatchWorld _w;
        readonly Dictionary<int, float> _meter = new Dictionary<int, float>();
        readonly Dictionary<int, float> _lastSeen = new Dictionary<int, float>();
        readonly HashSet<int> _spotted = new HashSet<int>();
        readonly List<int> _tmp = new List<int>();
        float _timer;

        public DetectionSystem(MatchWorld w) { _w = w; }

        public float Meter(int id) => _meter.TryGetValue(id, out var m) ? m : 0f;
        public bool IsSpotted(int id) => _spotted.Contains(id);
        public IEnumerable<int> Spotted => _spotted;
        /// <summary>Last world position where a prisoner was seen (AI search).</summary>
        public readonly Dictionary<int, Vector3> LastKnown = new Dictionary<int, Vector3>();

        /// <summary>Visibility range (meters) for a prisoner given lighting and posture.</summary>
        public static float VisibilityRange(Avatar a)
        {
            float light = PsxLightManager.SampleIllumination(a.ChestPosition);
            float range = Mathf.Lerp(Tuning.SightMinRange, Tuning.SightMaxRange, Mathf.Clamp01(light * 1.3f));
            if (a.LighterOn) range = Mathf.Max(range, 24f);
            if (a.FlashlightOn) range = Mathf.Max(range, 40f);
            if (a.Crouching) range *= 0.62f;
            if (a.Sprinting) range *= 1.15f;
            return range;
        }

        /// <summary>Line of sight + cone + range test.</summary>
        public bool CanSee(Vector3 eye, Vector3 forward, Avatar target, out float distance)
        {
            distance = 999f;
            if (target == null || !target.Visible) return false;
            var st = _w.StatusOf(target.Id);
            if (st == null || st.Life != LifeState.Free || st.Hidden) return false;
            Vector3 chest = target.ChestPosition;
            Vector3 to = chest - eye;
            distance = to.magnitude;
            if (distance < 0.01f) return true;
            float range = VisibilityRange(target);
            if (distance > range && distance > 2.2f) return false;
            float angle = Vector3.Angle(forward, to);
            if (angle > Tuning.SightConeHalfAngle && distance > 1.8f) return false;
            if (!Physics.Linecast(eye, chest, Layers.SightBlockers, QueryTriggerInteraction.Ignore)) return true;
            Vector3 head = target.EyePosition;
            return !Physics.Linecast(eye, head, Layers.SightBlockers, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Update at ~10 Hz. <paramref name="report"/> is called with (prisonerId, spotted) on changes.</summary>
        public void Tick(float dt, Vector3 eye, Vector3 forward, bool blind, Action<int, bool> report)
        {
            _timer -= dt;
            if (_timer > 0f) return;
            float step = 0.1f - _timer;
            _timer = 0.1f;
            float now = _w.Time;

            foreach (var kv in _w.Avatars)
            {
                var a = kv.Value;
                if (a == null || a.IsOmar) continue;
                int id = kv.Key;
                float d = 999f;
                bool seen = !blind && CanSee(eye, forward, a, out d);
                float m = Meter(id);
                if (seen)
                {
                    float range = Mathf.Max(3f, VisibilityRange(a));
                    float rate = d < 3.5f ? 10f : Mathf.Lerp(2.2f, 0.6f, Mathf.Clamp01(d / range));
                    if (a.Sprinting) rate *= 1.4f;
                    m = Mathf.Min(1f, m + rate * step);
                    _lastSeen[id] = now;
                    LastKnown[id] = a.Position;
                }
                else
                {
                    m = Mathf.Max(0f, m - 0.35f * step);
                }
                _meter[id] = m;

                if (!_spotted.Contains(id) && m >= 1f)
                {
                    _spotted.Add(id);
                    report?.Invoke(id, true);
                }
            }

            // end chases: out of sight for longer than the linger time (or the prisoner is no longer free)
            _tmp.Clear();
            foreach (var id in _spotted)
            {
                var st = _w.StatusOf(id);
                bool gone = st == null || st.Life != LifeState.Free;
                float last = _lastSeen.TryGetValue(id, out var t) ? t : -999f;
                if (gone || now - last > GameInfo.ChaseSoundLinger) _tmp.Add(id);
            }
            foreach (var id in _tmp)
            {
                _spotted.Remove(id);
                _meter[id] = Mathf.Min(Meter(id), 0.5f);
                report?.Invoke(id, false);
            }
        }

        /// <summary>Instantly mark a prisoner as seen (e.g. caught in a trap, pulled out of a wardrobe).</summary>
        public void ForceSpot(int id, Vector3 pos, Action<int, bool> report)
        {
            _meter[id] = 1f;
            _lastSeen[id] = _w.Time;
            LastKnown[id] = pos;
            if (_spotted.Add(id)) report?.Invoke(id, true);
        }
    }
}
