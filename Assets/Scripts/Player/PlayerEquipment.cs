using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Player
{
    /// <summary>
    /// Puts the selected inventory item into the player's hands: shows it at the item holder
    /// and turns on its <see cref="IEquippable"/> behaviors. Only one item is equipped at a time.
    /// </summary>
    public sealed class PlayerEquipment : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("The player's view; tools aim along its forward.")]
        [SerializeField] private Transform _camera;

        [Tooltip("Where the equipped item is shown.")]
        [SerializeField] private Transform _itemHolder;

        private readonly List<IEquippable> _behaviors = new List<IEquippable>();
        private EquipmentModel _model;

        /// <summary>Input of the player holding the item.</summary>
        public PlayerInputHandler Input => _input;

        /// <summary>The player's view; tools aim along its forward.</summary>
        public Transform Camera => _camera;

        /// <summary>The inventory the equipped item comes from.</summary>
        public PlayerInventory Inventory => _inventory;

        /// <summary>Item in the hands; null when the selected slot is empty.</summary>
        public IPickableItem Current => _model?.Current;

        private void Awake()
        {
            if (_input == null || _inventory == null || _camera == null || _itemHolder == null)
            {
                Debug.LogError($"{name}: PlayerEquipment needs Input, Inventory, Camera and Item Holder references.", this);
                enabled = false;
                return;
            }

            _model = new EquipmentModel(_inventory.Model);
        }

        private void OnEnable()
        {
            if (_model == null)
                return;

            _model.Equipped += OnEquipped;
            _model.Unequipped += OnUnequipped;
        }

        private void OnDisable()
        {
            if (_model == null)
                return;

            _model.Equipped -= OnEquipped;
            _model.Unequipped -= OnUnequipped;
        }

        // LateUpdate: pick up and throw finish inside Update, so the item is already stored or released here.
        private void LateUpdate()
        {
            _model.Sync();
        }

        private void OnEquipped(IPickableItem item)
        {
            if (!(item is PickableItem pickable))
                return;

            pickable.Hold(_itemHolder);

            pickable.GetComponentsInChildren(true, _behaviors);
            foreach (IEquippable behavior in _behaviors)
                behavior.Equip(this);
        }

        private void OnUnequipped(IPickableItem item)
        {
            if (!(item is PickableItem pickable))
                return;

            pickable.GetComponentsInChildren(true, _behaviors);
            foreach (IEquippable behavior in _behaviors)
                behavior.Unequip();

            // A thrown item has already been released into the world; only a stored one is hidden.
            if (_inventory.Model.Contains(item))
                pickable.Stow();
        }
    }
}
