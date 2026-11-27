using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>
    /// Finds the temperature zone a point belongs to among a set of zones.
    /// A point covered by no zone has no temperature at all (never "0 °C").
    /// </summary>
    public sealed class SurfaceTemperatureResolver : ITemperatureProvider
    {
        private readonly List<ITemperatureZone> _zones = new List<ITemperatureZone>();

        public int ZoneCount => _zones.Count;

        public void Add(ITemperatureZone zone)
        {
            if (zone != null && !_zones.Contains(zone))
                _zones.Add(zone);
        }

        public void Remove(ITemperatureZone zone)
        {
            _zones.Remove(zone);
        }

        public void Clear()
        {
            _zones.Clear();
        }

        /// <inheritdoc/>
        public bool TryGetTemperature(Vector3 position, out float temperature)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                ITemperatureZone zone = _zones[i];
                if (zone != null && zone.Contains(position))
                {
                    temperature = zone.Temperature;
                    return true;
                }
            }

            temperature = 0f;
            return false;
        }
    }
}
