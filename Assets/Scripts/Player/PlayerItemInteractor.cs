using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Items;

namespace WaterChaos.Player
{
    /// <summary>Offers "Pick Up" on the interact key (E) for the item under the crosshair; picking moves it into the inventory.</summary>
    public sealed class PlayerItemInteractor : MonoBehaviour, IInteractionProvider
    {
        private const string PickUpText = "Підібрати";

        [Header("References")]
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("The pickup ray starts here and goes along its forward.")]
        [SerializeField] private Transform _camera;

        [Header("Settings")]
        [Tooltip("How far the player can reach an item, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 3f;

        [Tooltip("Layers the pickup ray can hit. Exclude the Player layer.")]
        [SerializeField] private LayerMask _interactMask = ~0;

        private Action _pickUp;

        /// <summary>The player tried to pick up an item, but every slot is taken.</summary>
        public event Action BackpackFull;

        /// <summary>Item under the crosshair that pick up would take; null if none.</summary>
        public IPickableItem Target { get; private set; }

        private void Awake()
        {
            _pickUp = PickUpTarget;

            if (_inventory == null || _camera == null)
            {
                Debug.LogError($"{name}: PlayerItemInteractor needs Inventory and Camera references.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            Target = null;
        }

        private void Update()
        {
            Target = FindTarget();
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            if (Target is Component item)
                actions.Add(new InteractionAction(PickUpText, item, _pickUp, true, InteractionButton.Interact));
        }

        private void PickUpTarget()
        {
            IPickableItem item = FindTarget();
            if (item == null)
                return;

            if (_inventory.Model.TryAdd(item))
                item.PickUp();
            else if (_inventory.Model.IsFull)
                BackpackFull?.Invoke();
        }

        private IPickableItem FindTarget()
        {
            Ray ray = new Ray(_camera.position, _camera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _interactMask, QueryTriggerInteraction.Ignore))
                return null;

            return hit.collider.GetComponentInParent<IPickableItem>();
        }
    }
}
