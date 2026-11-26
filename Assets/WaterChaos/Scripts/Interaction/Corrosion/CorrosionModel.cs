using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure gameplay model of a surface that corrosive liquid eats away.
    /// Every liter of corrosive liquid adds corrosion, and a wet surface keeps corroding for a while after the hit.
    /// Resistance slows both down; at the threshold the surface is destroyed, exactly once.
    /// Any non-corrosive liquid (water) washes the surface: it stops corroding, but the damage stays.
    /// </summary>
    public sealed class CorrosionModel
    {
        private readonly float _resistance;
        private readonly float _corrosionPerLiter;
        private readonly float _threshold;
        private readonly float _soakRate;
        private readonly float _wetDuration;

        // Seconds (s) the surface still stays wet with corrosive liquid.
        private float _wetTimeLeft;

        /// <summary>Corrosion taken so far, in corrosion points; never above the threshold.</summary>
        public float Corrosion { get; private set; }

        /// <summary>0 = intact, 1 = destroyed.</summary>
        public float Progress => _threshold > 0f ? Corrosion / _threshold : 1f;

        public bool IsDestroyed { get; private set; }

        /// <summary>Corrosive liquid is still on the surface and keeps eating it.</summary>
        public bool IsWet => _wetTimeLeft > 0f && !IsDestroyed;

        /// <param name="resistance">0..1: share of the corrosion the surface shrugs off; 1 = immune.</param>
        /// <param name="corrosionPerLiter">Corrosion points added by one liter of full-strength corrosive liquid (1/L).</param>
        /// <param name="threshold">Corrosion points at which the surface is destroyed; 0 or less = destroyed at the first harm.</param>
        /// <param name="soakRate">Corrosion points per second (1/s) added while the surface stays wet.</param>
        /// <param name="wetDuration">Seconds (s) the surface stays wet after the last corrosive hit.</param>
        public CorrosionModel(float resistance, float corrosionPerLiter, float threshold, float soakRate, float wetDuration)
        {
            _resistance = Math.Min(Sanitize(resistance), 1f);
            _corrosionPerLiter = Sanitize(corrosionPerLiter);
            _threshold = Sanitize(threshold);
            _soakRate = Sanitize(soakRate);
            _wetDuration = Sanitize(wetDuration);
        }

        /// <summary>
        /// Applies liquid that hit the surface. Returns true only on the call that destroys it.
        /// </summary>
        /// <param name="volume">Liters (L) that hit; zero, negative and NaN are ignored.</param>
        /// <param name="corrosionPower">Strength of the liquid (0 = harmless, 1 = full); harmless liquid washes the surface.</param>
        public bool ApplyLiquid(float volume, float corrosionPower)
        {
            if (IsDestroyed || !(volume > 0f))
                return false;

            if (!(corrosionPower > 0f))
            {
                _wetTimeLeft = 0f;
                return false;
            }

            _wetTimeLeft = _wetDuration;
            return AddCorrosion(volume * corrosionPower * _corrosionPerLiter);
        }

        /// <summary>
        /// Advances time: a wet surface keeps corroding until it dries. Returns true only on the call that destroys it.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); zero, negative and NaN are ignored.</param>
        public bool Tick(float deltaTime)
        {
            if (!IsWet || !(deltaTime > 0f))
                return false;

            float wetTime = Math.Min(deltaTime, _wetTimeLeft);
            _wetTimeLeft -= wetTime;
            return AddCorrosion(wetTime * _soakRate);
        }

        private bool AddCorrosion(float amount)
        {
            float effective = amount * (1f - _resistance);
            if (!(effective > 0f))
                return false;

            Corrosion = Math.Min(_threshold, Corrosion + effective);
            if (Corrosion < _threshold)
                return false;

            IsDestroyed = true;
            _wetTimeLeft = 0f;
            return true;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
