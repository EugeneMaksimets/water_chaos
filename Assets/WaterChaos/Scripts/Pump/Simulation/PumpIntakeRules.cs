using System;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Pure rules that decide whether the pump intake can draw water from what it measured.
    /// </summary>
    public readonly struct PumpIntakeRules
    {
        private const float MaxPossibleTilt = 180f;

        /// <summary>How deep the intake must be below the surface, in meters (m).</summary>
        public float RequiredSubmersion { get; }

        /// <summary>Smallest water column (surface to bed) at the intake, in meters (m).</summary>
        public float MinWaterDepth { get; }

        /// <summary>Largest angle between the pump's up and world up, in degrees.</summary>
        public float MaxTiltAngle { get; }

        /// <param name="requiredSubmersion">In meters (m); negative and NaN become 0.</param>
        /// <param name="minWaterDepth">In meters (m); negative and NaN become 0.</param>
        /// <param name="maxTiltAngle">In degrees, clamped to 0..180.</param>
        public PumpIntakeRules(float requiredSubmersion, float minWaterDepth, float maxTiltAngle)
        {
            RequiredSubmersion = Sanitize(requiredSubmersion);
            MinWaterDepth = Sanitize(minWaterDepth);
            MaxTiltAngle = Math.Min(Sanitize(maxTiltAngle), MaxPossibleTilt);
        }

        /// <param name="inSource">The intake point is inside a water source volume.</param>
        /// <param name="submersion">Depth of the intake below the surface, in meters (m); negative = above.</param>
        /// <param name="waterDepth">Water column from the surface to the bed at the intake, in meters (m).</param>
        /// <param name="tiltAngle">Angle between the pump's up and world up, in degrees.</param>
        /// <param name="sourceEmpty">The source has no water left.</param>
        public IntakeStatus Evaluate(bool inSource, float submersion, float waterDepth, float tiltAngle, bool sourceEmpty)
        {
            if (!inSource)
                return IntakeStatus.NotInWater;

            // Comparisons are written so that NaN never passes.
            if (!(tiltAngle <= MaxTiltAngle))
                return IntakeStatus.Tilted;

            if (!(submersion >= RequiredSubmersion))
                return IntakeStatus.NotSubmerged;

            if (!(waterDepth >= MinWaterDepth))
                return IntakeStatus.TooShallow;

            return sourceEmpty ? IntakeStatus.SourceEmpty : IntakeStatus.Submerged;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
