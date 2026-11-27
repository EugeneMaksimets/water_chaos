using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Surface rules every new fire obeys, spreading or lit by a flamethrower alike:
    /// a point covered by any registered <see cref="IFireIgnitionBlocker"/> cannot catch fire.
    /// Fires that already burn are not affected.
    /// </summary>
    public static class FireIgnitionRules
    {
        private static readonly List<IFireIgnitionBlocker> s_blockers = new List<IFireIgnitionBlocker>();

        public static void Register(IFireIgnitionBlocker blocker)
        {
            if (blocker != null && !s_blockers.Contains(blocker))
                s_blockers.Add(blocker);
        }

        public static void Unregister(IFireIgnitionBlocker blocker)
        {
            s_blockers.Remove(blocker);
        }

        /// <summary>True if any blocker covers the point.</summary>
        public static bool IsBlocked(Vector3 firePosition)
        {
            for (int i = 0; i < s_blockers.Count; i++)
            {
                if (s_blockers[i] != null && s_blockers[i].BlocksIgnitionAt(firePosition))
                    return true;
            }

            return false;
        }

        // Domain reload on entering Play Mode is off in this project: drop blockers left from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_blockers.Clear();
        }
    }
}
