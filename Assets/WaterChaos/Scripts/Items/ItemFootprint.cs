using UnityEngine;

namespace WaterChaos.Items
{
    /// <summary>How much floor an item covers, seen from above. Pure math, no physics queries.</summary>
    public static class ItemFootprint
    {
        /// <summary>
        /// Horizontal distance from <paramref name="pivot"/> to the farthest corner of <paramref name="bounds"/>, in meters (m).
        /// Height is ignored. The circle covers the box at any turn around the vertical axis.
        /// </summary>
        public static float GetRadius(Vector3 pivot, Bounds bounds)
        {
            float x = Mathf.Abs(bounds.center.x - pivot.x) + bounds.extents.x;
            float z = Mathf.Abs(bounds.center.z - pivot.z) + bounds.extents.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
