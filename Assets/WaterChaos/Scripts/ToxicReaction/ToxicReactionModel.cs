using System;

namespace WaterChaos.ToxicReaction
{
    /// <summary>
    /// One fire's toxic reaction: toxic liquid starts it, it lasts a fixed time, then a cooldown follows during which
    /// toxic liquid does nothing. So one hit never makes endless clouds and the fire is never boosted for good.
    /// Times are in seconds (s) of game time, given by the caller.
    /// </summary>
    public sealed class ToxicReactionModel
    {
        private float _startedAt = float.NegativeInfinity;

        /// <param name="duration">How long one reaction lasts (s).</param>
        /// <param name="cooldown">After a reaction ends, how long until the next one may start (s).</param>
        public ToxicReactionModel(float duration, float cooldown)
        {
            Duration = NonNegative(duration);
            Cooldown = NonNegative(cooldown);
        }

        public float Duration { get; }
        public float Cooldown { get; }

        /// <summary>Strength of the reaction that is running or ran last: 0 = caused by liquid, 1+ = chain reactions.</summary>
        public int Generation { get; private set; }

        public bool IsReacting(float time) => time >= _startedAt && time < _startedAt + Duration;

        public bool IsCoolingDown(float time) => !IsReacting(time) && time < _startedAt + Duration + Cooldown;

        public bool CanStart(float time) => time >= _startedAt + Duration + Cooldown;

        /// <summary>Starts a reaction unless one is running or cooling down. Returns true if it started.</summary>
        /// <param name="generation">0 for toxic liquid hitting the fire, 1+ for a chain reaction from a cloud.</param>
        public bool TryStart(float time, int generation = 0)
        {
            if (!CanStart(time))
                return false;

            _startedAt = time;
            Generation = Math.Max(0, generation);
            return true;
        }

        /// <summary>Ends a running reaction at once (the fire went out); the cooldown still follows.</summary>
        public void Stop(float time)
        {
            if (IsReacting(time))
                _startedAt = time - Duration;
        }

        private static float NonNegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, value);
    }
}
