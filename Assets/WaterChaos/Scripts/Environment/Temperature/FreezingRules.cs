namespace WaterChaos.Environment
{
    /// <summary>
    /// When cold is cold enough. Only a real temperature zone can freeze anything:
    /// without a zone (outdoors) the answer is always "not freezing", whatever the threshold.
    /// </summary>
    public static class FreezingRules
    {
        /// <summary>Default freezing point, in degrees Celsius (°C).</summary>
        public const float DefaultFreezingPoint = 0f;

        /// <param name="hasZone">A temperature zone covers the point.</param>
        /// <param name="temperature">Temperature of that zone (°C); ignored without a zone.</param>
        /// <param name="threshold">Below this temperature (°C) it freezes.</param>
        public static bool IsFreezing(bool hasZone, float temperature, float threshold)
        {
            return hasZone && temperature < threshold;
        }

        /// <summary>Freezing at the point according to the provider (false when nothing simulates it).</summary>
        public static bool IsFreezingAt(ITemperatureProvider provider, UnityEngine.Vector3 position, float threshold)
        {
            if (provider == null)
                return false;

            bool hasZone = provider.TryGetTemperature(position, out float temperature);
            return IsFreezing(hasZone, temperature, threshold);
        }
    }
}
