using NUnit.Framework;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class WaterFlowDataTests
    {
        [Test]
        public void PositiveValues_ShouldBeStoredCorrectly()
        {
            // Arrange
            float flowRate = 5f;
            float pressure = 100000f;

            // Act
            WaterFlowData waterFlow = new WaterFlowData(flowRate, pressure);

            // Assert
            Assert.AreEqual(5f, waterFlow.FlowRate);
            Assert.AreEqual(100000f, waterFlow.Pressure);
        }

        [Test]
        public void NegativeValues_ShouldBeClampedToZero()
        {
            // Arrange
            float flowRate = -5f;
            float pressure = -100f;

            // Act
            WaterFlowData waterFlow = new WaterFlowData(flowRate, pressure);

            // Assert
            Assert.AreEqual(0f, waterFlow.FlowRate);
            Assert.AreEqual(0f, waterFlow.Pressure);
        }

        [Test]
        public void ZeroValues_ShouldRemainZero()
        {
            // Arrange
            float flowRate = 0f;
            float pressure = 0f;

            // Act
            WaterFlowData waterFlow = new WaterFlowData(flowRate, pressure);

            // Assert
            Assert.AreEqual(0f, waterFlow.FlowRate);
            Assert.AreEqual(0f, waterFlow.Pressure);
        }

        [Test]
        public void NaNValues_ShouldBeClampedToZero()
        {
            // Arrange
            float flowRate = float.NaN;
            float pressure = float.NaN;

            // Act
            WaterFlowData waterFlow = new WaterFlowData(flowRate, pressure);

            // Assert
            Assert.AreEqual(0f, waterFlow.FlowRate);
            Assert.AreEqual(0f, waterFlow.Pressure);
        }
    }
}