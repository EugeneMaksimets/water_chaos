using System;
using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>Tuning of sliding on ice, validated once.</summary>
    public readonly struct IceSlideConfig
    {
        /// <summary>How fast input speeds the slide up, in meters per second squared (m/s²).</summary>
        public float Acceleration { get; }
        /// <summary>Fastest slide, in meters per second (m/s).</summary>
        public float MaxSpeed { get; }
        /// <summary>
        /// Braking on normal ground, in m/s²; on ice it is scaled by the surface friction, so the slide slows down
        /// slowly but never keeps going forever.
        /// </summary>
        public float GroundDeceleration { get; }
        /// <summary>How fast the slide turns toward where the player faces, in degrees per second (°/s).</summary>
        public float TurnSpeed { get; }

        public IceSlideConfig(float acceleration, float maxSpeed, float groundDeceleration, float turnSpeed)
        {
            Acceleration = NonNegative(acceleration);
            MaxSpeed = NonNegative(maxSpeed);
            GroundDeceleration = NonNegative(groundDeceleration);
            TurnSpeed = NonNegative(turnSpeed);
        }

        public static IceSlideConfig Default => new IceSlideConfig(6f, 9f, 20f, 120f);

        private static float NonNegative(float value) => float.IsNaN(value) ? 0f : Math.Max(0f, value);
    }

    /// <summary>
    /// Horizontal motion of a character on ice. Never touches the character's own movement settings:
    /// it only owns the sliding velocity while the character is on ice.
    /// The slide always heads where the character faces (smoothly turning, never snapping),
    /// input speeds it up or brakes it, and without input it slowly loses speed by the ice friction.
    /// </summary>
    public sealed class IceSlideModel
    {
        private readonly IceSlideConfig _config;

        public IceSlideModel(IceSlideConfig config)
        {
            _config = config;
        }

        /// <summary>Horizontal sliding velocity, in meters per second (m/s).</summary>
        public Vector3 Velocity { get; private set; }
        public bool IsSliding { get; private set; }

        /// <summary>Steps onto ice carrying its current horizontal velocity (m/s).</summary>
        public void Enter(Vector3 velocity)
        {
            velocity.y = 0f;
            Velocity = Vector3.ClampMagnitude(velocity, _config.MaxSpeed);
            IsSliding = true;
        }

        /// <summary>Leaves the ice: normal movement takes over at once.</summary>
        public void Exit()
        {
            Velocity = Vector3.zero;
            IsSliding = false;
        }

        /// <summary>
        /// Advances the slide one step and returns the horizontal velocity (m/s).
        /// </summary>
        /// <param name="input">Wanted move direction (horizontal, length 0..1), already relative to the view.</param>
        /// <param name="facing">Where the character faces (horizontal).</param>
        /// <param name="friction">Grip of the surface (0..1); ice is low.</param>
        /// <param name="deltaTime">Elapsed time in seconds (s).</param>
        public Vector3 Step(Vector3 input, Vector3 facing, float friction, float deltaTime)
        {
            if (!IsSliding || !(deltaTime > 0f))
                return Velocity;

            input.y = 0f;
            input = Vector3.ClampMagnitude(input, 1f);
            facing.y = 0f;
            friction = float.IsNaN(friction) ? 0f : Mathf.Clamp01(friction);

            Vector3 velocity = TurnTowardFacing(Velocity, facing, deltaTime);
            velocity += input * (_config.Acceleration * deltaTime);

            // Without input the slide slowly runs out by the ice friction.
            if (input.sqrMagnitude < 0.0001f)
            {
                float speed = Mathf.Max(0f, velocity.magnitude - _config.GroundDeceleration * friction * deltaTime);
                velocity = velocity.sqrMagnitude > 0f ? velocity.normalized * speed : Vector3.zero;
            }

            Velocity = Vector3.ClampMagnitude(velocity, _config.MaxSpeed);
            return Velocity;
        }

        /// <summary>
        /// Turns the slide toward the facing direction (or straight away from it when sliding backwards),
        /// at the turn speed, keeping its speed.
        /// </summary>
        private Vector3 TurnTowardFacing(Vector3 velocity, Vector3 facing, float deltaTime)
        {
            if (velocity.sqrMagnitude < 0.0001f || facing.sqrMagnitude < 0.0001f)
                return velocity;

            Vector3 target = facing.normalized;
            if (Vector3.Dot(velocity, target) < 0f)
                target = -target;

            float maxRadians = _config.TurnSpeed * Mathf.Deg2Rad * deltaTime;
            return Vector3.RotateTowards(velocity, target * velocity.magnitude, maxRadians, 0f);
        }
    }
}
