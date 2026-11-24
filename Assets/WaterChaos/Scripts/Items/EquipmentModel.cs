using System;

namespace WaterChaos.Items
{
    /// <summary>
    /// Tracks the item in the player's hands: always the one in the selected inventory slot.
    /// Pure C#: the player component calls <see cref="Sync"/> and shows or hides items on its events.
    /// </summary>
    public sealed class EquipmentModel
    {
        private readonly InventoryModel _inventory;

        public EquipmentModel(InventoryModel inventory)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        }

        /// <summary>An item left the hands; raised before <see cref="Equipped"/> when switching.</summary>
        public event Action<IPickableItem> Unequipped;

        /// <summary>An item came into the hands.</summary>
        public event Action<IPickableItem> Equipped;

        /// <summary>Item in the hands; null when the selected slot is empty.</summary>
        public IPickableItem Current { get; private set; }

        /// <summary>Equips the selected slot's item, unequipping the previous one first. Does nothing if unchanged.</summary>
        public void Sync()
        {
            IPickableItem selected = _inventory.SelectedItem;
            if (selected == Current)
                return;

            IPickableItem previous = Current;
            Current = null;
            if (previous != null)
                Unequipped?.Invoke(previous);

            Current = selected;
            if (selected != null)
                Equipped?.Invoke(selected);
        }
    }
}
