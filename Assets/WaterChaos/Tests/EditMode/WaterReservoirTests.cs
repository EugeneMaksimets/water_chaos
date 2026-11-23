using NUnit.Framework;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class WaterReservoirTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void NewReservoir_HasGivenCapacityAndVolume()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 60f);

            Assert.AreEqual(100f, reservoir.Capacity);
            Assert.AreEqual(60f, reservoir.Volume);
            Assert.IsFalse(reservoir.IsEmpty);
        }

        [Test]
        public void NewReservoir_InitialVolumeAboveCapacity_IsClampedToCapacity()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 150f);

            Assert.AreEqual(100f, reservoir.Volume);
        }

        [TestCase(-10f)]
        [TestCase(float.NaN)]
        public void NewReservoir_InvalidValues_BecomeZero(float invalid)
        {
            WaterReservoir reservoir = new WaterReservoir(invalid, invalid);

            Assert.AreEqual(0f, reservoir.Capacity);
            Assert.AreEqual(0f, reservoir.Volume);
            Assert.IsTrue(reservoir.IsEmpty);
        }

        [Test]
        public void Draw_LessThanVolume_ReturnsRequestedAndReducesVolume()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 100f);

            float drawn = reservoir.Draw(30f);

            Assert.AreEqual(30f, drawn, Tolerance);
            Assert.AreEqual(70f, reservoir.Volume, Tolerance);
        }

        [Test]
        public void Draw_MoreThanVolume_ReturnsRemainingAndEmpties()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 20f);

            float drawn = reservoir.Draw(50f);

            Assert.AreEqual(20f, drawn, Tolerance);
            Assert.AreEqual(0f, reservoir.Volume);
            Assert.IsTrue(reservoir.IsEmpty);
        }

        [Test]
        public void Draw_FromEmpty_ReturnsZero()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 0f);

            Assert.AreEqual(0f, reservoir.Draw(10f));
        }

        [TestCase(-5f)]
        [TestCase(float.NaN)]
        public void Draw_InvalidRequest_ReturnsZeroAndKeepsVolume(float invalid)
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f);

            float drawn = reservoir.Draw(invalid);

            Assert.AreEqual(0f, drawn);
            Assert.AreEqual(50f, reservoir.Volume);
        }
        [Test]
        public void FillRatio_IsVolumeDividedByCapacity()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f);

            Assert.AreEqual(0.5f, reservoir.FillRatio, Tolerance);
        }

        [Test]
        public void FillRatio_DecreasesAfterDraw()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 100f);

            reservoir.Draw(25f);

            Assert.AreEqual(0.75f, reservoir.FillRatio, Tolerance);
        }

        [Test]
        public void FillRatio_ZeroCapacity_IsZero()
        {
            WaterReservoir reservoir = new WaterReservoir(0f, 0f);

            Assert.AreEqual(0f, reservoir.FillRatio);
        }

        private const float LowLevel = 0.2f;

        [Test]
        public void OutflowRatio_AboveLowLevel_IsOne()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, LowLevel);

            Assert.AreEqual(1f, reservoir.OutflowRatio);
        }

        [Test]
        public void OutflowRatio_BelowLowLevel_IsSqrtOfShareOfLowLevel()
        {
            // fill 0.05 of low level 0.2 → share 0.25 → sqrt = 0.5
            WaterReservoir reservoir = new WaterReservoir(100f, 5f, LowLevel);

            Assert.AreEqual(0.5f, reservoir.OutflowRatio, Tolerance);
        }

        [Test]
        public void OutflowRatio_ZeroLowLevel_IsAlwaysOne()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 5f, 0f);

            Assert.AreEqual(1f, reservoir.OutflowRatio);
        }

        [Test]
        public void Draw_BelowLowLevel_GivesReducedAmount()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 5f, LowLevel);

            float drawn = reservoir.Draw(4f);

            Assert.AreEqual(2f, drawn, Tolerance);
            Assert.AreEqual(3f, reservoir.Volume, Tolerance);
        }

        [TestCase(2f, 1f)]
        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        public void LowLevelRatio_IsClampedToZeroOne(float input, float expected)
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, input);

            Assert.AreEqual(expected, reservoir.LowLevelRatio);
        }

        [Test]
        public void Draw_RepeatedWithLowLevel_EmptiesInFiniteSteps()
        {
            // 50 L tank, pump wants 10 L/s at 50 Hz
            WaterReservoir reservoir = new WaterReservoir(50f, 50f, LowLevel);
            const float requestPerStep = 10f * 0.02f;

            for (int step = 0; step < 1000 && !reservoir.IsEmpty; step++)
                reservoir.Draw(requestPerStep);

            Assert.IsTrue(reservoir.IsEmpty);
        }
    }
}