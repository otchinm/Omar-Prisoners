// Minimal managed stand-ins for the UnityEngine object model (GameObject / Transform / Mesh / renderers)
// used by Scripts/Characters, for offline previews only. Magic methods (Awake/OnEnable/Start/Update/LateUpdate)
// are invoked by reflection (Awake + OnEnable on AddComponent, the rest by the harness through Runtime.Tick).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Rendering;

namespace UnityEngine
{
    public class Object
    {
        string _name = "";
        internal bool _destroyed;
        public string name { get => _name; set => _name = value ?? ""; }
        public int GetInstanceID() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);

        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a._destroyed, bn = ReferenceEquals(b, null) || b._destroyed;
            if (an && bn) return true;
            if (an || bn) return false;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) => !(a == b);
        public static implicit operator bool(Object o) => !(o == null);
        public override bool Equals(object o) => o is Object u ? this == u : false;
        public override int GetHashCode() => GetInstanceID();

        public static void Destroy(Object o) => Destroy(o, 0f);
        public static void Destroy(Object o, float t)
        {
            if (o == null) return;
            if (o is GameObject go) go.DestroyRecursive();
            else if (o is Component c) { c.gameObject._components.Remove(c); c._destroyed = true; }
            else o._destroyed = true;
        }
        public static void DestroyImmediate(Object o) => Destroy(o);
        public static T Instantiate<T>(T o) where T : Object => throw new NotSupportedException("Instantiate not supported in preview shim");
        public static T FindFirstObjectByType<T>() where T : Object => null;
    }

    public sealed class GameObject : Object
    {
        internal readonly List<Component> _components = new List<Component>();
        public Transform transform { get; }
        public int layer;
        public string tag = "Untagged";
        bool _active = true;
        public bool activeSelf => _active;
        public bool activeInHierarchy => _active && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);

        public GameObject() : this("GameObject") { }
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform();
            transform._go = this;
            _components.Add(transform);
            Runtime.All.Add(this);
        }
        public GameObject(string name, params Type[] comps) : this(name) { foreach (var t in comps) AddComponent(t); }

        public void SetActive(bool v) => _active = v;

        public T AddComponent<T>() where T : Component => (T)AddComponent(typeof(T));
        public Component AddComponent(Type t)
        {
            foreach (RequireComponent rc in t.GetCustomAttributes(typeof(RequireComponent), true))
                if (rc.m_Type0 != null && GetComponent(rc.m_Type0) == null) AddComponent(rc.m_Type0);
            var c = (Component)Activator.CreateInstance(t, true);
            c._go = this;
            _components.Add(c);
            Runtime.Invoke(c, "Awake");
            Runtime.Invoke(c, "OnEnable");
            return c;
        }
        public T GetComponent<T>() where T : class
        {
            foreach (var c in _components) if (c is T t && !c._destroyed) return t;
            return null;
        }
        public Component GetComponent(Type t)
        {
            foreach (var c in _components) if (t.IsInstanceOfType(c) && !c._destroyed) return c;
            return null;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class
        {
            var l = new List<T>();
            Collect(transform, l, includeInactive);
            return l.ToArray();
        }
        static void Collect<T>(Transform t, List<T> l, bool inc) where T : class
        {
            if (!inc && !t.gameObject.activeSelf) return;
            foreach (var c in t.gameObject._components) if (c is T x) l.Add(x);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), l, inc);
        }
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class
        {
            var a = GetComponentsInChildren<T>(includeInactive);
            return a.Length > 0 ? a[0] : null;
        }
        public T GetComponentInParent<T>() where T : class
        {
            for (Transform t = transform; t != null; t = t.parent)
            {
                var c = t.gameObject.GetComponent<T>();
                if (c != null) return c;
            }
            return null;
        }
        internal void DestroyRecursive()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) transform.GetChild(i).gameObject.DestroyRecursive();
            foreach (var c in _components) c._destroyed = true;
            transform.SetParent(null, false);
            _destroyed = true;
            Runtime.All.Remove(this);
        }
    }

    public class Component : Object
    {
        internal GameObject _go;
        public GameObject gameObject => _go;
        public Transform transform => _go.transform;
        public new string name { get => _go.name; set => _go.name = value; }
        public string tag => _go.tag;
        public T GetComponent<T>() where T : class => _go.GetComponent<T>();
        public Component GetComponent(Type t) => _go.GetComponent(t);
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class => _go.GetComponentsInChildren<T>(includeInactive);
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class => _go.GetComponentInChildren<T>(includeInactive);
        public T GetComponentInParent<T>() where T : class => _go.GetComponentInParent<T>();
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }

    public class MonoBehaviour : Behaviour
    {
        public static void print(object o) => Debug.Log(o);
    }

    public sealed class Transform : Component, IEnumerable
    {
        Transform _parent;
        readonly List<Transform> _children = new List<Transform>();
        public Vector3 localPosition = Vector3.zero;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;

        public Transform parent { get => _parent; set => SetParent(value, true); }
        public int childCount => _children.Count;
        public Transform GetChild(int i) => _children[i];
        public Transform root { get { var t = this; while (t._parent != null) t = t._parent; return t; } }
        public IEnumerator GetEnumerator() => _children.ToArray().GetEnumerator();

        public void SetParent(Transform p) => SetParent(p, true);
        public void SetParent(Transform p, bool worldPositionStays)
        {
            Vector3 wp = position; Quaternion wr = rotation;
            if (_parent != null) _parent._children.Remove(this);
            _parent = p;
            if (p != null) p._children.Add(this);
            if (worldPositionStays) { position = wp; rotation = wr; }
        }
        public void SetSiblingIndex(int i) { }
        public void SetAsFirstSibling() { }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }

        public Matrix4x4 localToWorldMatrix
        {
            get
            {
                var m = Matrix4x4.TRS(localPosition, localRotation, localScale);
                return _parent != null ? _parent.localToWorldMatrix * m : m;
            }
        }
        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;

        public Vector3 position
        {
            get => _parent != null ? _parent.localToWorldMatrix.MultiplyPoint3x4(localPosition) : localPosition;
            set => localPosition = _parent != null ? _parent.worldToLocalMatrix.MultiplyPoint3x4(value) : value;
        }
        public Quaternion rotation
        {
            get => _parent != null ? _parent.rotation * localRotation : localRotation;
            set => localRotation = _parent != null ? Quaternion.Inverse(_parent.rotation) * value : value;
        }
        public Vector3 eulerAngles { get => rotation.eulerAngles; set => rotation = Quaternion.Euler(value); }
        public Vector3 localEulerAngles { get => localRotation.eulerAngles; set => localRotation = Quaternion.Euler(value); }
        public Vector3 lossyScale => _parent != null ? Vector3.Scale(_parent.lossyScale, localScale) : localScale;
        public Vector3 forward { get => rotation * Vector3.forward; set => rotation = Quaternion.LookRotation(value); }
        public Vector3 up => rotation * Vector3.up;
        public Vector3 right => rotation * Vector3.right;

        public Vector3 TransformPoint(Vector3 p) => localToWorldMatrix.MultiplyPoint3x4(p);
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        public Vector3 TransformVector(Vector3 v) => localToWorldMatrix.MultiplyVector(v);
        public Vector3 InverseTransformPoint(Vector3 p) => worldToLocalMatrix.MultiplyPoint3x4(p);
        public Vector3 InverseTransformDirection(Vector3 d) => Quaternion.Inverse(rotation) * d;
        public Vector3 InverseTransformVector(Vector3 v) => worldToLocalMatrix.MultiplyVector(v);
        public void Rotate(Vector3 euler) => localRotation = localRotation * Quaternion.Euler(euler);
        public void Translate(Vector3 v) => localPosition += v;
        public void LookAt(Vector3 p) => rotation = Quaternion.LookRotation(p - position);

        public Transform Find(string n)
        {
            foreach (var c in _children) if (c.gameObject.name == n) return c;
            return null;
        }
    }

    public class Renderer : Component
    {
        public bool enabled = true;
        public Material[] sharedMaterials = new Material[0];
        public Material sharedMaterial { get => sharedMaterials.Length > 0 ? sharedMaterials[0] : null; set => sharedMaterials = new[] { value }; }
        public Material material { get => sharedMaterial; set => sharedMaterial = value; }
        public Material[] materials { get => sharedMaterials; set => sharedMaterials = value; }
        public ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public LightProbeUsage lightProbeUsage;
        public ReflectionProbeUsage reflectionProbeUsage;
        public bool forceRenderingOff;
        public Bounds bounds => new Bounds(transform.position, Vector3.one);
    }

    public sealed class MeshRenderer : Renderer { }

    public sealed class MeshFilter : Component
    {
        public Mesh sharedMesh;
        public Mesh mesh { get => sharedMesh; set => sharedMesh = value; }
    }

    public enum SkinQuality { Auto = 0, Bone1 = 1, Bone2 = 2, Bone4 = 4 }

    public sealed class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh;
        public Transform[] bones = new Transform[0];
        public Transform rootBone;
        public bool updateWhenOffscreen;
        public Bounds localBounds;
        public SkinQuality quality;
        public bool skinnedMotionVectors;

        public void BakeMesh(Mesh mesh) => BakeMesh(mesh, true);
        public void BakeMesh(Mesh mesh, bool useScale)
        {
            var src = sharedMesh;
            var bw = src.boneWeights;
            var bp = src.bindposes;
            var v = src.vertices; var n = src.normals;
            var skin = new Matrix4x4[bones.Length];
            Matrix4x4 toLocal = transform.worldToLocalMatrix;
            for (int i = 0; i < bones.Length; i++) skin[i] = toLocal * bones[i].localToWorldMatrix * bp[i];
            var ov = new Vector3[v.Length]; var on = new Vector3[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var w = bw[i];
                Vector3 p = Vector3.zero, q = Vector3.zero;
                void Acc(int b, float wt) { if (wt <= 0) return; p += skin[b].MultiplyPoint3x4(v[i]) * wt; q += skin[b].MultiplyVector(n.Length > i ? n[i] : Vector3.up) * wt; }
                Acc(w.boneIndex0, w.weight0); Acc(w.boneIndex1, w.weight1); Acc(w.boneIndex2, w.weight2); Acc(w.boneIndex3, w.weight3);
                ov[i] = p; on[i] = q.normalized;
            }
            mesh.Clear();
            mesh.vertices = ov; mesh.normals = on; mesh.uv = src.uv; mesh.colors32 = src.colors32;
            mesh.subMeshCount = src.subMeshCount;
            for (int s = 0; s < src.subMeshCount; s++) mesh.SetTriangles(src.GetTriangles(s), s);
        }
    }

    public sealed class Mesh : Object
    {
        Vector3[] _v = new Vector3[0], _n = new Vector3[0];
        Vector2[] _uv = new Vector2[0];
        Color32[] _c = new Color32[0];
        List<int[]> _sub = new List<int[]> { new int[0] };
        public BoneWeight[] boneWeights = new BoneWeight[0];
        public Matrix4x4[] bindposes = new Matrix4x4[0];
        public IndexFormat indexFormat;
        public Bounds bounds;

        public Mesh() { }
        public Vector3[] vertices { get => (Vector3[])_v.Clone(); set => _v = (Vector3[])value.Clone(); }
        public Vector3[] normals { get => (Vector3[])_n.Clone(); set => _n = (Vector3[])value.Clone(); }
        public Vector2[] uv { get => (Vector2[])_uv.Clone(); set => _uv = (Vector2[])value.Clone(); }
        public Color32[] colors32 { get => (Color32[])_c.Clone(); set => _c = (Color32[])value.Clone(); }
        public int vertexCount => _v.Length;
        public int[] triangles
        {
            get { var l = new List<int>(); foreach (var s in _sub) l.AddRange(s); return l.ToArray(); }
            set { _sub = new List<int[]> { (int[])value.Clone() }; }
        }
        public int subMeshCount
        {
            get => _sub.Count;
            set { while (_sub.Count < value) _sub.Add(new int[0]); while (_sub.Count > value) _sub.RemoveAt(_sub.Count - 1); }
        }
        public void SetVertices(List<Vector3> l) => _v = l.ToArray();
        public void SetNormals(List<Vector3> l) => _n = l.ToArray();
        public void SetUVs(int ch, List<Vector2> l) { if (ch == 0) _uv = l.ToArray(); }
        public void SetColors(List<Color32> l) => _c = l.ToArray();
        public void SetTriangles(List<int> t, int sub, bool calcBounds = true) { subMeshCount = Math.Max(subMeshCount, sub + 1); _sub[sub] = t.ToArray(); }
        public void SetTriangles(int[] t, int sub, bool calcBounds = true) { subMeshCount = Math.Max(subMeshCount, sub + 1); _sub[sub] = (int[])t.Clone(); }
        public int[] GetTriangles(int sub) => (int[])_sub[sub].Clone();
        public void RecalculateBounds()
        {
            if (_v.Length == 0) { bounds = new Bounds(); return; }
            Vector3 mn = _v[0], mx = _v[0];
            foreach (var p in _v) { mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
            bounds = new Bounds((mn + mx) * 0.5f, mx - mn);
        }
        public void RecalculateNormals()
        {
            var n = new Vector3[_v.Length];
            foreach (var s in _sub)
                for (int i = 0; i + 2 < s.Length; i += 3)
                {
                    Vector3 f = Vector3.Cross(_v[s[i + 1]] - _v[s[i]], _v[s[i + 2]] - _v[s[i]]);
                    n[s[i]] += f; n[s[i + 1]] += f; n[s[i + 2]] += f;
                }
            for (int i = 0; i < n.Length; i++) n[i] = n[i].normalized;
            _n = n;
        }
        public void Clear() { _v = new Vector3[0]; _n = new Vector3[0]; _uv = new Vector2[0]; _c = new Color32[0]; _sub = new List<int[]> { new int[0] }; }
        public void MarkDynamic() { }
        public void UploadMeshData(bool b) { }
    }

    public class Shader : Object
    {
        public static Shader Find(string n) => new Shader { name = n };
        public static int PropertyToID(string n) => n.GetHashCode();
        public bool isSupported => true;
    }

    public class Texture : Object
    {
        public FilterMode filterMode;
        public int anisoLevel;
        public TextureWrapMode wrapMode;
        public int width, height;
    }

    public enum FilterMode { Point = 0, Bilinear = 1, Trilinear = 2 }
    public enum TextureWrapMode { Repeat = 0, Clamp = 1, Mirror = 2, MirrorOnce = 3 }
    public enum TextureFormat { Alpha8 = 1, RGB24 = 3, RGBA32 = 4, ARGB32 = 5 }

    public sealed class Texture2D : Texture
    {
        public Texture2D(int w, int h) { width = w; height = h; }
        public Texture2D(int w, int h, TextureFormat f, bool mips) { width = w; height = h; }
        public static Texture2D whiteTexture { get; } = new Texture2D(4, 4) { name = "white" };
        public void SetPixel(int x, int y, Color c) { }
        public void SetPixels32(Color32[] c) { }
        public void Apply() { }
        public void Apply(bool a, bool b = false) { }
    }

    public class Material : Object
    {
        public Shader shader;
        public Texture mainTexture;
        public Color color = Color.white;
        public int renderQueue;
        public Material(Shader s) { shader = s; }
        public Material(Material m) { shader = m.shader; mainTexture = m.mainTexture; color = m.color; name = m.name; }
        public bool HasProperty(string n) => true;
        public bool HasProperty(int n) => true;
        public void SetFloat(int n, float v) { }
        public void SetFloat(string n, float v) { }
        public void SetColor(string n, Color c) { }
        public void SetTexture(string n, Texture t) { }
        public void EnableKeyword(string k) { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object
        {
            if (typeof(T) == typeof(Texture2D)) return new Texture2D(256, 256) { name = path } as T;
            return null;
        }
    }

    public class Collider : Component { public bool isTrigger; public bool enabled = true; }
    public sealed class BoxCollider : Collider { public Vector3 size = Vector3.one; public Vector3 center; }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequireComponent : Attribute
    {
        public Type m_Type0;
        public RequireComponent(Type t) { m_Type0 = t; }
    }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int o) { } }

    /// <summary>Preview runtime: registry + magic method dispatch.</summary>
    public static class Runtime
    {
        public static readonly List<GameObject> All = new List<GameObject>();

        public static void Invoke(object c, string method)
        {
            var m = c.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m != null && m.GetParameters().Length == 0) m.Invoke(c, null);
        }

        static readonly HashSet<object> _started = new HashSet<object>();

        /// <summary>Advance one frame: Start (once), Update, LateUpdate on every enabled behaviour.</summary>
        public static void Tick(float dt)
        {
            Time.deltaTime = dt;
            Time.time += dt;
            Time.frameCount++;
            var behaviours = new List<Behaviour>();
            foreach (var go in All.ToArray())
                if (go.activeInHierarchy)
                    foreach (var c in go._components)
                        if (c is Behaviour b && b.enabled && !b._destroyed) behaviours.Add(b);
            foreach (var b in behaviours) if (_started.Add(b)) Invoke(b, "Start");
            foreach (var b in behaviours) Invoke(b, "Update");
            foreach (var b in behaviours) Invoke(b, "LateUpdate");
        }
    }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off = 0, On = 1, TwoSided = 2, ShadowsOnly = 3 }
    public enum LightProbeUsage { Off = 0, BlendProbes = 1 }
    public enum ReflectionProbeUsage { Off = 0, BlendProbes = 1 }
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
}
