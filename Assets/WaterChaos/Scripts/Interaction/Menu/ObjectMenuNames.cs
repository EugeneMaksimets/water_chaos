using System;
using System.Collections.Generic;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Ukrainian names of the game's objects for the interaction menu, by scene name ("Axe" → "Сокира").
    /// Used when an object has no <see cref="IDisplayName"/> of its own; unknown names are shown as they are.
    /// Add a line here for every new interactive object.
    /// </summary>
    public static class ObjectMenuNames
    {
        // Mission objects are named "Pump · Underground Tank": only the part before it is the object.
        private const string Suffix = " · ";

        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Axe", "Сокира" },
            { "Nozzle", "Брандспойт" },
            { "Pump", "Насос" },
            { "AirPump", "Насос" },
            { "Soap", "Мило" },
            { "HoseSplitter", "Трійник" },
            { "Fridge", "Холодильник" },
            { "AirConditioner", "Кондиціонер" },
            { "FireTruck", "Пожежна машина" },
            { "FireHydrant_Red", "Гідрант" },
            { "Hydrant", "Гідрант" },
            { "Stairwell Grate", "Решітка" },
            { "Firefighter", "Напарник" },
            { "Teammate_Firefighter", "Напарник" },
            { "Ladder", "Драбина" },
        };

        // Families of objects with many scene names ("Door_Red", "DoorFront").
        private static readonly KeyValuePair<string, string>[] Prefixes =
        {
            new KeyValuePair<string, string>("Door", "Двері"),
            new KeyValuePair<string, string>("Window", "Вікно"),
        };

        /// <summary>Ukrainian name for a scene name without the duplicate suffix; the name itself if unknown.</summary>
        public static string Translate(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return string.Empty;

            int suffix = sceneName.IndexOf(Suffix, StringComparison.Ordinal);
            string key = suffix > 0 ? sceneName.Substring(0, suffix) : sceneName;

            if (Names.TryGetValue(key, out string name))
                return name;

            foreach (KeyValuePair<string, string> prefix in Prefixes)
            {
                if (key.StartsWith(prefix.Key, StringComparison.OrdinalIgnoreCase))
                    return prefix.Value;
            }

            return sceneName;
        }

        /// <summary>True if <see cref="Translate"/> knows the name.</summary>
        public static bool IsKnown(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && Translate(sceneName) != sceneName;
        }
    }
}
