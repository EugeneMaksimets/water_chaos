using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// For test PumpModel
    /// </summary>
    public sealed class PumpController : MonoBehaviour, IWaterFlowSource
    {
        [Header("Limits")]
        [Tooltip("In Pascals (Pa)")]
        [SerializeField, Min(0f)] 
        private float _maxPressure = 500_000f;

        [Tooltip("In Liter per second (l/s)")]
        [SerializeField, Min(0f)] 
        private float _maxFlowRate = 10f;

        [Header ("Responce")]
        [Tooltip("How fast pressure changes in Pascals (Pa)")]
        [SerializeField, Min(0f)] 
        private float _pressureChangeRate = 50_000f;

        [Tooltip("Target on start, in pascals (Pa)")]
        [SerializeField, Min(0f)] 
        private float _initialTargetPressure = 20_000f;

        private PumpModel _model;
        private WaterReservoir _reservoir;

        public bool isTurnOn => _model.IsTurnOn;
        public float MaxPressure => _model.MaxPressure;
        public float TargetPressure => _model.TargetPressure;
        public float CurrentPressure => _model.CurrentPressure;
        public WaterFlowData Output => _model.Output;

        private void Awake()
        {
            _model = new PumpModel(_maxPressure, _maxFlowRate, _pressureChangeRate);
            SetTargetPressure(_initialTargetPressure);
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            _model.Tick(deltaTime);

            if (_reservoir != null)
                DrawFromReservoir(deltaTime);
        }

        public void Toggle() => _model.Toggle();
        
        /// <summary>
        /// Connects the tank the pump draws from; null means an endless supply.
        /// </summary>
        public void SetReservoir(WaterReservoir reservoir)
        {
            _reservoir = reservoir;
            if (_reservoir == null)
                _model.SetSupplyRatio(1f);
        }

        public void SetTargetPressure(float pressure) => _model.SetTargetPressure(pressure);

        private void DrawFromReservoir(float deltaTime)
        {
            float requested = _model.DemandFlowRate * deltaTime;
            if (requested <= 0f)
                return;

            float drawn = _reservoir.Draw(requested);
            _model.SetSupplyRatio(drawn / requested);
        }
    }
}