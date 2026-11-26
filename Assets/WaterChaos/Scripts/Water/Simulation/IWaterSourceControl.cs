namespace WaterChaos.Water
{
    /// <summary>
    /// Player-facing controls of a water source the hose is attached to.
    /// </summary>
    public interface IWaterSourceControl
    {
        /// <summary>Turns the source on or off (pump motor, hydrant valve).</summary>
        void Toggle();

        /// <summary>Steps output pressure up (+1) or down (-1); sources with fixed pressure ignore it.</summary>
        void AdjustPressure(int direction);

        /// <summary>Turns the source off, e.g. when the hose is detached.</summary>
        void Stop();

        /// <summary>Switched on with a target pressure above zero: the player wants it to deliver.</summary>
        bool IsActive { get; }
    }
}