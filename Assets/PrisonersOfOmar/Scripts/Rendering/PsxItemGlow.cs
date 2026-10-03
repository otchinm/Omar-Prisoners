using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// (iteration 2) Puppet Combo style pickup highlight: a pixelated glow / glint drawn AROUND an item
    /// (the item itself is not tinted or outlined). Attach to a world item root; the gameplay sets
    /// <see cref="Highlighted"/> when the player looks at it.
    /// Placeholder implementation (no visuals) until the rendering pass replaces it.
    /// </summary>
    public sealed class PsxItemGlow : MonoBehaviour
    {
        /// <summary>Global multiplier (difficulty / settings). 0 disables every glow.</summary>
        public static float GlobalStrength = 1f;

        /// <summary>Approximate radius of the item in meters (glow size).</summary>
        public float Radius = 0.25f;
        /// <summary>The player is aiming at this item: stronger glow.</summary>
        public bool Highlighted;

        public static PsxItemGlow Attach(GameObject item, float radius = 0.25f)
        {
            if (item == null) return null;
            var g = item.GetComponent<PsxItemGlow>();
            if (g == null) g = item.AddComponent<PsxItemGlow>();
            g.Radius = radius;
            return g;
        }
    }
}
