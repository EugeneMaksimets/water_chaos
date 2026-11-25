using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>Text for the reservoir info panel, e.g. "750 L / 2000 L".</summary>
    public static class WaterReservoirInfoText
    {
        public const string Title = "WATER RESERVOIR";

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
