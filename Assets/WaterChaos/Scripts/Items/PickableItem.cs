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

        [Header("World")]
        [Tooltip("Always stands upright (e.g. a pump): released standing, turned only around the vertical axis, and never tips over.")]
        [SerializeField] private bool _keepUpright;

        private Rigidbody _body;
        private Collider[] _colliders;
        private bool[] _wasEnabled;
        private Vector3 _worldScale;
        private bool _isHeld;
        private RigidbodyInterpolation _worldInterpolation;

        public Sprite Icon => _icon;

        /// <summary>In the player's hands or stored with them; false once released into the world.</summary>
        public bool IsHeld => _isHeld;
        public float Mass => _body.mass;

        /// <summary>Measured once in the world (colliders and renderers, world scale); see <see cref="ItemFootprint"/>.</summary>
        public float Radius { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _colliders = GetComponentsInChildren<Collider>(true);
            _wasEnabled = new bool[_disableWhileHeld.Length];
            Radius = MeasureRadius();

            // Physics can still turn it around the vertical axis, but never tilt it.
            if (_keepUpright)
                _body.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
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
            if (_keepUpright)
                transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

            gameObject.SetActive(true);

            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.AddForce(impulse, ForceMode.Impulse);
        }

        private float MeasureRadius()
        {
            float radius = 0f;
            foreach (Collider itemCollider in _colliders)
            {
                if (itemCollider.enabled && !itemCollider.isTrigger)
                    radius = Mathf.Max(radius, ItemFootprint.GetRadius(transform.position, itemCollider.bounds));
            }

            // Collider bounds can be empty before physics has synced; the visible mesh is a safe second source.
            // Only meshes: stream lines and particles (e.g. on the nozzle) reach far beyond the item.
            foreach (Renderer itemRenderer in GetComponentsInChildren<Renderer>())
            {
                if (itemRenderer is MeshRenderer || itemRenderer is SkinnedMeshRenderer)
                    radius = Mathf.Max(radius, ItemFootprint.GetRadius(transform.position, itemRenderer.bounds));
            }

            return radius;
        }

        private void SetWorldPartsEnabled(bool value)
        {
            if (!value)
            {
                _body.isKinematic = true;

                // An interpolated body keeps moving the object to its own (old) physics pose,
                // so in the hands it would lag behind the player; it simply follows the holder instead.
                _worldInterpolation = _body.interpolation;
                _body.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                _body.interpolation = _worldInterpolation;
            }

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
