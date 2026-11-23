using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Pure geometry of the water stream: a simple parabola from the start nozzle.
    /// </summary>
    public static class WaterArc
    {
        /// <summary>
        /// Fills <paramref name="points"/> evenly along the arc, first point at origin, last at the stream end.
        /// Does not allocate; the caller owns and reuses the array.
        /// </summary>
        /// <param name="origin">Nozzle position in world space.</param>
        /// <param name="direction">Aim direction; normalized internally.</param>
        /// <param name="range">Distance along the aim direction, in meters (m).</param>
        /// <param name="drop">How far the stream end sinks below the aim line, in meters (m).</param>
        /// <param name="points">Output buffer; needs at least 2 elements.</param>
        public static void BuildPoints(
            Vector3 origin, 
            Vector3 direction, 
            float range, 
            float drop, 
            Vector3[] points)
        {
            BuildPoints(origin, direction, range, Vector3.down * drop, points);
        }

        /// <summary>
        /// Same arc, but the stream end is offset by an arbitrary vector (gravity + wind etc.).
        /// </summary>
        /// <param name="bend">Offset of the stream end from the aim line, in meters (m). Grows as t² along the arc.</param>
        public static void BuildPoints(
            Vector3 origin, 
            Vector3 direction, 
            float range, 
            Vector3 bend,
            Vector3[] points)
        {
            BuildPoints(origin, direction, range, bend, points, 1f);
        }

        /// <param name="tEnd">Where the arc stops: 1 = at range, above 1 = the same parabola keeps falling past range.</param>
        public static void BuildPoints(
            Vector3 origin,
            Vector3 direction,
            float range,
            Vector3 bend,
            Vector3[] points,
            float tEnd)
        {
            if (points == null || points.Length < 2)
                return;

            // This ensures the direction has a length of 1, and range controls the actual distance.
            Vector3 aim = direction.normalized;
            int lastIndex = points.Length - 1;

            // Distribute points evenly from 0 to 1 along the arc.
            // Each point moves forward linearly and is offset by bend quadratically
            // creating a parabolic trajectory.
            for (int i = 0; i <= lastIndex; i++)
            {
                float t = tEnd * i / lastIndex;
                points[i] = origin + aim * (range * t) + bend * (t * t);
            }
        }
    }
}