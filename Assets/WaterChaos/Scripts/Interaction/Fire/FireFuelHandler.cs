using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Flammable liquid (gasoline) hitting a burning fire feeds it: the fire grows above full intensity
    /// (bigger, spreads under its normal rules, needs more water) and calms back down when the fuel stops.
    /// Separate from <see cref="FireExtinguishHandler"/>, which only reacts to extinguishing liquids.
    /// </summary>
    [RequireComponent(typeof(FireExtinguishHandler))]
    public sealed class FireFuelHandler : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Fuel")]
        [Tooltip("Intensity added by one liter of fuel (1/L).")]
        [SerializeField, Min(0f)] private float _intensityPerLiter = 0.1f;

        [Tooltip("Highest intensity fuel can push the fire to (1 = normal full fire).")]
        [SerializeField, Min(1f)] private float _maxIntensity = 2f;

        [Tooltip("How fast an overburning fire calms back to full intensity, in intensity per second (1/s).")]
        [SerializeField, Min(0f)] private float _overburnFadeRate = 0.15f;

        private FireExtinguishHandler _fire;

        private void Awake()
        {
            _fire = GetComponent<FireExtinguishHandler>();
        }

        private void FixedUpdate()
        {
            _fire.ReduceOverburn(_overburnFadeRate * Time.fixedDeltaTime);
        }

        // Called during FixedUpdate by WaterStream.
        public void ReceiveImpact(in WaterImpact impact)
        {
            if (!impact.Liquid.IsFlammable())
                return;

            _fire.AddFuel(impact.Volume * _intensityPerLiter, _maxIntensity);
        }
    }
}
