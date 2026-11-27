namespace WaterChaos.ToxicReaction
{
    /// <summary>
    /// When a toxic cloud sets off another fire (a small blast and a smaller cloud), and how big that one is.
    /// Chains stop after a set number of steps, so clouds never multiply without end.
    /// </summary>
    public static class ToxicChainRules
    {
        /// <summary>A cloud of <paramref name="generation"/> may set off another fire while below the limit.</summary>
        public static bool CanTriggerChain(int generation, int maxChainGeneration)
        {
            return generation < maxChainGeneration;
        }

        /// <summary>
        /// Size of a cloud of <paramref name="generation"/> relative to the first one: each step of the chain is
        /// <paramref name="chainScale"/> (0..1) of the one before.
        /// </summary>
        public static float SizeScale(int generation, float chainScale)
        {
            float scale = 1f;
            for (int i = 0; i < generation; i++)
                scale *= chainScale;
            return scale;
        }
    }
}
