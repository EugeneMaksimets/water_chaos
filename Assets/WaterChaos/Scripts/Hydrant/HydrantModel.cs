using System;
using WaterChaos.Water;

namespace WaterChaos.Hydrant
{
    /// <summary>
    /// Pure model of a hydrant: fixed network pressure and flow behind a valve that opens smoothly.
    /// </summary>
    public sealed class HydrantModel
    {
        private readonly float _pressure;
        private readonly float _flowRate;
        private readonly float _valveSpeed;

        /// <summary>
        /// Creates a closed hydrant.
        /// </summary>
        /// <param name="pressure">Network pressure with the valve fully open, in pascals (Pa).</param>
        /// <param name="flowRate">Flow with the valve fully open, in liters per second (L/s).</param>
        /// <param name="valveSpeed">How fast the valve moves, in open-ratio per second (1/s).</param>
        public HydrantModel(float pressure, float flowRate, float valveSpeed)
        {
            _pressure = Sanitize(pressure);
            _flowRate = Sanitize(flowRate);
            _valveSpeed = Sanitize(valveSpeed);
        }

        public bool IsOpen { get; private set; }

        /// <summary>
        /// How far the valve is open: 0 = closed, 1 = fully open.
        /// </summary>
        public float OpenRatio { get; private set; }

        public WaterFlowData Output => new WaterFlowData(_flowRate * OpenRatio, _pressure * OpenRatio);

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
        public void Toggle() => IsOpen = !IsOpen;

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