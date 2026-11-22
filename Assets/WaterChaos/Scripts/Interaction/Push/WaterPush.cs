using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Converts a water impact into a push force.
    /// </summary>
    public static class WaterPush
    {
        /// <summary>
        /// Push force in newtons (N), directed along the water flow.
        /// Returns zero when the flow direction is zero.
        /// </summary>
        public static Vector3 CalculateForce(in WaterImpact impact)
        {
            return impact.Direction.normalized * impact.Force;
        }
    }
}