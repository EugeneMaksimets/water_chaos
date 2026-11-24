using System;
using WaterChaos.Water;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Turns the real water flow into numbers for the hose look. Pure math, no state:
    /// the flow itself always comes from the water source.
    /// </summary>
    public static class HoseWaterVisual
    {
        /// <summary>Fullness shown as soon as any water flows, so low pressure is still visible (0..1).</summary>
        public const float MinFullness = 0.35f;

        private const float LitersToCubicMeters = 0.001f;

        /// <summary>
        /// Average speed of water inside the hose, in meters per second (m/s):
        /// flow rate divided by the hose cross-section. Zero when nothing flows.
        /// </summary>
        /// <param name="diameter">Inner hose diameter, in meters (m).</param>
        public static float WaterSpeed(WaterFlowData flow, float diameter)
        {
            if (flow.FlowRate <= 0f || diameter <= 0f)
                return 0f;

            float radius = diameter * 0.5f;
            float area = (float)Math.PI * radius * radius;
            return flow.FlowRate * LitersToCubicMeters / area;
        }

        /// <summary>
        /// How full and tight the hose looks (0..1). Zero when nothing flows,
        /// otherwise grows from <see cref="MinFullness"/> to 1 at the reference pressure.
        /// </summary>
        /// <param name="referencePressure">Pressure that looks fully charged, in pascals (Pa).</param>
        public static float Fullness(WaterFlowData flow, float referencePressure)
        {
            if (flow.FlowRate <= 0f)
                return 0f;

            float t = referencePressure > 0f ? Math.Min(flow.Pressure / referencePressure, 1f) : 1f;
            return MinFullness + (1f - MinFullness) * t;
        }
    }
}
