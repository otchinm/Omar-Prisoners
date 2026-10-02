using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// Low-poly models for items (pickups, held items, inventory preview) and Omar's tools.
    /// Model conventions: pivot = grip point / bottom center when lying on a surface is NOT required;
    /// use <see cref="RestOffset"/> to place it on the ground. Forward = +Z, Up = +Y, real-world scale in meters.
    /// Optional named children: "Anchor_Flame" (lighter flame position), "Anchor_Light" (flashlight lens, forward = beam).
    /// </summary>
    public static class ItemMeshFactory
    {
        public static GameObject Build(ItemType type)
        {
            var go = new GameObject("Item_" + type);
            var mb = new MeshBuilder();
            mb.SetMaterial(Rendering.PsxMaterials.GetColor(new Color(0.5f, 0.5f, 0.5f)));
            mb.AddBox(Vector3.zero, new Vector3(0.08f, 0.08f, 0.08f), BoxUV.PerFace);
            var mesh = mb.Build("Mesh", go.transform, Layers.Item);
            go.layer = Layers.Item;
            return go;
        }

        /// <summary>Local offset to apply so the model rests on a surface (model placed at surface point + RestOffset, RestRotation).</summary>
        public static Vector3 RestOffset(ItemType type) => new Vector3(0, 0.04f, 0);
        public static Quaternion RestRotation(ItemType type) => Quaternion.identity;

        /// <summary>Omar's meat cleaver (pivot = handle grip).</summary>
        public static GameObject BuildCleaver()
        {
            var go = new GameObject("Cleaver");
            return go;
        }

        /// <summary>Omar's tripwire kit / placed tripwire posts (two small stakes; wire drawn by gameplay between anchors).</summary>
        public static GameObject BuildTripwireStake()
        {
            var go = new GameObject("TripwireStake");
            return go;
        }

        /// <summary>Open bear trap (pivot at ground center). Child "Jaw_L" / "Jaw_R" pivots rotate to snap shut.</summary>
        public static GameObject BuildBearTrap()
        {
            var go = new GameObject("BearTrap");
            return go;
        }
    }
}
