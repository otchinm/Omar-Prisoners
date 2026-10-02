using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Turns its quad (front face towards local -Z) to the world camera every frame, right before the camera culls
    /// (driven by PsxRenderDriver, so it is always exact even if gameplay moves the camera late).
    /// Spherical = parallel to the screen; Cylindrical = only rotates around world Y (flames, fire).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PsxBillboard : MonoBehaviour
    {
        static readonly List<PsxBillboard> _all = new List<PsxBillboard>(256);

        public bool Cylindrical;
        /// <summary>Extra rotation around the view axis (degrees), e.g. random smoke puff orientation.</summary>
        public float Roll;

        void OnEnable()
        {
            _all.Add(this);
            var cam = PsxRenderDriver.WorldCamera();
            if (cam != null) Face(cam.transform.position, cam.transform.rotation, cam.transform.forward);
        }

        void OnDisable() { _all.Remove(this); }

        /// <summary>Faces every billboard whose layer is in <paramref name="layerMask"/> towards <paramref name="cam"/>.</summary>
        internal static void FaceAll(Transform cam, int layerMask = ~0)
        {
            Vector3 camPos = cam.position;
            Quaternion camRot = cam.rotation;
            Vector3 camFwd = cam.forward;
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var b = _all[i];
                if (b == null) { _all.RemoveAt(i); continue; }
                if ((layerMask & (1 << b.gameObject.layer)) == 0) continue;
                b.Face(camPos, camRot, camFwd);
            }
        }

        void Face(Vector3 camPos, Quaternion camRot, Vector3 camFwd)
        {
            Quaternion q;
            if (Cylindrical)
            {
                Vector3 d = transform.position - camPos;
                d.y = 0f;
                if (d.sqrMagnitude < 1e-6f)
                {
                    d = camFwd; d.y = 0f;
                    if (d.sqrMagnitude < 1e-6f) d = Vector3.forward;
                }
                q = Quaternion.LookRotation(d, Vector3.up);
            }
            else q = camRot;
            if (Roll != 0f) q *= Quaternion.Euler(0f, 0f, Roll);
            transform.rotation = q;
        }
    }
}
