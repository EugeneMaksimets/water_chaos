using System;

namespace WaterChaos.Surfaces
{
    /// <summary>Tuning of snowdrifts, validated once. Units are in the parameter docs.</summary>
    public readonly struct SnowdriftConfig
    {
        /// <summary>Snow added per liter of water arriving as snow (1 = one liter of snow per liter).</summary>
        public float AccumulationPerLiter { get; }
        /// <summary>Snow (L) that makes a full-size drift.</summary>
        public float FullVolume { get; }
        /// <summary>Radius of a fresh drift, in meters (m).</summary>
        public float MinRadius { get; }
        /// <summary>Radius of a full drift, in meters (m).</summary>
        public float MaxRadius { get; }
        /// <summary>Height of a full drift, in meters (m).</summary>
        public float MaxHeight { get; }
        /// <summary>Snow landing this far past a drift's edge joins it, in meters (m).</summary>
        public float MergeDistance { get; }
        /// <summary>Snow melts in a zone warmer than this (°C), or outside every zone.</summary>
        public float MeltAbove { get; }
        /// <summary>Snow lost while melting, in liters per second (L/s).</summary>
        public float MeltRate { get; }
        /// <summary>Share of a full drift (0..1) from which it covers the surface and stops new fires.</summary>
        public float BlockingFill { get; }

        public SnowdriftConfig(float accumulationPerLiter, float fullVolume, float minRadius, float maxRadius,
            float maxHeight, float mergeDistance, float meltAbove, float meltRate, float blockingFill)
        {
            if (!(fullVolume > 0f))
                throw new ArgumentException("Full volume must be positive.", nameof(fullVolume));
            if (!(minRadius > 0f) || !(maxRadius >= minRadius))
                throw new ArgumentException("Radii must be positive and max must not be below min.");
            if (float.IsNaN(meltAbove))
                throw new ArgumentException("Melt temperature must be a number.", nameof(meltAbove));

            AccumulationPerLiter = NonNegative(accumulationPerLiter);
            FullVolume = fullVolume;
            MinRadius = minRadius;
            MaxRadius = maxRadius;
            MaxHeight = NonNegative(maxHeight);
            MergeDistance = NonNegative(mergeDistance);
            MeltAbove = meltAbove;
            MeltRate = NonNegative(meltRate);
            BlockingFill = float.IsNaN(blockingFill) ? 0f : Math.Min(1f, Math.Max(0f, blockingFill));
        }

        public static SnowdriftConfig Default => new SnowdriftConfig(1f, 20f, 0.2f, 0.7f, 0.35f, 0.3f, 0f, 0.5f, 0.05f);

        private static float NonNegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, value);
    }
}
