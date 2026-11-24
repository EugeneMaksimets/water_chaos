using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Player
{
    /// <summary>
    /// Owns the player's inventory model, shares it with player components and UI,
    /// and selects slots from the number keys.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;

        [Header("Settings")]
        [Tooltip("Number of inventory slots.")]
        [SerializeField, Min(1)] private int _capacity = 3;

        private InventoryModel _model;

        /// <summary>Created on first use, so other components can read it in their own Awake/OnEnable.</summary>
        public InventoryModel Model => _model ??= new InventoryModel(_capacity);

        private void Awake()
        {
            if (_input == null)
                Debug.LogError($"{name}: PlayerInventory needs an Input reference to select slots.", this);
        }

        private void OnEnable()
        {
            if (_input != null)
                _input.SlotSelected += OnSlotSelected;
        }

        private void OnDisable()
        {
            if (_input != null)
                _input.SlotSelected -= OnSlotSelected;
        }

        private void OnSlotSelected(int index)
        {
            Model.Select(index);
        }
    }
}
