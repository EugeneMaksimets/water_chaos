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

        /// <summary>
        /// Push force in newtons (N) scaled for a particular object.
        /// Returns zero while the impact is weaker than <paramref name="minImpactForce"/>.
        /// </summary>
        /// <param name="minImpactForce">Weakest impact (N) that moves the object.</param>
        /// <param name="multiplier">Share of the impact force applied (1 = all); negative and NaN give zero.</param>
        public static Vector3 CalculateForce(in WaterImpact impact, float minImpactForce, float multiplier)
        {
            if (impact.Force < minImpactForce || !(multiplier > 0f))
                return Vector3.zero;

            return CalculateForce(impact) * multiplier;
        }
    }
}