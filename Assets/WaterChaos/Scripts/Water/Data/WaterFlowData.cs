using System;

namespace WaterChaos.Water
{
    /// <summary>
    /// Immutable snapshot of water leaving the pump
    /// </summary>
    public readonly struct WaterFlowData
    {
        /// <summary>
        /// In liter per second (L/s) (cannot be negative)
        /// </summary>
        public float FlowRate {get;}
        /// <summary>
        /// in Pascals (Pa) (cannot be negative)
        /// </summary>
        public float Pressure {get;}

        public WaterFlowData(float flowRate, float pressure)
        {
            // Negative values have no physical meaning here; normalize once at construction
            FlowRate = float.IsNaN(flowRate) ? 0f : Math.Max(0f, flowRate);
            Pressure = float.IsNaN(pressure) ? 0f : Math.Max(0f, pressure);
        }
    }
}
