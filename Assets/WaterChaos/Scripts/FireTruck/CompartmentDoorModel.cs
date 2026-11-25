using System;

namespace WaterChaos.FireTruck
{
    /// <summary>Pure model of a compartment door that opens and closes smoothly.</summary>
    public sealed class CompartmentDoorModel
    {
        private readonly float _speed;

        /// <param name="speed">Open-ratio per second (1/s): 2 = opens fully in 0.5 s.</param>
        public CompartmentDoorModel(float speed)
        {
            _speed = float.IsNaN(speed) ? 0f : Math.Max(0f, speed);
        }

        /// <summary>Where the door is heading: true = open.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>How far the door is open: 0 = closed, 1 = fully open.</summary>
        public float OpenRatio { get; private set; }

        public void Toggle() => IsOpen = !IsOpen;

        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
                return;

            float goal = IsOpen ? 1f : 0f;
            float step = _speed * deltaTime;
            float difference = goal - OpenRatio;
            OpenRatio = Math.Abs(difference) <= step ? goal : OpenRatio + Math.Sign(difference) * step;
        }
    }
}
