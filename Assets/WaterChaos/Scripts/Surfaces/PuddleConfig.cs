using System;

namespace WaterChaos.Surfaces
{
    /// <summary>Tuning of puddles, validated once. Units are in the parameter docs.</summary>
    public readonly struct PuddleConfig
    {
        /// <summary>Puddle area one liter covers, in square meters per liter (m²/L).</summary>
        public float AreaPerLiter { get; }
        /// <summary>Radius of a fresh puddle, in meters (m).</summary>
        public float MinRadius { get; }
        /// <summary>A puddle never grows wider than this, in meters (m).</summary>
        public float MaxRadius { get; }
        /// <summary>Liquid landing this far past a puddle's edge joins it, in meters (m).</summary>
        public float MergeDistance { get; }
        /// <summary>Water freezes in a temperature zone colder than this (°C).</summary>
        public float FreezeBelow { get; }
        /// <summary>Ice melts in a zone warmer than this (°C), or outside every zone.</summary>
        public float MeltAbove { get; }
        /// <summary>Seconds (s) of steady cold before a puddle turns to ice.</summary>
        public float FreezeDelay { get; }
        /// <summary>Seconds (s) of steady warmth before ice turns back to water.</summary>
        public float MeltDelay { get; }
        /// <summary>Grip on ice compared to normal ground (0..1).</summary>
        public float IceFriction { get; }
        /// <summary>Seconds (s) a puddle lasts after it appeared; then it dries up (liquid still coming starts a new one). 0 = forever.</summary>
        public float Lifetime { get; }

        public PuddleConfig(float areaPerLiter, float minRadius, float maxRadius, float mergeDistance,
            float freezeBelow, float meltAbove, float freezeDelay, float meltDelay, float iceFriction, float lifetime = 60f)
        {
            if (!(areaPerLiter > 0f))
                throw new ArgumentException("Area per liter must be positive.", nameof(areaPerLiter));
            if (!(minRadius > 0f) || !(maxRadius >= minRadius))
                throw new ArgumentException("Radii must be positive and max must not be below min.");
            if (float.IsNaN(freezeBelow) || float.IsNaN(meltAbove) || meltAbove < freezeBelow)
                throw new ArgumentException("Melt temperature must not be below the freeze temperature.");

            AreaPerLiter = areaPerLiter;
            MinRadius = minRadius;
            MaxRadius = maxRadius;
            MergeDistance = NonNegative(mergeDistance);
            FreezeBelow = freezeBelow;
            MeltAbove = meltAbove;
            FreezeDelay = NonNegative(freezeDelay);
            MeltDelay = NonNegative(meltDelay);
            IceFriction = float.IsNaN(iceFriction) ? 0f : Math.Min(1f, Math.Max(0f, iceFriction));
            Lifetime = NonNegative(lifetime);
        }

        /// <summary>Most liquid one puddle holds: enough to reach <see cref="MaxRadius"/>, in liters (L).</summary>
        public float MaxVolume => (float)Math.PI * MaxRadius * MaxRadius / AreaPerLiter;

        /// <summary>Default tuning (used by tests and as Inspector defaults).</summary>
        public static PuddleConfig Default => new PuddleConfig(0.1f, 0.4f, 3f, 0.6f, 0f, 0.5f, 1f, 3f, 0.05f);

        private static float NonNegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, value);
    }
}
