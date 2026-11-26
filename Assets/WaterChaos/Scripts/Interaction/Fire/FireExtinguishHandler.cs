using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Owns the fire's FireModel and puts it out with extinguishing liquids (water) hitting this object.
    /// Other liquids are ignored here; fuel is handled by <see cref="FireFuelHandler"/>.
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

        [Tooltip("Outdoors (no room): an extinguished fire lights again by itself after Reignite Delay. " +
                 "Off = once out, it stays out. Fires in a room are always re-lit by the room (temperature), not by this.")]
        [SerializeField]
        private bool _reigniteOutdoors;

        [Tooltip("Seconds (s) after which an extinguished outdoor fire burns again, if Reignite Outdoors is on.")]
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
            // The delay only drives outdoor re-ignition (Tick below); a room re-lights its fires itself.
            _model = new FireModel(_coolingPerLiter, _extinguishThreshold, _reigniteOutdoors ? _reigniteDelay : 0f);
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
            if (!impact.Liquid.Extinguishes())
                return;

            _model.ApplyWater(impact.Volume);
            NotifyIfChanged();
        }

        /// <summary>Feeds the burning fire (see <see cref="FireModel.AddFuel"/>); an extinguished fire stays out.</summary>
        public void AddFuel(float amount, float maxIntensity)
        {
            _model.AddFuel(amount, maxIntensity);
        }

        /// <summary>Calms an overburning fire toward full intensity (see <see cref="FireModel.ReduceOverburn"/>).</summary>
        public void ReduceOverburn(float amount)
        {
            _model.ReduceOverburn(amount);
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
