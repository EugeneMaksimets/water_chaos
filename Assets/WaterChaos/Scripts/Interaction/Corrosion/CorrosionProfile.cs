using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// How one kind of material stands up to corrosive liquid (fence, interior wall, reinforced wall).
    /// Shared by every object of that kind through <see cref="CorrodibleObject"/>. Gameplay only, no visuals.
    /// </summary>
    [CreateAssetMenu(fileName = "Corrosion_", menuName = "Water Chaos/Corrosion Profile")]
    public sealed class CorrosionProfile : ScriptableObject
    {
        [Header("Strength")]
        [Tooltip("0..1: share of the corrosion this material shrugs off. 0 = soft (fence), 0.85 = reinforced, 1 = immune.")]
        [SerializeField, Range(0f, 1f)] private float _resistance;

        [Tooltip("Corrosion points at which the object is destroyed.")]
        [SerializeField, Min(0.1f)] private float _threshold = 10f;

        [Header("Accumulation")]
        [Tooltip("Corrosion points added by one liter of full-strength corrosive liquid, before resistance (1/L).")]
        [SerializeField, Min(0f)] private float _corrosionPerLiter = 1f;

        [Tooltip("Corrosion points per second (1/s) added while the surface stays wet after a hit, before resistance.")]
        [SerializeField, Min(0f)] private float _soakRate = 1f;

        [Tooltip("Seconds (s) the surface stays wet and keeps corroding after the last hit. Water washes it off at once.")]
        [SerializeField, Min(0f)] private float _wetDuration = 2f;

        public float Resistance => _resistance;
        public float Threshold => _threshold;
        public float CorrosionPerLiter => _corrosionPerLiter;
        public float SoakRate => _soakRate;
        public float WetDuration => _wetDuration;

        /// <summary>Fresh, intact corrosion state for one object.</summary>
        public CorrosionModel CreateModel()
        {
            return new CorrosionModel(_resistance, _corrosionPerLiter, _threshold, _soakRate, _wetDuration);
        }
    }
}
