using System;

namespace WaterChaos.Water
{
    /// <summary>
    /// Pure model of a water tank: finite volume that can be drawn until empty.
    /// </summary>
    public sealed class WaterReservoir
    {
        /// <summary>Maximum volume in liters (L).</summary>
        public float Capacity { get; }

        /// <summary>Current volume in liters (L); never negative.</summary>
        public float Volume { get; private set; }

        public bool IsEmpty => Volume <= 0f;
        
        /// <summary>Fill level from 0 (empty) to 1 (full); 0 when capacity is 0.</summary>
        public float FillRatio => Capacity > 0f ? Volume / Capacity : 0f;

        /// <param name="capacity">Maximum volume in liters (L); negative and NaN become 0.</param>
        /// <param name="initialVolume">Starting volume in liters (L); clamped to 0..capacity.</param>
        public WaterReservoir(float capacity, float initialVolume)
        {
            Capacity = Sanitize(capacity);
            Volume = Math.Min(Sanitize(initialVolume), Capacity);
        }

        /// <summary>
        /// Takes up to the requested volume from the reservoir.
        /// </summary>
        /// <param name="requested">Wanted volume in liters (L); negative and NaN are treated as 0.</param>
        /// <returns>Volume actually drawn in liters (L), never more than was stored.</returns>
        public float Draw(float requested)
        {
            float drawn = Math.Min(Sanitize(requested), Volume);
            Volume -= drawn;
            return drawn;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}