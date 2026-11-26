using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Scene adapter for <see cref="PumpModel"/>: ticks it in FixedUpdate,
    /// tells it whether a hose is on the outlet and exposes the output to the water system.
    /// The water source is set from outside (<see cref="SetReservoir"/>): a fixed tank or a deployment check.
    /// </summary>
    public sealed class PumpController : MonoBehaviour, IWaterFlowSource, IWaterSourceControl
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

        [Header("Control")]
        [Tooltip("Target pressure change per adjust step, in pascals (Pa)")]
        [SerializeField, Min(0f)]
        private float _pressureStep = 50_000f;

        [Tooltip("Target on start, in pascals (Pa)")]
        [SerializeField, Min(0f)]
        private float _initialTargetPressure = 20_000f;

        [Header("Supply")]
        [Tooltip("Fixed tank the pump draws from. Leave empty on a deployable pump: PumpDeployment picks the source.")]
        [SerializeField]
        private WaterReservoirComponent _reservoirSource;

        [Header("Outlet")]
        [Tooltip("Where the hose attaches; empty = first WaterConnectionPoint in children.")]
        [SerializeField]
        private WaterConnectionPoint _outlet;

        private PumpModel _model;
        private bool _externalOutletConnected;
        private float _lastTickTime = -1f;

        /// <summary>Raised when the pump state changes; argument is the new state.</summary>
        public event Action<PumpState> StateChanged;

        public bool isTurnOn => _model.IsTurnOn;
        public bool IsActive => _model.IsActive;
        public float MaxPressure => _model.MaxPressure;

        /// <summary>Flow at max pressure, in liters per second (L/s).</summary>
        public float MaxFlowRate => _maxFlowRate;
        public float TargetPressure => _model.TargetPressure;
        public float CurrentPressure => _model.CurrentPressure;
        public WaterFlowData Output => _model.Output;

        /// <summary>Liquid the pump draws from its reservoir.</summary>
        public LiquidType Liquid => _model.Liquid;
        public PumpState State => _model.State;
        public bool IsOutletConnected => _model.IsOutletConnected;
        public void AdjustPressure(int direction) => SetTargetPressure(TargetPressure + direction * _pressureStep);

        public void Stop() => _model.Stop();

        public float SupplyRatio => _model.SupplyRatio;

        /// <summary>Water the pump draws from; null means no supply.</summary>
        public WaterReservoir Reservoir => _model.Source;

        /// <summary>Volume drawn in the current physics step, in liters (L); 0 if the pump has not ticked in it.</summary>
        public float DrawnThisStep => _lastTickTime == Time.fixedTime ? _model.LastDrawnVolume : 0f;

        /// <summary>Reservoir the current physics step's water came from; null if none.</summary>
        public WaterReservoir DrawnSourceThisStep => _lastTickTime == Time.fixedTime ? _model.LastDrawnSource : null;

        private void Awake()
        {
            _model = new PumpModel(_maxPressure, _maxFlowRate, _pressureChangeRate);
            _model.StateChanged += OnModelStateChanged;
            SetTargetPressure(_initialTargetPressure);

            if (_outlet == null)
                _outlet = GetComponentInChildren<WaterConnectionPoint>(true);
        }

        private void Start()
        {
            if (_reservoirSource != null)
                SetReservoir(_reservoirSource.Reservoir);
        }

        private void FixedUpdate()
        {
            RefreshOutlet();
            _model.Tick(Time.fixedDeltaTime);
            _lastTickTime = Time.fixedTime;
        }

        public void Toggle()
        {
            RefreshOutlet();
            _model.Toggle();
        }

        /// <summary>
        /// Connects the water the pump draws from; null means no supply, so the pump gives no flow.
        /// </summary>
        public void SetReservoir(WaterReservoir reservoir)
        {
            if (reservoir != _model.Source)
                _model.SetSource(reservoir);
        }

        public void SetTargetPressure(float pressure) => _model.SetTargetPressure(pressure);

        /// <summary>
        /// Another system (e.g. the water transfer hose) delivers from its own connector:
        /// counts as an attached outlet, in addition to the regular one.
        /// </summary>
        public void SetExternalOutletConnected(bool connected) => _externalOutletConnected = connected;

        private void RefreshOutlet()
        {
            _model.SetOutletConnected((_outlet != null && _outlet.IsAttached) || _externalOutletConnected);
        }

        private void OnModelStateChanged(PumpState state) => StateChanged?.Invoke(state);
    }
}
