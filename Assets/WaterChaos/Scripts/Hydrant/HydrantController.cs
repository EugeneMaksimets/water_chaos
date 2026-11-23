using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Hydrant
{
    /// <summary>
    /// Scene adapter for <see cref="HydrantModel"/>; a water source that needs no pump.
    /// </summary>
    public sealed class HydrantController : MonoBehaviour, IWaterFlowSource, IWaterSourceControl
    {
        [Header("Network")]
        [Tooltip("In Pascals (Pa), with the valve fully open")]
        [SerializeField, Min(0f)]
        private float _pressure = 300_000f;

        [Tooltip("In liters per second (L/s), with the valve fully open")]
        [SerializeField, Min(0f)]
        private float _flowRate = 8f;

        [Header("Valve")]
        [Tooltip("Open-ratio per second (1/s): 2 = opens fully in 0.5 s")]
        [SerializeField, Min(0.01f)]
        private float _valveSpeed = 2f;

        [SerializeField]
        private bool _openOnStart;

        private HydrantModel _model;

        public bool IsOpen => _model.IsOpen;
        public float OpenRatio => _model.OpenRatio;
        public WaterFlowData Output => _model.Output;
        public void Stop() => _model.Close();

        /// <summary>Network pressure is fixed; the hydrant ignores pressure steps.</summary>
        public void AdjustPressure(int direction) { }

        private void Awake()
        {
            _model = new HydrantModel(_pressure, _flowRate, _valveSpeed);
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