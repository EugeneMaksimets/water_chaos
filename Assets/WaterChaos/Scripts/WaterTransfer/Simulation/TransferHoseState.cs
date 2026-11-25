namespace WaterChaos.WaterTransfer
{
    /// <summary>Where a transfer hose is in its use cycle.</summary>
    public enum TransferHoseState
    {
        /// <summary>Not connected: a coil in the inventory, in the hands or lying in the world.</summary>
        Stored,

        /// <summary>Connected to a pump; the player carries the free end.</summary>
        Carried,

        /// <summary>Connected to a pump; the free end lies in the world.</summary>
        Placed
    }
}
