using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>Continuous spawner of pooled sprites at its position (fire smoke, embers). Used by PsxFx.CreateFire / CreateFlare.</summary>
    [AddComponentMenu("")]
    public sealed class PsxEmitter : MonoBehaviour
    {
        public enum Kind { Smoke = 0, Embers = 1 }

        public Kind Type = Kind.Smoke;
        /// <summary>Particles per second.</summary>
        public float Rate = 3f;
        /// <summary>Scale of the particles / their speed.</summary>
        public float Size = 1f;
        public Color Tint = Color.white;

        float _acc;

        void Update()
        {
            _acc += Time.deltaTime * Mathf.Max(0f, Rate);
            int n = 0;
            while (_acc >= 1f && n < 4)
            {
                _acc -= 1f;
                n++;
                PsxFx.Emit(this);
            }
            if (_acc > 1f) _acc = 1f;
        }
    }
}
