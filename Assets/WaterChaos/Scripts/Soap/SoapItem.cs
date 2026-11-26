using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Water;

namespace WaterChaos.Soap
{
    /// <summary>
    /// A bar of soap. Picked up, carried and thrown like any <see cref="PickableItem"/>; once it lands in a
    /// <see cref="WaterBody"/> holding water, it makes that water soapy (<see cref="WaterReservoir.AddSoap"/>)
    /// and, if set to, dissolves. Gasoline and toxic liquid do not take soap: the bar just lies there.
    /// </summary>
    [RequireComponent(typeof(PickableItem))]
    public sealed class SoapItem : MonoBehaviour
    {
        [Header("Activation")]
        [Tooltip("The bar is used up when it makes water soapy; off = it stays and can be fished out again.")]
        [SerializeField] private bool _consumeOnUse = true;

        [Tooltip("Time the bar takes to dissolve and vanish, in seconds (s).")]
        [SerializeField, Min(0f)] private float _dissolveDuration = 1.5f;

        [Header("Effect")]
        [Tooltip("Optional effect played where the soap dissolves (a puff of bubbles).")]
        [SerializeField] private ParticleSystem _dissolveEffect;

        private PickableItem _item;
        private bool _isDissolving;
        private float _dissolveTime;
        private Vector3 _startScale;

        private void Awake()
        {
            _item = GetComponent<PickableItem>();
        }

        private void OnTriggerStay(Collider other)
        {
            if (_isDissolving || _item.IsHeld)
                return;

            var water = other.GetComponent<WaterBody>();
            if (water == null || water.Reservoir == null || !water.Contains(transform.position))
                return;

            if (water.Reservoir.AddSoap() && _consumeOnUse)
                StartDissolving();
        }

        private void Update()
        {
            if (!_isDissolving)
                return;

            _dissolveTime += Time.deltaTime;
            float left = _dissolveDuration > 0f ? 1f - _dissolveTime / _dissolveDuration : 0f;
            transform.localScale = _startScale * Mathf.Max(0f, left);
            if (left <= 0f)
                Destroy(gameObject);
        }

        private void StartDissolving()
        {
            _isDissolving = true;
            _startScale = transform.localScale;

            // A dissolving bar can no longer be picked up or bump into things.
            foreach (Collider part in GetComponentsInChildren<Collider>())
                part.enabled = false;
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;

            if (_dissolveEffect != null)
            {
                ParticleSystem effect = Instantiate(_dissolveEffect, transform.position, Quaternion.identity);
                effect.Play(true);
            }
        }
    }
}
