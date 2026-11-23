using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure gameplay model of a fire: intensity that water cools down until it goes out.
    /// </summary>
    public sealed class FireModel
    {
        private readonly float _coolingPerLiter;
        private readonly float _extinguishThreshold;
        private readonly float _reigniteDelay;
        // Time since the fire went out (or was last watered while out), in seconds (s).
        private float _timeExtinguished;

        /// <summary>
        /// Normalized fire strength: 1 = full, 0 = extinguished.
        /// </summary>
        public float Intensity { get; private set; }
        public bool IsBurning => Intensity > 0f;

        /// <summary>
        /// Creates a fire burning at full intensity (1).
        /// <param name="coolingPerLiter">Intensity removed by one liter of water (1/L).</param>
        /// <param name="extinguishThreshold">Intensity (0..1) at or below which the fire goes out.</param>
        /// <param name="reigniteDelay">Seconds (s) after which an extinguished fire burns again; 0 = never.</param>
        /// </summary>
        public FireModel(float coolingPerLiter, float extinguishThreshold, float reigniteDelay = 0f)
        {
            _coolingPerLiter = Sanitize(coolingPerLiter);
            _extinguishThreshold = Math.Min(Sanitize(extinguishThreshold), 1f);
            _reigniteDelay = Sanitize(reigniteDelay);
            Intensity = 1f;
        }

        /// <summary>
        /// Cools the fire with the given water.
        /// </summary>
        /// <param name="volume">Water volume in liters (L); negative and NaN values are ignored.</param>
        public void ApplyWater(float volume)
        {
            if(volume <= 0f || float.IsNaN(volume))
                return;

            if(!IsBurning)
            {
                // Watering the embers keeps the fire from coming back.
                _timeExtinguished = 0f;
                return;
            }

            Intensity -= volume * _coolingPerLiter;

            if(Intensity <= _extinguishThreshold)
            {
                Intensity = 0f;
                _timeExtinguished = 0f;
            }
        }

        /// <summary>
        /// Advances time; an extinguished fire reignites after the delay.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        public void Tick(float deltaTime)
        {
            if(IsBurning || _reigniteDelay <= 0f || deltaTime <= 0f || float.IsNaN(deltaTime))
                return;

            _timeExtinguished += deltaTime;

            if(_timeExtinguished >= _reigniteDelay)
                Intensity = 1f;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}