using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>CharacterController movement with gravity, crouch height and ground probing (local players + AI).</summary>
    public sealed class CharacterMotor
    {
        public readonly CharacterController Controller;
        public Vector3 Velocity;
        public bool Grounded { get; private set; }
        public float Height { get; private set; }
        public SurfaceType GroundSurface { get; private set; }
        float _vy;
        readonly float _standHeight, _crouchHeight, _radius;

        public CharacterMotor(GameObject go, float radius, float standHeight, float crouchHeight)
        {
            _radius = radius; _standHeight = standHeight; _crouchHeight = crouchHeight;
            Controller = go.GetComponent<CharacterController>();
            if (Controller == null) Controller = go.AddComponent<CharacterController>();
            Controller.radius = radius;
            Controller.height = standHeight;
            Controller.center = new Vector3(0, standHeight * 0.5f, 0);
            Controller.stepOffset = 0.35f;
            Controller.slopeLimit = 50f;
            Controller.skinWidth = 0.04f;
            Controller.minMoveDistance = 0f;
            Height = standHeight;
        }

        /// <summary>Can we stand up here (nothing above the crouched capsule)?</summary>
        public bool CanStand()
        {
            var t = Controller.transform;
            Vector3 bottom = t.position + Vector3.up * (_radius + 0.05f);
            Vector3 top = t.position + Vector3.up * (_standHeight - _radius);
            return !Physics.CheckCapsule(bottom + Vector3.up * 0.3f, top, _radius * 0.9f, Layers.Solid, QueryTriggerInteraction.Ignore);
        }

        public void SetCrouched(bool crouched, float dt)
        {
            float target = crouched ? _crouchHeight : _standHeight;
            Height = Mathf.MoveTowards(Height, target, dt * 3.5f);
            Controller.height = Height;
            Controller.center = new Vector3(0, Height * 0.5f, 0);
        }

        /// <summary>Move with a horizontal wish velocity; handles gravity. Returns actual horizontal speed.</summary>
        public float Move(Vector3 horizontalVelocity, float dt)
        {
            if (!Controller.enabled) return 0f;
            if (Grounded && _vy < 0f) _vy = -2f;
            _vy -= 18f * dt;
            _vy = Mathf.Max(_vy, -30f);
            Vector3 before = Controller.transform.position;
            var flags = Controller.Move((horizontalVelocity + Vector3.up * _vy) * dt);
            Grounded = (flags & CollisionFlags.Below) != 0 || Controller.isGrounded;
            if ((flags & CollisionFlags.Above) != 0 && _vy > 0) _vy = 0;
            Vector3 delta = Controller.transform.position - before;
            Velocity = dt > 0 ? delta / dt : Vector3.zero;
            ProbeGround();
            return new Vector2(Velocity.x, Velocity.z).magnitude;
        }

        void ProbeGround()
        {
            var t = Controller.transform;
            if (Physics.Raycast(t.position + Vector3.up * 0.3f, Vector3.down, out var hit, 0.8f, Layers.Solid, QueryTriggerInteraction.Ignore))
                GroundSurface = SurfaceTag.Of(hit.collider);
        }

        public void Teleport(Vector3 position, float yaw)
        {
            bool en = Controller.enabled;
            Controller.enabled = false;
            Controller.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            Controller.enabled = en;
            _vy = 0; Velocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        /// <summary>Surface type at an arbitrary position (for remote footsteps).</summary>
        public static SurfaceType SurfaceAt(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 0.4f, Vector3.down, out var hit, 1.2f, Layers.Solid, QueryTriggerInteraction.Ignore))
                return SurfaceTag.Of(hit.collider);
            return SurfaceType.Dirt;
        }
    }
}
