using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>
    /// Every enabled temperature zone in the scene (rooms register themselves), so streams and puddles
    /// can ask the temperature at a point without searching the scene. The room temperature stays owned by the rooms.
    /// </summary>
    public static class TemperatureZones
    {
        private static readonly SurfaceTemperatureResolver s_resolver = new SurfaceTemperatureResolver();

        /// <summary>Temperature lookup over all registered zones.</summary>
        public static ITemperatureProvider Provider => s_resolver;

        public static void Register(ITemperatureZone zone)
        {
            s_resolver.Add(zone);
        }

        public static void Unregister(ITemperatureZone zone)
        {
            s_resolver.Remove(zone);
        }

        /// <inheritdoc cref="ITemperatureProvider.TryGetTemperature"/>
        public static bool TryGetTemperature(Vector3 position, out float temperature)
        {
            return s_resolver.TryGetTemperature(position, out temperature);
        }

        // Domain reload on entering Play Mode is off in this project: drop zones left from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_resolver.Clear();
        }
    }
}
