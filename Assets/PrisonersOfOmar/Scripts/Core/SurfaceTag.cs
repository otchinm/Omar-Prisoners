using UnityEngine;

namespace PrisonersOfOmar
{
    /// <summary>Marks a collider with the surface type used for footsteps and impacts.</summary>
    public sealed class SurfaceTag : MonoBehaviour
    {
        public SurfaceType Surface = SurfaceType.Default;

        public static SurfaceType Of(Collider c)
        {
            if (c == null) return SurfaceType.Default;
            var tag = c.GetComponent<SurfaceTag>();
            if (tag == null) tag = c.GetComponentInParent<SurfaceTag>();
            return tag != null ? tag.Surface : SurfaceType.Default;
        }
    }
}
