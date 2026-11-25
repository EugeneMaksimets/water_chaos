using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Feeds water hitting this object into a FireModel.
    /// </summary>
    public sealed class FireExtinguishHandler : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Fire")]
        [Tooltip("Intensity removed by one liter of water (1/L).")]
        [SerializeField, Min(0f)]
        private float _coolingPerLiter = 0.033f;
        [Tooltip("Intensity (0..1) at or below which the fire goes out.")]
        [SerializeField, Range(0f, 1f)]
        private float _extinguishThreshold = 0.05f;

        [Tooltip("Seconds (s) after which an extinguished fire burns again; 0 = never. " +
                 "Not used while the fire belongs to an area (room) that controls re-ignition.")]
        [SerializeField, Min(0f)]
        private float _reigniteDelay = 5f;

        private FireModel _model;
        private Collider[] _colliders;
        private bool _wasBurning;

        /// <summary>
        /// Normalized fire strength: 1 = full, 0 = extinguished.
        /// </summary>
        public float Intensity => _model.Intensity;
        public bool IsBurning => _model.IsBurning;

        /// <summary>Area (room) this fire belongs to; null outdoors. Set by the area itself.</summary>
        public IFireArea Area { get; private set; }

        /// <summary>Raised when the fire goes out or starts burning again.</summary>
        public event Action<FireExtinguishHandler> BurningChanged;

        private void Awake()
        {
            _model = new FireModel(_coolingPerLiter, _extinguishThreshold, _reigniteDelay);
            _wasBurning = _model.IsBurning;
            _colliders = GetComponentsInChildren<Collider>();
        }

        private void FixedUpdate()
        {
            // An area decides itself when the fire comes back.
            if (Area == null)
            {
                _model.Tick(Time.fixedDeltaTime);
                NotifyIfChanged();
            }
        }

        // Called during FixedUpdate by WaterStream
        public void ReceiveImpact(in WaterImpact impact)
        {
            _model.ApplyWater(impact.Volume);
            NotifyIfChanged();
        }

        /// <summary>Lights the extinguished fire again at full intensity.</summary>
        public void Reignite()
        {
            _model.Reignite();
            NotifyIfChanged();
        }

        /// <summary>Joins an area (room), or leaves it with null.</summary>
        public void SetArea(IFireArea area)
        {
            Area = area;
        }

        private void NotifyIfChanged()
        {
            bool burning = _model.IsBurning;
            if (burning == _wasBurning)
                return;

            _wasBurning = burning;
            SetCollidersEnabled(burning);
            BurningChanged?.Invoke(this);
        }

        // An extinguished fire lets water pass through; it is hit again once it burns.
        private void SetCollidersEnabled(bool value)
        {
            foreach (Collider part in _colliders)
            {
                if (part != null)
                    part.enabled = value;
            }
        }
    }
}
