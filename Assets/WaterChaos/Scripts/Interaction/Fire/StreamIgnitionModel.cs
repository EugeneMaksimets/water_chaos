using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure rules of a flammable stream catching fire: it lights when it touches a burning fire,
    /// stays lit while flammable liquid keeps flowing, and goes out when the flow stops or turns non-flammable.
    /// A lit stream tries to set what it hits alight at most once per interval.
    /// </summary>
    public sealed class StreamIgnitionModel
    {
        /// <summary>Shortest allowed interval between ignition attempts, in seconds (s).</summary>
        public const float MinInterval = 0.1f;

        // Remaining time counts below this are float noise, in seconds (s).
        private const float Epsilon = 1e-5f;

        private readonly float _interval;
        // Time until the next ignition attempt is allowed, in seconds (s).
        private float _cooldown;

        /// <param name="interval">Seconds (s) between ignition attempts; at least <see cref="MinInterval"/>.</param>
        public StreamIgnitionModel(float interval)
        {
            _interval = float.IsNaN(interval) ? MinInterval : Math.Max(MinInterval, interval);
        }

        /// <summary>The stream burns (flamethrower).</summary>
        public bool IsLit { get; private set; }

        /// <summary>
        /// Advances one step; returns true when the lit stream should try to ignite what it hits now.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values count as 0.</param>
        /// <param name="flammableFlowing">Flammable liquid is flowing out of the nozzle.</param>
        /// <param name="touchingFire">The stream hits a burning fire.</param>
        /// <param name="touchingFlammable">The stream hits something that can catch fire.</param>
        public bool Tick(float deltaTime, bool flammableFlowing, bool touchingFire, bool touchingFlammable)
        {
            if (!flammableFlowing)
            {
                IsLit = false;
                _cooldown = 0f;
                return false;
            }

            if (touchingFire)
                IsLit = true;

            if (deltaTime > 0f)
                _cooldown = Math.Max(0f, _cooldown - deltaTime);

            if (!IsLit || !touchingFlammable || _cooldown > Epsilon)
                return false;

            _cooldown = _interval;
            return true;
        }
    }
}
