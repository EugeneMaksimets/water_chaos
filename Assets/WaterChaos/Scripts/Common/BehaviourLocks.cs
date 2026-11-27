using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Common
{
    /// <summary>
    /// Switches a component (e.g. the player's movement) off for as long as anything needs it off.
    /// Several systems can lock the same component at once (a knockdown, a soap bubble, a pause, a menu): it comes back
    /// on only when the last of them lets go, and then in the state it had before the first lock. Without this, each
    /// system restoring "what it saw" could leave the component off for good.
    /// </summary>
    public static class BehaviourLocks
    {
        private sealed class Entry
        {
            public readonly HashSet<object> Owners = new HashSet<object>();
            public bool WasEnabled;
        }

        private static readonly Dictionary<Behaviour, Entry> s_locks = new Dictionary<Behaviour, Entry>();

        /// <summary>Switches <paramref name="target"/> off on behalf of <paramref name="owner"/>.</summary>
        public static void Lock(Behaviour target, object owner)
        {
            if (target == null || owner == null)
                return;

            if (!s_locks.TryGetValue(target, out Entry entry))
            {
                entry = new Entry { WasEnabled = target.enabled };
                s_locks.Add(target, entry);
            }

            entry.Owners.Add(owner);
            target.enabled = false;
        }

        /// <summary>Lets go of <paramref name="owner"/>'s lock; the last one restores the state before the first lock.</summary>
        public static void Unlock(Behaviour target, object owner)
        {
            if (ReferenceEquals(target, null) || !s_locks.TryGetValue(target, out Entry entry))
                return;

            entry.Owners.Remove(owner);
            if (entry.Owners.Count > 0)
                return;

            s_locks.Remove(target);
            if (target != null)
                target.enabled = entry.WasEnabled;
        }

        public static bool IsLocked(Behaviour target) => target != null && s_locks.ContainsKey(target);

        // Domain reload on entering Play Mode is off in this project: drop locks left from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_locks.Clear();
        }
    }
}
