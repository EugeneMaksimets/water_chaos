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

        /// <summary>
        /// Normalized fire strength: 1 = full, 0 = extinguished.
        /// </summary>
        public float Intensity { get; private set; }
        public bool IsBurning => Intensity > 0f;

        /// <summary>
        /// Creates a fire burning at full intensity (1).
        /// </summary>
        /// <param name="coolingPerLiter">Intensity removed by one liter of water (1/L).</param>
        /// <param name="extinguishThreshold">Intensity (0..1) at or below which the fire goes out.</param>
        public FireModel(float coolingPerLiter, float extinguishThreshold)
        {
            _coolingPerLiter = Sanitize(coolingPerLiter);
            _extinguishThreshold = Math.Min(Sanitize(extinguishThreshold), 1f);
            Intensity = 1f;
        }

        /// <summary>
        /// Cools the fire with the given water.
        /// </summary>
        /// <param name="volume">Water volume in liters (L); negative and NaN values are ignored.</param>
        public void ApplyWater(float volume)
        {
            if(!IsBurning || volume <= 0f || float.IsNaN(volume))
                return;

            Intensity -= volume * _coolingPerLiter;

            if(Intensity <= _extinguishThreshold)
                Intensity = 0f;
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}