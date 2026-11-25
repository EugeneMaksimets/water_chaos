using NUnit.Framework;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    public class TransferHoseModelTests
    {
        [Test]
        public void NewHose_IsStoredAndNotConnected()
        {
            TransferHoseModel model = new TransferHoseModel();

            Assert.AreEqual(TransferHoseState.Stored, model.State);
            Assert.IsFalse(model.IsConnected);
        }

        [Test]
        public void Connect_FromStored_CarriesFreeEnd()
        {
            TransferHoseModel model = new TransferHoseModel();

            Assert.IsTrue(model.TryConnect());
            Assert.AreEqual(TransferHoseState.Carried, model.State);
        }

        [Test]
        public void Connect_Twice_IsRejected()
        {
            TransferHoseModel model = new TransferHoseModel();
            model.TryConnect();

            Assert.IsFalse(model.TryConnect());
        }

        [Test]
        public void Place_WhileCarried_StaysConnected()
        {
            TransferHoseModel model = new TransferHoseModel();
            model.TryConnect();

            Assert.IsTrue(model.TryPlace());
            Assert.AreEqual(TransferHoseState.Placed, model.State);
            Assert.IsTrue(model.IsConnected);
        }

        [Test]
        public void Place_WithoutCarriedEnd_IsRejected()
        {
            TransferHoseModel stored = new TransferHoseModel();
            TransferHoseModel placed = new TransferHoseModel();
            placed.TryConnect();
            placed.TryPlace();

            Assert.IsFalse(stored.TryPlace());
            Assert.IsFalse(placed.TryPlace());
        }

        [Test]
        public void PickUpEnd_OnlyFromPlaced()
        {
            TransferHoseModel model = new TransferHoseModel();
            Assert.IsFalse(model.TryPickUpEnd());

            model.TryConnect();
            Assert.IsFalse(model.TryPickUpEnd());

            model.TryPlace();
            Assert.IsTrue(model.TryPickUpEnd());
            Assert.AreEqual(TransferHoseState.Carried, model.State);
        }

        [Test]
        public void Disconnect_FromCarriedOrPlaced_StoresHose()
        {
            TransferHoseModel carried = new TransferHoseModel();
            carried.TryConnect();
            TransferHoseModel placed = new TransferHoseModel();
            placed.TryConnect();
            placed.TryPlace();

            Assert.IsTrue(carried.TryDisconnect());
            Assert.IsTrue(placed.TryDisconnect());
            Assert.AreEqual(TransferHoseState.Stored, placed.State);
        }

        [Test]
        public void Disconnect_WhenStored_IsRejected()
        {
            Assert.IsFalse(new TransferHoseModel().TryDisconnect());
        }

        [Test]
        public void Changed_RaisedOnlyOnSuccessfulSteps()
        {
            TransferHoseModel model = new TransferHoseModel();
            int changes = 0;
            model.Changed += () => changes++;

            model.TryPlace();
            model.TryConnect();
            model.TryConnect();
            model.TryPlace();

            Assert.AreEqual(2, changes);
        }
    }
}
