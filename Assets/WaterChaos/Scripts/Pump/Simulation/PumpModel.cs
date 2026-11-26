using System;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Pure gameplay model of the pump: on/off state, pressure that ramps toward a target,
    /// and the water it draws from its source. The source owns the water; the pump only takes it.
    /// </summary>
    public sealed class PumpModel
    {
        private readonly float _maxPressure;
        private readonly float _maxFlowRate;
        private readonly float _pressureChangeRate;

        /// <summary>
        /// Creates a stopped pump with zero pressure, no source and no hose.
        /// </summary>
        /// <param name="maxPressure">Upper pressure limit in pascals (Pa).</param>
        /// <param name="maxFlowRate">Flow rate at max pressure in liters per second (L/s).</param>
        /// <param name="pressureChangeRate">How fast pressure moves toward its goal, in Pa per second.</param>
        public PumpModel(float maxPressure, float maxFlowRate, float pressureChangeRate)
        {
            _maxPressure = Sanitize(maxPressure);
            _maxFlowRate = Sanitize(maxFlowRate);
            _pressureChangeRate = Sanitize(pressureChangeRate);
        }

        /// <summary>Raised when <see cref="State"/> changes; argument is the new state.</summary>
        public event Action<PumpState> StateChanged;

        public bool IsTurnOn { get; private set; }
        /// <summary>
        /// Pressure the pump aims for while running, in pascals (Pa).
        /// </summary>
        public float TargetPressure { get; private set; }
        public float CurrentPressure { get; private set; }
        public float MaxPressure => _maxPressure;

        /// <summary>Water the intake draws from; null when the intake has no usable water.</summary>
        public WaterReservoir Source { get; private set; }

        /// <summary>A hose is attached to the outlet.</summary>
        public bool IsOutletConnected { get; private set; }

        /// <summary>Liquid the intake draws; water without a source.</summary>
        public LiquidType Liquid => Source != null ? Source.Liquid : LiquidType.Water;

        /// <summary>The source exists and still has water.</summary>
        public bool HasWater => Source != null && !Source.IsEmpty;

        /// <summary>Every condition to pump is met: water at the intake and a hose on the outlet.</summary>
        public bool CanRun => HasWater && IsOutletConnected;

        public PumpState State { get; private set; } = PumpState.NoWater;

        /// <summary>Volume drawn by the last <see cref="Tick"/>, in liters (L); 0 if it drew nothing.</summary>
        public float LastDrawnVolume { get; private set; }

        /// <summary>Reservoir the last <see cref="Tick"/> drew from; null if it drew nothing.</summary>
        public WaterReservoir LastDrawnSource { get; private set; }

        /// <summary>
        /// Share of the demanded water the intake actually gets: 1 = enough, 0 = dry.
        /// </summary>
        public float SupplyRatio { get; private set; } = 1f;

        /// <summary>
        /// Flow the pump wants at current pressure, ignoring supply, in liters per second (L/s).
        /// Prototype simplification: scales linearly with current pressure.
        /// </summary>
        public float DemandFlowRate => _maxPressure > 0f ? _maxFlowRate * CurrentPressure / _maxPressure : 0f;

        /// <summary>
        /// Actual output: demand and pressure reduced by the water supply; zero unless the pump can run.
        /// </summary>
        public WaterFlowData Output => CanRun
            ? new WaterFlowData(DemandFlowRate * SupplyRatio, CurrentPressure * SupplyRatio, Liquid)
            : new WaterFlowData(0f, 0f, Liquid);

        /// <summary>Turns the motor on; ignored while the pump cannot run (no water or no hose).</summary>
        public void Start()
        {
            if (CanRun)
                IsTurnOn = true;
            UpdateState();
        }

        public void Stop()
        {
            IsTurnOn = false;
            UpdateState();
        }

        public void Toggle()
        {
            if (IsTurnOn)
                Stop();
            else
                Start();
        }

        /// <summary>
        /// Sets the water the intake draws from. Null = no usable water: output drops to zero,
        /// a running motor pauses and resumes when water returns.
        /// </summary>
        public void SetSource(WaterReservoir source)
        {
            Source = source;
            if (Source == null)
                SupplyRatio = 0f;
            UpdateState();
        }

        /// <summary>Tells the pump whether a hose is attached; without it nothing is delivered or drawn.</summary>
        public void SetOutletConnected(bool connected)
        {
            IsOutletConnected = connected;
            UpdateState();
        }

        public void SetSupplyRatio(float ratio)
        {
            SupplyRatio = Math.Min(Sanitize(ratio), 1f);
        }

        public void SetTargetPressure(float pressure)
        {
            TargetPressure = Math.Min(Sanitize(pressure), _maxPressure);
        }

        /// <summary>
        /// Advances the simulation: moves pressure toward its goal, then draws this step's water
        /// (flow × time, in liters) from the source. While stopped or unable to run, pressure decays toward zero.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds; non-positive values are ignored.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime))
                return;

            LastDrawnVolume = 0f;
            LastDrawnSource = null;

            float goal = IsTurnOn && CanRun ? TargetPressure : 0f;
            float maxStep = _pressureChangeRate * deltaTime;
            float difference = goal - CurrentPressure;

            CurrentPressure = Math.Abs(difference) <= maxStep
                ? goal
                : CurrentPressure + Math.Sign(difference) * maxStep;

            if (CanRun)
                DrawFromSource(deltaTime);

            UpdateState();
        }

        private void DrawFromSource(float deltaTime)
        {
            float requested = DemandFlowRate * deltaTime;
            if (requested <= 0f)
                return;

            float drawn = Source.Draw(requested);
            SetSupplyRatio(drawn / requested);

            LastDrawnVolume = drawn;
            LastDrawnSource = drawn > 0f ? Source : null;
        }

        private void UpdateState()
        {
            PumpState state = EvaluateState();
            if (state == State)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }

        private PumpState EvaluateState()
        {
            if (!HasWater)
                return PumpState.NoWater;

            if (!IsOutletConnected)
                return PumpState.Disconnected;

            return IsTurnOn ? PumpState.Running : PumpState.Ready;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
