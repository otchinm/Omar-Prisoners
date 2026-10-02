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
    [DefaultExecutionOrder(31000)]
    public sealed class PsxCameraRig : MonoBehaviour
    {
        public static PsxCameraRig Instance { get; private set; }

        public Camera WorldCamera { get; private set; }
        public Camera ViewModelCamera { get; private set; }
        public RenderTexture LowRes { get; private set; }

        /// <summary>Screen camera (culling mask 0) that draws the overlay into LowRes and runs the VHS pass.</summary>
        public Camera PresenterCamera { get; private set; }

        /// <summary>Low-res pixel size of the current target (width depends on screen aspect).</summary>
        public Vector2Int Resolution => LowRes != null ? new Vector2Int(LowRes.width, LowRes.height) : new Vector2Int(426, 240);

        /// <summary>Vertical internal resolution (180..480). Default 240 (PS1/VHS).</summary>
        public int InternalHeight = 240;

        public float FieldOfView
        {
            get => WorldCamera != null ? WorldCamera.fieldOfView : 70f;
            set { if (WorldCamera != null) WorldCamera.fieldOfView = value; }
        }

        /// <summary>Field of view of the first-person view model camera (default 60).</summary>
        public float ViewModelFieldOfView
        {
            get => ViewModelCamera != null ? ViewModelCamera.fieldOfView : 60f;
            set { if (ViewModelCamera != null) ViewModelCamera.fieldOfView = value; }
        }

        Action<RenderTexture> _drawOverlay;
        Delegate[] _overlayHandlers; // cached invocation list (no per-frame allocation)

        /// <summary>
        /// Invoked every frame after world + view model are rendered into LowRes and before the VHS pass.
        /// RenderTexture.active is LowRes and GL is set up so that GL.LoadPixelMatrix(0, w, h, 0) gives
        /// top-left origin pixel coordinates. Handlers draw with Graphics.DrawTexture / GL.
        /// Each handler is isolated: an exception is logged and the next handler still runs.
        /// </summary>
        public event Action<RenderTexture> DrawOverlay
        {
            add { _drawOverlay += value; _overlayHandlers = null; }
            remove { _drawOverlay -= value; _overlayHandlers = null; }
        }

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
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[PsxCameraRig] a rig already exists; use PsxCameraRig.Create().");
                Destroy(this);
                return;
            }
            Instance = this;
            ApplyQualitySettings();
            PsxRenderDriver.Ensure();

            // ---- world camera (this object = the eye)
            WorldCamera = gameObject.AddComponent<Camera>();
            WorldCamera.nearClipPlane = 0.05f;
            WorldCamera.farClipPlane = 400f;
            WorldCamera.fieldOfView = 70f;
            WorldCamera.clearFlags = CameraClearFlags.SolidColor;
            WorldCamera.backgroundColor = Color.black;
            WorldCamera.cullingMask = ~((1 << Layers.ViewModel) | (1 << Layers.LocalBody) | (1 << Layers.Preview));
            WorldCamera.depth = 0f;
            ConfigureCommon(WorldCamera);
            if (gameObject.CompareTag("Untagged")) gameObject.tag = "MainCamera";

            // ---- view model camera (first person arms / held items), same eye, drawn on top
            var vm = new GameObject("ViewModelCamera");
            vm.transform.SetParent(transform, false);
            ViewModelCamera = vm.AddComponent<Camera>();
            ViewModelCamera.clearFlags = CameraClearFlags.Depth;
            ViewModelCamera.cullingMask = 1 << Layers.ViewModel;
            ViewModelCamera.nearClipPlane = 0.01f;
            ViewModelCamera.farClipPlane = 10f;
            ViewModelCamera.fieldOfView = 60f;
            ViewModelCamera.depth = 1f;
            ConfigureCommon(ViewModelCamera);

            // ---- presenter: overlay + VHS to the screen
            var pres = new GameObject("VhsPresenter");
            pres.transform.SetParent(transform, false);
            PresenterCamera = pres.AddComponent<Camera>();
            PresenterCamera.cullingMask = 0;
            PresenterCamera.clearFlags = CameraClearFlags.SolidColor;
            PresenterCamera.backgroundColor = Color.black;
            PresenterCamera.orthographic = true;
            PresenterCamera.orthographicSize = 1f;
            PresenterCamera.nearClipPlane = 0.01f;
            PresenterCamera.farClipPlane = 1f;
            PresenterCamera.depth = 100f;
            ConfigureCommon(PresenterCamera);
            var presenter = pres.AddComponent<PsxVhsPresenter>();
            presenter.Rig = this;

            EnsureLowRes();
        }

        static void ConfigureCommon(Camera c)
        {
            c.renderingPath = RenderingPath.Forward;
            c.allowHDR = false;
            c.allowMSAA = false;
            c.useOcclusionCulling = false;
            c.depthTextureMode = DepthTextureMode.None;
        }

        static bool _qualityApplied;

        static void ApplyQualitySettings()
        {
            if (_qualityApplied) return;
            _qualityApplied = true;
            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.pixelLightCount = 0;
            QualitySettings.softParticles = false;
            QualitySettings.realtimeReflectionProbes = false;
            RenderSettings.fog = false; // the PSX shaders do their own per-vertex fog
        }

        void LateUpdate()
        {
            EnsureLowRes();
            if (WorldCamera != null)
            {
                Color c = PsxEnvironment.FogColor;
                c.a = 1f;
                WorldCamera.backgroundColor = c;
            }
        }

        /// <summary>(Re)creates LowRes when the screen size or InternalHeight changed (or the GPU lost it).</summary>
        void EnsureLowRes()
        {
            int sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            int h = Mathf.Clamp(InternalHeight, 120, 1080);
            int w = Mathf.Clamp(Mathf.RoundToInt(h * (float)sw / sh), 16, 4096);
            if (LowRes != null && LowRes.width == w && LowRes.height == h)
            {
                if (!LowRes.IsCreated()) LowRes.Create();
                return;
            }

            var old = LowRes;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            rt.name = "PsxLowRes";
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Clamp;
            rt.antiAliasing = 1;
            rt.useMipMap = false;
            rt.autoGenerateMips = false;
            rt.anisoLevel = 0;
            rt.Create();
            LowRes = rt;
            if (WorldCamera != null) WorldCamera.targetTexture = rt;
            if (ViewModelCamera != null) ViewModelCamera.targetTexture = rt;
            if (old != null)
            {
                old.Release();
                Destroy(old);
            }
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (WorldCamera != null) WorldCamera.targetTexture = null;
            if (ViewModelCamera != null) ViewModelCamera.targetTexture = null;
            if (LowRes != null)
            {
                LowRes.Release();
                Destroy(LowRes);
                LowRes = null;
            }
        }

        /// <summary>Converts a screen pixel position (origin bottom-left, like Input.mousePosition) to LowRes pixels (origin top-left).
        /// Accounts for the slight CRT barrel of the VHS pass, so the result is the low-res pixel displayed under that screen point.</summary>
        public Vector2 ScreenToLowRes(Vector2 screenPos)
        {
            var r = Resolution;
            float u = screenPos.x / Mathf.Max(1, Screen.width);
            float v = screenPos.y / Mathf.Max(1, Screen.height);
            float k = VhsEffect.CurrentBarrel;
            float cx = u - 0.5f, cy = v - 0.5f;
            float s = (1f + (cx * cx + cy * cy) * k) / (1f + 0.25f * k);
            u = cx * s + 0.5f;
            v = cy * s + 0.5f;
            return new Vector2(u * r.x, (1f - v) * r.y);
        }

        internal void RaiseDrawOverlay(RenderTexture rt)
        {
            var handlers = _overlayHandlers;
            if (handlers == null)
            {
                if (_drawOverlay == null) return;
                handlers = _overlayHandlers = _drawOverlay.GetInvocationList();
            }
            for (int i = 0; i < handlers.Length; i++)
            {
                // a previous handler may have changed the target / matrices (e.g. a preview render): restore them
                Graphics.SetRenderTarget(rt);
                RenderTexture.active = rt;
                GL.LoadPixelMatrix(0, rt.width, rt.height, 0);
                try { ((Action<RenderTexture>)handlers[i])(rt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
