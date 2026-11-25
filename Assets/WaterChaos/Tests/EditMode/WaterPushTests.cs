using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class WaterPushTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Force_ShouldPushAlongWaterDirection()
        {
            // Arrange
            WaterImpact impact = CreateImpact(Vector3.forward, 100f);

            // Act
            Vector3 force = WaterPush.CalculateForce(impact);

            // Assert
            Assert.AreEqual(0f, force.x, Tolerance);
            Assert.AreEqual(0f, force.y, Tolerance);
            Assert.AreEqual(100f, force.z, Tolerance);
        }

        [Test]
        public void NonNormalizedDirection_ShouldNotScaleForce()
        {
            // Arrange
            WaterImpact impact = CreateImpact(new Vector3(0f, 0f, 5f), 100f);

            // Act
            Vector3 force = WaterPush.CalculateForce(impact);

            // Assert
            Assert.AreEqual(100f, force.magnitude, Tolerance);
        }

        [Test]
        public void ZeroForce_ShouldReturnZeroVector()
        {
            // Arrange
            WaterImpact impact = CreateImpact(Vector3.forward, 0f);

            // Act
            Vector3 force = WaterPush.CalculateForce(impact);

            // Assert
            Assert.AreEqual(Vector3.zero, force);
        }

        [Test]
        public void ZeroDirection_ShouldReturnZeroVector()
        {
            // Arrange
            WaterImpact impact = CreateImpact(Vector3.zero, 100f);

            // Act
            Vector3 force = WaterPush.CalculateForce(impact);

            // Assert
            Assert.AreEqual(Vector3.zero, force);
        }

        // ── Scaled by a push profile ──

        [Test]
        public void ScaledForce_BelowMinimum_ShouldReturnZeroVector()
        {
            WaterImpact impact = CreateImpact(Vector3.forward, 49f);

            Vector3 force = WaterPush.CalculateForce(impact, 50f, 1f);

            Assert.AreEqual(Vector3.zero, force);
        }

        [Test]
        public void ScaledForce_AtMinimum_ShouldPush()
        {
            WaterImpact impact = CreateImpact(Vector3.forward, 50f);

            Vector3 force = WaterPush.CalculateForce(impact, 50f, 1f);

            Assert.AreEqual(50f, force.z, Tolerance);
        }

        [Test]
        public void ScaledForce_ShouldApplyMultiplier()
        {
            WaterImpact impact = CreateImpact(Vector3.right, 200f);

            Vector3 force = WaterPush.CalculateForce(impact, 0f, 0.25f);

            Assert.AreEqual(50f, force.x, Tolerance);
            Assert.AreEqual(50f, force.magnitude, Tolerance);
        }

        [Test]
        public void ScaledForce_NegativeOrNaNMultiplier_ShouldReturnZeroVector()
        {
            WaterImpact impact = CreateImpact(Vector3.forward, 100f);

            Assert.AreEqual(Vector3.zero, WaterPush.CalculateForce(impact, 0f, -1f));
            Assert.AreEqual(Vector3.zero, WaterPush.CalculateForce(impact, 0f, float.NaN));
        }

        private static WaterImpact CreateImpact(Vector3 direction, float force)
        {
            return new WaterImpact(Vector3.zero, Vector3.up, direction, 1f, force);
        }
    }
}