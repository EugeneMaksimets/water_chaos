using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure gameplay model of a fire: intensity that water cools down until it goes out.
    /// Fuel can push it above full (overburn), which fades back to full over time.
    /// </summary>
    public sealed class FireModel
    {
        /// <summary>Normal burning intensity; fuel may raise it above this for a while.</summary>
        public const float FullIntensity = 1f;

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
        /// Feeds a burning fire: raises its intensity, never above <paramref name="maxIntensity"/>.
        /// An extinguished fire has nothing to feed, so it stays out.
        /// </summary>
        /// <param name="amount">Intensity to add; negative and NaN values are ignored.</param>
        /// <param name="maxIntensity">Upper limit, at least <see cref="FullIntensity"/>.</param>
        public void AddFuel(float amount, float maxIntensity)
        {
            if(!IsBurning || !(amount > 0f))
                return;

            float limit = Math.Max(FullIntensity, Sanitize(maxIntensity));
            if(Intensity >= limit)
                return;

            Intensity = Math.Min(limit, Intensity + amount);
        }

        /// <summary>Lets an overburning fire calm down toward <see cref="FullIntensity"/>, never below it.</summary>
        /// <param name="amount">Intensity to remove from the part above full; negative and NaN values are ignored.</param>
        public void ReduceOverburn(float amount)
        {
            if(Intensity <= FullIntensity || !(amount > 0f))
                return;

            Intensity = Math.Max(FullIntensity, Intensity - amount);
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

        /// <summary>Puts the fire out at once, whatever its intensity; the reignite delay starts from now.</summary>
        public void Extinguish()
        {
            Intensity = 0f;
            _timeExtinguished = 0f;
        }

        /// <summary>Lights an extinguished fire again at full intensity; a burning fire is left as is.</summary>
        public void Reignite()
        {
            if (IsBurning)
                return;

            Intensity = 1f;
            _timeExtinguished = 0f;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}