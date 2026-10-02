using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>Small tumbling cube with simple gravity and bounces against Layers.Solid (explosion debris). Destroys itself.</summary>
    [AddComponentMenu("")]
    public sealed class PsxDebris : MonoBehaviour
    {
        public Vector3 Velocity;
        /// <summary>Degrees per second (world axes).</summary>
        public Vector3 AngularVelocity;
        public float Size = 0.1f;
        public float Lifetime = 6f;

        float _age;
        bool _resting;

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= Lifetime) { Destroy(gameObject); return; }

            if (!_resting)
            {
                Velocity += Physics.gravity * dt;
                Vector3 p = transform.position;
                Vector3 step = Velocity * dt;
                float len = step.magnitude;
                if (len > 1e-5f && Physics.Raycast(p, step / len, out RaycastHit hit, len + Size * 0.5f, Layers.Solid, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + hit.normal * (Size * 0.5f);
                    Velocity = Vector3.Reflect(Velocity, hit.normal) * 0.35f;
                    AngularVelocity *= 0.5f;
                    if (Velocity.sqrMagnitude < 0.3f) _resting = true;
                }
                else transform.position = p + step;
                transform.Rotate(AngularVelocity * dt, Space.World);
            }

            float shrink = Mathf.Clamp01(Lifetime - _age); // last second: shrink away
            transform.localScale = Vector3.one * (Size * shrink);
        }
    }
}
