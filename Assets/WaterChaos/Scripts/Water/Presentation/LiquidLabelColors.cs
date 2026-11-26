using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// The one text color per liquid for every UI label that shows what a reservoir or source holds:
    /// water is white, gasoline is yellow.
    /// </summary>
    public static class LiquidLabelColors
    {
        public static readonly Color Water = Color.white;
        public static readonly Color Gasoline = new Color(1f, 0.85f, 0.1f, 1f);

        public static Color Get(LiquidType liquid)
        {
            switch (liquid)
            {
                case LiquidType.Gasoline: return Gasoline;
                default: return Water;
            }
        }
    }
}
