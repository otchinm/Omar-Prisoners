using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// Bone references of a procedurally generated low-poly character.
    /// The root's origin is between the feet on the ground, character faces +Z.
    /// All bones have an identity rotation in the bind pose (arms hanging down, palms facing the body).
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

        /// <summary>The single skinned body renderer (also in <see cref="Renderers"/>).</summary>
        public SkinnedMeshRenderer BodyRenderer;

        // ---- internal data used by the animator / figure baker
        internal Transform[] BoneArray;       // indexed by BoneId
        internal Vector3[] BindPositions;     // root space bind positions, indexed by BoneId
        internal Vector3 BindHipsPosition;    // Hips.localPosition in the bind pose
        internal BodySpec Spec;

        /// <summary>Shows / hides the body and anything attached to the hand sockets (held items, Omar's cleaver).</summary>
        public void SetVisible(bool visible)
        {
            for (int i = 0; i < Renderers.Length; i++) if (Renderers[i] != null) Renderers[i].enabled = visible;
            SetSocketVisible(RightHandSocket, visible);
            SetSocketVisible(LeftHandSocket, visible);
        }

        static void SetSocketVisible(Transform socket, bool visible)
        {
            if (socket == null || socket.childCount == 0) return;
            var rs = socket.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rs.Length; i++) rs[i].enabled = visible;
        }

        public void SetLayer(int layer) => GeoUtil.SetLayerRecursive(gameObject, layer);
    }
}
