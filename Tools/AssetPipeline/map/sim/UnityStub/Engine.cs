// More managed UnityEngine stand-ins for the offline map QA harness (cameras, skinning, property blocks, globals).
// Everything that only feeds the GPU is a no-op; everything the map builder reads back (bake/skinning, randomness)
// is implemented with Unity semantics and stays deterministic. NOT Unity; Unity never compiles this folder.
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class AddComponentMenu : Attribute { public AddComponentMenu(string menuName) { } public AddComponentMenu(string menuName, int order) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class ExecuteAlways : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }
    public enum RuntimeInitializeLoadType { AfterSceneLoad = 0, BeforeSceneLoad, AfterAssembliesLoaded, BeforeSplashScreen, SubsystemRegistration }

    [Flags] public enum HideFlags { None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4, NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32, DontSave = 52, HideAndDontSave = 61 }
    public enum CameraClearFlags { Skybox = 1, Color = 2, SolidColor = 2, Depth = 3, Nothing = 4 }
    public enum RenderingPath { UsePlayerSettings = -1, VertexLit = 0, Forward = 1, DeferredLighting = 2, DeferredShading = 3 }
    public enum RenderTextureFormat { ARGB32 = 0, Depth = 1, ARGBHalf = 2, Default = 7, RGB565 = 4 }
    public enum SkinQuality { Auto = 0, Bone1 = 1, Bone2 = 2, Bone4 = 4 }

    /// <summary>Headless: never in play mode (no lifecycle, no DontDestroyOnLoad scenes, no preview renders).</summary>
    public static class Application
    {
        public static bool isPlaying = false;
        public static bool isEditor = false;
        public static bool isBatchMode = true;
        public static string dataPath = "";
        public static string persistentDataPath = "";
        public static int targetFrameRate = -1;
    }

    public static class Screen
    {
        public static int width = 640, height = 400;
    }

    /// <summary>Deterministic UnityEngine.Random (fixed seed, reset with Object.ResetWorld).</summary>
    public static class Random
    {
        static System.Random _r = new System.Random(1234567);
        public static void InitState(int seed) => _r = new System.Random(seed);
        internal static void Reset() => _r = new System.Random(1234567);
        public static float value => (float)_r.NextDouble();
        public static int Range(int minInclusive, int maxExclusive) => maxExclusive <= minInclusive ? minInclusive : _r.Next(minInclusive, maxExclusive);
        public static float Range(float min, float max) => min + (max - min) * value;
        public static Vector3 insideUnitSphere
        {
            get
            {
                while (true)
                {
                    var v = new Vector3(value * 2f - 1f, value * 2f - 1f, value * 2f - 1f);
                    if (v.sqrMagnitude <= 1f) return v;
                }
            }
        }
        public static Vector2 insideUnitCircle
        {
            get
            {
                while (true)
                {
                    var v = new Vector2(value * 2f - 1f, value * 2f - 1f);
                    if (v.sqrMagnitude <= 1f) return v;
                }
            }
        }
        public static Vector3 onUnitSphere => insideUnitSphere.normalized is var n && n.sqrMagnitude > 0f ? n : Vector3.up;
        public static Quaternion rotation => Quaternion.Euler(value * 360f, value * 360f, value * 360f);
        public static Color ColorHSV() => new Color(value, value, value, 1f);
    }

    public struct BoneWeight
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
    }

    public struct Plane
    {
        Vector3 _n; float _d;
        public Plane(Vector3 inNormal, float d) { _n = inNormal.normalized; _d = d; }
        public Plane(Vector3 inNormal, Vector3 inPoint) { _n = inNormal.normalized; _d = -Vector3.Dot(_n, inPoint); }
        public Plane(Vector3 a, Vector3 b, Vector3 c) { _n = Vector3.Cross(b - a, c - a).normalized; _d = -Vector3.Dot(_n, a); }
        public Vector3 normal { get => _n; set => _n = value; }
        public float distance { get => _d; set => _d = value; }
        public float GetDistanceToPoint(Vector3 p) => Vector3.Dot(_n, p) + _d;
        public bool GetSide(Vector3 p) => GetDistanceToPoint(p) > 0f;
        public Vector3 ClosestPointOnPlane(Vector3 p) => p - _n * GetDistanceToPoint(p);
        public void SetNormalAndPosition(Vector3 n, Vector3 p) { _n = n.normalized; _d = -Vector3.Dot(_n, p); }
        public bool Raycast(Ray ray, out float enter)
        {
            float vd = Vector3.Dot(ray.direction, _n), nd = -Vector3.Dot(ray.origin, _n) - _d;
            if (Mathf.Approximately(vd, 0f)) { enter = 0f; return false; }
            enter = nd / vd;
            return enter > 0f;
        }
    }

    public struct Ray
    {
        Vector3 _o, _d;
        public Ray(Vector3 origin, Vector3 direction) { _o = origin; _d = direction.normalized; }
        public Vector3 origin { get => _o; set => _o = value; }
        public Vector3 direction { get => _d; set => _d = value.normalized; }
        public Vector3 GetPoint(float t) => _o + _d * t;
    }

    public static class GeometryUtility
    {
        /// <summary>Frustum planes of a perspective camera (normals point inwards, like Unity).</summary>
        public static void CalculateFrustumPlanes(Camera cam, Plane[] planes)
        {
            var t = cam.transform;
            Vector3 p = t.position, f = t.forward, r = t.right, u = t.up;
            float tv = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), th = tv * cam.aspect;
            planes[0] = new Plane(Vector3.Cross(u, f + r * th).normalized * -1f, p);   // left
            planes[1] = new Plane(Vector3.Cross(f - r * th, u).normalized * -1f, p);   // right
            planes[2] = new Plane(Vector3.Cross(f - u * tv, r).normalized * 1f, p);    // bottom
            planes[3] = new Plane(Vector3.Cross(r, f + u * tv).normalized * 1f, p);    // top
            planes[4] = new Plane(f, p + f * cam.nearClipPlane);                       // near
            planes[5] = new Plane(-f, p + f * cam.farClipPlane);                       // far
            // make every normal point towards the frustum center
            Vector3 c = p + f * (cam.nearClipPlane + cam.farClipPlane) * 0.5f;
            for (int i = 0; i < 4; i++) if (planes[i].GetDistanceToPoint(c) < 0f) planes[i] = new Plane(-planes[i].normal, p);
        }

        public static Plane[] CalculateFrustumPlanes(Camera cam) { var a = new Plane[6]; CalculateFrustumPlanes(cam, a); return a; }

        public static bool TestPlanesAABB(Plane[] planes, Bounds b)
        {
            foreach (var pl in planes)
            {
                Vector3 n = pl.normal, e = b.extents;
                float r = e.x * Mathf.Abs(n.x) + e.y * Mathf.Abs(n.y) + e.z * Mathf.Abs(n.z);
                if (pl.GetDistanceToPoint(b.center) + r < 0f) return false;
            }
            return true;
        }
    }

    public static class GL
    {
        public static void PushMatrix() { }
        public static void PopMatrix() { }
        public static void Clear(bool depth, bool color, Color c) { }
        public static bool sRGBWrite;
    }

    public static class Graphics
    {
        public static void Blit(Texture src, RenderTexture dst) { }
        public static void Blit(Texture src, RenderTexture dst, Material m) { }
    }

    public sealed class MaterialPropertyBlock
    {
        readonly Dictionary<int, object> _v = new Dictionary<int, object>();
        public bool isEmpty => _v.Count == 0;
        public void Clear() => _v.Clear();
        public void SetColor(int id, Color c) => _v[id] = c;
        public void SetColor(string n, Color c) => SetColor(Shader.PropertyToID(n), c);
        public void SetFloat(int id, float f) => _v[id] = f;
        public void SetFloat(string n, float f) => SetFloat(Shader.PropertyToID(n), f);
        public void SetVector(int id, Vector4 v) => _v[id] = v;
        public void SetVector(string n, Vector4 v) => SetVector(Shader.PropertyToID(n), v);
        public void SetTexture(int id, Texture t) { if (t != null) _v[id] = t; }
        public void SetTexture(string n, Texture t) => SetTexture(Shader.PropertyToID(n), t);
        public Color GetColor(int id) => _v.TryGetValue(id, out var o) && o is Color c ? c : Color.clear;
        public float GetFloat(int id) => _v.TryGetValue(id, out var o) && o is float f ? f : 0f;
        public Texture GetTexture(int id) => _v.TryGetValue(id, out var o) ? o as Texture : null;
    }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public static Camera current => null;
        public static event Action<Camera> onPreCull;
        public static event Action<Camera> onPreRender;
        public static event Action<Camera> onPostRender;
        public float fieldOfView = 60f, nearClipPlane = 0.3f, farClipPlane = 1000f, depth;
        float _aspect = -1f;
        public float aspect { get => _aspect > 0f ? _aspect : (float)Screen.width / Mathf.Max(1, Screen.height); set => _aspect = value; }
        public void ResetAspect() => _aspect = -1f;
        public bool orthographic;
        public float orthographicSize = 5f;
        public CameraClearFlags clearFlags = CameraClearFlags.Skybox;
        public Color backgroundColor = Color.black;
        public int cullingMask = -1;
        public RenderingPath renderingPath = RenderingPath.UsePlayerSettings;
        public bool allowHDR, allowMSAA, useOcclusionCulling = true, forceIntoRenderTexture;
        public RenderTexture targetTexture;
        public Rect rect = new Rect(0, 0, 1, 1);
        public int pixelWidth => targetTexture != null ? targetTexture.width : Screen.width;
        public int pixelHeight => targetTexture != null ? targetTexture.height : Screen.height;
        public void Render() { }
        public Matrix4x4 worldToCameraMatrix => transform.worldToLocalMatrix;
        public Vector3 WorldToViewportPoint(Vector3 p)
        {
            Vector3 v = transform.InverseTransformPoint(p);
            float tv = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            if (Mathf.Abs(v.z) < 1e-6f) v.z = 1e-6f;
            return new Vector3(v.x / (v.z * tv * aspect) * 0.5f + 0.5f, v.y / (v.z * tv) * 0.5f + 0.5f, v.z);
        }
        public Vector3 WorldToScreenPoint(Vector3 p) { var v = WorldToViewportPoint(p); return new Vector3(v.x * pixelWidth, v.y * pixelHeight, v.z); }
        public Ray ViewportPointToRay(Vector3 vp)
        {
            float tv = Mathf.Tan(fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 d = new Vector3((vp.x * 2f - 1f) * tv * aspect, (vp.y * 2f - 1f) * tv, 1f);
            return new Ray(transform.position, transform.TransformDirection(d));
        }
    }

    /// <summary>
    /// CPU skinning stand-in: BakeMesh does linear blend skinning with the bones' current transforms and the mesh
    /// bindposes/boneWeights, output in the renderer's local space (Unity's BakeMesh semantics, unscaled).
    /// </summary>
    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh;
        public Transform[] bones = new Transform[0];
        public Transform rootBone;
        public bool updateWhenOffscreen;
        public Bounds localBounds;
        public SkinQuality quality;
        public bool skinnedMotionVectors;
        public bool forceMatrixRecalculationPerRender;

        public void BakeMesh(Mesh mesh) => BakeMesh(mesh, false);

        public void BakeMesh(Mesh mesh, bool useScale)
        {
            var src = sharedMesh;
            if (mesh == null || src == null) return;
            var t = transform;
            Matrix4x4 rendererToWorld = useScale ? t.localToWorldMatrix : Matrix4x4.TRS(t.position, t.rotation, Vector3.one);
            Matrix4x4 worldToRenderer = rendererToWorld.inverse;
            var bp = src.bindposes;
            var bw = src.boneWeights;
            int bc = Math.Min(bones.Length, bp.Length);
            var skin = new Matrix4x4[bc];
            for (int i = 0; i < bc; i++) skin[i] = bones[i] != null ? worldToRenderer * bones[i].localToWorldMatrix * bp[i] : Matrix4x4.identity;

            int n = src.vertices.Length;
            var v = new Vector3[n];
            var nn = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 p = src.vertices[i], q = i < src.normals.Length ? src.normals[i] : Vector3.up;
                if (i >= bw.Length || bc == 0) { v[i] = p; nn[i] = q; continue; }
                var w = bw[i];
                Vector3 sp = Vector3.zero, sn = Vector3.zero;
                Acc(skin, w.boneIndex0, w.weight0, p, q, ref sp, ref sn);
                Acc(skin, w.boneIndex1, w.weight1, p, q, ref sp, ref sn);
                Acc(skin, w.boneIndex2, w.weight2, p, q, ref sp, ref sn);
                Acc(skin, w.boneIndex3, w.weight3, p, q, ref sp, ref sn);
                float ws = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                if (ws < 1e-6f) { sp = p; sn = q; }
                v[i] = sp; nn[i] = sn.normalized;
            }
            mesh.vertices = v;
            mesh.normals = nn;
            mesh.uv = (Vector2[])src.uv.Clone();
            mesh.colors32 = (Color32[])src.colors32.Clone();
            mesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(new List<int>(src.GetTriangles(s)), s, false);
            mesh.RecalculateBounds();
        }

        static void Acc(Matrix4x4[] skin, int b, float w, Vector3 p, Vector3 n, ref Vector3 sp, ref Vector3 sn)
        {
            if (w <= 0f || b < 0 || b >= skin.Length) return;
            sp += skin[b].MultiplyPoint3x4(p) * w;
            sn += skin[b].MultiplyVector(n) * w;
        }
    }
}
