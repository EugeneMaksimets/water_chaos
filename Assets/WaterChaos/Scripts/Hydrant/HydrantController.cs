using UnityEngine;
using UnityEngine.Serialization;
using WaterChaos.Water;

namespace WaterChaos.Hydrant
{
    /// <summary>
    /// Scene adapter for <see cref="HydrantModel"/>; an endless water source that needs no pump.
    /// The mouse wheel steps its pressure regulator.
    /// </summary>
    public sealed class HydrantController : MonoBehaviour, IWaterFlowSource, IWaterSourceControl
    {
        [Header("Network")]
        [Tooltip("Highest regulator pressure, in pascals (Pa)")]
        [SerializeField, Min(0f), FormerlySerializedAs("_pressure")]
        private float _maxPressure = 500_000f;

        [Tooltip("Flow at max pressure with the valve fully open, in liters per second (L/s)")]
        [SerializeField, Min(0f), FormerlySerializedAs("_flowRate")]
        private float _maxFlowRate = 10f;

        [Header("Regulator")]
        [Tooltip("Pressure change per mouse wheel step, in pascals (Pa)")]
        [SerializeField, Min(0f)]
        private float _pressureStep = 50_000f;

        [Tooltip("Regulator pressure on start, in pascals (Pa)")]
        [SerializeField, Min(0f)]
        private float _initialPressure = 250_000f;

        [Header("Valve")]
        [Tooltip("Open-ratio per second (1/s): 2 = opens fully in 0.5 s")]
        [SerializeField, Min(0.01f)]
        private float _valveSpeed = 2f;

        [SerializeField]
        private bool _openOnStart;

        private HydrantModel _model;

        public bool IsOpen => _model.IsOpen;
        public bool IsActive => _model.IsOpen && _model.TargetPressure > 0f;
        public float MaxPressure => _model.MaxPressure;
        public float TargetPressure => _model.TargetPressure;
        public float OpenRatio => _model.OpenRatio;
        public WaterFlowData Output => _model.Output;
        public void Stop() => _model.Close();

        public void AdjustPressure(int direction) =>
            _model.SetTargetPressure(_model.TargetPressure + direction * _pressureStep);

        private void Awake()
        {
            _model = new HydrantModel(_maxPressure, _maxFlowRate, _valveSpeed);
            _model.SetTargetPressure(_initialPressure);
            if (_openOnStart)
                _model.Open();
        }

        private void FixedUpdate()
        {
            _model.Tick(Time.fixedDeltaTime);
        }

        [ContextMenu("Toggle Valve")]
        public void Toggle() => _model.Toggle();
    }
}