using System;

namespace WaterChaos.UI
{
    /// <summary>
    /// Pure timing of the heat warning: shows once when the temperature rises to the threshold, for a fixed time,
    /// then hides even if it stays hot. Shows again only after the room cooled clearly below the threshold,
    /// so small ups and downs around it do not make the hint blink.
    /// </summary>
    public sealed class HeatWarningHint
    {
        private readonly float _threshold;
        private readonly float _duration;
        private readonly float _rearmMargin;
        private bool _armed = true;
        // Seconds (s) the hint stays on screen.
        private float _timeLeft;

        public bool IsVisible => _timeLeft > 0f;

        /// <param name="threshold">Temperature (°C) at which the hint appears.</param>
        /// <param name="duration">Seconds (s) the hint is shown.</param>
        /// <param name="rearmMargin">Degrees (°C) below the threshold the room must cool before the hint can show again.</param>
        public HeatWarningHint(float threshold, float duration, float rearmMargin)
        {
            if (float.IsNaN(threshold))
                throw new ArgumentException("The hint threshold must be a number.", nameof(threshold));

            _threshold = threshold;
            _duration = Sanitize(duration);
            _rearmMargin = Sanitize(rearmMargin);
        }

        /// <param name="temperature">Current room temperature (°C).</param>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        public void Tick(float temperature, float deltaTime)
        {
            if (deltaTime > 0f)
                _timeLeft = Math.Max(_timeLeft - deltaTime, 0f);

            if (temperature < _threshold - _rearmMargin)
            {
                _armed = true;
            }
            else if (_armed && temperature >= _threshold)
            {
                _armed = false;
                _timeLeft = _duration;
            }
        }

        /// <summary>Hides the hint and lets it show again (e.g. the player entered another room).</summary>
        public void Reset()
        {
            _armed = true;
            _timeLeft = 0f;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
