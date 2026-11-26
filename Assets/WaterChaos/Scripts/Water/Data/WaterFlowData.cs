using System;

namespace WaterChaos.Water
{
    /// <summary>
    /// Immutable snapshot of liquid leaving a source (pump, hydrant, truck).
    /// Carries the liquid type, so every stage after the reservoir knows what flows.
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
        /// <summary>
        /// What flows; water unless the source says otherwise
        /// </summary>
        public LiquidType Liquid {get;}

        public WaterFlowData(float flowRate, float pressure, LiquidType liquid = LiquidType.Water)
        {
            Liquid = liquid;
            // Negative values have no physical meaning here; normalize once at construction
            FlowRate = float.IsNaN(flowRate) ? 0f : Math.Max(0f, flowRate);
            Pressure = float.IsNaN(pressure) ? 0f : Math.Max(0f, pressure);
        }
    }
}
