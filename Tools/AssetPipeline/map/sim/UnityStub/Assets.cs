// Minimal managed mesh / renderer / material stand-ins for the offline map QA harness. NOT Unity.
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace UnityEngine
{
    public class Shader : Object
    {
        static readonly Dictionary<string, Shader> _cache = new Dictionary<string, Shader>();
        public static Shader Find(string n)
        {
            if (!_cache.TryGetValue(n, out var s)) _cache[n] = s = new Shader { name = n };
            return s;
        }

        // property ids: stable per name (assigned in first-use order), resolvable back to the name
        static readonly Dictionary<string, int> _ids = new Dictionary<string, int>();
        static readonly List<string> _names = new List<string>();
        public static int PropertyToID(string n)
        {
            if (!_ids.TryGetValue(n, out int id)) { id = _names.Count + 1; _ids[n] = id; _names.Add(n); }
            return id;
        }
        internal static string NameOf(int id) => id > 0 && id <= _names.Count ? _names[id - 1] : "";

        // globals only feed the GPU: no-ops
        public static void SetGlobalFloat(int id, float v) { }
        public static void SetGlobalFloat(string n, float v) { }
        public static void SetGlobalInt(int id, int v) { }
        public static void SetGlobalVector(int id, Vector4 v) { }
        public static void SetGlobalVector(string n, Vector4 v) { }
        public static void SetGlobalColor(int id, Color c) { }
        public static void SetGlobalColor(string n, Color c) { }
        public static void SetGlobalTexture(int id, Texture t) { }
        public static void SetGlobalVectorArray(int id, Vector4[] v) { }
        public static void SetGlobalVectorArray(int id, List<Vector4> v) { }
        public static void SetGlobalFloatArray(int id, float[] v) { }
        public static void SetGlobalMatrix(int id, Matrix4x4 m) { }
        public static void EnableKeyword(string k) { }
        public static void DisableKeyword(string k) { }
    }

    public class Texture : Object
    {
        public FilterMode filterMode;
        public int anisoLevel;
        public TextureWrapMode wrapMode { get => wrapModeU; set { wrapModeU = value; wrapModeV = value; } }
        public TextureWrapMode wrapModeU, wrapModeV;
        public int width = 16, height = 16;
    }

    public class Texture2D : Texture
    {
        static Texture2D _white;
        public Texture2D(int w, int h, TextureFormat f = TextureFormat.RGBA32, bool mip = false) { width = w; height = h; }
        public static Texture2D whiteTexture => _white ?? (_white = new Texture2D(4, 4) { name = "white" });
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels(Color[] c) { }
        public void SetPixels32(Color32[] c) { }
        public void Apply() { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable = false) { }
    }

    public class RenderTexture : Texture
    {
        public int depth;
        public RenderTextureFormat format;
        bool _created;
        public RenderTexture(int w, int h, int depth, RenderTextureFormat f = RenderTextureFormat.Default) { width = w; height = h; this.depth = depth; format = f; }
        public bool IsCreated() => _created;
        public bool Create() { _created = true; return true; }
        public void Release() { _created = false; }
        public static RenderTexture active;
        public static RenderTexture GetTemporary(int w, int h, int depth = 0, RenderTextureFormat f = RenderTextureFormat.Default) => new RenderTexture(w, h, depth, f);
        public static void ReleaseTemporary(RenderTexture rt) { }
    }

    public static class Resources
    {
        public static readonly HashSet<string> Requested = new HashSet<string>();
        public static T Load<T>(string path) where T : Object { Requested.Add(path); return null; }
    }

    public class Material : Object
    {
        public Shader shader;
        public Texture mainTexture;
        public Color color = Color.white;
        public Material(Shader s) { shader = s; }
        public Material(Material m) { shader = m.shader; mainTexture = m.mainTexture; color = m.color; name = m.name; }
        public int renderQueue = -1;
        public bool HasProperty(string p) => p == "_Cutoff" || p == "_Color" || p == "_MainTex";
        public bool HasProperty(int id) => HasProperty(Shader.NameOf(id));
        public void SetFloat(string p, float v) { }
        public void SetFloat(int id, float v) { }
        public void SetInt(string p, int v) { }
        public void SetInt(int id, int v) { }
        public void SetColor(string p, Color c) { if (p == "_Color") color = c; }
        public void SetColor(int id, Color c) => SetColor(Shader.NameOf(id), c);
        public void SetTexture(string p, Texture t) { if (p == "_MainTex") mainTexture = t; }
        public void SetTexture(int id, Texture t) => SetTexture(Shader.NameOf(id), t);
        public void SetVector(string p, Vector4 v) { }
        public void SetVector(int id, Vector4 v) { }
        public Color GetColor(string p) => p == "_Color" ? color : Color.clear;
        public Color GetColor(int id) => GetColor(Shader.NameOf(id));
        public Texture GetTexture(string p) => p == "_MainTex" ? mainTexture : null;
        public Texture GetTexture(int id) => GetTexture(Shader.NameOf(id));
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
    }

    public class Mesh : Object
    {
        public Vector3[] vertices = new Vector3[0];
        public Vector3[] normals = new Vector3[0];
        public Vector2[] uv = new Vector2[0];
        public Color32[] colors32 = new Color32[0];
        public Matrix4x4[] bindposes = new Matrix4x4[0];
        public BoneWeight[] boneWeights = new BoneWeight[0];
        readonly List<int[]> _subs = new List<int[]>();
        public IndexFormat indexFormat;
        public Bounds bounds;
        public int vertexCount => vertices.Length;
        public int subMeshCount
        {
            get => _subs.Count;
            set { while (_subs.Count < value) _subs.Add(new int[0]); while (_subs.Count > value) _subs.RemoveAt(_subs.Count - 1); }
        }
        public void SetVertices(List<Vector3> v) => vertices = v.ToArray();
        public void SetNormals(List<Vector3> n) => normals = n.ToArray();
        public void SetUVs(int ch, List<Vector2> u) { if (ch == 0) uv = u.ToArray(); }
        public void SetColors(List<Color32> c) => colors32 = c.ToArray();
        public void SetTriangles(List<int> t, int sub, bool calc = true) { subMeshCount = System.Math.Max(subMeshCount, sub + 1); _subs[sub] = t.ToArray(); }
        public void SetTriangles(int[] t, int sub, bool calc = true) => SetTriangles(new List<int>(t), sub, calc);
        public int[] GetTriangles(int sub) => _subs[sub];
        /// <summary>All submeshes concatenated (get); set replaces the mesh with a single submesh, like Unity.</summary>
        public int[] triangles
        {
            get { var l = new List<int>(); foreach (var t in _subs) l.AddRange(t); return l.ToArray(); }
            set { subMeshCount = 1; _subs[0] = (int[])value.Clone(); }
        }
        public void RecalculateBounds()
        {
            if (vertices.Length == 0) { bounds = new Bounds(); return; }
            Vector3 mn = vertices[0], mx = vertices[0];
            foreach (var v in vertices) { mn = Vector3.Min(mn, v); mx = Vector3.Max(mx, v); }
            bounds = new Bounds((mn + mx) * 0.5f, mx - mn);
        }
    }

    public class MeshFilter : Component
    {
        public Mesh sharedMesh;
        public Mesh mesh { get => sharedMesh; set => sharedMesh = value; }
    }

    public class Renderer : Component
    {
        public bool enabled = true;
        public Material[] sharedMaterials = new Material[0];
        public Material sharedMaterial { get => sharedMaterials.Length > 0 ? sharedMaterials[0] : null; set => sharedMaterials = new[] { value }; }
        public Material material { get => sharedMaterial; set => sharedMaterial = value; }
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public LightProbeUsage lightProbeUsage;
        public ReflectionProbeUsage reflectionProbeUsage;
        public MotionVectorGenerationMode motionVectorGenerationMode;
        public int sortingOrder;
        public bool allowOcclusionWhenDynamic = true;
        public bool isVisible => false;
        MaterialPropertyBlock _block;
        public bool HasPropertyBlock() => _block != null;
        public void SetPropertyBlock(MaterialPropertyBlock b) => _block = b;
        public void GetPropertyBlock(MaterialPropertyBlock b) { }

        /// <summary>World AABB of the local mesh bounds (MeshFilter mesh, or SkinnedMeshRenderer.localBounds).</summary>
        public virtual Bounds bounds
        {
            get
            {
                Bounds lb;
                if (this is SkinnedMeshRenderer smr) lb = smr.localBounds;
                else { var mf = GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) return new Bounds(transform.position, Vector3.zero); lb = mf.sharedMesh.bounds; }
                var m = transform.localToWorldMatrix;
                Vector3 c = m.MultiplyPoint3x4(lb.center), e = lb.extents;
                Vector3 ax = m.MultiplyVector(new Vector3(e.x, 0, 0)), ay = m.MultiplyVector(new Vector3(0, e.y, 0)), az = m.MultiplyVector(new Vector3(0, 0, e.z));
                var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
                return new Bounds(c, ext * 2f);
            }
        }
    }

    public enum MotionVectorGenerationMode { Camera = 0, Object = 1, ForceNoMotion = 2 }

    public class MeshRenderer : Renderer { }
}
