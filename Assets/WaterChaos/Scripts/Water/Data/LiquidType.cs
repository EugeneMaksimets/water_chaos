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
    }

    /// <summary>Gameplay roles of each liquid.</summary>
    public static class LiquidTypeExtensions
    {
        /// <summary>Cools and puts out fire.</summary>
        public static bool Extinguishes(this LiquidType liquid)
        {
            return liquid == LiquidType.Water;
        }

        /// <summary>Burns: feeds fire it touches and can be lit into a stream of flame.</summary>
        public static bool IsFlammable(this LiquidType liquid)
        {
            return liquid == LiquidType.Gasoline;
        }

        /// <summary>
        /// Liquid that results from adding <paramref name="added"/> to <paramref name="stored"/>:
        /// a flammable liquid spoils the whole mix (water into gasoline stays gasoline, gasoline into water makes gasoline).
        /// </summary>
        public static LiquidType MixWith(this LiquidType stored, LiquidType added)
        {
            return added.IsFlammable() ? added : stored;
        }
    }
}
