using System;
using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// One small snowdrift: grows with snow landing on it up to a full size and melts away while warm
    /// (above the melt temperature, or outside every temperature zone).
    /// </summary>
    public sealed class SnowdriftModel : ISurfacePatch
    {
        private readonly SnowdriftConfig _config;

        public SnowdriftModel(Vector3 center, SnowdriftConfig config)
        {
            Center = center;
            _config = config;
        }

        public Vector3 Center { get; }
        /// <summary>Snow in the drift, in liters (L); never above the full volume.</summary>
        public float Volume { get; private set; }
        /// <summary>Size as a share of a full drift (0..1).</summary>
        public float Fill => Volume / _config.FullVolume;
        public float Radius => Mathf.Lerp(_config.MinRadius, _config.MaxRadius, Mathf.Sqrt(Fill));
        /// <summary>Height of the drift, in meters (m).</summary>
        public float Height => _config.MaxHeight * Fill;
        public bool IsMelted => Volume <= 0f;
        /// <summary>Deep enough to cover the surface: no new fire starts under it.</summary>
        public bool BlocksIgnition => !IsMelted && Fill >= _config.BlockingFill;

        /// <summary>Raised when the size changes.</summary>
        public event Action<SnowdriftModel> Changed;

        /// <summary>Water arrived as snow. Returns the snow (L) taken; a full drift takes nothing more.</summary>
        public float AddSnow(float liters)
        {
            if (!(liters > 0f))
                return 0f;

            float taken = Math.Min(liters * _config.AccumulationPerLiter, _config.FullVolume - Volume);
            if (!(taken > 0f))
                return 0f;

            Volume += taken;
            Changed?.Invoke(this);
            return taken;
        }

        /// <summary>Melts the drift while it is warm where it lies.</summary>
        /// <param name="hasZone">A temperature zone covers the drift; false outdoors (snow melts there).</param>
        /// <param name="temperature">Temperature of that zone (°C); ignored without a zone.</param>
        /// <param name="deltaTime">Elapsed time in seconds (s).</param>
        public void UpdateTemperature(bool hasZone, float temperature, float deltaTime)
        {
            if (!(deltaTime > 0f) || IsMelted)
                return;

            bool warm = !hasZone || temperature > _config.MeltAbove;
            if (!warm || _config.MeltRate <= 0f)
                return;

            Volume = Math.Max(0f, Volume - _config.MeltRate * deltaTime);
            Changed?.Invoke(this);
        }
    }
}
