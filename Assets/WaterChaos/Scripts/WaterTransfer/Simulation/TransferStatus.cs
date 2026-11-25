namespace WaterChaos.WaterTransfer
{
    /// <summary>What the transfer chain is doing, for player feedback. Derived, never stored.</summary>
    public enum TransferStatus
    {
        /// <summary>The transfer hose is not attached to a pump.</summary>
        NotConnected,

        /// <summary>Attached; the player is laying the hose out.</summary>
        Deploying,

        /// <summary>The pump's regular outlet has the nozzle hose; the pump output goes there.</summary>
        OutletBusy,

        /// <summary>The pump intake sits in a source that has run dry.</summary>
        SourceEmpty,

        /// <summary>No usable water at the pump intake.</summary>
        NoWater,

        /// <summary>The reservoir at the hose end cannot take more water.</summary>
        DestinationFull,

        /// <summary>Everything is in place; the pump is off.</summary>
        Ready,

        /// <summary>The pump is on, but no water moves (pressure still building or supply lost).</summary>
        Interrupted,

        /// <summary>Water moves into the reservoir at the hose end.</summary>
        TransferringToReservoir,

        /// <summary>Water leaves the hose end into the environment.</summary>
        DischargingToEnvironment
    }
}
