using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure timing rules of fire spreading: a strong enough fire tries to spread once per interval.
    /// Where it spreads to is decided by the caller.
    /// </summary>
    public sealed class FireSpreadModel
    {
        /// <summary>Shortest allowed interval, in seconds (s), so a fire never spreads every frame.</summary>
        public const float MinInterval = 0.5f;

        private readonly float _interval;
        private readonly float _minIntensity;
        private readonly float _chance;
        // Time the fire has been strong enough since the last attempt, in seconds (s).
        private float _timer;

        /// <param name="interval">Seconds (s) between spread attempts; at least <see cref="MinInterval"/>.</param>
        /// <param name="minIntensity">Intensity (0..1) the fire needs to spread.</param>
        /// <param name="chance">Probability (0..1) that an attempt succeeds.</param>
        public FireSpreadModel(float interval, float minIntensity, float chance)
        {
            _interval = Math.Max(Sanitize(interval), MinInterval);
            _minIntensity = Math.Min(Sanitize(minIntensity), 1f);
            _chance = Math.Min(Sanitize(chance), 1f);
        }

        /// <summary>
        /// Advances time; returns true when the fire should spread now.
        /// A fire that is out or too weak restarts the wait.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        /// <param name="intensity">Current fire intensity (0..1).</param>
        /// <param name="roll">Random value in [0, 1) that decides the chance.</param>
        public bool Tick(float deltaTime, float intensity, float roll)
        {
            if(deltaTime <= 0f || float.IsNaN(deltaTime))
                return false;

            if(!(intensity > 0f) || intensity < _minIntensity)
            {
                _timer = 0f;
                return false;
            }

            _timer += deltaTime;
            if(_timer < _interval)
                return false;

            _timer = 0f;
            return roll < _chance;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
