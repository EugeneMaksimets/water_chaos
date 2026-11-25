namespace WaterChaos.Player
{
    /// <summary>What holding interact on a placed transfer hose end would do, or why it cannot.</summary>
    public enum TransferHoseAction
    {
        /// <summary>Nothing to do on this target.</summary>
        None,

        /// <summary>The player takes the placed free end again.</summary>
        PullHose,

        /// <summary>Blocked: another hose end is already in the hands.</summary>
        PlaceCarriedFirst
    }
}
