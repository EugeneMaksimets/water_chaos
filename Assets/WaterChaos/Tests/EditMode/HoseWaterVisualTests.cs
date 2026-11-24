using NUnit.Framework;
using WaterChaos.Hose;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class HoseWaterVisualTests
    {
        private const float Diameter = 0.05f;
        private const float ReferencePressure = 200_000f;

        [Test]
        public void ZeroFlow_ShouldGiveNoSpeedAndNoFullness()
        {
            WaterFlowData flow = new WaterFlowData(0f, 300_000f);

            Assert.AreEqual(0f, HoseWaterVisual.WaterSpeed(flow, Diameter));
            Assert.AreEqual(0f, HoseWaterVisual.Fullness(flow, ReferencePressure));
        }

        [Test]
        public void WaterSpeed_ShouldBeFlowRateDividedByCrossSection()
        {
            // 10 L/s = 0.01 m³/s through a 5 cm hose: 0.01 / (π · 0.025²) ≈ 5.09 m/s
            WaterFlowData flow = new WaterFlowData(10f, 100_000f);

            Assert.AreEqual(5.093f, HoseWaterVisual.WaterSpeed(flow, Diameter), 0.01f);
        }

        [Test]
        public void WaterSpeed_ShouldGrowWithFlowRate()
        {
            float slow = HoseWaterVisual.WaterSpeed(new WaterFlowData(2f, 100_000f), Diameter);
            float fast = HoseWaterVisual.WaterSpeed(new WaterFlowData(4f, 100_000f), Diameter);

            Assert.AreEqual(slow * 2f, fast, 0.0001f);
        }

        [Test]
        public void WaterSpeed_WithInvalidDiameter_ShouldBeZero()
        {
            WaterFlowData flow = new WaterFlowData(10f, 100_000f);

            Assert.AreEqual(0f, HoseWaterVisual.WaterSpeed(flow, 0f));
        }

        [Test]
        public void Fullness_AtReferencePressure_ShouldBeOne()
        {
            WaterFlowData flow = new WaterFlowData(5f, ReferencePressure);

            Assert.AreEqual(1f, HoseWaterVisual.Fullness(flow, ReferencePressure), 0.0001f);
        }

        [Test]
        public void Fullness_AbovePressureReference_ShouldClampToOne()
        {
            WaterFlowData flow = new WaterFlowData(5f, ReferencePressure * 3f);

            Assert.AreEqual(1f, HoseWaterVisual.Fullness(flow, ReferencePressure), 0.0001f);
        }

        [Test]
        public void Fullness_WhenFlowingAtLowPressure_ShouldStillBeVisible()
        {
            WaterFlowData flow = new WaterFlowData(5f, 0f);

            Assert.AreEqual(HoseWaterVisual.MinFullness, HoseWaterVisual.Fullness(flow, ReferencePressure), 0.0001f);
        }

        [Test]
        public void Fullness_ShouldGrowWithPressure()
        {
            float low = HoseWaterVisual.Fullness(new WaterFlowData(5f, 50_000f), ReferencePressure);
            float high = HoseWaterVisual.Fullness(new WaterFlowData(5f, 150_000f), ReferencePressure);

            Assert.Greater(high, low);
        }
    }
}
