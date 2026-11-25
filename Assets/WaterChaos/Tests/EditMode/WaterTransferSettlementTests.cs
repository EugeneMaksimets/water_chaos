using NUnit.Framework;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    /// <summary>Settling water the pump already drew: <c>drawn</c> is taken from the source by hand, as the pump would.</summary>
    public class WaterTransferSettlementTests
    {
        private const float Tolerance = 0.0001f;

        private static TransferResult DrawAndSettle(WaterReservoir source, WaterReservoir destination, float requested)
        {
            float drawn = source.Draw(requested);
            return WaterTransferSettlement.Settle(source, destination, drawn);
        }

        [Test]
        public void Destination_ReceivesEverythingThatFits()
        {
            WaterReservoir a = new WaterReservoir(1000f, 500f);
            WaterReservoir b = new WaterReservoir(1000f, 100f);

            TransferResult result = DrawAndSettle(a, b, 10f);

            Assert.AreEqual(10f, result.Delivered, Tolerance);
            Assert.AreEqual(490f, a.Volume, Tolerance);
            Assert.AreEqual(110f, b.Volume, Tolerance);
        }

        [Test]
        public void PartialCapacity_TakesOnlyFreeSpace_RestGoesBack()
        {
            WaterReservoir a = new WaterReservoir(1000f, 500f);
            WaterReservoir b = new WaterReservoir(100f, 95f);

            TransferResult result = DrawAndSettle(a, b, 10f);

            Assert.AreEqual(5f, result.Delivered, Tolerance);
            Assert.AreEqual(5f, result.Returned, Tolerance);
            Assert.AreEqual(0f, result.Discharged, Tolerance);
            Assert.AreEqual(100f, b.Volume, Tolerance);
            Assert.AreEqual(495f, a.Volume, Tolerance);
        }

        [Test]
        public void FullDestination_SourceKeepsItsWater()
        {
            WaterReservoir a = new WaterReservoir(1000f, 500f);
            WaterReservoir b = new WaterReservoir(100f, 100f);

            DrawAndSettle(a, b, 10f);

            Assert.AreEqual(500f, a.Volume, Tolerance);
            Assert.AreEqual(100f, b.Volume, Tolerance);
        }

        [Test]
        public void NoDestination_WaterIsDischargedIntoEnvironment()
        {
            WaterReservoir a = new WaterReservoir(1000f, 500f);

            TransferResult result = DrawAndSettle(a, null, 10f);

            Assert.AreEqual(10f, result.Discharged, Tolerance);
            Assert.AreEqual(0f, result.Delivered);
            Assert.AreEqual(490f, a.Volume, Tolerance);
        }

        [TestCase(0f)]
        [TestCase(-5f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidOrZeroAmount_MovesNothing(float drawn)
        {
            WaterReservoir a = new WaterReservoir(1000f, 500f);
            WaterReservoir b = new WaterReservoir(1000f, 100f);

            TransferResult result = WaterTransferSettlement.Settle(a, b, drawn);

            Assert.AreEqual(0f, result.Delivered);
            Assert.AreEqual(500f, a.Volume);
            Assert.AreEqual(100f, b.Volume);
        }

        [Test]
        public void SameReservoir_KeepsVolume()
        {
            WaterReservoir a = new WaterReservoir(1000f, 500f);

            DrawAndSettle(a, a, 10f);

            Assert.AreEqual(500f, a.Volume, Tolerance);
        }
    }
}
