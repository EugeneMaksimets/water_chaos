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

        private FireModel _model;

        /// <summary>
        /// Normalized fire strength: 1 = full, 0 = extinguished.
        /// </summary>
        public float Intensity => _model.Intensity;
        public bool IsBurning => _model.IsBurning;

        private void Awake()
        {
            _model = new FireModel(_coolingPerLiter, _extinguishThreshold);
        }

        // Called during FixedUpdate by WaterStream
        public void ReceiveImpact(in WaterImpact impact)
        {
            _model.ApplyWater(impact.Volume);
        }
    }
}