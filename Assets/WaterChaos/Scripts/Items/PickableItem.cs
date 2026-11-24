using UnityEngine;

namespace WaterChaos.Items
{
    /// <summary>
    /// World object that can be stored in the inventory.
    /// Hidden while stored; shown in the player's hands while its slot is selected;
    /// thrown back out as a normal physics body.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PickableItem : MonoBehaviour, IPickableItem
    {
        [Header("Presentation")]
        [Tooltip("Thumbnail shown in the inventory slot.")]
        [SerializeField] private Sprite _icon;

        [Header("Held")]
        [Tooltip("Offset from the item holder while held, in meters (m).")]
        [SerializeField] private Vector3 _heldPosition;

        [Tooltip("Rotation relative to the item holder while held, in degrees (°).")]
        [SerializeField] private Vector3 _heldRotation;

        [Tooltip("Size in the hands relative to the size in the world; 1 = same size.")]
        [SerializeField, Min(0.01f)] private float _heldScale = 1f;

        [Tooltip("Components that only make sense in the world (e.g. a pump's connection point); off while held.")]
        [SerializeField] private Behaviour[] _disableWhileHeld = new Behaviour[0];

        private Rigidbody _body;
        private Collider[] _colliders;
        private bool[] _wasEnabled;
        private Vector3 _worldScale;
        private bool _isHeld;

        public Sprite Icon => _icon;
        public float Mass => _body.mass;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _wasEnabled = new bool[_disableWhileHeld.Length];
        }

        public void PickUp()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Shows the item at <paramref name="holder"/>, following it, with physics, colliders and world-only parts off.</summary>
        public void Hold(Transform holder)
        {
            if (!_isHeld)
            {
                _isHeld = true;
                _worldScale = transform.localScale;
                SetWorldPartsEnabled(false);
            }

            transform.SetParent(holder, false);
            transform.SetLocalPositionAndRotation(_heldPosition, Quaternion.Euler(_heldRotation));
            transform.localScale = _worldScale * _heldScale;

            gameObject.SetActive(true);
        }

        /// <summary>Hides a held item while it stays in the inventory.</summary>
        public void Stow()
        {
            gameObject.SetActive(false);
        }

        public void Release(Vector3 position, Vector3 impulse)
        {
            if (_isHeld)
            {
                _isHeld = false;
                transform.SetParent(null, true);
                transform.localScale = _worldScale;
                SetWorldPartsEnabled(true);
            }

            transform.position = position;
            gameObject.SetActive(true);

            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.AddForce(impulse, ForceMode.Impulse);
        }

        private void SetWorldPartsEnabled(bool value)
        {
            if (!value)
                _body.isKinematic = true;
            _body.detectCollisions = value;

            foreach (Collider itemCollider in _colliders)
                itemCollider.enabled = value;

            for (int i = 0; i < _disableWhileHeld.Length; i++)
            {
                Behaviour part = _disableWhileHeld[i];
                if (part == null)
                    continue;

                if (value)
                {
                    part.enabled = _wasEnabled[i];
                }
                else
                {
                    _wasEnabled[i] = part.enabled;
                    part.enabled = false;
                }
            }
        }
    }
}
