using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Scene adapter for <see cref="PumpModel"/>: exposes tuning in the Inspector
    /// and advances the model on the physics tick so flow consumers see a stable value.
    /// </summary>
    public sealed class PumpController : MonoBehaviour
    {
        [Header("Limits")]
        [Tooltip("Maximum pump pressure in pascals (Pa).")]
        [SerializeField, Min(0f)] private float _maxPressure = 500_000f;

        [Tooltip("Flow rate at maximum pressure in liters per second (L/s).")]
        [SerializeField, Min(0f)] private float _maxFlowRate = 10f;

        [Header("Response")]
        [Tooltip("How fast pressure changes toward its goal, in Pa per second.")]
        [SerializeField, Min(0f)] private float _pressureChangeRate = 250_000f;

        [Tooltip("Target pressure applied on start, in pascals (Pa).")]
        [SerializeField, Min(0f)] private float _initialTargetPressure = 250_000f;

        private PumpModel _model;

        public bool IsRunning => _model.IsRunning;
        public float TargetPressure => _model.TargetPressure;
        public float CurrentPressure => _model.CurrentPressure;
        public float MaxPressure => _model.MaxPressure;
        public WaterFlowData Output => _model.Output;

        private void Awake()
        {
            _model = new PumpModel(_maxPressure, _maxFlowRate, _pressureChangeRate);
            _model.SetTargetPressure(_initialTargetPressure);
        }

        private void FixedUpdate()
        {
            _model.Tick(Time.fixedDeltaTime);
        }

        public void Toggle() => _model.Toggle();

        public void SetTargetPressure(float pressure) => _model.SetTargetPressure(pressure);
    }
}
