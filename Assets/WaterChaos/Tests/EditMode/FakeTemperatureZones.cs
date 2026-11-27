using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Environment;

namespace WaterChaos.Tests
{
    /// <summary>Box-shaped temperature zones for tests; everything outside them is "outdoors" (no temperature).</summary>
    public sealed class FakeTemperatureZones : ITemperatureProvider
    {
        private readonly List<FakeZone> _zones = new List<FakeZone>();

        public FakeZone Add(Vector3 center, Vector3 size, float temperature)
        {
            var zone = new FakeZone(new Bounds(center, size), temperature);
            _zones.Add(zone);
            return zone;
        }

        public bool TryGetTemperature(Vector3 position, out float temperature)
        {
            foreach (FakeZone zone in _zones)
            {
                if (zone.Contains(position))
                {
                    temperature = zone.Temperature;
                    return true;
                }
            }

            temperature = 0f;
            return false;
        }
    }

    public sealed class FakeZone : ITemperatureZone
    {
        private readonly Bounds _bounds;

        public FakeZone(Bounds bounds, float temperature)
        {
            _bounds = bounds;
            Temperature = temperature;
        }

        public float Temperature { get; set; }

        public bool Contains(Vector3 position) => _bounds.Contains(position);
    }
}
