namespace WaterChaos.WaterTransfer
{
    /// <summary>Snapshot of everything the pump menu depends on.</summary>
    public readonly struct PumpMenuContext
    {
        /// <summary>A transfer hose item is in the hands.</summary>
        public bool HoldsHose { get; }

        /// <summary>The player carries the free end of some transfer hose.</summary>
        public bool CarriesEnd { get; }

        /// <summary>The pump transfer outlet has a hose.</summary>
        public bool OutletAttached { get; }

        /// <summary>State of the hose on the outlet; Stored when none.</summary>
        public TransferHoseState HoseState { get; }

        /// <summary>The pump motor is on.</summary>
        public bool PumpOn { get; }

        /// <summary>The pump's regular outlet has the nozzle hose.</summary>
        public bool RegularOutletBusy { get; }

        /// <summary>The pump intake has usable water.</summary>
        public bool HasWater { get; }

        /// <summary>The reservoir at the hose end is full.</summary>
        public bool DestinationFull { get; }

        public PumpMenuContext(bool holdsHose, bool carriesEnd, bool outletAttached, TransferHoseState hoseState,
            bool pumpOn, bool regularOutletBusy, bool hasWater, bool destinationFull)
        {
            HoldsHose = holdsHose;
            CarriesEnd = carriesEnd;
            OutletAttached = outletAttached;
            HoseState = hoseState;
            PumpOn = pumpOn;
            RegularOutletBusy = regularOutletBusy;
            HasWater = hasWater;
            DestinationFull = destinationFull;
        }
    }
}
