using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Player
{
    /// <summary>
    /// Drop (F) puts the selected item down in front of the player without a push.
    /// This is the only way an item leaves the hands: the primary button (left mouse button) never drops.
    /// </summary>
    public sealed class PlayerItemThrower : MonoBehaviour
    {
        /// <summary>Distance kept between a dropped item and a wall or the player, in meters (m).</summary>
        private const float Gap = 0.35f;

        /// <summary>Used when the player has no CharacterController, in meters (m).</summary>
        private const float DefaultPlayerRadius = 0.5f;

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("The item is dropped in front of it, at its height.")]
        [SerializeField] private Transform _camera;

        [Header("Drop")]
        [Tooltip("Smallest distance from the player to a dropped item, in meters (m). Big items go farther, so they never overlap the player.")]
        [SerializeField, Min(0f)] private float _spawnDistance = 0.8f;

        [Tooltip("How far below the eyes the item appears, in meters (m).")]
        [SerializeField, Min(0f)] private float _dropBelowEyes = 0.6f;

        [Tooltip("Layers that block the item: a drop spot needs free room up to them. Exclude the Player layer.")]
        [SerializeField] private LayerMask _collisionMask = ~0;

        private float _playerRadius = DefaultPlayerRadius;

        /// <summary>The selected slot holds an item that Drop would put down.</summary>
        public bool CanDrop { get; private set; }

        private void Awake()
        {
            if (_input == null || _inventory == null || _camera == null)
            {
                Debug.LogError($"{name}: PlayerItemThrower needs Input, Inventory and Camera references.", this);
                enabled = false;
                return;
            }

            if (TryGetComponent(out CharacterController body))
                _playerRadius = body.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        }

        private void OnEnable()
        {
            _input.DropPressed += OnDropPressed;
        }

        private void OnDisable()
        {
            _input.DropPressed -= OnDropPressed;
            CanDrop = false;
        }

        private void Update()
        {
            CanDrop = _inventory.Model.SelectedItem != null;
        }

        /// <summary>Takes the selected item out of the inventory into the world; does nothing when the slot is empty.</summary>
        private void OnDropPressed()
        {
            IPickableItem item = _inventory.Model.RemoveSelected();
            if (item == null)
                return;

            item.Release(GetSpawnPosition(item.Radius), Vector3.zero);
        }

        /// <summary>
        /// A point below the eyes, far enough that an item of <paramref name="itemRadius"/> does not overlap the
        /// player (otherwise physics pushes it up onto the player's head). In front if there is room before a wall,
        /// else to the right, left or behind; in front as a last resort.
        /// </summary>
        private Vector3 GetSpawnPosition(float itemRadius)
        {
            Vector3 forward = Vector3.ProjectOnPlane(_camera.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f)
                forward = transform.forward;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);

            float distance = Mathf.Max(_spawnDistance, _playerRadius + itemRadius + Gap);
            float needed = distance + itemRadius + Gap;

            Vector3 origin = _camera.position + Vector3.down * _dropBelowEyes;
            Vector3[] directions = { forward, right, -right, -forward };
            foreach (Vector3 direction in directions)
            {
                if (!Physics.Raycast(origin, direction, needed, _collisionMask, QueryTriggerInteraction.Ignore))
                    return origin + direction * distance;
            }

            return origin + forward * distance;
        }
    }
}
