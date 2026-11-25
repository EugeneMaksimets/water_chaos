using System.Collections.Generic;

namespace WaterChaos.WaterTransfer
{
    /// <summary>Pure rules for which pump menu actions are valid right now; invalid ones are never offered.</summary>
    public static class PumpMenuRules
    {
        /// <summary>Clears <paramref name="actions"/> and fills it with the valid actions, in menu order.</summary>
        public static void Collect(in PumpMenuContext context, List<PumpMenuAction> actions)
        {
            actions.Clear();

            if (CanTurnOn(context))
                actions.Add(PumpMenuAction.TurnOn);

            if (CanTurnOff(context))
                actions.Add(PumpMenuAction.TurnOff);

            if (CanConnect(context))
                actions.Add(PumpMenuAction.Connect);

            if (CanPickUp(context))
                actions.Add(PumpMenuAction.PickUp);
        }

        /// <summary>Hose laid out, water at the intake, room at the destination, pump free and off.</summary>
        public static bool CanTurnOn(in PumpMenuContext context)
        {
            return context.OutletAttached
                && context.HoseState == TransferHoseState.Placed
                && !context.PumpOn
                && !context.RegularOutletBusy
                && context.HasWater
                && !context.DestinationFull;
        }

        /// <summary>Only a pump the transfer hose runs; a nozzle-driven pump is controlled by the nozzle.</summary>
        public static bool CanTurnOff(in PumpMenuContext context)
        {
            return context.OutletAttached && context.PumpOn && !context.RegularOutletBusy;
        }

        /// <summary>A hose in the hands, a free outlet, and no other hose end already in the hands.</summary>
        public static bool CanConnect(in PumpMenuContext context)
        {
            return context.HoldsHose && !context.OutletAttached && !context.CarriesEnd;
        }

        /// <summary>The hose is not taken off while water moves through it.</summary>
        public static bool CanPickUp(in PumpMenuContext context)
        {
            return context.OutletAttached && !context.PumpOn;
        }
    }
}
