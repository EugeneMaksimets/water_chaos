using NUnit.Framework;
using WaterChaos.Water;
using UnityEngine;

namespace WaterChaos.Tests
{
    public class WaterImpactTests
    {
        [Test]
        public void PositiveValues_ShouldBeStoredCorrectly()
        {
            // Arrange
            Vector3 point = new Vector3(1f, 2f, 3f);
            Vector3 normal = Vector3.up;
            Vector3 direction = Vector3.forward;
            float volume = 30f;
            float force = 1000f;

            // Act
            WaterImpact waterImpact = new WaterImpact(
                point,
                normal,
                direction,
                volume,
                force);
            
            // Assert
            Assert.AreEqual(volume, waterImpact.Volume);
            Assert.AreEqual(force, waterImpact.Force);
            Assert.AreEqual(point, waterImpact.ContactPoint);
            Assert.AreEqual(normal, waterImpact.Normal);
            Assert.AreEqual(direction, waterImpact.Direction);
        }

        [Test]
        public void NegativeValues_ShouldBeClampedToZero()
        {
            // Arrange
            Vector3 point = Vector3.zero;
            Vector3 normal = Vector3.up;
            Vector3 direction = Vector3.forward;
            float volume = -10f;
            float force = -100f;

            // Act
            WaterImpact waterImpact = new WaterImpact(
                point,
                normal,
                direction,
                volume,
                force);

            // Assert
            Assert.AreEqual(0f, waterImpact.Volume);
            Assert.AreEqual(0f, waterImpact.Force);
            Assert.AreEqual(point, waterImpact.ContactPoint);
            Assert.AreEqual(normal, waterImpact.Normal);
            Assert.AreEqual(direction, waterImpact.Direction);
        }

        [Test]
        public void ZeroValues_ShouldRemainZero()
        {
            // Arrange
            Vector3 point = Vector3.zero;
            Vector3 normal = Vector3.up;
            Vector3 direction = Vector3.forward;
            float volume = 0f;
            float force = 0f;

            // Act
            WaterImpact waterImpact = new WaterImpact(
                point,
                normal,
                direction,
                volume,
                force);

            // Assert
            Assert.AreEqual(0f, waterImpact.Volume);
            Assert.AreEqual(0f, waterImpact.Force);
            Assert.AreEqual(point, waterImpact.ContactPoint);
            Assert.AreEqual(normal, waterImpact.Normal);
            Assert.AreEqual(direction, waterImpact.Direction);
        }

        [Test]
        public void NaNValues_ShouldBeClampedToZero()
        {
            // Arrange
            Vector3 point = Vector3.zero;
            Vector3 normal = Vector3.up;
            Vector3 direction = Vector3.forward;
            float volume = float.NaN;
            float force = float.NaN;
            
            // Act
            WaterImpact waterImpact = new WaterImpact(
                point,
                normal,
                direction,
                volume,
                force);

            // Assert
            Assert.AreEqual(0f, waterImpact.Volume);
            Assert.AreEqual(0f, waterImpact.Force);
            Assert.AreEqual(point, waterImpact.ContactPoint);
            Assert.AreEqual(normal, waterImpact.Normal);
            Assert.AreEqual(direction, waterImpact.Direction);
        }
    }
}