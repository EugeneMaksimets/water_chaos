using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Turns a hose path into drawn points that look like a real hose lying on the ground:
    /// 1. wrap points drop to the ground below them (the hose goes round a pole at its foot, not at hand height);
    /// 2. corners are rounded, with the curve's apex kept on the wrap point, so it never cuts into the obstacle;
    /// 3. points are spaced evenly along the curve;
    /// 4. the hose settles under its own weight: it rests on the ground and bends down smoothly
    ///    from the attachments instead of dropping straight down.
    /// Only the two ends stay exactly where they are given. Shared by every hose look. Look only, changes no path.
    /// </summary>
    public sealed class HoseGroundDrape
    {
        /// <summary>Most points one drawn hose may have.</summary>
        public const int MaxPoints = 256;

        private const int CornerSegments = 8;
        private const int MaxCurvePoints = MaxPoints * 4;

        // A corner may use at most this share of each neighbouring segment, so neighbouring corners never overlap.
        private const float CornerShare = 0.45f;
        private const float MinCornerRadius = 0.01f;

        // Ground rays start this far above the hose line, so the hose can climb a curb, in meters (m).
        private const float GroundRayLift = 0.3f;

        // Rays that lower wrap points start just above them, in meters (m).
        private const float WrapRayLift = 0.05f;

        // Settling passes (each one forward and back). Enough for a few meters of hanging hose.
        private const int SettleIterations = 64;

        private readonly Vector3[] _control = new Vector3[MaxPoints];
        private readonly Vector3[] _curve = new Vector3[MaxCurvePoints];
        private readonly Vector3[] _points = new Vector3[MaxPoints];
        private readonly float[] _floor = new float[MaxPoints];

        /// <summary>Drawn points from the last <see cref="Build"/>, world space; only the first returned count are valid.</summary>
        public Vector3[] Points => _points;

        /// <param name="path">Hose path from the anchor to the end; its wrap points are used, its ends are replaced.</param>
        /// <param name="start">Where the drawn hose starts (the connection point), world space.</param>
        /// <param name="end">Where the drawn hose ends (e.g. the nozzle), world space.</param>
        /// <param name="groundMask">Layers the hose rests on.</param>
        /// <param name="pointSpacing">Distance between drawn points, in meters (m).</param>
        /// <param name="maxSag">How far below its path the hose may hang or drop to reach the ground, in meters (m).</param>
        /// <param name="halfWidth">Half the hose diameter, in meters (m): the hose rests on the ground, not in it.</param>
        /// <param name="cornerRadius">How far from a wrap point the rounding of its corner starts, in meters (m).</param>
        /// <param name="bendCurvature">How sharply a hanging hose bends down, in 1/m. Lower = stiffer hose, wider arcs.</param>
        /// <returns>Number of valid points in <see cref="Points"/>.</returns>
        public int Build(HoseModel path, Vector3 start, Vector3 end, LayerMask groundMask, float pointSpacing, float maxSag,
            float halfWidth, float cornerRadius, float bendCurvature)
        {
            int controlCount = CollectControlPoints(path, start, end, groundMask, maxSag, halfWidth);
            int curveCount = RoundCorners(controlCount, cornerRadius);
            int count = Resample(curveCount, Mathf.Max(pointSpacing, 0.01f), out float spacing);
            Settle(count, groundMask, maxSag, halfWidth, Mathf.Max(0f, bendCurvature) * spacing * spacing * 0.5f);
            return count;
        }

        /// <summary>Start, the path's wrap points lowered to the ground below them, end.</summary>
        private int CollectControlPoints(HoseModel path, Vector3 start, Vector3 end, LayerMask groundMask, float maxSag, float halfWidth)
        {
            // Too many wrap points: the extra ones are dropped.
            int count = Mathf.Min(path.PointCount, MaxPoints);
            _control[0] = start;
            for (int i = 1; i < count - 1; i++)
                _control[i] = LowerToGround(path.GetPoint(i), groundMask, maxSag, halfWidth);
            _control[count - 1] = end;

            return count;
        }

        private static Vector3 LowerToGround(Vector3 point, LayerMask groundMask, float maxSag, float halfWidth)
        {
            Vector3 origin = point + Vector3.up * WrapRayLift;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxSag + WrapRayLift, groundMask, QueryTriggerInteraction.Ignore))
                point.y = Mathf.Min(point.y, hit.point.y + halfWidth);

            return point;
        }

        /// <summary>
        /// Replaces each corner with a curve. The curve would cut inside the corner, toward the obstacle,
        /// so the corner is first pushed outward by that amount: the curve's apex stays on the wrap point.
        /// </summary>
        private int RoundCorners(int controlCount, float cornerRadius)
        {
            int count = 0;
            _curve[count++] = _control[0];

            for (int i = 1; i < controlCount - 1; i++)
            {
                Vector3 previous = _control[i - 1];
                Vector3 point = _control[i];
                Vector3 next = _control[i + 1];

                float toPrevious = Vector3.Distance(previous, point);
                float toNext = Vector3.Distance(point, next);
                float radius = Mathf.Min(cornerRadius, toPrevious * CornerShare, toNext * CornerShare);

                if (radius < MinCornerRadius || count + CornerSegments + 2 >= MaxCurvePoints)
                {
                    _curve[count++] = point;
                    continue;
                }

                // Points into the bend; the curve's apex lies radius / 4 along it from the corner.
                Vector3 inward = (previous - point) / toPrevious + (next - point) / toNext;
                Vector3 corner = point - inward * (radius * 0.25f);

                Vector3 start = corner + (previous - corner).normalized * radius;
                Vector3 end = corner + (next - corner).normalized * radius;
                for (int step = 0; step <= CornerSegments; step++)
                    _curve[count++] = Bezier(start, corner, end, (float)step / CornerSegments);
            }

            _curve[count++] = _control[controlCount - 1];
            return count;
        }

        /// <summary>
        /// Evenly spaced points along the curve, from its first to its last point. The spacing is adjusted
        /// a little so the last step is as long as the others (settling assumes equal steps).
        /// </summary>
        private int Resample(int curveCount, float pointSpacing, out float spacing)
        {
            float length = 0f;
            for (int i = 1; i < curveCount; i++)
                length += Vector3.Distance(_curve[i - 1], _curve[i]);

            // A very long hose gets wider spacing instead of running out of points.
            int steps = Mathf.Clamp(Mathf.CeilToInt(length / pointSpacing), 1, MaxPoints - 1);
            spacing = length > 0f ? length / steps : pointSpacing;

            int count = 0;
            _points[count++] = _curve[0];

            float travelled = 0f;
            float nextAt = spacing;
            for (int i = 1; i < curveCount; i++)
            {
                Vector3 from = _curve[i - 1];
                Vector3 to = _curve[i];
                float segment = Vector3.Distance(from, to);

                // The small allowance stops float rounding from skipping the last inner point.
                while (segment > 0f && travelled + segment >= nextAt - spacing * 1e-3f && count < steps)
                {
                    _points[count++] = Vector3.Lerp(from, to, (nextAt - travelled) / segment);
                    nextAt += spacing;
                }

                travelled += segment;
            }

            _points[count++] = _curve[curveCount - 1];
            return count;
        }

        /// <summary>
        /// Lets the hose hang: each inner point moves to the middle of its neighbours, pulled down a little by weight,
        /// but never below the ground (or more than maxSag below its path where there is no ground).
        /// The result is a smooth curve that rests on the ground and bends down from the attachments.
        /// </summary>
        private void Settle(int count, LayerMask groundMask, float maxSag, float halfWidth, float weight)
        {
            if (count < 3)
                return;

            for (int i = 1; i < count - 1; i++)
            {
                Vector3 point = _points[i];
                Vector3 origin = point + Vector3.up * GroundRayLift;
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, maxSag + GroundRayLift, groundMask, QueryTriggerInteraction.Ignore))
                {
                    _floor[i] = hit.point.y + halfWidth;
                    _points[i].y = _floor[i];
                }
                else
                {
                    _floor[i] = point.y - maxSag;
                }
            }

            for (int iteration = 0; iteration < SettleIterations; iteration++)
            {
                for (int i = 1; i < count - 1; i++)
                    SettlePoint(i, weight);
                for (int i = count - 2; i > 0; i--)
                    SettlePoint(i, weight);
            }
        }

        private void SettlePoint(int i, float weight)
        {
            float hanging = (_points[i - 1].y + _points[i + 1].y) * 0.5f - weight;
            _points[i].y = Mathf.Max(hanging, _floor[i]);
        }

        private static Vector3 Bezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float u = 1f - t;
            return start * (u * u) + control * (2f * u * t) + end * (t * t);
        }
    }
}
