using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Soft dark blob on the ground under <see cref="Target"/> (created by BlobShadow.Attach, child of the target so it
    /// follows its active state / layer). Each frame it raycasts down against Layers.Solid, lies on the hit surface,
    /// and shrinks / fades with the height above the ground.
    /// </summary>
    [DefaultExecutionOrder(30500)]
    [AddComponentMenu("")]
    public sealed class PsxBlobShadow : MonoBehaviour
    {
        public Transform Target;
        public float Radius = 0.4f;
        /// <summary>Height above the ground at which the blob has faded out completely.</summary>
        public float MaxHeight = 2.5f;
        public float Opacity = 0.7f;

        MeshRenderer _renderer;
        MaterialPropertyBlock _mpb;

        void Start() { UpdateBlob(); }
        void LateUpdate() { UpdateBlob(); }

        void UpdateBlob()
        {
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (Target == null || _renderer == null) return;

            const float lift = 0.3f;
            Vector3 origin = Target.position + Vector3.up * lift;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, MaxHeight + lift, Layers.Solid, QueryTriggerInteraction.Ignore))
            {
                _renderer.enabled = false;
                return;
            }
            float height = Mathf.Max(0f, hit.distance - lift);
            float k = 1f - Mathf.Clamp01(height / Mathf.Max(0.01f, MaxHeight));
            if (k <= 0.01f)
            {
                _renderer.enabled = false;
                return;
            }
            _renderer.enabled = true;

            transform.SetPositionAndRotation(hit.point + hit.normal * 0.02f, Quaternion.FromToRotation(Vector3.back, hit.normal));
            float size = Radius * 2f * Mathf.Lerp(0.6f, 1f, k);
            Vector3 ps = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(size / NonZero(ps.x), size / NonZero(ps.y), 1f / NonZero(ps.z));

            _mpb.SetColor(PsxShaderIds.Color, new Color(1f, 1f, 1f, Opacity * k));
            _renderer.SetPropertyBlock(_mpb);
        }

        static float NonZero(float v) => Mathf.Abs(v) < 1e-4f ? 1e-4f : v;
    }
}
