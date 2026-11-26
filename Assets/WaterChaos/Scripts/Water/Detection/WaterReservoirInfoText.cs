using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Text for the reservoir info panel and the HUD tank bar, e.g. "750 L / 2000 L".
    /// Never names the liquid: the player tells it by color.
    /// </summary>
    public static class WaterReservoirInfoText
    {
        public const string Title = "RESERVOIR";
        public const string BarTitle = "Tank";
        public const string EmptyText = "EMPTY";

        /// <summary>The volume, with "EMPTY" in front when nothing is left.</summary>
        /// <param name="volume">Current volume in whole liters (L).</param>
        /// <param name="capacity">Maximum volume in whole liters (L).</param>
        public static string FormatStatus(int volume, int capacity)
        {
            string text = FormatVolume(volume, capacity);
            return volume <= 0 ? EmptyText + " · " + text : text;
        }

        /// <summary>HUD bar title: "Tank", or "Tank (EMPTY)".</summary>
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
            return volume + " L / " + capacity + " L";
        }
    }
}
