using System;
using UnityEngine;

namespace WaterChaos.ToxicReaction
{
    /// <summary>
    /// A toxic cloud: a sphere that spreads from a small start radius to its full radius, hangs, then thins out
    /// and is gone after its lifetime. Pure data; the age (s) is given by the caller.
    /// </summary>
    public sealed class ToxicCloudModel
    {
        /// <param name="center">Center in world space.</param>
        /// <param name="startRadius">Radius when it appears, in meters (m).</param>
        /// <param name="maxRadius">Radius once spread, in meters (m).</param>
        /// <param name="spreadTime">Seconds (s) to spread to the full radius.</param>
        /// <param name="lifetime">Seconds (s) the cloud is toxic.</param>
        /// <param name="generation">0 = from a reaction to toxic liquid, 1+ = from a chain reaction.</param>
        public ToxicCloudModel(Vector3 center, float startRadius, float maxRadius, float spreadTime, float lifetime, int generation)
        {
            Center = center;
            StartRadius = NonNegative(startRadius);
            MaxRadius = Math.Max(StartRadius, NonNegative(maxRadius));
            SpreadTime = NonNegative(spreadTime);
            Lifetime = NonNegative(lifetime);
            Generation = Math.Max(0, generation);
        }

        public Vector3 Center { get; }
        public float StartRadius { get; }
        public float MaxRadius { get; }
        public float SpreadTime { get; }
        public float Lifetime { get; }
        public int Generation { get; }

        public bool IsAlive(float age) => age >= 0f && age < Lifetime;

        /// <summary>Radius at <paramref name="age"/> (s): grows smoothly to the full radius, then holds; 0 once gone.</summary>
        public float Radius(float age)
        {
            if (!IsAlive(age))
                return 0f;

            if (SpreadTime <= 0f)
                return MaxRadius;

            float t = Mathf.Clamp01(age / SpreadTime);
            // Ease out: a quick puff that slows as it spreads.
            return Mathf.Lerp(StartRadius, MaxRadius, 1f - (1f - t) * (1f - t));
        }

        /// <summary>True if the point is inside the cloud at <paramref name="age"/> (s).</summary>
        public bool Contains(Vector3 point, float age)
        {
            float radius = Radius(age);
            return radius > 0f && (point - Center).sqrMagnitude <= radius * radius;
        }

        private static float NonNegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, value);
    }
}
