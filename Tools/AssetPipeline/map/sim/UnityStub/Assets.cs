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
    }

    public class Texture : Object
    {
        public FilterMode filterMode;
        public int anisoLevel;
        public TextureWrapMode wrapMode;
        public int width = 16, height = 16;
    }

    public class Texture2D : Texture
    {
        static Texture2D _white;
        public Texture2D(int w, int h, TextureFormat f = TextureFormat.RGBA32, bool mip = false) { width = w; height = h; }
        public static Texture2D whiteTexture => _white ?? (_white = new Texture2D(4, 4) { name = "white" });
        public void SetPixel(int x, int y, Color c) { }
        public void Apply() { }
    }

    public class RenderTexture : Texture { }

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
        public bool HasProperty(string p) => p == "_Cutoff" || p == "_Color" || p == "_MainTex";
        public void SetFloat(string p, float v) { }
        public void SetColor(string p, Color c) { if (p == "_Color") color = c; }
        public void SetTexture(string p, Texture t) { if (p == "_MainTex") mainTexture = t; }
        public void SetVector(string p, Vector4 v) { }
    }

    public class Mesh : Object
    {
        public Vector3[] vertices = new Vector3[0];
        public Vector3[] normals = new Vector3[0];
        public Vector2[] uv = new Vector2[0];
        public Color32[] colors32 = new Color32[0];
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
        public int[] GetTriangles(int sub) => _subs[sub];
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
    }

    public class MeshRenderer : Renderer { }
}
