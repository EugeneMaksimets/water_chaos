using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Teammate
{
    /// <summary>
    /// Which fire a teammate aims at: the closest one in range, where a difference in height counts more than the same
    /// distance sideways. So a teammate lifted to the roof picks the roof fires, and one on the ground the fires around them.
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
    }
}
