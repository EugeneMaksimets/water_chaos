using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Turns a hose path into drawn points that lie on the ground: samples between path points drop to the
    /// ground below (never above the straight line), path points stay where the simulation put them.
    /// Shared by every hose look, so all hoses trail on the floor the same way. Look only, changes no path.
    /// </summary>
    public sealed class HoseGroundDrape
    {
        /// <summary>Most points one drawn hose may have.</summary>
        public const int MaxPoints = 256;

        // Ground rays start this far above the hose line, so they never start inside the ground, in meters (m).
        private const float GroundRayLift = 0.1f;

        private readonly Vector3[] _points = new Vector3[MaxPoints];

        /// <summary>Drawn points from the last <see cref="Build"/>, world space; only the first returned count are valid.</summary>
        public Vector3[] Points => _points;

        /// <param name="path">Hose path from the anchor to the end.</param>
        /// <param name="groundMask">Layers the hose rests on.</param>
        /// <param name="sampleSpacing">Distance between ground samples, in meters (m).</param>
        /// <param name="maxSag">How far below the straight path a sample may drop, in meters (m).</param>
        /// <param name="halfWidth">Half the hose diameter, in meters (m): the hose rests on the ground, not in it.</param>
        /// <returns>Number of valid points in <see cref="Points"/>.</returns>
        public int Build(HoseModel path, LayerMask groundMask, float sampleSpacing, float maxSag, float halfWidth)
        {
            int pathCount = Mathf.Min(path.PointCount, MaxPoints);
            float spacing = Mathf.Max(sampleSpacing, path.Length / Mathf.Max(1, MaxPoints - pathCount));
            int count = 0;

            for (int i = 0; i < pathCount - 1; i++)
            {
                Vector3 from = path.GetPoint(i);
                Vector3 to = path.GetPoint(i + 1);
                _points[count++] = from;

                int budget = MaxPoints - count - (pathCount - 1 - i);
                int steps = Mathf.Min(Mathf.CeilToInt(Vector3.Distance(from, to) / spacing), budget);
                for (int step = 1; step < steps; step++)
                    _points[count++] = DropToGround(Vector3.Lerp(from, to, (float)step / steps), groundMask, maxSag, halfWidth);
            }

            _points[count++] = path.GetPoint(pathCount - 1);
            return count;
        }

        private static Vector3 DropToGround(Vector3 point, LayerMask groundMask, float maxSag, float halfWidth)
        {
            Vector3 origin = point + Vector3.up * GroundRayLift;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxSag + GroundRayLift, groundMask, QueryTriggerInteraction.Ignore))
                point.y = Mathf.Min(point.y, hit.point.y + halfWidth);

            return point;
        }
    }
}
