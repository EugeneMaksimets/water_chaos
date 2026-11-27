using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>
    /// A volume with a simulated temperature (a room or floor). Outside every zone nothing is simulated:
    /// that is not the same as 0 °C, so nothing freezes there.
    /// </summary>
    public interface ITemperatureZone
    {
        /// <summary>Current temperature of the zone, in degrees Celsius (°C).</summary>
        float Temperature { get; }

        /// <summary>True if the point belongs to this zone.</summary>
        bool Contains(Vector3 position);
    }
}
