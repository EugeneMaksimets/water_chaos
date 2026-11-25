using UnityEngine;

namespace WaterChaos.WaterTransfer
{
    /// <summary>Pure rules for laying the free hose end down without stretching the hose.</summary>
    public static class TransferHosePlacement
    {
        /// <summary>
        /// Resolves where the free end goes. Rejected (false) when there is no ground under it
        /// or any input is invalid; otherwise the ground point, moved toward the pivot if the hose is too short.
        /// Positions in world space, reach in meters (m).
        /// </summary>
        /// <param name="pivot">Last fixed hose point (last wrap point or the pump connector).</param>
        /// <param name="reach">Hose left for the last segment, in meters (m).</param>
        /// <param name="hasGround">Something solid was found under the end.</param>
        /// <param name="groundPoint">Where the end would rest on it.</param>
        public static bool TryResolve(Vector3 pivot, float reach, bool hasGround, Vector3 groundPoint, out Vector3 point)
        {
            point = groundPoint;
            if (!hasGround || !IsFinite(pivot) || !IsFinite(groundPoint) || !IsFinite(reach) || reach <= 0f)
                return false;

            Vector3 offset = groundPoint - pivot;
            if (offset.sqrMagnitude > reach * reach)
                point = pivot + offset.normalized * reach;

            return true;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
}
