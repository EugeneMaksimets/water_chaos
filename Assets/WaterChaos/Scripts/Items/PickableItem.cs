using UnityEngine;

namespace WaterChaos.Items
{
    /// <summary>
    /// World object that can be stored in the inventory.
    /// Hidden while stored; thrown back out as a normal physics body.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PickableItem : MonoBehaviour, IPickableItem
    {
        [Header("Presentation")]
        [Tooltip("Thumbnail shown in the inventory slot.")]
        [SerializeField] private Sprite _icon;

        private Rigidbody _body;

        public Sprite Icon => _icon;
        public float Mass => _body.mass;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
        }

        public void PickUp()
        {
            gameObject.SetActive(false);
        }

        public void Release(Vector3 position, Vector3 impulse)
        {
            transform.position = position;
            gameObject.SetActive(true);

            _body.isKinematic = false;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _body.AddForce(impulse, ForceMode.Impulse);
        }
    }
}