namespace WaterChaos.Pump
{
    /// <summary>
    /// What the pump can do right now. Derived from its conditions, checked in this order:
    /// water at the intake → hose on the outlet → motor on.
    /// </summary>
    public enum PumpState
    {
        /// <summary>No usable water at the intake (not submerged, too shallow, tilted, source empty).</summary>
        NoWater,

        /// <summary>Water is available, but no hose is attached to the outlet.</summary>
        Disconnected,

        /// <summary>Water and hose are fine; the motor is off.</summary>
        Ready,

        /// <summary>Motor is on and the pump delivers water.</summary>
        Running
    }
}
