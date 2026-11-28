using System;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// Timing of a stun from a toxic hit: a hit stuns for the duration; hits meanwhile change nothing,
    /// and right after it the character cannot be stunned again for the immunity time (so two characters
    /// spraying each other do not lock each other forever). Times are in seconds (s).
    /// </summary>
    public sealed class ToxicStunModel
    {
        private readonly float _duration;
        private readonly float _immunity;
        private float _stunnedUntil = float.NegativeInfinity;

        /// <param name="duration">How long a hit stuns, in seconds (s).</param>
        /// <param name="immunity">How long after a stun no new hit stuns, in seconds (s).</param>
        public ToxicStunModel(float duration, float immunity)
        {
            _duration = Sanitize(duration);
            _immunity = Sanitize(immunity);
        }

        public bool IsStunned(float time) => time < _stunnedUntil;

        /// <summary>A toxic hit at <paramref name="time"/>; true if it starts a new stun.</summary>
        public bool TryStun(float time)
        {
            if (time < _stunnedUntil + _immunity)
                return false;

            _stunnedUntil = time + _duration;
            return true;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
