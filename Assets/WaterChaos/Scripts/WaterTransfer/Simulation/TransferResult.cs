namespace WaterChaos.WaterTransfer
{
    /// <summary>Where one step of pumped water ended up. All volumes in liters (L), never negative.</summary>
    public readonly struct TransferResult
    {
        /// <summary>Added to the destination reservoir.</summary>
        public float Delivered { get; }

        /// <summary>Did not fit into the destination and went back into the source.</summary>
        public float Returned { get; }

        /// <summary>Left the hose into the environment.</summary>
        public float Discharged { get; }

        public TransferResult(float delivered, float returned, float discharged)
        {
            Delivered = delivered;
            Returned = returned;
            Discharged = discharged;
        }
    }
}
