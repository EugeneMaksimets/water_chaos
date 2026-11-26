using System;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Pure rules of a flammable stream catching fire: it lights when it touches a burning fire,
    /// stays lit while the nozzle is active and flammable liquid flows, and goes out (resets) as soon as it is not.
    /// Switched on again, it starts unlit until it touches a fire again.
    /// A lit stream tries to set its current target alight at most once per interval; a new target is tried at once.
    /// </summary>
    public sealed class StreamIgnitionModel
    {
        /// <summary>Default interval between ignition attempts on one target, in seconds (s).</summary>
        public const float DefaultInterval = 0.75f;

        /// <summary>Shortest allowed interval between ignition attempts, in seconds (s).</summary>
        public const float MinInterval = 0.1f;

        // Remaining time counts below this are float noise, in seconds (s).
        private const float Epsilon = 1e-5f;

        private readonly float _interval;
        // Time until the next ignition attempt on the current target is allowed, in seconds (s).
        private float _cooldown;
        private object _target;

        /// <param name="interval">Seconds (s) between attempts on one target; at least <see cref="MinInterval"/>.</param>
        public StreamIgnitionModel(float interval = DefaultInterval)
        {
            _interval = float.IsNaN(interval) ? DefaultInterval : Math.Max(MinInterval, interval);
        }

        /// <summary>Raised when the stream lights (true) or goes out (false).</summary>
        public event Action<bool> LitChanged;

        /// <summary>The stream burns (flamethrower).</summary>
        public bool IsLit { get; private set; }

        /// <summary>
        /// Advances one step; returns true when the lit stream should try to ignite <paramref name="target"/> now.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values count as 0.</param>
        /// <param name="flammableFlowing">The nozzle is active and flammable liquid flows out of it.</param>
        /// <param name="touchingFire">The stream hits a burning fire.</param>
        /// <param name="target">What the stream hits that can catch fire; null if nothing.</param>
        public bool Tick(float deltaTime, bool flammableFlowing, bool touchingFire, object target)
        {
            if (!flammableFlowing)
            {
                SetLit(false);
                _cooldown = 0f;
                _target = null;
                return false;
            }

            if (touchingFire)
                SetLit(true);

            if (!ReferenceEquals(target, _target))
            {
                // Each target has its own wait: moving on to a new one never waits for the old one.
                _target = target;
                _cooldown = 0f;
            }
            else if (deltaTime > 0f)
            {
                _cooldown = Math.Max(0f, _cooldown - deltaTime);
            }

            if (!IsLit || target == null || _cooldown > Epsilon)
                return false;

            _cooldown = _interval;
            return true;
        }

        private void SetLit(bool value)
        {
            if (IsLit == value)
                return;

            IsLit = value;
            LitChanged?.Invoke(value);
        }
    }
}
