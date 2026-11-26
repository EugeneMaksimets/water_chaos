using System;

namespace WaterChaos.Soap
{
    /// <summary>Stage of the soap bubble effect on one character.</summary>
    public enum SoapBubblePhase
    {
        /// <summary>No bubble; the character moves on its own.</summary>
        Normal = 0,
        /// <summary>The bubble grows around the character.</summary>
        BubbleForming = 1,
        /// <summary>The bubble is closed; a short pause before it lifts off.</summary>
        Encapsulated = 2,
        /// <summary>Bubble and character go up together, then hang at the top for a moment.</summary>
        Rising = 3,
        /// <summary>The bubble pops.</summary>
        Bursting = 4,
        /// <summary>The character is free again and falls under normal gravity until it lands.</summary>
        Falling = 5,
    }

    /// <summary>
    /// Pure timeline of the soap bubble that lifts a teammate:
    /// Normal → BubbleForming → Encapsulated → Rising → Bursting → Falling → Normal.
    /// Only one bubble at a time: a capture is accepted only in <see cref="SoapBubblePhase.Normal"/> after the cooldown.
    /// The model only says how far to lift; the caller moves the character and reports free space above.
    /// </summary>
    public sealed class SoapBubbleModel
    {
        // Below this distance to the top (m) the rise slows down, so the bubble does not stop abruptly.
        private const float SlowDownDistance = 1f;
        // Slowest share of the rise speed near the top, so the bubble still arrives.
        private const float MinSpeedShare = 0.2f;
        // Free space (m) below which the way up counts as blocked.
        private const float BlockedSpace = 0.001f;

        private readonly float _formDuration;
        private readonly float _encapsulatedDuration;
        private readonly float _riseSpeed;
        private readonly float _hangDuration;
        private readonly float _burstDuration;
        private readonly float _recaptureCooldown;

        private float _timer;
        private float _cooldown;
        private bool _atTop;

        /// <param name="formDuration">Time for the bubble to grow around the character, in seconds (s).</param>
        /// <param name="encapsulatedDuration">Pause in the closed bubble before lift-off, in seconds (s).</param>
        /// <param name="riseSpeed">Top lift speed, in meters per second (m/s).</param>
        /// <param name="riseHeight">How high to lift above the start, in meters (m).</param>
        /// <param name="hangDuration">Time at the top (or under a ceiling) before the bubble pops, in seconds (s).</param>
        /// <param name="burstDuration">Length of the pop before the character is released, in seconds (s).</param>
        /// <param name="recaptureCooldown">After landing, time before a new bubble can form, in seconds (s).</param>
        /// <remarks>Negative and NaN values become 0.</remarks>
        public SoapBubbleModel(float formDuration, float encapsulatedDuration, float riseSpeed, float riseHeight,
            float hangDuration, float burstDuration, float recaptureCooldown)
        {
            _formDuration = Sanitize(formDuration);
            _encapsulatedDuration = Sanitize(encapsulatedDuration);
            _riseSpeed = Sanitize(riseSpeed);
            RiseHeight = Sanitize(riseHeight);
            _hangDuration = Sanitize(hangDuration);
            _burstDuration = Sanitize(burstDuration);
            _recaptureCooldown = Sanitize(recaptureCooldown);
        }

        /// <summary>Raised when <see cref="Phase"/> changes; argument is the new phase.</summary>
        public event Action<SoapBubblePhase> PhaseChanged;

        public SoapBubblePhase Phase { get; private set; } = SoapBubblePhase.Normal;

        /// <summary>The bubble holds the character: its own movement and gravity must stay off.</summary>
        public bool IsCarrying =>
            Phase == SoapBubblePhase.BubbleForming || Phase == SoapBubblePhase.Encapsulated ||
            Phase == SoapBubblePhase.Rising || Phase == SoapBubblePhase.Bursting;

        /// <summary>A new bubble can form now.</summary>
        public bool CanCapture => Phase == SoapBubblePhase.Normal && _cooldown <= 0f;

