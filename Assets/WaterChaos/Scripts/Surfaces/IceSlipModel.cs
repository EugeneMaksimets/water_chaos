using System;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// Whether a character slips (falls) on ice. Each time it walks onto ice it rolls once, the first moment it moves there;
    /// below <see cref="Chance"/> it slips. No more rolls until it has left the ice.
    /// </summary>
    public sealed class IceSlipModel
    {
        private bool _rolled;

        /// <param name="chance">Probability (0..1) of slipping on one visit to the ice.</param>
        public IceSlipModel(float chance)
        {
            Chance = float.IsNaN(chance) ? 0f : Math.Min(1f, Math.Max(0f, chance));
        }

        /// <summary>Probability (0..1) of slipping on one visit to the ice.</summary>
        public float Chance { get; }

        /// <summary>Returns true on the frame the character slips.</summary>
        /// <param name="onIce">The feet stand on ice.</param>
        /// <param name="moving">The character is walking (standing still never slips).</param>
        /// <param name="roll">Random value in 0..1.</param>
        public bool Step(bool onIce, bool moving, float roll)
        {
            if (!onIce)
            {
                _rolled = false;
                return false;
            }

            if (_rolled || !moving)
                return false;

            _rolled = true;
            return roll < Chance;
        }
    }
}
