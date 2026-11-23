using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>
    /// Something in the world that bends the water stream (wind, fans...).
    /// </summary>
    public interface IFlowInfluence
    {
        /// <summary>
        /// Offset this influence adds to the stream end, in meters (m).
        /// </summary>
        /// <param name="range">Current stream length along the aim, in meters (m).</param>
        Vector3 GetEndOffset(float range);
    }
}