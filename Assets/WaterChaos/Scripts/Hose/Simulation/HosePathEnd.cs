using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Where the gameplay path of a carried hose ends. It normally runs low (<paramref name="low"/>), so it catches fences
    /// and walls and wraps around them. But when the hands are above an obstacle the low line runs into (the player
    /// jumped or climbed over a fence), the path follows the hands, so it wraps over the top instead of cutting through.
    /// </summary>
    public static class HosePathEnd
    {
        /// <param name="lastFixed">The last fixed point of the path (the anchor or the last wrap).</param>
        /// <param name="hands">Where the hose end really is.</param>
        /// <param name="low">The same point moved down to the obstacle search height.</param>
        public static Vector3 Choose(IHoseObstacleQuery query, Vector3 lastFixed, Vector3 hands, Vector3 low)
        {
            bool lowBlocked = query.Linecast(lastFixed, low, out _, out _);
            return lowBlocked && !query.Linecast(lastFixed, hands, out _, out _) ? hands : low;
        }
    }
}
