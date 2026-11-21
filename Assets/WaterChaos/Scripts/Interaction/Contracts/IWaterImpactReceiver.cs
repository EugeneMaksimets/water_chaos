using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Implemented by components that react to being hit by a water stream.
    /// Called from FixedUpdate once per physics step while the stream hits the object.
    /// </summary>
    public interface IWaterImpactReceiver
    {
        /// <summary>
        /// Receives the water delivered to this object during the current physics step.
        /// </summary>
        void ReceiveImpact(in WaterImpact impact);
    }
}
