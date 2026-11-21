namespace WaterChaos.Water
{
    /// <summary>
    /// Anything that can feed water into a stream (pump, hydrant, ...).
    /// </summary>
    public interface IWaterFlowSource
    {
        /// <summary>Current output: flow rate in L/s, pressure in Pa.</summary>
        WaterFlowData Output { get; }
    }
}