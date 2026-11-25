namespace WaterChaos.WaterTransfer
{
    /// <summary>Pure rules: when the transfer hose may take the pump output, and what to tell the player.</summary>
    public static class TransferRules
    {
        /// <summary>
        /// Pump target pressure that gives the wanted transfer flow, for a pump whose flow grows linearly
        /// with pressure up to <paramref name="maxFlowRate"/> at <paramref name="maxPressure"/>.
        /// Capped at max pressure; 0 for invalid input.
        /// </summary>
        /// <param name="flowRate">Wanted flow, in liters per second (L/s).</param>
        /// <param name="maxFlowRate">Pump flow at max pressure, in liters per second (L/s).</param>
        /// <param name="maxPressure">Pump max pressure, in pascals (Pa).</param>
        /// <returns>Target pressure, in pascals (Pa).</returns>
        public static float TargetPressureFor(float flowRate, float maxFlowRate, float maxPressure)
        {
            if (!(flowRate > 0f) || !(maxFlowRate > 0f) || !(maxPressure > 0f) || float.IsInfinity(maxPressure))
                return 0f;

            return maxPressure * System.Math.Min(1f, flowRate / maxFlowRate);
        }

        /// <summary>
        /// The hose takes the pump output only when its end is placed, the pump's regular outlet is free
        /// (otherwise the output belongs to the nozzle hose) and the destination, if any, has room.
        /// </summary>
        public static bool CanDeliver(TransferHoseState state, bool outletBusy, bool destinationFull)
        {
            return state == TransferHoseState.Placed && !outletBusy && !destinationFull;
        }

        /// <summary>The most basic missing condition wins: hose → outlet → source → destination → motor → flow.</summary>
        /// <param name="outletBusy">The pump's regular outlet has the nozzle hose attached.</param>
        /// <param name="sourceEmpty">The pump intake sits in a source with no water left.</param>
        /// <param name="hasWater">The pump intake has usable water.</param>
        /// <param name="hasDestination">The hose end lies in a reservoir.</param>
        /// <param name="destinationFull">That reservoir cannot take more water.</param>
        /// <param name="pumpOn">The pump motor is switched on.</param>
        /// <param name="flowRate">Water moving through the hose, in liters per second (L/s).</param>
        public static TransferStatus Evaluate(
            TransferHoseState state,
            bool outletBusy,
            bool sourceEmpty,
            bool hasWater,
            bool hasDestination,
            bool destinationFull,
            bool pumpOn,
            float flowRate)
        {
            if (state == TransferHoseState.Stored)
                return TransferStatus.NotConnected;

            if (state == TransferHoseState.Carried)
                return TransferStatus.Deploying;

            if (outletBusy)
                return TransferStatus.OutletBusy;

            if (sourceEmpty)
                return TransferStatus.SourceEmpty;

            if (!hasWater)
                return TransferStatus.NoWater;

            if (hasDestination && destinationFull)
                return TransferStatus.DestinationFull;

            if (!pumpOn)
                return TransferStatus.Ready;

            // Written so that NaN never counts as flow.
            if (!(flowRate > 0f))
                return TransferStatus.Interrupted;

            return hasDestination ? TransferStatus.TransferringToReservoir : TransferStatus.DischargingToEnvironment;
        }
    }
}
