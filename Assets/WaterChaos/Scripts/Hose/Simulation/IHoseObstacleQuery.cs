using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Answers "is something between these two points?" for the hose path solver,
    /// so the solver works without physics in tests.
    /// </summary>
    public interface IHoseObstacleQuery
    {
        /// <summary>Finds the first obstacle on the segment from → to.</summary>
        /// <param name="point">Hit point in world space.</param>
        /// <param name="normal">Surface normal at the hit point.</param>
        bool Linecast(Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal);

        /// <summary>True when a tube of this radius, in meters (m), from → to touches no obstacle.</summary>
        bool IsClear(Vector3 from, Vector3 to, float radius);
    }
}
