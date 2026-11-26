using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Scene adapter for <see cref="WaterReservoir"/>: builds the tank from inspector settings.
    /// The same component makes a water or a gasoline reservoir (<see cref="LiquidType"/>).
    /// </summary>
    public sealed class WaterReservoirComponent : MonoBehaviour
    {
        [Header("Liquid")]
        [Tooltip("What the reservoir holds at start. Gasoline pumped in turns all of it into gasoline until it is drained empty.")]
        [SerializeField]
        private LiquidType _liquid = LiquidType.Water;

        [Header("Volume")]
        [Tooltip("Maximum volume in liters (L)")]
        [SerializeField, Min(0f)]
        private float _capacity = 5000f;
        [Tooltip("Fill level (0..1) below which the outflow weakens smoothly; 0 = full flow until empty")]
        [SerializeField, Range(0f, 1f)]
        private float _lowLevelRatio = 0.2f;

        [Tooltip("Volume at start in liters (L); cannot exceed capacity")]
        [SerializeField, Min(0f)]
        private float _initialVolume = 500f;

        public WaterReservoir Reservoir { get; private set; }

        private void Awake()
        {
            Reservoir = new WaterReservoir(_capacity, _initialVolume, _lowLevelRatio, _liquid);
        }

        private void OnValidate()
        {
            _initialVolume = Mathf.Min(_initialVolume, _capacity);
        }
    }
}