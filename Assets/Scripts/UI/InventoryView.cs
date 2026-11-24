using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>Keeps the slot views in sync with the inventory model through its events.</summary>
    public sealed class InventoryView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("Slot views in slot order: the first one is slot 1.")]
        [SerializeField] private InventorySlotView[] _slots;

        private InventoryModel _model;

        private void Awake()
        {
            if (_inventory == null || _slots == null || _slots.Length == 0)
            {
                Debug.LogError($"{name}: InventoryView needs Inventory and Slots references.", this);
                enabled = false;
                return;
            }

            _model = _inventory.Model;
            if (_slots.Length != _model.Capacity)
                Debug.LogWarning($"{name}: {_slots.Length} slot views for {_model.Capacity} inventory slots.", this);
        }

        private void OnEnable()
        {
            _model.SlotChanged += OnSlotChanged;
            _model.SelectionChanged += OnSelectionChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            _model.SlotChanged -= OnSlotChanged;
            _model.SelectionChanged -= OnSelectionChanged;
        }

        private void RefreshAll()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                    continue;

                _slots[i].SetNumber(i + 1);
                _slots[i].SetItem(_model.GetItem(i));
                _slots[i].SetSelected(i == _model.SelectedIndex);
            }
        }

        private void OnSlotChanged(int index)
        {
            if (index < _slots.Length && _slots[index] != null)
                _slots[index].SetItem(_model.GetItem(index));
        }

        private void OnSelectionChanged(int index)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                    _slots[i].SetSelected(i == index);
            }
        }
    }
}
