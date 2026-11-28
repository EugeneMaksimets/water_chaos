using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Teammate
{
    /// <summary>
    /// Which fire a teammate aims at: the closest one in range (<see cref="Pick"/>) or a random one in range
    /// (<see cref="PickRandom"/>). Range is a distance where a difference in height counts more than the same distance
    /// sideways, so a teammate lifted to the roof reaches the roof fires, and one on the ground the fires around them.
    /// </summary>
    public static class FireTargeting
    {
        /// <summary>Weighted distance from <paramref name="from"/> to <paramref name="fire"/>, in meters (m).</summary>
        public static float Score(Vector3 from, Vector3 fire, float heightWeight)
        {
            Vector3 offset = fire - from;
            float sideways = new Vector2(offset.x, offset.z).magnitude;
            return sideways + Mathf.Abs(offset.y) * Mathf.Max(1f, heightWeight);
        }

        /// <summary>Index of the fire to aim at, or -1 when none is within <paramref name="range"/> (weighted).</summary>
        public static int Pick(Vector3 from, IReadOnlyList<Vector3> fires, float range, float heightWeight)
        {
            int best = -1;
            float bestScore = range;
            for (int i = 0; i < fires.Count; i++)
            {
                float score = Score(from, fires[i], heightWeight);
                if (score <= bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>True if <paramref name="fire"/> is within <paramref name="range"/> (weighted, see <see cref="Score"/>).</summary>
        public static bool IsInRange(Vector3 from, Vector3 fire, float range, float heightWeight)
        {
            return Score(from, fire, heightWeight) <= range;
        }

        /// <summary>
        /// Index of a random fire within <paramref name="range"/> (weighted), or -1 when none is.
        /// </summary>
        /// <param name="roll">Random value in 0..1 that chooses among the fires in range.</param>
        public static int PickRandom(Vector3 from, IReadOnlyList<Vector3> fires, float range, float heightWeight, float roll)
        {
            int inRange = 0;
            for (int i = 0; i < fires.Count; i++)
            {
                if (IsInRange(from, fires[i], range, heightWeight))
                    inRange++;
            }

            if (inRange == 0)
                return -1;

            int chosen = Mathf.Min(Mathf.FloorToInt(Mathf.Clamp01(roll) * inRange), inRange - 1);
            for (int i = 0; i < fires.Count; i++)
            {
                if (!IsInRange(from, fires[i], range, heightWeight))
                    continue;
                if (chosen == 0)
                    return i;
                chosen--;
            }

            return -1;
        }
    }
}
