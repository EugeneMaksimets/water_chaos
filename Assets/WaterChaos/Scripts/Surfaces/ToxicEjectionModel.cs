using System;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// How long a character can stay in toxic air without protection before it has to get out:
    /// the time adds up while it is inside and unprotected and resets as soon as it is out or protected.
    /// </summary>
    public sealed class ToxicEjectionModel
    {
        /// <param name="tolerance">Seconds (s) in toxic air before the character is pushed out.</param>
        public ToxicEjectionModel(float tolerance)
        {
            Tolerance = float.IsNaN(tolerance) ? 0f : Math.Max(0f, tolerance);
        }

        public float Tolerance { get; }

        /// <summary>Seconds (s) spent in toxic air so far without protection.</summary>
        public float Exposure { get; private set; }

        /// <summary>Share of the tolerance used up (0..1), e.g. for a cough or a warning.</summary>
        public float Ratio => Tolerance > 0f ? Math.Min(1f, Exposure / Tolerance) : (Exposure > 0f ? 1f : 0f);

        /// <summary>Advances the timer. Returns true when the character must be pushed out now (the timer restarts).</summary>
        /// <param name="inside">The character is inside the toxic air.</param>
        /// <param name="isProtected">It wears protection (a gas mask).</param>
        /// <param name="deltaTime">Elapsed time in seconds (s).</param>
        public bool Tick(bool inside, bool isProtected, float deltaTime)
        {
            if (!inside || isProtected)
            {
                Exposure = 0f;
                return false;
            }

            if (deltaTime > 0f)
                Exposure += deltaTime;

            if (Exposure < Tolerance)
                return false;

            Exposure = 0f;
            return true;
        }
    }
}
