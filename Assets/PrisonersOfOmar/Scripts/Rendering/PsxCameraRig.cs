using System;
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// The one camera setup of the game (menus and matches):
    ///   WorldCamera  (this GameObject)  -> renders the world into the low resolution target (LowRes)
    ///   ViewModelCamera (child)         -> renders Layers.ViewModel on top (clears depth only) into LowRes
    ///   DrawOverlay event               -> UI / HUD draws into LowRes in low-res pixel space
    ///   presenter                       -> VHS post-process blits LowRes to the screen
    /// Gameplay moves/rotates this transform (it IS the eye). FOV via FieldOfView.
    /// Never add an AudioListener here; gameplay owns that.
    /// </summary>
    public sealed class PsxCameraRig : MonoBehaviour
    {
        public static PsxCameraRig Instance { get; private set; }

        public Camera WorldCamera { get; private set; }
        public Camera ViewModelCamera { get; private set; }
        public RenderTexture LowRes { get; private set; }

        /// <summary>Low-res pixel size of the current target (width depends on screen aspect).</summary>
        public Vector2Int Resolution => LowRes != null ? new Vector2Int(LowRes.width, LowRes.height) : new Vector2Int(426, 240);

        /// <summary>Vertical internal resolution (180..480). Default 240 (PS1/VHS).</summary>
        public int InternalHeight = 240;

        public float FieldOfView
        {
            get => WorldCamera != null ? WorldCamera.fieldOfView : 70f;
            set { if (WorldCamera != null) WorldCamera.fieldOfView = value; }
        }

        /// <summary>
        /// Invoked every frame after world + view model are rendered into LowRes and before the VHS pass.
        /// RenderTexture.active is LowRes and GL is set up so that GL.LoadPixelMatrix(0, w, h, 0) gives
        /// top-left origin pixel coordinates. Handlers draw with Graphics.DrawTexture / GL.
        /// </summary>
        public event Action<RenderTexture> DrawOverlay;

        /// <summary>Creates the rig (or returns the existing one). Survives scene loads.</summary>
        public static PsxCameraRig Create()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PsxCameraRig");
            DontDestroyOnLoad(go);
            var rig = go.AddComponent<PsxCameraRig>();
            return rig;
        }

        void Awake()
        {
            Instance = this;
            WorldCamera = gameObject.AddComponent<Camera>();
            WorldCamera.nearClipPlane = 0.05f;
            WorldCamera.farClipPlane = 400f;
            WorldCamera.fieldOfView = 70f;
            WorldCamera.clearFlags = CameraClearFlags.SolidColor;
            WorldCamera.backgroundColor = Color.black;
            WorldCamera.cullingMask = ~((1 << Layers.ViewModel) | (1 << Layers.LocalBody) | (1 << Layers.Preview));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Converts a screen pixel position (origin bottom-left, like Input.mousePosition) to LowRes pixels (origin top-left).</summary>
        public Vector2 ScreenToLowRes(Vector2 screenPos)
        {
            var r = Resolution;
            return new Vector2(screenPos.x / Mathf.Max(1, Screen.width) * r.x, (1f - screenPos.y / Mathf.Max(1, Screen.height)) * r.y);
        }

        internal void RaiseDrawOverlay(RenderTexture rt) => DrawOverlay?.Invoke(rt);
    }
}
