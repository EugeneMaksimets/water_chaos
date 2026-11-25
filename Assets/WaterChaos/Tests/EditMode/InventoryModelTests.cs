using NUnit.Framework;
using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Tests
{
    public class InventoryModelTests
    {
        private sealed class FakeItem : IPickableItem
        {
            public Sprite Icon => null;
            public float Mass => 1f;
            public float Radius => 0f;
            public void PickUp() { }
            public void Release(Vector3 position, Vector3 impulse) { }
        }

        private InventoryModel _inventory;

        [SetUp]
        public void SetUp()
        {
            _inventory = new InventoryModel(3);
        }

        [Test]
        public void NewInventory_IsEmptyAndFirstSlotSelected()
        {
            Assert.AreEqual(3, _inventory.Capacity);
            Assert.AreEqual(0, _inventory.SelectedIndex);
            Assert.IsNull(_inventory.SelectedItem);
            Assert.IsFalse(_inventory.IsFull);
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void NewInventory_CapacityBelowOne_BecomesOne(int capacity)
        {
            Assert.AreEqual(1, new InventoryModel(capacity).Capacity);
        }

        [Test]
        public void TryAdd_ToEmptyInventory_PutsItemInFirstSlot()
        {
            FakeItem item = new FakeItem();

            Assert.IsTrue(_inventory.TryAdd(item));
            Assert.AreSame(item, _inventory.GetItem(0));
        }

        [Test]
        public void TryAdd_ThreeItems_FillsAllSlots()
        {
            FakeItem a = new FakeItem(), b = new FakeItem(), c = new FakeItem();

            Assert.IsTrue(_inventory.TryAdd(a));
            Assert.IsTrue(_inventory.TryAdd(b));
            Assert.IsTrue(_inventory.TryAdd(c));

            Assert.AreSame(a, _inventory.GetItem(0));
            Assert.AreSame(b, _inventory.GetItem(1));
            Assert.AreSame(c, _inventory.GetItem(2));
            Assert.IsTrue(_inventory.IsFull);
        }

        [Test]
        public void TryAdd_WhenFull_RejectsFourthItem()
        {
            FillInventory();
            FakeItem extra = new FakeItem();

            Assert.IsFalse(_inventory.TryAdd(extra));
            for (int i = 0; i < _inventory.Capacity; i++)
                Assert.AreNotSame(extra, _inventory.GetItem(i));
        }

        [Test]
        public void TryAdd_SameItemTwice_IsRejected()
        {
            FakeItem item = new FakeItem();
            _inventory.TryAdd(item);

            Assert.IsFalse(_inventory.TryAdd(item));
            Assert.IsNull(_inventory.GetItem(1));
        }

        [Test]
        public void TryAdd_Null_IsRejected()
        {
            Assert.IsFalse(_inventory.TryAdd(null));
        }

        [Test]
        public void TryAdd_RaisesSlotChangedWithIndex()
        {
            int changed = -1;
            _inventory.SlotChanged += index => changed = index;

            _inventory.TryAdd(new FakeItem());

            Assert.AreEqual(0, changed);
        }

        [Test]
        public void RemoveSelected_ReturnsItemAndEmptiesSlot()
        {
            FakeItem item = new FakeItem();
            _inventory.TryAdd(item);

            Assert.AreSame(item, _inventory.RemoveSelected());
            Assert.IsNull(_inventory.GetItem(0));
        }

        [Test]
        public void RemoveSelected_EmptySlot_ReturnsNullWithoutEvent()
        {
            bool raised = false;
            _inventory.SlotChanged += _ => raised = true;

            Assert.IsNull(_inventory.RemoveSelected());
            Assert.IsFalse(raised);
        }

        [Test]
        public void Select_FilledSlot_MakesItsItemSelected()
        {
            FillInventory();
            int selected = -1;
            _inventory.SelectionChanged += index => selected = index;

            Assert.IsTrue(_inventory.Select(2));

            Assert.AreEqual(2, _inventory.SelectedIndex);
            Assert.AreSame(_inventory.GetItem(2), _inventory.SelectedItem);
            Assert.AreEqual(2, selected);
        }

        [Test]
        public void Select_EmptySlot_IsAllowedAndHasNoItem()
        {
            Assert.IsTrue(_inventory.Select(1));

            Assert.AreEqual(1, _inventory.SelectedIndex);
            Assert.IsNull(_inventory.SelectedItem);
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void Select_OutOfRange_KeepsSelection(int index)
        {
            Assert.IsFalse(_inventory.Select(index));
            Assert.AreEqual(0, _inventory.SelectedIndex);
        }

        [Test]
        public void TryAdd_AfterRemove_ReusesFreedSlot()
        {
            FillInventory();
            _inventory.Select(1);
            _inventory.RemoveSelected();
            FakeItem item = new FakeItem();

            Assert.IsTrue(_inventory.TryAdd(item));
            Assert.AreSame(item, _inventory.GetItem(1));
        }

        [Test]
        public void Contains_TrueOnlyWhileItemIsStored()
        {
            FakeItem item = new FakeItem();
            Assert.IsFalse(_inventory.Contains(item));

            _inventory.TryAdd(item);
            Assert.IsTrue(_inventory.Contains(item));

            _inventory.RemoveSelected();
            Assert.IsFalse(_inventory.Contains(item));
            Assert.IsFalse(_inventory.Contains(null));
        }

        private void FillInventory()
        {
            for (int i = 0; i < _inventory.Capacity; i++)
                _inventory.TryAdd(new FakeItem());
        }
    }
}