namespace WaterChaos.Pump
{
    /// <summary>Result of checking the pump intake against a water source.</summary>
    public enum IntakeStatus
    {
        /// <summary>The intake is not inside any water source (ground, concrete, air).</summary>
        NotInWater,

        /// <summary>The pump lies at too steep an angle to draw water.</summary>
        Tilted,

        /// <summary>The intake is in a source, but not deep enough below the surface.</summary>
        NotSubmerged,

        /// <summary>The water column under the surface is too shallow.</summary>
        TooShallow,

        /// <summary>The source has no water left.</summary>
        SourceEmpty,

        /// <summary>The intake is properly submerged; the pump can draw water.</summary>
        Submerged
    }
}
