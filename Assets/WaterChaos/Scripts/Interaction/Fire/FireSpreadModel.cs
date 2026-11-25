using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure timing rules of fire spreading: a strong enough fire tries to spread once per interval,
    /// and each attempt succeeds only by chance. The interval itself varies randomly (jitter),
    /// so fires do not spread in lockstep. Where it spreads to is decided by the caller.
    /// </summary>
    public sealed class FireSpreadModel
    {
        /// <summary>Shortest allowed interval, in seconds (s), so a fire never spreads every frame.</summary>
        public const float MinInterval = 0.5f;

        private readonly float _interval;
        private readonly float _minIntensity;
        private readonly float _chance;
        private readonly float _intervalJitter;
        // Time the fire has been strong enough since the last attempt, in seconds (s).
        private float _timer;
        // Wait before the next attempt, in seconds (s): the interval with random jitter.
        private float _nextInterval;

        /// <param name="interval">Seconds (s) between spread attempts; at least <see cref="MinInterval"/>.</param>
        /// <param name="minIntensity">Intensity (0..1) the fire needs to spread.</param>
        /// <param name="chance">Probability (0..1) that an attempt succeeds.</param>
        /// <param name="intervalJitter">Random spread of the interval (0..1): 0.5 = from 50 % to 150 % of it.</param>
        /// <param name="firstIntervalRoll">Random value in [0, 1] that picks the wait before the first attempt.</param>
        public FireSpreadModel(float interval, float minIntensity, float chance,
            float intervalJitter = 0f, float firstIntervalRoll = 0.5f)
        {
            _interval = Math.Max(Sanitize(interval), MinInterval);
            _minIntensity = Math.Min(Sanitize(minIntensity), 1f);
            _chance = Math.Min(Sanitize(chance), 1f);
            _intervalJitter = Math.Min(Sanitize(intervalJitter), 1f);
            _nextInterval = PickInterval(firstIntervalRoll);
        }

        /// <summary>
        /// Advances time; returns true when the fire should spread now.
        /// A fire that is out or too weak restarts the wait.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        /// <param name="intensity">Current fire intensity (0..1).</param>
        /// <param name="roll">Random value in [0, 1) that decides the chance.</param>
        /// <param name="intervalRoll">Random value in [0, 1] that picks the wait before the next attempt.</param>
        public bool Tick(float deltaTime, float intensity, float roll, float intervalRoll = 0.5f)
        {
            if(deltaTime <= 0f || float.IsNaN(deltaTime))
                return false;

            if(!(intensity > 0f) || intensity < _minIntensity)
            {
                _timer = 0f;
                return false;
            }

            _timer += deltaTime;
            if(_timer < _nextInterval)
                return false;

            _timer = 0f;
            _nextInterval = PickInterval(intervalRoll);
            return roll < _chance;
        }

        private float PickInterval(float roll)
        {
            float centered = float.IsNaN(roll) ? 0f : Math.Min(Math.Max(roll, 0f), 1f) * 2f - 1f;
            return Math.Max(_interval * (1f + _intervalJitter * centered), MinInterval);
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
