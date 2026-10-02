using UnityEngine;

namespace PrisonersOfOmar
{
    /// <summary>Small helpers for building runtime GameObjects / colliders.</summary>
    public static class GeoUtil
    {
        public static Transform CreateChild(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, int layer = Layers.World)
        {
            var go = new GameObject(name);
            go.layer = layer;
            var t = go.transform;
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = localRotation;
            return t;
        }

        public static Transform CreateChild(Transform parent, string name) => CreateChild(parent, name, Vector3.zero, Quaternion.identity);

        /// <summary>
        /// Adds a box collider on its own child object. <paramref name="center"/> and <paramref name="rotation"/>
        /// are in the parent's local space.
        /// </summary>
        public static BoxCollider AddBox(Transform parent, Vector3 center, Vector3 size, Quaternion rotation,
            int layer = Layers.World, SurfaceType surface = SurfaceType.Default, bool isTrigger = false, string name = "Collider")
        {
            var t = CreateChild(parent, name, center, rotation, layer);
            var bc = t.gameObject.AddComponent<BoxCollider>();
            bc.size = size;
            bc.isTrigger = isTrigger;
            if (surface != SurfaceType.Default) t.gameObject.AddComponent<SurfaceTag>().Surface = surface;
            return bc;
        }

        public static BoxCollider AddBox(Transform parent, Vector3 center, Vector3 size,
            int layer = Layers.World, SurfaceType surface = SurfaceType.Default, bool isTrigger = false, string name = "Collider")
            => AddBox(parent, center, size, Quaternion.identity, layer, surface, isTrigger, name);

        /// <summary>Box collider spanning two points (walls, beams), thickness/height given.</summary>
        public static BoxCollider AddWallCollider(Transform parent, Vector3 from, Vector3 to, float bottomY, float height, float thickness,
            int layer = Layers.World, SurfaceType surface = SurfaceType.Default)
        {
            Vector3 d = to - from; d.y = 0;
            float len = d.magnitude;
            if (len < 1e-4f) return null;
            Quaternion rot = Quaternion.LookRotation(d / len, Vector3.up);
            Vector3 c = (from + to) * 0.5f; c.y = bottomY + height * 0.5f;
            return AddBox(parent, c, new Vector3(thickness, height, len), rot, layer, surface, false, "Wall");
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            return c != null ? c : go.AddComponent<T>();
        }

        public static void DestroyChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.Destroy(t.GetChild(i).gameObject);
        }

        /// <summary>Flat distance on the XZ plane.</summary>
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
