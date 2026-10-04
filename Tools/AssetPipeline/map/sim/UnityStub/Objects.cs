// Minimal managed scene graph + physics used by the offline map QA harness. NOT Unity.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public sealed class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }

    public enum Space { World = 0, Self = 1 }
    public enum FilterMode { Point = 0, Bilinear, Trilinear }
    public enum TextureFormat { RGB24 = 3, RGBA32 = 4, ARGB32 = 5 }
    public enum TextureWrapMode { Repeat = 0, Clamp, Mirror }
    public enum QueryTriggerInteraction { UseGlobal = 0, Ignore, Collide }

    public static class Debug
    {
        public static int Errors, Warnings;
        public static bool Quiet;
        public static readonly List<string> ErrorLog = new List<string>();
        public static void Log(object m) { if (!Quiet) Console.WriteLine("[log] " + m); }
        public static void LogWarning(object m) { Warnings++; if (!Quiet) Console.WriteLine("[warn] " + m); }
        public static void LogError(object m) { Errors++; ErrorLog.Add(m?.ToString()); Console.WriteLine("[ERROR] " + m); }
        public static void LogException(Exception e) => LogError(e);
        public static void Log(object m, Object ctx) => Log(m);
        public static void LogWarning(object m, Object ctx) => LogWarning(m);
        public static void LogError(object m, Object ctx) => LogError(m);
    }

    public static class Time
    {
        public static float time = 0f, deltaTime = 1f / 60f, unscaledTime = 0f, unscaledDeltaTime = 1f / 60f, realtimeSinceStartup = 0f, timeScale = 1f;
        public static double timeAsDouble => time;
        public static int frameCount = 0;
    }

    public class Object
    {
        static int _nextId = 1;
        readonly int _id = _nextId++;
        public string name;
        public HideFlags hideFlags;
        public static readonly List<GameObject> AllGameObjects = new List<GameObject>();
        public int GetInstanceID() => _id;
        public static void Destroy(Object o) { if (o is GameObject g) g.Destroyed = true; }
        public static void Destroy(Object o, float t) => Destroy(o);
        public static void DestroyImmediate(Object o) => Destroy(o);
        public static void DontDestroyOnLoad(Object o) { }
        public static void ResetWorld() { AllGameObjects.Clear(); Physics.Clear(); Random.Reset(); }
        public override string ToString() => name;
    }

    public class GameObject : Object
    {
        readonly List<Component> _components = new List<Component>();
        public readonly Transform transform;
        public int layer;
        public bool Destroyed;
        bool _active = true;

        public GameObject(string name = "GameObject")
        {
            this.name = name;
            transform = new Transform();
            transform.Attach(this);
            _components.Add(transform);
            AllGameObjects.Add(this);
        }

        public bool activeSelf => _active;
        public bool activeInHierarchy => _active && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        /// <summary>Like Unity, (de)activation adds / removes the colliders from physics immediately.</summary>
        public void SetActive(bool v) { if (_active == v) return; _active = v; Physics.Invalidate(); }
        public IReadOnlyList<Component> Components => _components;

        public T AddComponent<T>() where T : Component
        {
            var c = (T)Activator.CreateInstance(typeof(T));
            c.Attach(this);
            _components.Add(c);
            if (c is Collider col) Physics.Register(col);
            return c;
        }

        public T GetComponent<T>() where T : class
        {
            foreach (var c in _components) if (c is T t) return t;
            return null;
        }

        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class
        {
            var list = new List<T>();
            Collect(transform, list, includeInactive);
            return list.ToArray();
        }

        public void GetComponentsInChildren<T>(bool includeInactive, List<T> result) where T : class
        {
            result.Clear();
            Collect(transform, result, includeInactive);
        }

        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class
        {
            var a = GetComponentsInChildren<T>(includeInactive);
            return a.Length > 0 ? a[0] : null;
        }

        static void Collect<T>(Transform t, List<T> list, bool inactive) where T : class
        {
            if (!inactive && !t.gameObject.activeSelf) return;
            foreach (var c in t.gameObject._components) if (c is T x) list.Add(x);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), list, inactive);
        }
    }

    public class Component : Object
    {
        GameObject _go;
        internal void Attach(GameObject go) { _go = go; }
        public GameObject gameObject => _go;
        public Transform transform => _go.transform;
        public new string name { get => _go.name; set => _go.name = value; }
        public T GetComponent<T>() where T : class => _go.GetComponent<T>();
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : class => _go.GetComponentsInChildren<T>(includeInactive);
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> result) where T : class => _go.GetComponentsInChildren(includeInactive, result);
        public T GetComponentInChildren<T>(bool includeInactive = false) where T : class => _go.GetComponentInChildren<T>(includeInactive);
        public T GetComponentInParent<T>() where T : class
        {
            var t = transform;
            while (t != null) { var c = t.gameObject.GetComponent<T>(); if (c != null) return c; t = t.parent; }
            return null;
        }
    }

    public class Transform : Component, IEnumerable
    {
        Transform _parent;
        readonly List<Transform> _children = new List<Transform>();
        Vector3 _lp = Vector3.zero, _ls = Vector3.one;
        Quaternion _lr = Quaternion.identity;

        public Transform parent { get => _parent; set => SetParent(value, true); }
        public int childCount => _children.Count;
        public Transform GetChild(int i) => _children[i];
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

        public Vector3 localPosition { get => _lp; set => _lp = value; }
        public Quaternion localRotation { get => _lr; set => _lr = value.normalized; }
        public Vector3 localScale { get => _ls; set => _ls = value; }
        public Vector3 localEulerAngles { get => _lr.eulerAngles; set => _lr = Quaternion.Euler(value); }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public void SetLocalPositionAndRotation(Vector3 p, Quaternion r) { _lp = p; localRotation = r; }

        public Matrix4x4 localToWorldMatrix => _parent == null ? Matrix4x4.TRS(_lp, _lr, _ls) : _parent.localToWorldMatrix * Matrix4x4.TRS(_lp, _lr, _ls);
        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;

        public Vector3 position
        {
            get => _parent == null ? _lp : _parent.localToWorldMatrix.MultiplyPoint3x4(_lp);
            set => _lp = _parent == null ? value : _parent.worldToLocalMatrix.MultiplyPoint3x4(value);
        }

        public Quaternion rotation
        {
            get => _parent == null ? _lr : _parent.rotation * _lr;
            set => _lr = (_parent == null ? value : Quaternion.Inverse(_parent.rotation) * value).normalized;
        }

        public Vector3 lossyScale => _parent == null ? _ls : Vector3.Scale(_parent.lossyScale, _ls);
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 right => rotation * Vector3.right;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 eulerAngles => rotation.eulerAngles;

        public Vector3 TransformPoint(Vector3 p) => localToWorldMatrix.MultiplyPoint3x4(p);
        public Vector3 InverseTransformPoint(Vector3 p) => worldToLocalMatrix.MultiplyPoint3x4(p);
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        public Vector3 InverseTransformDirection(Vector3 d) => Quaternion.Inverse(rotation) * d;
        public void Rotate(float x, float y, float z, Space s = Space.Self)
        {
            var q = Quaternion.Euler(x, y, z);
            if (s == Space.Self) localRotation = _lr * q; else rotation = q * rotation;
        }
        public void Rotate(Vector3 euler, Space s = Space.Self) => Rotate(euler.x, euler.y, euler.z, s);
        public void Rotate(Vector3 axis, float angle, Space s = Space.Self)
        {
            if (s == Space.Self) localRotation = _lr * Quaternion.AngleAxis(angle, axis); else rotation = Quaternion.AngleAxis(angle, axis) * rotation;
        }
        public Transform Find(string n) { foreach (var c in _children) if (c.name == n) return c; return null; }
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }

    public class MonoBehaviour : Behaviour { }

    public class Collider : Component
    {
        public bool enabled = true;
        public bool isTrigger;
        public virtual Bounds bounds => new Bounds(transform.position, Vector3.zero);
    }

    public class BoxCollider : Collider
    {
        public Vector3 center = Vector3.zero;
        public Vector3 size = Vector3.one;

        public void WorldBox(out Vector3 c, out Quaternion r, out Vector3 half)
        {
            var t = transform;
            r = t.rotation;
            var s = t.lossyScale;
            c = t.TransformPoint(center);
            half = new Vector3(Mathf.Abs(size.x * s.x), Mathf.Abs(size.y * s.y), Mathf.Abs(size.z * s.z)) * 0.5f;
        }

        public override Bounds bounds
        {
            get
            {
                WorldBox(out var c, out var r, out var h);
                Vector3 ax = r * new Vector3(h.x, 0, 0), ay = r * new Vector3(0, h.y, 0), az = r * new Vector3(0, 0, h.z);
                var e = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
                return new Bounds(c, e * 2f);
            }
        }
    }

    public struct RaycastHit
    {
        public Vector3 point, normal;
        public float distance;
        public Collider collider;
        public Transform transform => collider != null ? collider.transform : null;
    }

    /// <summary>Box-collider-only physics with Unity's query semantics (rays starting inside a collider do not hit it).</summary>
    public static class Physics
    {
        static readonly List<Collider> _all = new List<Collider>();
        sealed class Box { public BoxCollider C; public Vector3 Center; public Quaternion Inv; public Vector3 Half; public Vector3 Min, Max; public int Layer; public bool Trigger; public int Stamp; }
        static readonly List<Box> _boxes = new List<Box>();
        static readonly Dictionary<long, List<Box>> _grid = new Dictionary<long, List<Box>>();
        static readonly List<Box> _big = new List<Box>();
        const float Cell = 8f;
        static int _stamp;
        static bool _synced;
        public static bool autoSyncTransforms = false;
        public static Vector3 gravity = new Vector3(0f, -9.81f, 0f);
        public static bool queriesHitTriggers = true;
        public static int RaycastCount, OverlapCount;

        internal static void Register(Collider c) { _all.Add(c); _synced = false; }
        internal static void Clear() { _all.Clear(); _boxes.Clear(); _grid.Clear(); _big.Clear(); _synced = false; }
        internal static void Invalidate() => _synced = false;

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

        public static void SyncTransforms()
        {
            _boxes.Clear(); _grid.Clear(); _big.Clear();
            foreach (var c in _all)
            {
                if (!(c is BoxCollider b) || !b.enabled || b.gameObject.Destroyed || !b.gameObject.activeInHierarchy) continue;
                b.WorldBox(out var center, out var rot, out var half);
                var bb = b.bounds;
                var box = new Box { C = b, Center = center, Inv = Quaternion.Inverse(rot), Half = half, Min = bb.min, Max = bb.max, Layer = b.gameObject.layer, Trigger = b.isTrigger };
                _boxes.Add(box);
                int x0 = Mathf.FloorToInt(bb.min.x / Cell), x1 = Mathf.FloorToInt(bb.max.x / Cell), z0 = Mathf.FloorToInt(bb.min.z / Cell), z1 = Mathf.FloorToInt(bb.max.z / Cell);
                if ((x1 - x0 + 1) * (z1 - z0 + 1) > 64) { _big.Add(box); continue; }
                for (int x = x0; x <= x1; x++)
                    for (int z = z0; z <= z1; z++)
                    {
                        long k = Key(x, z);
                        if (!_grid.TryGetValue(k, out var l)) _grid[k] = l = new List<Box>();
                        l.Add(box);
                    }
            }
            _synced = true;
        }

        static IEnumerable<Box> Candidates(Vector3 min, Vector3 max)
        {
            if (!_synced) SyncTransforms();
            _stamp++;
            foreach (var b in _big) { b.Stamp = _stamp; yield return b; }
            int x0 = Mathf.FloorToInt(min.x / Cell), x1 = Mathf.FloorToInt(max.x / Cell), z0 = Mathf.FloorToInt(min.z / Cell), z1 = Mathf.FloorToInt(max.z / Cell);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    if (!_grid.TryGetValue(Key(x, z), out var l)) continue;
                    foreach (var b in l) { if (b.Stamp == _stamp) continue; b.Stamp = _stamp; yield return b; }
                }
        }

        static bool Accept(Box b, int mask, QueryTriggerInteraction q)
        {
            if ((mask & (1 << b.Layer)) == 0) return false;
            if (b.Trigger && (q == QueryTriggerInteraction.Ignore || (q == QueryTriggerInteraction.UseGlobal && !queriesHitTriggers))) return false;
            return true;
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit hit, float maxDistance = float.PositiveInfinity, int layerMask = -1, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal)
        {
            RaycastCount++;
            hit = default;
            Vector3 dir = direction.normalized;
            if (float.IsInfinity(maxDistance)) maxDistance = 10000f;
            Vector3 end = origin + dir * maxDistance;
            float best = maxDistance;
            Box bestBox = null; Vector3 bestN = Vector3.zero;
            foreach (var b in Candidates(Vector3.Min(origin, end), Vector3.Max(origin, end)))
            {
                if (!Accept(b, layerMask, q)) continue;
                Vector3 o = b.Inv * (origin - b.Center), d = b.Inv * dir;
                float tmin = float.NegativeInfinity, tmax = float.PositiveInfinity;
                int axis = -1; float sign = 0;
                bool miss = false;
                for (int i = 0; i < 3; i++)
                {
                    float oi = o[i], di = d[i], h = b.Half[i];
                    if (Mathf.Abs(di) < 1e-9f) { if (oi < -h || oi > h) { miss = true; break; } continue; }
                    float t1 = (-h - oi) / di, t2 = (h - oi) / di;
                    float s = -1f;
                    if (t1 > t2) { var tt = t1; t1 = t2; t2 = tt; s = 1f; }
                    if (t1 > tmin) { tmin = t1; axis = i; sign = s; }
                    if (t2 < tmax) tmax = t2;
                    if (tmin > tmax) { miss = true; break; }
                }
                if (miss || tmax < 0f || tmin < 0f) continue;   // behind, or the origin is inside the box
                if (tmin < best)
                {
                    best = tmin; bestBox = b;
                    var nl = Vector3.zero; if (axis >= 0) nl[axis] = d[axis] > 0 ? -1f : 1f;
                    bestN = Quaternion.Inverse(b.Inv) * nl;
                }
            }
            if (bestBox == null) return false;
            hit = new RaycastHit { point = origin + dir * best, distance = best, collider = bestBox.C, normal = bestN };
            return true;
        }

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal)
            => Raycast(origin, direction, out _, maxDistance, layerMask, q);

        public static bool Raycast(Vector3 origin, Vector3 direction, float maxDistance = float.PositiveInfinity)
            => Raycast(origin, direction, out _, maxDistance, -1, QueryTriggerInteraction.UseGlobal);

        static bool SphereBox(Box b, Vector3 p, float r)
        {
            Vector3 l = b.Inv * (p - b.Center);
            Vector3 c = new Vector3(Mathf.Clamp(l.x, -b.Half.x, b.Half.x), Mathf.Clamp(l.y, -b.Half.y, b.Half.y), Mathf.Clamp(l.z, -b.Half.z, b.Half.z));
            return (l - c).sqrMagnitude <= r * r;
        }

        public static bool CheckSphere(Vector3 p, float r, int layerMask = -1, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal)
        {
            OverlapCount++;
            foreach (var b in Candidates(p - Vector3.one * r, p + Vector3.one * r))
                if (Accept(b, layerMask, q) && SphereBox(b, p, r)) return true;
            return false;
        }

        public static bool CheckCapsule(Vector3 p0, Vector3 p1, float r, int layerMask = -1, QueryTriggerInteraction q = QueryTriggerInteraction.UseGlobal)
        {
            OverlapCount++;
            float len = Vector3.Distance(p0, p1);
            int n = Mathf.Max(1, Mathf.CeilToInt(len / (r * 0.5f)));
            foreach (var b in Candidates(Vector3.Min(p0, p1) - Vector3.one * r, Vector3.Max(p0, p1) + Vector3.one * r))
            {
                if (!Accept(b, layerMask, q)) continue;
                for (int i = 0; i <= n; i++)
                    if (SphereBox(b, Vector3.Lerp(p0, p1, (float)i / n), r)) return true;
            }
            return false;
        }

        public static IReadOnlyList<Collider> AllColliders => _all;
    }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16 = 0, UInt32 = 1 }
    public enum ShadowCastingMode { Off = 0, On, TwoSided, ShadowsOnly }
    public enum LightProbeUsage { Off = 0, BlendProbes, UseProxyVolume, CustomProvided }
    public enum ReflectionProbeUsage { Off = 0, BlendProbes, BlendProbesAndSkybox, Simple }
}
