using System;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>The local player's inventory slots (item ids), mirrored from host events. 3 usable slots,
    /// 5 while wearing a backpack (iteration 3); the backpack itself is worn, not kept in a slot.</summary>
    public sealed class LocalInventory
    {
        /// <summary>Slots without / with a backpack.</summary>
        public const int BaseSlots = 3, MaxSlots = 5;
        /// <summary>Slot number the host uses for a worn item (<see cref="ItemDefs.IsWorn"/>): never shown in the strip.</summary>
        public const int WornSlot = 15;

        public readonly int[] Slots = { -1, -1, -1, -1, -1 };
        public int Selected;
        /// <summary>Item id of the worn backpack, -1 = none.</summary>
        public int Backpack { get; private set; } = -1;
        readonly MatchWorld _world;

        public event Action Changed;

        public LocalInventory(MatchWorld world) { _world = world; }

        public bool HasBackpack => Backpack >= 0;
        /// <summary>Usable slots right now.</summary>
        public int Capacity => HasBackpack ? MaxSlots : BaseSlots;

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
            get { int n = 0; for (int i = 0; i < Capacity; i++) if (Slots[i] >= 0) n++; return n; }
        }

        public bool Full => Count >= Capacity;

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
            if (slot < 0 || slot >= Capacity) return;
            if (Selected == slot) return;
            Selected = slot;
            Changed?.Invoke();
        }

        public void Cycle(int dir)
        {
            Selected = (Selected + dir + Capacity) % Capacity;
            Changed?.Invoke();
        }

        internal void Set(int slot, int itemId)
        {
            if (slot < 0 || slot >= Slots.Length) return;
            Slots[slot] = itemId;
            Changed?.Invoke();
        }

        internal void SetBackpack(int itemId)
        {
            Backpack = itemId;
            if (Selected >= Capacity) Selected = 0;
            Changed?.Invoke();
        }

        internal void Remove(int itemId)
        {
            if (itemId >= 0 && itemId == Backpack) { SetBackpack(-1); return; }
            int s = SlotOf(itemId);
            if (s < 0) return;
            Slots[s] = -1;
            Changed?.Invoke();
        }

        internal void Clear()
        {
            for (int i = 0; i < Slots.Length; i++) Slots[i] = -1;
            Backpack = -1;
            Selected = 0;
            Changed?.Invoke();
        }
    }
}
