using System;

namespace WaterChaos.Items
{
    /// <summary>
    /// Pure timing of axe swings: a new swing may start only after the previous one has recovered.
    /// </summary>
    public sealed class AxeSwingModel
    {
        private readonly float _cooldown;
        private float _readyAt = float.NegativeInfinity;

        /// <param name="cooldown">Seconds (s) between two swings; negative and NaN become 0.</param>
        public AxeSwingModel(float cooldown)
        {
            _cooldown = float.IsNaN(cooldown) ? 0f : Math.Max(0f, cooldown);
        }

        /// <summary>True if a swing can start at <paramref name="time"/>.</summary>
        public bool IsReady(float time) => time >= _readyAt;

        /// <summary>Starts a swing at <paramref name="time"/> (s) if ready. Returns false while recovering.</summary>
        public bool TrySwing(float time)
        {
            if (float.IsNaN(time) || !IsReady(time))
                return false;

            _readyAt = time + _cooldown;
            return true;
        }
    }
}
