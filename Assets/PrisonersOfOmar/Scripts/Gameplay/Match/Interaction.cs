using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>What the interaction ray / prompt shows for the looked-at object.</summary>
    public struct InteractPrompt
    {
        public string Text;
        /// <summary>Seconds E must be held (0 = press).</summary>
        public float HoldTime;
        /// <summary>false = greyed out explanation ("LOCKED").</summary>
        public bool Enabled;
        /// <summary>Item auto-equipped (shown in hand) while doing the action.</summary>
        public ItemType UsesItem;
        /// <summary>Noise radius emitted while holding (prying, cutting...).</summary>
        public float NoiseWhileHolding;
        /// <summary>Hold the left mouse button and move the mouse (doors) instead of pressing E.</summary>
        public bool IsDrag;
        /// <summary>(iteration 3) A looping sound heard (by this player only) while E is held, e.g. the VCR rewinding.</summary>
        public string HoldSound;

        public static InteractPrompt Press(string text, ItemType item = ItemType.None) => new InteractPrompt { Text = text, Enabled = true, UsesItem = item };
        public static InteractPrompt Hold(string text, float seconds, ItemType item = ItemType.None, float noise = 0f) => new InteractPrompt { Text = text, HoldTime = seconds, Enabled = true, UsesItem = item, NoiseWhileHolding = noise };
        public static InteractPrompt Drag(string text) => new InteractPrompt { Text = text, Enabled = true, IsDrag = true };
        public static InteractPrompt Info(string text) => new InteractPrompt { Text = text, Enabled = false };
    }

    /// <summary>Who is interacting (always the local player).</summary>
    public sealed class Interactor
    {
        public int PlayerId;
        public bool IsOmar;
        public PlayerStatus Status;
        public LocalInventory Inventory;
        public Vector3 Position;
        public bool Crouching;

        public bool Has(ItemType t) => Inventory != null && Inventory.Has(t);
        public int ItemId(ItemType t) => Inventory != null ? Inventory.Find(t) : -1;
        public ItemType Held => Inventory != null ? Inventory.HeldType : ItemType.None;
    }

    public interface IInteractable
    {
        /// <summary>Return false when there is nothing to show for this interactor.</summary>
        bool GetPrompt(Interactor who, out InteractPrompt prompt);
        /// <summary>Executed on press (HoldTime 0) or when the hold completes.</summary>
        void Interact(Interactor who);
        /// <summary>Point used for range checks / prompt placement.</summary>
        Vector3 InteractPoint { get; }
    }

    /// <summary>Put on a collider's GameObject so the interaction ray can find its target.</summary>
    public sealed class InteractableRef : MonoBehaviour
    {
        public IInteractable Target;

        public static InteractableRef Attach(Component c, IInteractable target)
        {
            if (c == null) return null;
            var r = c.gameObject.GetComponent<InteractableRef>();
            if (r == null) r = c.gameObject.AddComponent<InteractableRef>();
            r.Target = target;
            return r;
        }

        public static IInteractable From(Collider c)
        {
            if (c == null) return null;
            var r = c.GetComponent<InteractableRef>();
            if (r == null) r = c.GetComponentInParent<InteractableRef>();
            return r != null ? r.Target : null;
        }
    }
}
