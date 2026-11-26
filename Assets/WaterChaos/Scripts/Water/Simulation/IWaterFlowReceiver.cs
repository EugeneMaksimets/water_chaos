namespace WaterChaos.Water
{
    /// <summary>
    /// Anything a hose end feeds from a source: a nozzle's stream, a splitter's input.
    /// Whoever connects the hose hands it the source; null disconnects.
    /// </summary>
    public interface IWaterFlowReceiver
    {
        void SetSource(IWaterFlowSource source);
    }
}
