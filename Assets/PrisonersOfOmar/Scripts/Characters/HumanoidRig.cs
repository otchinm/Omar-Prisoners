using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// Bone references of a procedurally generated low-poly character.
    /// The root's origin is between the feet on the ground, character faces +Z.
    /// </summary>
    public sealed class HumanoidRig : MonoBehaviour
    {
        public CharacterSkin Skin;
        public Transform Hips, Spine, Chest, Neck, Head;
        public Transform LeftUpperArm, LeftLowerArm, LeftHand;
        public Transform RightUpperArm, RightLowerArm, RightHand;
        public Transform LeftUpperLeg, LeftLowerLeg, LeftFoot;
        public Transform RightUpperLeg, RightLowerLeg, RightFoot;

        /// <summary>Attach held items here (item pivot = grip point, item +Z forward, +Y up).</summary>
        public Transform RightHandSocket, LeftHandSocket;
        /// <summary>Approximate eye position (for first person camera height and sight checks).</summary>
        public Transform EyePoint;

        public Renderer[] Renderers = new Renderer[0];
        public float Height = 1.75f;

        public void SetVisible(bool visible)
        {
            for (int i = 0; i < Renderers.Length; i++) if (Renderers[i] != null) Renderers[i].enabled = visible;
        }

        public void SetLayer(int layer) => GeoUtil.SetLayerRecursive(gameObject, layer);
    }
}
