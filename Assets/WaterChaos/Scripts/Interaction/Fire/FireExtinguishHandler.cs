using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Owns the fire's FireModel and puts it out with extinguishing liquids hitting this object,
    /// each as strong as its <see cref="LiquidTypeExtensions.ExtinguishingPower"/> (toxic liquid cools far less than water).
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

        // Every enabled fire, so a mission can count fires in an area without searching the scene.
        private static readonly List<FireExtinguishHandler> s_all = new List<FireExtinguishHandler>();

        private FireModel _model;
        private Collider[] _colliders;
        private bool _wasBurning;

        /// <summary>Every enabled fire in the scene, burning or extinguished.</summary>
        public static IReadOnlyList<FireExtinguishHandler> All => s_all;

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

        // Domain reload on entering Play Mode is off in this project: drop fires left from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_all.Clear();
        }

        private void OnEnable()
        {
            s_all.Add(this);
        }

        private void OnDisable()
        {
            s_all.Remove(this);
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
            float power = impact.Liquid.ExtinguishingPower();
            if (!(power > 0f))
                return;

            // A weak extinguisher (toxic liquid) cools like a smaller amount of water.
            _model.ApplyWater(impact.Volume * power);
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

        /// <summary>Sets the burning fire's intensity (0..1); at or below the threshold it goes out.</summary>
        public void SetIntensity(float intensity)
        {
            _model.SetIntensity(intensity);
            NotifyIfChanged();
        }

        /// <summary>Puts the fire out at once (e.g. a mission time limit); its area may still re-light it later.</summary>
        public void Extinguish()
        {
            _model.Extinguish();
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
