using NUnit.Framework;
using UnityEngine;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>Ice is slippery: the slide heads where the player faces, runs out slowly and stops at once off the ice.</summary>
    public class IceSlideModelTests
    {
        private static readonly IceSlideConfig Config = IceSlideConfig.Default;
        private const float Dt = 0.02f;
        private const float IceFriction = 0.05f;

        [Test]
        public void FrozenPuddle_ExposesIceFriction()
        {
            var zones = new FakeTemperatureZones();
            zones.Add(Vector3.zero, new Vector3(10f, 4f, 10f), -5f);
            var field = new PuddleField(PuddleConfig.Default, 10);
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 20f);

            Assert.IsFalse(field.TryGetIceAt(Vector3.zero, out float liquidFriction), "liquid puddle is not ice");
            Assert.AreEqual(1f, liquidFriction);
            Assert.AreEqual(1f, puddle.Friction);

            field.Tick(5f, zones);
            field.Tick(5f, zones);

            Assert.IsTrue(field.TryGetIceAt(new Vector3(0.05f, 0.1f, 0f), out float iceFriction));
            Assert.AreEqual(PuddleConfig.Default.IceFriction, iceFriction, 1e-5f);
            Assert.IsFalse(field.TryGetIceAt(new Vector3(5f, 0f, 0f), out _), "off the ice");
        }

        [Test]
        public void Slide_FollowsFacingDirection()
        {
            var ice = new IceSlideModel(Config);
            ice.Enter(new Vector3(4f, 0f, 0f)); // entered moving along +X
            Vector3 facing = Vector3.forward;   // looking along +Z

            for (int i = 0; i < 100; i++)
                ice.Step(Vector3.zero, facing, IceFriction, Dt);

            Assert.Greater(Vector3.Dot(ice.Velocity.normalized, facing), 0.99f);
            Assert.Greater(ice.Velocity.magnitude, 0f);
        }

        [Test]
        public void Slide_TurnsSmoothly_NotSnapping()
        {
            var ice = new IceSlideModel(Config);
            ice.Enter(new Vector3(4f, 0f, 0f));

            ice.Step(Vector3.zero, Vector3.forward, IceFriction, Dt);

            float turned = Vector3.Angle(Vector3.right, ice.Velocity);
            Assert.AreEqual(Config.TurnSpeed * Dt, turned, 0.01f);
        }

        [Test]
        public void ForwardInput_AcceleratesAlongFacing_UpToMaxSpeed()
        {
            var ice = new IceSlideModel(Config);
            ice.Enter(Vector3.zero);
            Vector3 facing = new Vector3(1f, 0f, 1f).normalized;

            ice.Step(facing, facing, IceFriction, Dt);
            Assert.AreEqual(Config.Acceleration * Dt, ice.Velocity.magnitude, 1e-4f);
            Assert.Greater(Vector3.Dot(ice.Velocity.normalized, facing), 0.999f);

            for (int i = 0; i < 1000; i++)
                ice.Step(facing, facing, IceFriction, Dt);
            Assert.AreEqual(Config.MaxSpeed, ice.Velocity.magnitude, 1e-3f);
        }

        [Test]
        public void WithoutInput_SlideSlowsGradually()
        {
            var ice = new IceSlideModel(Config);
            ice.Enter(new Vector3(0f, 0f, 5f));

            ice.Step(Vector3.zero, Vector3.forward, IceFriction, 1f);
            float afterOneSecond = ice.Velocity.magnitude;
            Assert.Less(afterOneSecond, 5f);
            Assert.Greater(afterOneSecond, 3f, "ice brakes far less than ground");

            for (int i = 0; i < 1000; i++)
                ice.Step(Vector3.zero, Vector3.forward, IceFriction, Dt);
            Assert.AreEqual(0f, ice.Velocity.magnitude, 1e-4f, "deceleration is never zero: the slide ends");
        }

        [Test]
        public void LeavingIce_RestoresNormalMovement()
        {
            var ice = new IceSlideModel(Config);
            ice.Enter(new Vector3(0f, 0f, 5f));
            ice.Step(Vector3.forward, Vector3.forward, IceFriction, Dt);

            ice.Exit();

            Assert.IsFalse(ice.IsSliding);
            Assert.AreEqual(Vector3.zero, ice.Velocity);
            Assert.AreEqual(Vector3.zero, ice.Step(Vector3.forward, Vector3.forward, IceFriction, Dt), "off ice the model does nothing");
        }

        [Test]
        public void OriginalMovementSettings_AreNotChanged()
        {
            // The model owns only its own velocity; the config it was given (copied from the player's settings) stays as is.
            var config = new IceSlideConfig(6f, 9f, 20f, 120f);
            var ice = new IceSlideModel(config);
            ice.Enter(new Vector3(20f, 3f, 0f));
            for (int i = 0; i < 50; i++)
                ice.Step(Vector3.right, Vector3.forward, IceFriction, Dt);
            ice.Exit();

            Assert.AreEqual(6f, config.Acceleration);
            Assert.AreEqual(9f, config.MaxSpeed);
            Assert.AreEqual(20f, config.GroundDeceleration);
            Assert.AreEqual(120f, config.TurnSpeed);
        }

        [Test]
        public void EnteringIce_CarriesSpeed_ButNeverAboveMax_AndFlat()
        {
            var ice = new IceSlideModel(Config);
            ice.Enter(new Vector3(30f, 5f, 0f));

            Assert.AreEqual(0f, ice.Velocity.y);
            Assert.AreEqual(Config.MaxSpeed, ice.Velocity.magnitude, 1e-4f);
        }
    }
}
