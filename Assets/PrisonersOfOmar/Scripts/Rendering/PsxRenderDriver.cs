using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Hidden, automatically created (DontDestroyOnLoad) driver of the PSX shader globals.
    /// Every frame, right before the world camera (PsxCameraRig.Instance.WorldCamera, else Camera.main) culls:
    ///   1. updates light flicker (PsxLight.CurrentIntensity),
    ///   2. selects up to 16 lights and uploads them,
    ///   3. uploads ambient / fog / vertex snap / affine / time / anomaly globals,
    ///   4. turns billboards towards the camera and keeps the sky dome on the camera.
    /// Without an active world camera the globals are still refreshed in LateUpdate (menus, loading, previews).
    /// Gameplay never needs to call anything here.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    [AddComponentMenu("")]
    public sealed class PsxRenderDriver : MonoBehaviour
    {
        static PsxRenderDriver _instance;
        int _tickFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoCreate() { Ensure(); }

        /// <summary>Creates the driver if needed (play mode only). Safe to call any time.</summary>
        public static void Ensure()
        {
            if (_instance != null || !Application.isPlaying) return;
            var go = new GameObject("PsxRenderDriver");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PsxRenderDriver>();
        }

        /// <summary>The camera the globals are prepared for: the rig's world camera, else Camera.main (may be null).</summary>
        public static Camera WorldCamera()
        {
            var rig = PsxCameraRig.Instance;
            if (rig != null && rig.WorldCamera != null) return rig.WorldCamera;
            return Camera.main;
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            Tick(null); // valid globals before anything renders
        }

        void OnEnable() { Camera.onPreCull += OnCameraPreCull; }
        void OnDisable() { Camera.onPreCull -= OnCameraPreCull; }
        void OnDestroy() { if (_instance == this) _instance = null; }

        void LateUpdate()
        {
            var world = WorldCamera();
            if (world == null || !world.isActiveAndEnabled) Tick(null);
        }

        void OnCameraPreCull(Camera cam)
        {
            if (cam == null || PreviewRenderer.IsRendering) return;
            if (cam != WorldCamera()) return;
            if (_tickFrame == Time.frameCount) return;
            Tick(cam);
            Transform ct = cam.transform;
            PsxBillboard.FaceAll(ct);
            PsxSky.Follow(ct.position);
        }

        void Tick(Camera cam)
        {
            _tickFrame = Time.frameCount;
            PsxLightManager.UpdateFlicker(Time.time);
            PsxLightManager.SelectAndUpload(cam);
            PsxEnvironment.Upload(cam);
            AnomalySystem.UploadGlobals(cam != null ? cam.transform.position : Vector3.zero);
        }

        /// <summary>Re-uploads the globals of the current frame (after a temporary override such as a preview render).</summary>
        internal static void ReapplyCached()
        {
            PsxLightManager.ReuploadCached();
            PsxEnvironment.ReuploadCached();
            AnomalySystem.ReuploadCached();
        }
    }
}
