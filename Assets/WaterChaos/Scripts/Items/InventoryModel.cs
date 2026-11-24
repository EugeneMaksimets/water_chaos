using System;

namespace WaterChaos.Items
{
    /// <summary>
    /// Fixed number of slots holding one item each, plus the selected slot.
    /// Pure C#: the UI listens to its events, player components call its methods.
    /// </summary>
    public sealed class InventoryModel
    {
        private readonly IPickableItem[] _slots;

        /// <param name="capacity">Number of slots; values below 1 become 1.</param>
        public InventoryModel(int capacity)
        {
            _slots = new IPickableItem[Math.Max(1, capacity)];
        }

        /// <summary>A slot's content changed; argument is the slot index.</summary>
        public event Action<int> SlotChanged;

        /// <summary>Another slot became selected; argument is the new index.</summary>
        public event Action<int> SelectionChanged;

        public int Capacity => _slots.Length;
        public int SelectedIndex { get; private set; }

        /// <summary>Item in the selected slot; null if the slot is empty.</summary>
        public IPickableItem SelectedItem => _slots[SelectedIndex];

        public bool IsFull => FindFreeSlot() < 0;

        /// <summary>Item in the slot; null if the slot is empty or the index is out of range.</summary>
        public IPickableItem GetItem(int index) => IsValidIndex(index) ? _slots[index] : null;

        /// <summary>Puts the item into the first free slot. False if null, already stored or full.</summary>
        public bool TryAdd(IPickableItem item)
        {
            if (item == null || Contains(item))
                return false;

            int index = FindFreeSlot();
            if (index < 0)
                return false;

            _slots[index] = item;
            SlotChanged?.Invoke(index);
            return true;
        }

        /// <summary>Selects a slot, empty or not. False if the index is out of range.</summary>
        public bool Select(int index)
        {
            if (!IsValidIndex(index))
                return false;

            if (index != SelectedIndex)
            {
                SelectedIndex = index;
                SelectionChanged?.Invoke(index);
            }

            return true;
        }

        /// <summary>Takes the item out of the selected slot; null if the slot is empty.</summary>
        public IPickableItem RemoveSelected()
        {
            IPickableItem item = _slots[SelectedIndex];
            if (item == null)
                return null;

            _slots[SelectedIndex] = null;
            SlotChanged?.Invoke(SelectedIndex);
            return item;
        }

        private bool IsValidIndex(int index) => index >= 0 && index < _slots.Length;

        private int FindFreeSlot() => Array.IndexOf(_slots, null);

        private bool Contains(IPickableItem item) => Array.IndexOf(_slots, item) >= 0;
    }
}