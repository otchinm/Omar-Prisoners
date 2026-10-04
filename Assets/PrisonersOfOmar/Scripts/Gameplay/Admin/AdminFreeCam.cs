using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>(iteration 2) Admin free camera: takes the camera rig after everybody else (WASD, mouse, Space / Ctrl,
    /// Shift faster). The character stays where it is while it is on.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class AdminFreeCam : MonoBehaviour
    {
        static AdminFreeCam _instance;
        public static bool Active => _instance != null && _instance.enabled;
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
        }
    }
}
