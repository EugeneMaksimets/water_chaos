using NUnit.Framework;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    public class TransferRulesTests
    {
        private static TransferStatus Evaluate(
            TransferHoseState state = TransferHoseState.Placed,
            bool outletBusy = false,
            bool sourceEmpty = false,
            bool hasWater = true,
            bool hasDestination = false,
            bool destinationFull = false,
            bool pumpOn = true,
            float flowRate = 10f)
        {
            return TransferRules.Evaluate(state, outletBusy, sourceEmpty, hasWater, hasDestination, destinationFull, pumpOn, flowRate);
        }

        [Test]
        public void CanDeliver_OnlyPlacedFreeOutletWithRoom()
        {
            Assert.IsTrue(TransferRules.CanDeliver(TransferHoseState.Placed, false, false));
            Assert.IsFalse(TransferRules.CanDeliver(TransferHoseState.Carried, false, false));
            Assert.IsFalse(TransferRules.CanDeliver(TransferHoseState.Stored, false, false));
            Assert.IsFalse(TransferRules.CanDeliver(TransferHoseState.Placed, true, false));
            Assert.IsFalse(TransferRules.CanDeliver(TransferHoseState.Placed, false, true));
        }

        [Test]
        public void Stored_IsNotConnected() => Assert.AreEqual(TransferStatus.NotConnected, Evaluate(TransferHoseState.Stored));

        [Test]
        public void Carried_IsDeploying() => Assert.AreEqual(TransferStatus.Deploying, Evaluate(TransferHoseState.Carried));

        [Test]
        public void NozzleOnRegularOutlet_IsOutletBusy() => Assert.AreEqual(TransferStatus.OutletBusy, Evaluate(outletBusy: true));

        [Test]
        public void EmptySource_BeforeNoWater() => Assert.AreEqual(TransferStatus.SourceEmpty, Evaluate(sourceEmpty: true, hasWater: false));

        [Test]
        public void NoWater_IsNoWater() => Assert.AreEqual(TransferStatus.NoWater, Evaluate(hasWater: false));

        [Test]
        public void FullDestination_IsDestinationFull() => Assert.AreEqual(TransferStatus.DestinationFull, Evaluate(hasDestination: true, destinationFull: true, pumpOn: false));

        [Test]
        public void PumpOff_IsReady() => Assert.AreEqual(TransferStatus.Ready, Evaluate(pumpOn: false, flowRate: 0f));

        [TestCase(0f)]
        [TestCase(float.NaN)]
        public void PumpOnWithoutFlow_IsInterrupted(float flow) => Assert.AreEqual(TransferStatus.Interrupted, Evaluate(flowRate: flow));

        [Test]
        public void IntoReservoir_IsTransferring() => Assert.AreEqual(TransferStatus.TransferringToReservoir, Evaluate(hasDestination: true));

        [Test]
        public void WithoutReservoir_IsDischarging() => Assert.AreEqual(TransferStatus.DischargingToEnvironment, Evaluate());

        [Test]
        public void TargetPressure_TenLitersOnTenLiterPump_IsMaxPressure()
        {
            Assert.AreEqual(500_000f, TransferRules.TargetPressureFor(10f, 10f, 500_000f), 0.01f);
        }

        [Test]
        public void TargetPressure_TenLitersOnTwentyLiterPump_IsHalf()
        {
            Assert.AreEqual(250_000f, TransferRules.TargetPressureFor(10f, 20f, 500_000f), 0.01f);
        }

        [Test]
        public void TargetPressure_MoreThanPumpCanGive_IsCappedAtMax()
        {
            Assert.AreEqual(500_000f, TransferRules.TargetPressureFor(30f, 10f, 500_000f), 0.01f);
        }

        [TestCase(0f, 10f, 500000f)]
        [TestCase(10f, 0f, 500000f)]
        [TestCase(float.NaN, 10f, 500000f)]
        [TestCase(10f, 10f, float.PositiveInfinity)]
        public void TargetPressure_InvalidInput_IsZero(float flow, float maxFlow, float maxPressure)
        {
            Assert.AreEqual(0f, TransferRules.TargetPressureFor(flow, maxFlow, maxPressure));
        }
    }
}
