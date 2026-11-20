using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// For test PumpModel
    /// </summary>
    public sealed class PumpController : MonoBehaviour
    {
        [Header("Limits")]
        [Tooltip("In Pascals (Pa)")]
        [SerializeField, Min(0f)] private float _maxPressure = 500_000f;

        [Tooltip("In Liter per second (l/s)")]
        [SerializeField, Min(0f)] private float _maxFlowRate = 10f;

        [Header ("Responce")]
        [Tooltip("How fast pressure changes in Pascals (Pa)")]
        [SerializeField, Min(0f)] private float _pressureChangeRate = 50_000f;

        private PumpModel _model;

        public bool isTurnOn => _model.IsTurnOn;
        public float MaxPressure => _model.MaxPressure;
        public float TargetPressure => _model.TargetPressure;
        public float CurrentPressure => _model.CurrentPressure;
        public WaterFlowData Output => _model.Output;

        private void Start()
        {
            _model = new PumpModel(_maxPressure, _maxFlowRate, _pressureChangeRate);
            SetTargetPressure(_pressureChangeRate);
        }

        private void FixedUpdate()
        {
            _model.Tick(Time.fixedDeltaTime);
        }

        public void Toggle() => _model.Toggle();

        public void SetTargetPressure(float pressure) => _model.SetTargetPressure(pressure);

    }
}