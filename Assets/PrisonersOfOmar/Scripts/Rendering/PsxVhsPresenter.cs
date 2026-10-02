using System;
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Lives on the rig's presenter camera (renders nothing itself). In OnRenderImage it
    /// (1) lets the UI draw into LowRes (PsxCameraRig.DrawOverlay) and (2) blits LowRes to the screen through the VHS shader.
    /// Created by PsxCameraRig; never add it manually.
    /// </summary>
    [AddComponentMenu("")]
    [RequireComponent(typeof(Camera))]
    public sealed class PsxVhsPresenter : MonoBehaviour
    {
        public const string ShaderName = "PrisonersOfOmar/VHS";

        internal PsxCameraRig Rig;
        Material _material;

        void Awake()
        {
            var shader = Shader.Find(ShaderName);
            if (shader != null && shader.isSupported)
                _material = new Material(shader) { name = "PsxVhs" };
            else
                Debug.LogWarning("[PsxVhsPresenter] VHS shader missing or unsupported - presenting without post-process.");
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            var rig = Rig != null ? Rig : PsxCameraRig.Instance;
            RenderTexture low = rig != null ? rig.LowRes : null;
            if (low == null)
            {
                Graphics.Blit(source, destination);
                return;
            }
            if (!low.IsCreated()) low.Create();

            // (1) UI / HUD overlay into the low-res frame, top-left pixel coordinates
            var prevActive = RenderTexture.active;
            Graphics.SetRenderTarget(low);
            RenderTexture.active = low;
            if (rig.WorldCamera == null || !rig.WorldCamera.isActiveAndEnabled)
                GL.Clear(true, true, Color.black); // nothing rendered the world this frame: do not accumulate stale UI
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, low.width, low.height, 0);
            try { rig.RaiseDrawOverlay(low); }
            catch (Exception e) { Debug.LogException(e); }
            finally { GL.PopMatrix(); }
            RenderTexture.active = prevActive;

            // (2) VHS pass to the screen
            if (_material != null)
            {
                VhsEffect.Apply(_material, low);
                Graphics.Blit(low, destination, _material);
            }
            else
            {
                Graphics.Blit(low, destination);
            }
        }
    }
}
