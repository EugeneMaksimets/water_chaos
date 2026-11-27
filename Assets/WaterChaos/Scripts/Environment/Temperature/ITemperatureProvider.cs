using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>Answers what temperature a point has, if any zone simulates it.</summary>
    public interface ITemperatureProvider
    {
        /// <summary>
        /// False when no temperature zone covers the point (outdoors): there is no temperature to freeze at.
        /// </summary>
        /// <param name="temperature">Temperature of the covering zone, in degrees Celsius (°C); 0 when false.</param>
        bool TryGetTemperature(Vector3 position, out float temperature);
    }
}