        /// <summary>Bubble growth from 0 (just appeared) to 1 (fully around the character).</summary>
        public float FormProgress { get; private set; }

        /// <summary>Planned lift above the start, in meters (m).</summary>
        public float RiseHeight { get; }

        /// <summary>Lift so far above the start, in meters (m).</summary>
        public float RisenHeight { get; private set; }

        /// <summary>Starts a bubble; ignored (false) while one is active or during the cooldown after landing.</summary>
        public bool TryCapture()
        {
            if (!CanCapture)
                return false;

            FormProgress = 0f;
            RisenHeight = 0f;
            _atTop = false;
            SetPhase(SoapBubblePhase.BubbleForming);
            return true;
        }

        /// <summary>
        /// Advances the effect.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); non-positive and NaN values do nothing.</param>
        /// <param name="freeSpaceAbove">How far the bubble can still go up before it touches something, in meters (m).</param>
        /// <returns>How far to lift the character (with the bubble) this step, in meters (m); never negative.</returns>
        public float Tick(float deltaTime, float freeSpaceAbove = float.PositiveInfinity)
        {
            if (!(deltaTime > 0f))
                return 0f;

            switch (Phase)
            {
                case SoapBubblePhase.Normal:
                    _cooldown = Math.Max(0f, _cooldown - deltaTime);
                    return 0f;

                case SoapBubblePhase.BubbleForming:
                    _timer += deltaTime;
                    FormProgress = _formDuration > 0f ? Math.Min(_timer / _formDuration, 1f) : 1f;
                    if (_timer >= _formDuration)
                        SetPhase(SoapBubblePhase.Encapsulated);
                    return 0f;

                case SoapBubblePhase.Encapsulated:
                    _timer += deltaTime;
                    if (_timer >= _encapsulatedDuration)
                        SetPhase(SoapBubblePhase.Rising);
                    return 0f;

                case SoapBubblePhase.Rising:
                    return TickRising(deltaTime, freeSpaceAbove);

                case SoapBubblePhase.Bursting:
                    _timer += deltaTime;
                    if (_timer >= _burstDuration)
                        SetPhase(SoapBubblePhase.Falling);
                    return 0f;

                default:
                    return 0f;
            }
        }

        /// <summary>The released character is on the ground again; ignored unless <see cref="SoapBubblePhase.Falling"/>.</summary>
        public void Land()
        {
            if (Phase != SoapBubblePhase.Falling)
                return;

            _cooldown = _recaptureCooldown;
            SetPhase(SoapBubblePhase.Normal);
        }

        /// <summary>Stops the effect at once (character disabled, destroyed, scene unloaded); the cooldown still applies.</summary>
        public void Cancel()
        {
            if (Phase == SoapBubblePhase.Normal)
                return;

            _cooldown = _recaptureCooldown;
            SetPhase(SoapBubblePhase.Normal);
        }

        private float TickRising(float deltaTime, float freeSpaceAbove)
        {
            float step = 0f;
            if (!_atTop)
            {
                float remaining = RiseHeight - RisenHeight;
                float free = float.IsNaN(freeSpaceAbove) ? 0f : Math.Max(0f, freeSpaceAbove);
                float speedShare = Math.Max(MinSpeedShare, Math.Min(remaining / SlowDownDistance, 1f));
                step = Math.Min(Math.Min(_riseSpeed * speedShare * deltaTime, remaining), free);
                step = Math.Max(0f, step);
                RisenHeight += step;

                // Reached the planned height, or a ceiling is in the way: hang here, then pop.
                if (RisenHeight >= RiseHeight || free <= BlockedSpace || _riseSpeed <= 0f)
                    _atTop = true;

                return step;
            }

            _timer += deltaTime;
            if (_timer >= _hangDuration)
                SetPhase(SoapBubblePhase.Bursting);

            return step;
        }

        private void SetPhase(SoapBubblePhase phase)
        {
            _timer = 0f;
            if (Phase == phase)
                return;

            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
