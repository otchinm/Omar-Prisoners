using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>(iteration 2) Admin free camera: takes the camera rig after everybody else (WASD, mouse, Space / Ctrl,
    /// Shift faster). The character stays where it is while it is on. With <see cref="ShowSelf"/> (admin "SEE MYSELF")
    /// the local player's own body is drawn too (and the first person arms are hidden) so the admin can look at it.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AdminFreeCam : MonoBehaviour
    {
        static AdminFreeCam _instance;
        public static bool Active => _instance != null && _instance.enabled;
        /// <summary>Admin "SEE MYSELF": draw the local body while the free camera is on.</summary>
        public static bool ShowSelf;
        /// <summary>The local body is being shown right now (read by <see cref="Avatar"/>).</summary>
        public static bool ShowingSelf => ShowSelf && Active;
        float _yaw, _pitch;
        Vector3 _pos;

        public static void Set(bool on)
        {
            if (on)
            {
                if (_instance != null) return;
                var rig = PsxCameraRig.Instance;
                if (rig == null) return;
                var go = new GameObject("AdminFreeCam");
                _instance = go.AddComponent<AdminFreeCam>();
                _instance._pos = rig.transform.position;
                var e = rig.transform.rotation.eulerAngles;
                _instance._yaw = e.y;
                _instance._pitch = e.x > 180f ? e.x - 360f : e.x;
            }
            else if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        /// <summary>Puts the free camera a couple of metres in front of <paramref name="target"/> (facing
        /// <paramref name="forward"/>), looking back at it; stops short of walls.</summary>
        public static void Frame(Vector3 target, Vector3 forward)
        {
            if (_instance == null) return;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            Vector3 want = target + forward * 2.2f + Vector3.up * 0.25f;
            Vector3 dir = want - target;
            float dist = dir.magnitude;
            if (Physics.SphereCast(target, 0.2f, dir / dist, out var hit, dist, Layers.Solid, QueryTriggerInteraction.Ignore))
                want = target + dir / dist * Mathf.Max(0.6f, hit.distance - 0.1f);
            _instance._pos = want;
            Vector3 look = target - want;
            _instance._yaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            _instance._pitch = -Mathf.Atan2(look.y, new Vector2(look.x, look.z).magnitude) * Mathf.Rad2Deg;
        }

        void LateUpdate()
        {
            var rig = PsxCameraRig.Instance;
            if (rig == null) return;
            float dt = Time.unscaledDeltaTime;
            if (GameInput.CursorLocked && !GameInput.GameplayBlocked)
            {
                float sens = Settings.MouseSensitivity;
                _yaw += Input.GetAxisRaw("Mouse X") * sens;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxisRaw("Mouse Y") * sens * (Settings.InvertY ? -1f : 1f), -89f, 89f);
                Vector3 v = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) v += Vector3.forward;
                if (Input.GetKey(KeyCode.S)) v += Vector3.back;
                if (Input.GetKey(KeyCode.D)) v += Vector3.right;
                if (Input.GetKey(KeyCode.A)) v += Vector3.left;
                Vector3 move = Quaternion.Euler(_pitch, _yaw, 0f) * v;
                if (Input.GetKey(KeyCode.Space)) move += Vector3.up;
                if (Input.GetKey(KeyCode.LeftControl)) move += Vector3.down;
                float speed = Input.GetKey(KeyCode.LeftShift) ? 14f : 5f;
                _pos += move * speed * dt;
            }
            rig.transform.SetPositionAndRotation(_pos, Quaternion.Euler(_pitch, _yaw, 0f));
            ShowLocalBody(rig, ShowSelf);
        }

        void OnDestroy()
        {
            var rig = PsxCameraRig.Instance;
            if (rig != null) ShowLocalBody(rig, false);
        }

        /// <summary>The world camera draws the local body layer (normally culled: it is our own head) and the first
        /// person arms / held item are hidden while we look at ourselves from outside.</summary>
        static void ShowLocalBody(PsxCameraRig rig, bool show)
        {
            int bit = 1 << Layers.LocalBody;
            var cam = rig.WorldCamera;
            if (cam != null) cam.cullingMask = show ? cam.cullingMask | bit : cam.cullingMask & ~bit;
            if (rig.ViewModelCamera != null) rig.ViewModelCamera.enabled = !show;
        }
    }
}
