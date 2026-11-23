using System;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Pure gameplay model of the pump: on/off state and pressure that ramps toward a target.
    /// </summary>
    public sealed class PumpModel
    {
        private readonly float _maxPressure;
        private readonly float _maxFlowRate;
        private readonly float _pressureChangeRate;

        /// <summary>
        /// Creates a stopped pump with zero pressure.
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

        public bool IsTurnOn { get; private set; }
        /// <summary>
        /// Pressure the pump aims for while running, in pascals (Pa).
        /// </summary>
        public float TargetPressure { get; private set; }
        public float CurrentPressure { get; private set; }
        public float MaxPressure => _maxPressure;
        public void Start() => IsTurnOn = true;
        public void Stop() => IsTurnOn = false;
        public void Toggle() => IsTurnOn = !IsTurnOn;
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
        /// Actual output: demand and pressure reduced by the water supply.
        /// </summary>
        public WaterFlowData Output => new WaterFlowData(DemandFlowRate * SupplyRatio, CurrentPressure * SupplyRatio);

        public void SetSupplyRatio(float ratio)
        {
            SupplyRatio = Math.Min(Sanitize(ratio), 1f);
        }
        public void SetTargetPressure(float pressure)
        {
            TargetPressure = Math.Min(Sanitize(pressure), _maxPressure);
        }

        /// <summary>
        /// Advances the simulation. While stopped, pressure decays toward zero at the same rate.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds; non-positive values are ignored.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime))
                return;

            float goal = IsTurnOn ? TargetPressure : 0f;
            float maxStep = _pressureChangeRate * deltaTime;
            float difference = goal - CurrentPressure;

            CurrentPressure = Math.Abs(difference) <= maxStep 
                ? goal
                : CurrentPressure + Math.Sign(difference) * maxStep;
        }

        private static float Sanitize (float value)
        {
            return float.IsNaN(value) ? 0f: Math.Max(0f, value);
        }

    }

}