using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Editable conversion from physical flow values (Pa, L/s) to gameplay values (range, force)
    /// </summary>
    [CreateAssetMenu(fileName = "WaterFlowTuning", menuName = "Water Chaos/Water Flow Tuning")]
    public sealed class WaterFlowTuning : ScriptableObject
    {
        [Header("Range")]
        [Tooltip("Pressure that give max range in pascals")]
        [SerializeField, Min(0f)]
        private float _referencePressure = 500_000f;

        [Tooltip("Stream range at reference pressure, in meters (m).")]
        [SerializeField, Min(0f)]
        private float _maxRange = 15f;

        [Tooltip("X: pressure / reference pressure (0..1). Y: range / max range (0..1).")]
        [SerializeField]
        private AnimationCurve _rangeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Force")]
        [Tooltip("Flow rate that gives max force, in liters per second (L/s).")]
        [SerializeField, Min(0f)]
        private float _referenceFlowRate = 10f;

        [Tooltip("Impact force at reference flow rate, in newtons (N).")]
        [SerializeField, Min(0f)] 
        private float _maxImpactForce = 200f;

        [Tooltip("X: flow rate / reference flow rate (0..1). Y: force / max force (0..1).")]
        [SerializeField] 
        private AnimationCurve _forceCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public float MaxRange => _maxRange;
        public float MaxImpactForce => _maxImpactForce;

        /// <summary>Stream range in meters (m) for the given pressure in pascals (Pa).</summary>
        public float GetRange(float pressure)
        {
            return Evaluate(_rangeCurve, pressure, _referencePressure) * _maxRange;
        }

        /// <summary>Impact force in newtons (N) for the given flow rate in liters per second (L/s).</summary>
        public float GetImpactForce(float flowRate)
        {
            return Evaluate(_forceCurve, flowRate, _referenceFlowRate) * _maxImpactForce;
        }

        // Normalizes input to 0..1, samples the curve, and clamps the result so a badly drawn curve
        // can never produce negative or oversized gameplay values.
        private static float Evaluate(AnimationCurve curve, float value, float reference)
        {
            if(reference <= 0f)
                return 0f;

            float ratio = Mathf.Clamp01(value / reference);
            if(float.IsNaN(ratio))
                return 0f;

            return Mathf.Clamp01(curve.Evaluate(ratio));
        }
    }
}