using System;
using WaterChaos.Water;

namespace WaterChaos.Hydrant
{
    /// <summary>
    /// Pure model of a hydrant: an endless network source with an adjustable pressure regulator
    /// behind a valve that opens smoothly. Flow scales linearly with the set pressure.
    /// </summary>
    public sealed class HydrantModel
    {
        private readonly float _maxPressure;
        private readonly float _maxFlowRate;
        private readonly float _valveSpeed;

        /// <summary>
        /// Creates a closed hydrant with its target pressure at the maximum.
        /// </summary>
        /// <param name="maxPressure">Highest pressure the regulator allows, in pascals (Pa).</param>
        /// <param name="maxFlowRate">Flow at max pressure with the valve fully open, in liters per second (L/s).</param>
        /// <param name="valveSpeed">How fast the valve moves, in open-ratio per second (1/s).</param>
        public HydrantModel(float maxPressure, float maxFlowRate, float valveSpeed)
        {
            _maxPressure = Sanitize(maxPressure);
            _maxFlowRate = Sanitize(maxFlowRate);
            _valveSpeed = Sanitize(valveSpeed);
            TargetPressure = _maxPressure;
        }

        public float MaxPressure => _maxPressure;

        /// <summary>
        /// Pressure the regulator delivers with the valve fully open, in pascals (Pa).
        /// </summary>
        public float TargetPressure { get; private set; }

        public bool IsOpen { get; private set; }

        /// <summary>
        /// How far the valve is open: 0 = closed, 1 = fully open.
        /// </summary>
        public float OpenRatio { get; private set; }

        public WaterFlowData Output => new WaterFlowData(TargetFlowRate * OpenRatio, TargetPressure * OpenRatio);

        /// <summary>Flow at the target pressure, in liters per second (L/s).</summary>
        private float TargetFlowRate => _maxPressure > 0f ? _maxFlowRate * TargetPressure / _maxPressure : 0f;

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
        public void Toggle() => IsOpen = !IsOpen;

        /// <summary>
        /// Sets the regulator, clamped to 0..<see cref="MaxPressure"/>.
        /// </summary>
        /// <param name="pressure">In pascals (Pa); NaN becomes 0.</param>
        public void SetTargetPressure(float pressure)
        {
            TargetPressure = Math.Min(Sanitize(pressure), _maxPressure);
        }

        /// <summary>
        /// Moves the valve toward open or closed.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f || float.IsNaN(deltaTime))
                return;

            float goal = IsOpen ? 1f : 0f;
            float maxStep = _valveSpeed * deltaTime;
            float difference = goal - OpenRatio;

            OpenRatio = Math.Abs(difference) <= maxStep
                ? goal
                : OpenRatio + Math.Sign(difference) * maxStep;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}