using System;

namespace WaterChaos.Water
{
    /// <summary>
    /// What flows through a reservoir, pump, hose and stream. A reservoir holds one liquid at a time;
    /// mixing two gives the dominant one (<see cref="LiquidTypeExtensions.MixWith"/>).
    /// Targets react through the liquid's roles (<see cref="LiquidTypeExtensions"/>), never through the type itself,
    /// so a new liquid (foam, oil) needs only a new value and its roles.
    /// </summary>
    public enum LiquidType
    {
        Water = 0,
        Gasoline = 1,
        /// <summary>Corrodes walls and fences; never puts fire out, it reacts with it (toxic smoke).</summary>
        Toxic = 2,
    }

    /// <summary>Gameplay roles of each liquid.</summary>
    public static class LiquidTypeExtensions
    {
        /// <summary>Cools and puts out fire (at any strength, see <see cref="ExtinguishingPower"/>).</summary>
        public static bool Extinguishes(this LiquidType liquid)
        {
            return liquid.ExtinguishingPower() > 0f;
        }

        /// <summary>
        /// How well the liquid cools fire compared to water: 1 = water, 0 = does not cool at all.
        /// A fire hit by one liter cools as if hit by this many liters of water.
        /// </summary>
        public static float ExtinguishingPower(this LiquidType liquid)
        {
            switch (liquid)
            {
                case LiquidType.Water: return 1f;
                default: return 0f;
            }
        }

        /// <summary>Share of water's cooling that snow gives a fire by default (0.2 = 20 %).</summary>
        public const float DefaultSnowExtinguishingPower = 0.2f;

        /// <summary>
        /// Cooling of the liquid compared to water, taking snow into account:
        /// snow cools only <paramref name="snowMultiplier"/> (0..1) of what the same liquid would as liquid.
        /// </summary>
        public static float ExtinguishingPower(this LiquidType liquid, bool isSnow, float snowMultiplier)
        {
            float power = liquid.ExtinguishingPower();
            if (!isSnow || !liquid.CanFreeze())
                return power;

            float multiplier = float.IsNaN(snowMultiplier) ? 0f : Math.Min(1f, Math.Max(0f, snowMultiplier));
            return power * multiplier;
        }

        /// <summary>
        /// Leaves a cold nozzle as snow and freezes into ice in a puddle: only water (soapy or not).
        /// Gasoline and toxic liquid always stay liquid.
        /// </summary>
        public static bool CanFreeze(this LiquidType liquid)
        {
            return liquid == LiquidType.Water;
        }

        /// <summary>
        /// Soap dissolves in it and makes it soapy (<see cref="WaterFlowData.IsSoapy"/>): only water.
        /// Soap is a modifier of water, never a liquid of its own.
        /// </summary>
        public static bool CanBeSoapy(this LiquidType liquid)
        {
            return liquid == LiquidType.Water;
        }

        /// <summary>
        /// Does not cool a fire it hits but reacts with it: the fire turns toxic for a while, gives off a toxic cloud
        /// and splashes toxic puddles around (only corrosive liquids).
        /// </summary>
        public static bool ReactsWithFire(this LiquidType liquid)
        {
            return liquid.Corrodes();
        }

        /// <summary>Burns: feeds fire it touches and can be lit into a stream of flame.</summary>
        public static bool IsFlammable(this LiquidType liquid)
        {
            return liquid == LiquidType.Gasoline;
        }

        /// <summary>
        /// How strongly the liquid eats into corrodible surfaces: 1 = full strength, 0 = harmless.
        /// </summary>
        public static float CorrosionPower(this LiquidType liquid)
        {
            return liquid == LiquidType.Toxic ? 1f : 0f;
        }

        /// <summary>Eats into corrodible surfaces (see <see cref="CorrosionPower"/>).</summary>
        public static bool Corrodes(this LiquidType liquid)
        {
            return liquid.CorrosionPower() > 0f;
        }

        /// <summary>
        /// Soap thrown in dissolves and leaves soapy water: in water, and in toxic liquid, which the soap neutralizes.
        /// Gasoline does not take soap (the bar just lies there).
        /// </summary>
        public static bool AcceptsSoap(this LiquidType liquid)
        {
            return !liquid.IsFlammable();
        }

        /// <summary>
        /// Liquid that results from mixing <paramref name="added"/> with <paramref name="stored"/>; the order does not
        /// matter. The more dominant liquid wins: corrosive over flammable over water
        /// (water with gasoline is gasoline, water with toxic is toxic, gasoline with toxic is toxic).
        /// Soapy water is handled by the tank: it wins over every liquid (see <see cref="WaterReservoir.Add(float, LiquidType, bool)"/>).
        /// </summary>
        public static LiquidType MixWith(this LiquidType stored, LiquidType added)
        {
            return Dominance(added) > Dominance(stored) ? added : stored;
        }

        private static int Dominance(LiquidType liquid)
        {
            if (liquid.Corrodes())
                return 2;

            return liquid.IsFlammable() ? 1 : 0;
        }
    }
}
