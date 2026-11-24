using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Limits a move so the hose end stays within the free length around the last fixed point.
    /// Only the horizontal part is limited, so gravity and jumps are never blocked.
    /// Never pulls the end back: when it is already too far, the move just cannot add distance.
    /// </summary>
    public static class HoseMovementConstraint
    {
        // A move counts as blocked when the hose took away more than this share of it;
        // sliding along the limit loses far less, so it is not reported.
        private const float BlockedShare = 0.25f;

        /// <param name="pivot">Last fixed point of the hose (last wrap point or anchor), world space.</param>
        /// <param name="freeLength">Hose left for the last segment, in meters (m).</param>
        /// <param name="end">Current hose end, world space.</param>
        /// <param name="delta">Desired move this frame, in meters (m).</param>
        /// <param name="blocked">True when the hose stopped a real part of the move.</param>
        /// <returns>The allowed move, in meters (m).</returns>
        public static Vector3 Constrain(Vector3 pivot, float freeLength, Vector3 end, Vector3 delta, out bool blocked)
        {
            blocked = false;

            if (!HoseModel.IsFinite(delta))
                return Vector3.zero;

            if (!HoseModel.IsFinite(pivot) || !HoseModel.IsFinite(end))
                return delta;

            // Invalid free length: treat as none left, the safe side.
            if (!HoseModel.IsFinite(freeLength))
                freeLength = 0f;

            Vector3 offset = end - pivot;
            float limit = Mathf.Max(Mathf.Max(0f, freeLength), offset.magnitude);

            Vector3 target = offset + delta;
            if (target.sqrMagnitude <= limit * limit)
                return delta;

            // Horizontal room left at the target height; never less than the current horizontal distance.
            Vector2 current = new Vector2(offset.x, offset.z);
            float horizontalLimit = Mathf.Sqrt(Mathf.Max(0f, limit * limit - target.y * target.y));
            horizontalLimit = Mathf.Max(horizontalLimit, current.magnitude);

            Vector2 move = new Vector2(delta.x, delta.z);
            Vector2 next = current + move;
            if (next.sqrMagnitude > horizontalLimit * horizontalLimit)
                next = next.normalized * horizontalLimit;

            Vector2 allowed = next - current;
            blocked = (move - allowed).magnitude > move.magnitude * BlockedShare;
            return new Vector3(allowed.x, delta.y, allowed.y);
        }
    }
}
