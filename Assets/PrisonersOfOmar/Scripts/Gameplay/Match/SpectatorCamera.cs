using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Camera for players who escaped, died or only spectate: follows a free prisoner from behind
    /// (LMB / RMB or arrows cycle), or cycles the map's overview cameras when nobody is left.
    /// </summary>
    public sealed class SpectatorCamera : MonoBehaviour
    {
        MatchWorld _w;
        int _index;
        float _yaw, _pitch = 15f;
        int _overview;
        float _overviewTimer;

        public Avatar Target { get; private set; }

        public static SpectatorCamera Create(MatchWorld w)
        {
            var go = new GameObject("SpectatorCamera");
            go.transform.SetParent(w.transform, false);
            var s = go.AddComponent<SpectatorCamera>();
            s._w = w;
            return s;
        }

        void OnEnable()
        {
            VhsEffect.Mode = VhsMode.Spectator;
            GameInput.SetCursorLocked(true);
        }

        List<Avatar> Candidates()
        {
            var list = new List<Avatar>();
            foreach (var kv in _w.Avatars)
            {
                var a = kv.Value;
                if (a == null || a.IsOmar || a.Id == _w.LocalId) continue;
                var st = _w.StatusOf(a.Id);
                if (st != null && (st.Life == LifeState.Free || st.Life == LifeState.Caged)) list.Add(a);
            }
            list.Sort((x, y) => x.Id.CompareTo(y.Id));
            // Omar last, so you can always watch him
            var o = _w.OmarAvatar;
            if (o != null) list.Add(o);
            return list;
        }

        void LateUpdate()
        {
            var rig = PsxCameraRig.Instance;
            if (rig == null || _w == null) return;
            var c = Candidates();
            if (c.Count > 0)
            {
                if (!GameInput.GameplayBlocked)
                {
                    if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) _index++;
                    if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) _index--;
                }
                _index = ((_index % c.Count) + c.Count) % c.Count;
                Target = c[_index];
                var look = GameInput.Look;
                _yaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, -20f, 60f);
                Quaternion rot = Quaternion.Euler(_pitch, Target.State.Yaw + _yaw, 0);
                Vector3 pivot = Target.Position + Vector3.up * (Target.IsOmar ? 1.8f : 1.5f);
                Vector3 want = pivot - rot * Vector3.forward * 2.6f;
                if (Physics.SphereCast(pivot, 0.2f, (want - pivot).normalized, out var hit, 2.6f, Layers.Solid, QueryTriggerInteraction.Ignore))
                    want = pivot + (want - pivot).normalized * Mathf.Max(0.3f, hit.distance - 0.1f);
                rig.transform.SetPositionAndRotation(want, rot);
            }
            else
            {
                Target = null;
                var cams = _w.Map.SpectatorCameras;
                if (cams.Count == 0) return;
                _overviewTimer -= Time.deltaTime;
                if (_overviewTimer <= 0f) { _overviewTimer = 8f; _overview = (_overview + 1) % cams.Count; }
                var p = cams[_overview];
                rig.transform.SetPositionAndRotation(p.position, p.rotation);
            }
            rig.FieldOfView = Settings.FieldOfView;
        }

        public string TargetName
        {
            get
            {
                if (Target == null) return "";
                var info = _w.InfoOf(Target.Id);
                return info != null ? info.Name : "";
            }
        }
    }
}
