using UnityEngine;

namespace WaterChaos.Items
{
    /// <summary>Flight path of a thrown body under gravity, ignoring drag. Fills a ready array, no allocations.</summary>
    public static class ThrowTrajectory
    {
        /// <summary>
        /// Sets points[i] to the position after i * timeStep seconds.
        /// Origin in m, velocity in m/s, gravity in m/s², timeStep in s; an invalid timeStep keeps every point at the origin.
        /// </summary>
        public static void Fill(Vector3[] points, Vector3 origin, Vector3 velocity, Vector3 gravity, float timeStep)
        {
            if (points == null)
                return;

            if (!(timeStep > 0f))
                timeStep = 0f;

            for (int i = 0; i < points.Length; i++)
            {
                float time = i * timeStep;
                points[i] = origin + velocity * time + gravity * (0.5f * time * time);
            }
        }
    }
}
