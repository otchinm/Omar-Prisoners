using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>Fades a PsxLight out (quadratic) over Duration seconds, then destroys the GameObject (light flashes).</summary>
    [AddComponentMenu("")]
    public sealed class PsxTimedLight : MonoBehaviour
    {
        public PsxLight Light;
        public float Duration = 0.2f;
        public float StartIntensity = 1f;

        float _age;

        void Update()
        {
            _age += Time.deltaTime;
            float k = 1f - _age / Mathf.Max(0.01f, Duration);
            if (k <= 0f || Light == null) { Destroy(gameObject); return; }
            Light.Intensity = StartIntensity * k * k;
        }
    }
}
