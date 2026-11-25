namespace WaterChaos.Pump
{
    /// <summary>What an <see cref="IntakeStatus"/> asks the player to do.</summary>
    public static class IntakeStatusRules
    {
        /// <summary>
        /// The pump has to be put (deeper) into water: the intake is out of water, not deep enough,
        /// or in water that is too shallow. Tilted and empty sources are other problems.
        /// </summary>
        public static bool NeedsWater(IntakeStatus status)
        {
            return status == IntakeStatus.NotInWater
                || status == IntakeStatus.NotSubmerged
                || status == IntakeStatus.TooShallow;
        }
    }
}
