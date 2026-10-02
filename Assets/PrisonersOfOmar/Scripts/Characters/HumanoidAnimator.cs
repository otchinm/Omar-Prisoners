using System;
using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// Procedural animation of a <see cref="HumanoidRig"/>. Controllers (local player, network proxies, AI)
    /// write the state fields every frame; the animator turns them into bone rotations in LateUpdate.
    /// </summary>
    [RequireComponent(typeof(HumanoidRig))]
    public sealed class HumanoidAnimator : MonoBehaviour
    {
        /// <summary>World space velocity of the character (m/s).</summary>
        public Vector3 Velocity;
        public bool Grounded = true;
        public bool Crouching;
        public bool Sprinting;
        /// <summary>Limp + hand on wound.</summary>
        public bool Injured;
        /// <summary>Camera pitch in degrees (+ = looking down); bends spine / neck / head.</summary>
        public float LookPitch;
        public HoldPose Hold = HoldPose.None;
        public CharacterPose Pose = CharacterPose.Normal;

        /// <summary>Raised on each foot plant (0 = left, 1 = right) while walking/running.</summary>
        public event Action<int> Footstep;
        /// <summary>Raised at the key frame of an action (e.g. the cleaver hits at the bottom of Attack).</summary>
        public event Action<CharacterAction> ActionImpact;

        public CharacterAction CurrentAction { get; private set; }

        /// <summary>Starts a one-shot action (restarts if already playing).</summary>
        public void Play(CharacterAction action)
        {
            CurrentAction = action;
        }

        public bool IsPlaying(CharacterAction action) => CurrentAction == action;

        /// <summary>Duration in seconds of a one-shot action.</summary>
        public static float DurationOf(CharacterAction action)
        {
            switch (action)
            {
                case CharacterAction.Attack: return 0.9f;
                case CharacterAction.Scream: return 2.2f;
                case CharacterAction.Search: return 2.5f;
                case CharacterAction.Grab: return 1.2f;
                case CharacterAction.PlaceTrap: return 1.6f;
                case CharacterAction.Stunned: return 3f;
                case CharacterAction.Pour: return 2f;
                case CharacterAction.Heal: return 2.5f;
                default: return 0.8f;
            }
        }

        // Kept so subclasses / implementation can raise them.
        void RaiseFootstep(int foot) => Footstep?.Invoke(foot);
        void RaiseImpact(CharacterAction a) => ActionImpact?.Invoke(a);
    }
}
