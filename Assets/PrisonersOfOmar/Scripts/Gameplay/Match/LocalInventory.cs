using System;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>The local player's 3 inventory slots (item ids), mirrored from host events.</summary>
    public sealed class LocalInventory
    {
        public readonly int[] Slots = { -1, -1, -1 };
        public int Selected;
        readonly MatchWorld _world;

        public event Action Changed;

        public LocalInventory(MatchWorld world) { _world = world; }

        public ItemEntity At(int slot)
        {
            if (slot < 0 || slot >= Slots.Length || Slots[slot] < 0) return null;
            return _world.GetItem(Slots[slot]);
        }

        public ItemEntity Held => At(Selected);
        public ItemType HeldType => Held != null ? Held.Type : ItemType.None;
        public int HeldId => Held != null ? Held.Id : -1;

        public int Count
        {
            get { int n = 0; for (int i = 0; i < Slots.Length; i++) if (Slots[i] >= 0) n++; return n; }
        }

        public bool Full => Count >= Slots.Length;

        public bool Has(ItemType t) => Find(t) >= 0;

        /// <summary>Item id of the first item of that type (prefers the selected slot), -1 if none.</summary>
        public int Find(ItemType t)
        {
            var h = Held;
            if (h != null && h.Type == t) return h.Id;
            for (int i = 0; i < Slots.Length; i++)
            {
                var it = At(i);
                if (it != null && it.Type == t) return it.Id;
            }
            return -1;
        }

        public int SlotOf(int itemId)
        {
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] == itemId) return i;
            return -1;
        }

        public void Select(int slot)
        {
            if (slot < 0 || slot >= Slots.Length) return;
            if (Selected == slot) return;
            Selected = slot;
            Changed?.Invoke();
        }

        public void Cycle(int dir)
        {
            Selected = (Selected + dir + Slots.Length) % Slots.Length;
            Changed?.Invoke();
        }

        internal void Set(int slot, int itemId)
        {
            if (slot < 0 || slot >= Slots.Length) return;
            Slots[slot] = itemId;
            Changed?.Invoke();
        }

        internal void Remove(int itemId)
        {
            int s = SlotOf(itemId);
            if (s < 0) return;
            Slots[s] = -1;
            Changed?.Invoke();
        }

        internal void Clear()
        {
            for (int i = 0; i < Slots.Length; i++) Slots[i] = -1;
            Changed?.Invoke();
        }
    }
}
