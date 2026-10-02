using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// First person view model: forearm(s) + hand(s) in the skin's sleeves, holding the equipped item.
    /// Lives under the camera on Layers.ViewModel. Gameplay feeds movement state for bob/sway and calls Play() for actions.
    /// </summary>
    public sealed class FirstPersonArms : MonoBehaviour
    {
        /// <summary>Item model attach point in the right hand (item pivot = grip).</summary>
        public Transform HandSocket { get; private set; }
        public CharacterSkin Skin { get; private set; }

        /// <summary>Horizontal movement speed (m/s) for bobbing.</summary>
        public float MoveSpeed;
        public bool Sprinting;
        public bool Crouching;
        /// <summary>Mouse delta this frame (for sway).</summary>
        public Vector2 LookDelta;

        public ItemType HeldItem { get; private set; }
        /// <summary>The instantiated model of the held item (null when empty-handed).</summary>
        public GameObject HeldModel { get; private set; }

        public static FirstPersonArms Create(CharacterSkin skin, Transform cameraTransform)
        {
            var go = new GameObject("FirstPersonArms");
            go.layer = Layers.ViewModel;
            go.transform.SetParent(cameraTransform, false);
            var arms = go.AddComponent<FirstPersonArms>();
            arms.Skin = skin;
            arms.HandSocket = GeoUtil.CreateChild(go.transform, "HandSocket", new Vector3(0.18f, -0.2f, 0.4f), Quaternion.identity, Layers.ViewModel);
            return arms;
        }

        /// <summary>Show this item in hand (builds the model via ItemMeshFactory). None = empty hand / lowered arms.</summary>
        public void SetHeld(ItemType item)
        {
            if (HeldItem == item && (HeldModel != null || item == ItemType.None)) return;
            HeldItem = item;
            if (HeldModel != null) Destroy(HeldModel);
            HeldModel = null;
            if (item != ItemType.None)
            {
                HeldModel = ItemMeshFactory.Build(item);
                HeldModel.transform.SetParent(HandSocket, false);
                GeoUtil.SetLayerRecursive(HeldModel, Layers.ViewModel);
            }
        }

        /// <summary>One-shot hand animation (pickup, interact, use, pour, throw, attack for Omar's cleaver...).</summary>
        public void Play(CharacterAction action) { }

        public void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }
    }
}
