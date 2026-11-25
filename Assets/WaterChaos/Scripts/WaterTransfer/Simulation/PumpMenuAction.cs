namespace WaterChaos.WaterTransfer
{
    /// <summary>Action the player can pick in the pump menu.</summary>
    public enum PumpMenuAction
    {
        /// <summary>Start the pump; water starts moving through the transfer hose.</summary>
        TurnOn,

        /// <summary>Stop the pump.</summary>
        TurnOff,

        /// <summary>Attach the transfer hose in the hands to the pump transfer outlet.</summary>
        Connect,

        /// <summary>Take the transfer hose off the pump, back into the inventory.</summary>
        PickUp
    }
}
