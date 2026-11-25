using System.Collections.Generic;
using NUnit.Framework;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    public class PumpMenuRulesTests
    {
        private readonly List<PumpMenuAction> _actions = new List<PumpMenuAction>();

        /// <summary>Hose laid out from a pump in water, motor off; each test changes what it needs.</summary>
        private List<PumpMenuAction> Collect(
            bool holdsHose = false,
            bool carriesEnd = false,
            bool outletAttached = true,
            TransferHoseState hoseState = TransferHoseState.Placed,
            bool pumpOn = false,
            bool regularOutletBusy = false,
            bool hasWater = true,
            bool destinationFull = false)
        {
            PumpMenuContext context = new PumpMenuContext(holdsHose, carriesEnd, outletAttached, hoseState, pumpOn, regularOutletBusy, hasWater, destinationFull);
            PumpMenuRules.Collect(context, _actions);
            return _actions;
        }

        [Test]
        public void PlacedHosePumpOff_OffersTurnOnAndPickUp()
        {
            CollectionAssert.AreEqual(new[] { PumpMenuAction.TurnOn, PumpMenuAction.PickUp }, Collect());
        }

        [Test]
        public void HoseInHandsFreeOutlet_OffersConnectOnly()
        {
            CollectionAssert.AreEqual(new[] { PumpMenuAction.Connect },
                Collect(holdsHose: true, outletAttached: false, hoseState: TransferHoseState.Stored));
        }

        [Test]
        public void NoHoseNothingInHands_OffersNothing()
        {
            Assert.IsEmpty(Collect(outletAttached: false, hoseState: TransferHoseState.Stored));
        }

        [Test]
        public void Connect_NotOfferedWhenOutletTaken()
        {
            CollectionAssert.DoesNotContain(Collect(holdsHose: true), PumpMenuAction.Connect);
        }

        [Test]
        public void Connect_NotOfferedWhileCarryingAnotherEnd()
        {
            CollectionAssert.DoesNotContain(
                Collect(holdsHose: true, carriesEnd: true, outletAttached: false, hoseState: TransferHoseState.Stored),
                PumpMenuAction.Connect);
        }

        [Test]
        public void Connect_NotOfferedWhileNozzleUsesSharedOutlet()
        {
            CollectionAssert.DoesNotContain(
                Collect(holdsHose: true, outletAttached: false, hoseState: TransferHoseState.Stored, regularOutletBusy: true),
                PumpMenuAction.Connect);
        }

        [Test]
        public void TurnOn_NotOfferedWhileEndIsCarried()
        {
            CollectionAssert.DoesNotContain(Collect(hoseState: TransferHoseState.Carried), PumpMenuAction.TurnOn);
        }

        [Test]
        public void TurnOn_NotOfferedWithoutWater()
        {
            CollectionAssert.DoesNotContain(Collect(hasWater: false), PumpMenuAction.TurnOn);
        }

        [Test]
        public void TurnOn_NotOfferedWhenDestinationFull()
        {
            CollectionAssert.DoesNotContain(Collect(destinationFull: true), PumpMenuAction.TurnOn);
        }

        [Test]
        public void TurnOn_NotOfferedWithoutHose()
        {
            CollectionAssert.DoesNotContain(Collect(outletAttached: false, hoseState: TransferHoseState.Stored), PumpMenuAction.TurnOn);
        }

        [Test]
        public void RunningPump_OffersTurnOffAndNoPickUp()
        {
            CollectionAssert.AreEqual(new[] { PumpMenuAction.TurnOff }, Collect(pumpOn: true));
        }

        [Test]
        public void NozzleOnRegularOutlet_TransferMenuDoesNotDriveThePump()
        {
            List<PumpMenuAction> actions = Collect(pumpOn: true, regularOutletBusy: true);

            CollectionAssert.DoesNotContain(actions, PumpMenuAction.TurnOn);
            CollectionAssert.DoesNotContain(actions, PumpMenuAction.TurnOff);
        }
    }
}
