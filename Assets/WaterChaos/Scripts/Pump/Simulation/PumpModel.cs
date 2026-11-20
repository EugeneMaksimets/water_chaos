using System;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Pure gameplay model of the pump: on/off state and pressure that ramps toward a target.
    /// Has no Unity dependencies so it can be driven and tested without a scene.
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

        public bool IsRunning { get; private set; }

        /// <summary>
        /// Pressure the pump aims for while running, in pascals (Pa).
        /// </summary>
        public float TargetPressure { get; private set; }

        /// <summary>
        /// Actual pressure right now, in pascals (Pa).
        /// </summary>
        public float CurrentPressure { get; private set; }

        public float MaxPressure => _maxPressure;

        /// <summary>
        /// Prototype simplification: flow rate scales linearly with current pressure.
        /// </summary>
        public WaterFlowData Output
        {
            get
            {
                float pressureRatio = _maxPressure > 0f ? CurrentPressure / _maxPressure : 0f;
                return new WaterFlowData(_maxFlowRate * pressureRatio, CurrentPressure);
            }
        }

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Toggle() => IsRunning = !IsRunning;

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
            if (!(deltaTime > 0f))
                return;

            float goal = IsRunning ? TargetPressure : 0f;
            float maxStep = _pressureChangeRate * deltaTime;
            float difference = goal - CurrentPressure;

            CurrentPressure = Math.Abs(difference) <= maxStep
                ? goal
                : CurrentPressure + Math.Sign(difference) * maxStep;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
