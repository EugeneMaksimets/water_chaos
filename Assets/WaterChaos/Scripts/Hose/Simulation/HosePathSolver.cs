using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Moves the hose end and keeps the path out of obstacles:
    /// adds wrap points at corners the hose catches on, removes them when the hose swings back off.
    /// Only the end moves, so only the last wrap point can come free.
    /// </summary>
    public sealed class HosePathSolver
    {
        // A fast move can catch several corners in one frame.
        private const int MaxWrapsPerStep = 4;

        // Bisection steps between last and current end: 10 steps = 1/1024 of the frame's move.
        private const int CornerSearchSteps = 10;

        // Shorter segments are noise from repeated hits on one spot, in meters (m).
        private const float MinSegmentLength = 0.02f;

        // Unwrapping needs a tube this share of the surface offset to be clear, while wrapping
        // reacts to a thin line: the gap between the two stops wrap/unwrap flickering.
        private const float UnwrapClearanceShare = 0.5f;

        private readonly IHoseObstacleQuery _query;
        private readonly float _surfaceOffset;
        private readonly int _maxWrapPoints;

        /// <param name="surfaceOffset">Gap between a wrap point and the obstacle surface, in meters (m).</param>
        /// <param name="maxWrapPoints">Wrap points the hose may hold at once.</param>
        public HosePathSolver(IHoseObstacleQuery query, float surfaceOffset, int maxWrapPoints)
        {
            _query = query;
            _surfaceOffset = HoseModel.IsFinite(surfaceOffset) ? Mathf.Max(0.001f, surfaceOffset) : 0.05f;
            _maxWrapPoints = Mathf.Max(0, maxWrapPoints);
        }

        /// <summary>Moves the hose end to newEnd (world space); non-finite positions are ignored.</summary>
        public void Step(HoseModel model, Vector3 newEnd)
        {
            if (model == null || !HoseModel.IsFinite(newEnd))
                return;

            Vector3 previousEnd = model.End;

            while (model.WrapCount > 0 && CanUnwrap(model, newEnd))
                model.RemoveLastWrap();

            for (int i = 0; i < MaxWrapsPerStep && model.WrapCount < _maxWrapPoints; i++)
            {
                if (!TryWrap(model, previousEnd, newEnd))
                    break;
            }

            model.SetEnd(newEnd);
        }

        /// <summary>
        /// The last wrap point is released when the hose bends the other way there
        /// (it swung off the corner) and the shortcut past it is clear.
        /// </summary>
        private bool CanUnwrap(HoseModel model, Vector3 newEnd)
        {
            Vector3 wrap = model.LastFixedPoint;
            Vector3 before = model.GetPoint(model.PointCount - 3);

            Vector3 bend = Vector3.Cross(wrap - before, newEnd - wrap);
            if (Vector3.Dot(bend, model.LastBendNormal) > 0f)
                return false;

            return _query.IsClear(before, newEnd, _surfaceOffset * UnwrapClearanceShare);
        }

        private bool TryWrap(HoseModel model, Vector3 previousEnd, Vector3 newEnd)
        {
            Vector3 from = model.LastFixedPoint;
            if (!_query.Linecast(from, newEnd, out Vector3 hitPoint, out Vector3 hitNormal))
                return false;

            Vector3 wrap = FindCorner(from, previousEnd, newEnd, hitPoint, hitNormal);
            if ((wrap - from).sqrMagnitude < MinSegmentLength * MinSegmentLength)
                return false;

            Vector3 bend = Vector3.Cross(wrap - from, newEnd - wrap);
            if (bend.sqrMagnitude < 1e-10f)
                return false;

            return model.AddWrap(wrap, bend);
        }

        /// <summary>
        /// Between the last clear end and the blocked one, bisects for the sight line that just
        /// grazes the obstacle; its hit point is the corner. The wrap point sits just outside it.
        /// </summary>
        private Vector3 FindCorner(Vector3 from, Vector3 previousEnd, Vector3 newEnd, Vector3 hitPoint, Vector3 hitNormal)
        {
            // No clear reference (first frame, teleport): rest on the surface that was hit.
            if (_query.Linecast(from, previousEnd, out _, out _))
                return hitPoint + hitNormal * _surfaceOffset;

            float clear = 0f;
            float blocked = 1f;
            for (int i = 0; i < CornerSearchSteps; i++)
            {
                float middle = (clear + blocked) * 0.5f;
                if (_query.Linecast(from, Vector3.Lerp(previousEnd, newEnd, middle), out Vector3 point, out Vector3 normal))
                {
                    blocked = middle;
                    hitPoint = point;
                    hitNormal = normal;
                }
                else
                {
                    clear = middle;
                }
            }

            // The clear sight line passes just outside the corner: push away from the surface and toward that line.
            Vector3 clearEnd = Vector3.Lerp(previousEnd, newEnd, clear);
            Vector3 outward = ClosestPointOnSegment(from, clearEnd, hitPoint) - hitPoint;
            Vector3 push = hitNormal;
            if (outward.sqrMagnitude > 1e-10f)
                push += outward.normalized;

            if (push.sqrMagnitude < 1e-10f)
                push = hitNormal;

            return hitPoint + push.normalized * _surfaceOffset;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            Vector3 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 1e-10f)
                return a;

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSquared);
            return a + ab * t;
        }
    }
}
