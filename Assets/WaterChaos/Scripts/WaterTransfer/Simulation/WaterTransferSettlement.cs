using System;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Pure accounting of water the pump has already drawn from its source this step
    /// (the pump stays the only one that draws). Each reservoir keeps owning its volume:
    /// the destination takes what fits, the rest goes back to the source, nothing is created or lost.
    /// The delivered liquid mixes into the destination (water into gasoline becomes gasoline).
    /// </summary>
    public static class WaterTransferSettlement
    {
        /// <param name="source">Reservoir the pump drew from; gets back what the destination could not take.</param>
        /// <param name="destination">Reservoir at the hose end; null = the water leaves into the environment.</param>
        /// <param name="drawn">Volume the pump drew this step, in liters (L); negative, NaN and infinity count as 0.</param>
        public static TransferResult Settle(WaterReservoir source, WaterReservoir destination, float drawn)
        {
            if (float.IsNaN(drawn) || float.IsInfinity(drawn) || drawn <= 0f)
                return new TransferResult(0f, 0f, 0f);

            if (destination == null)
                return new TransferResult(0f, 0f, drawn);

            LiquidType liquid = source != null ? source.Liquid : LiquidType.Water;
            float delivered = destination.Add(drawn, liquid);
            float rest = Math.Max(0f, drawn - delivered);
            float returned = source != null && source != destination ? source.Add(rest) : 0f;

            // Only if the source itself is full too: the overflow spills out at the hose end.
            return new TransferResult(delivered, returned, Math.Max(0f, rest - returned));
        }
    }
}
