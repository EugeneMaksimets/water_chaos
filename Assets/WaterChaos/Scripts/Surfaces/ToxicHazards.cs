using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>A volume of toxic air (e.g. a cloud from a toxic fire reaction).</summary>
    public interface IToxicHazardArea
    {
        /// <summary>True if the point is inside the toxic air.</summary>
        bool Contains(Vector3 position);
    }

    /// <summary>
    /// Every active toxic air volume, so a character's <see cref="ToxicExposure"/> can ask "am I in toxic air?"
    /// without knowing what made it or searching the scene.
    /// </summary>
    public static class ToxicHazards
    {
        private static readonly List<IToxicHazardArea> s_areas = new List<IToxicHazardArea>();

        public static int Count => s_areas.Count;

        public static void Register(IToxicHazardArea area)
        {
            if (area != null && !s_areas.Contains(area))
                s_areas.Add(area);
        }

        public static void Unregister(IToxicHazardArea area)
        {
            s_areas.Remove(area);
        }

        /// <summary>True if any active area contains the point.</summary>
        public static bool AnyContains(Vector3 position)
        {
            for (int i = 0; i < s_areas.Count; i++)
            {
                if (s_areas[i] != null && s_areas[i].Contains(position))
                    return true;
            }

            return false;
        }

        // Domain reload on entering Play Mode is off in this project: drop areas left from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_areas.Clear();
        }
    }
}
