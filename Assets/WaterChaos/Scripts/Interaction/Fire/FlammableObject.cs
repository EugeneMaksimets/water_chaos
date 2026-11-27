using System.Collections;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Marks an object that nearby fires can set alight. Burns at most one fire at a time;
    /// objects without this component (walls, floor, water, player) never catch fire.
    /// For ground that many fires can spread across, use FlammableSurface.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlammableObject : MonoBehaviour
    {
        [Header("Ignition")]
        [Tooltip("Where the fire appears. Empty = top center of this object's colliders.")]
        [SerializeField] private Transform _ignitionPoint;

        private Collider[] _colliders;
        private Rigidbody _body;
        private FireExtinguishHandler _fire;
        private bool _isIgniting;
        private Vector3 _reserved;

        /// <summary>False while a fire is on its way or already here (burning or waiting to reignite).</summary>
        public bool CanIgnite => isActiveAndEnabled && !_isIgniting && _fire == null;

        /// <summary>This object's fire went out and waits (e.g. for its room to re-light it); it can be lit again at once.</summary>
        public bool CanRekindle => isActiveAndEnabled && _fire != null && !_fire.IsBurning;

        public Vector3 IgnitionPosition => _ignitionPoint != null ? _ignitionPoint.position : GetTopCenter();

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider>();
            _body = GetComponentInParent<Rigidbody>();
        }

        private void OnDisable()
        {
            // Disabling stops the pending coroutine; free its spot.
            if (_isIgniting)
                FireSpreader.Release(_reserved);
            _isIgniting = false;
        }

        /// <summary>
        /// Starts catching fire; the fire appears after the ignition delay.
        /// Returns false if the object cannot catch fire now (already burning, in water, fire nearby).
        /// </summary>
        public bool TryIgnite(FireSpreadSettings settings)
        {
            if (!CanIgnite || settings == null || settings.FirePrefab == null)
                return false;

            Vector3 firePosition = settings.GetFirePosition(IgnitionPosition);
            if (!FireSpreader.CanBurnAt(firePosition, settings))
                return false;

            StartCoroutine(Ignite(settings, firePosition));
            return true;
        }

        /// <summary>
        /// Lights this object's extinguished fire again at once (e.g. hit by a flamethrower), without waiting for
        /// its room's temperature. Returns false if there is no extinguished fire here.
        /// </summary>
        public bool TryRekindle()
        {
            if (!CanRekindle)
                return false;

            _fire.Reignite();
            return true;
        }

        private IEnumerator Ignite(FireSpreadSettings settings, Vector3 firePosition)
        {
            _isIgniting = true;
            _reserved = firePosition;
            FireSpreader.Reserve(firePosition);

            if (settings.IgnitionDelay > 0f)
                yield return new WaitForSeconds(settings.IgnitionDelay);

            FireSpreader.Release(firePosition);
            _isIgniting = false;

            // Checked again: the object may have moved into water or another fire may be here now.
            firePosition = settings.GetFirePosition(IgnitionPosition);
            if (!FireSpreader.CanBurnAt(firePosition, settings))
                yield break;

            _fire = FireSpreader.SpawnFire(firePosition, settings);

            // A movable object carries its fire along.
            if (_body != null)
                _fire.transform.SetParent(transform, true);
        }

        private Vector3 GetTopCenter()
        {
            bool found = false;
            Bounds bounds = default;
            foreach (Collider part in _colliders)
            {
                if (part == null || !part.enabled || part.isTrigger)
                    continue;

                if (found)
                {
                    bounds.Encapsulate(part.bounds);
                }
                else
                {
                    bounds = part.bounds;
                    found = true;
                }
            }

            return found ? new Vector3(bounds.center.x, bounds.max.y, bounds.center.z) : transform.position;
        }
    }
}
