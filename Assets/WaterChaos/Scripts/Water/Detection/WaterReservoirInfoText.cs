using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Text and the one style of the reservoir info panel ("РЕЗЕРВУАР" / "750 л / 2000 л") and the HUD tank bar title.
    /// Every reservoir looks the same: yellow text of one size, red once it holds 0 liters. Never names the liquid.
    /// </summary>
    public static class WaterReservoirInfoText
    {
        /// <summary>Font size of every line of the panel, in reference pixels (1920x1080).</summary>
        public const int FontSize = 22;

        public static readonly Color TextColor = new Color(1f, 0.85f, 0.1f, 1f);
        public static readonly Color EmptyColor = new Color(0.95f, 0.25f, 0.2f, 1f);

        /// <summary>Panel text color: red when the reservoir holds 0 liters, yellow otherwise.</summary>
        /// <param name="volume">Current volume in whole liters (L).</param>
        public static Color GetColor(int volume)
        {
            return volume <= 0 ? EmptyColor : TextColor;
        }

        public const string Title = "РЕЗЕРВУАР";
        public const string BarTitle = "Бак";
        public const string EmptyText = "ПОРОЖНІЙ";

        /// <summary>HUD bar title: "Бак", or "Бак (ПОРОЖНІЙ)".</summary>
        public static string BarLabel(bool empty)
        {
            return empty ? BarTitle + " (" + EmptyText + ")" : BarTitle;
        }

        /// <summary>Volume rounded to whole liters (L) for display.</summary>
        public static int ToLiters(float liters)
        {
            return Mathf.RoundToInt(liters);
        }

        /// <param name="volume">Current volume in whole liters (L).</param>
        /// <param name="capacity">Maximum volume in whole liters (L).</param>
        public static string FormatVolume(int volume, int capacity)
        {
            return volume + " л / " + capacity + " л";
        }
    }
}
