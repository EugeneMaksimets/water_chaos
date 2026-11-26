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
        /// <summary>Corrodes walls and fences; a poor extinguisher.</summary>
        Toxic = 2,
    }

    /// <summary>Gameplay roles of each liquid.</summary>
    public static class LiquidTypeExtensions
    {
        /// <summary>Share of water's cooling that toxic liquid gives a fire (0.15 = 15 %).</summary>
        public const float ToxicExtinguishingPower = 0.15f;

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
                case LiquidType.Toxic: return ToxicExtinguishingPower;
                default: return 0f;
            }
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
        /// Liquid that results from adding <paramref name="added"/> to <paramref name="stored"/>.
        /// A flammable and a corrosive liquid neutralize each other: the whole mix turns into water at once
        /// (gasoline with toxic is water). Otherwise the more dominant one wins: a flammable liquid spoils water
        /// (water with gasoline is gasoline), and so does a corrosive one (water with toxic is toxic).
        /// </summary>
        public static LiquidType MixWith(this LiquidType stored, LiquidType added)
        {
            if (Neutralize(stored, added))
                return LiquidType.Water;

            return Dominance(added) > Dominance(stored) ? added : stored;
        }

        private static bool Neutralize(LiquidType a, LiquidType b)
        {
            return (a.IsFlammable() && b.Corrodes()) || (a.Corrodes() && b.IsFlammable());
        }

        private static int Dominance(LiquidType liquid)
        {
            if (liquid.IsFlammable())
                return 2;

            return liquid.Corrodes() ? 1 : 0;
        }
    }
}
