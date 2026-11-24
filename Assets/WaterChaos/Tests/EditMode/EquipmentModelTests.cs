using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Tests
{
    public class EquipmentModelTests
    {
        private sealed class FakeItem : IPickableItem
        {
            public Sprite Icon => null;
            public float Mass => 1f;
            public void PickUp() { }
            public void Release(Vector3 position, Vector3 impulse) { }
        }

        private InventoryModel _inventory;
        private EquipmentModel _equipment;
        private List<string> _log;
        private FakeItem _nozzle;
        private FakeItem _pump;

        [SetUp]
        public void SetUp()
        {
            _inventory = new InventoryModel(3);
            _equipment = new EquipmentModel(_inventory);
            _log = new List<string>();
            _nozzle = new FakeItem();
            _pump = new FakeItem();

            _equipment.Equipped += item => _log.Add("equip " + Name(item));
            _equipment.Unequipped += item => _log.Add("unequip " + Name(item));
        }

        private string Name(IPickableItem item) => item == _nozzle ? "nozzle" : item == _pump ? "pump" : "?";

        [Test]
        public void Sync_EmptySelectedSlot_EquipsNothing()
        {
            _equipment.Sync();

            Assert.IsNull(_equipment.Current);
            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void Sync_ItemInSelectedSlot_EquipsIt()
        {
            _inventory.TryAdd(_nozzle);

            _equipment.Sync();

            Assert.AreSame(_nozzle, _equipment.Current);
            CollectionAssert.AreEqual(new[] { "equip nozzle" }, _log);
        }

        [Test]
        public void Sync_NothingChanged_RaisesNoEvents()
        {
            _inventory.TryAdd(_nozzle);
            _equipment.Sync();
            _log.Clear();

            _equipment.Sync();
            _equipment.Sync();

            CollectionAssert.IsEmpty(_log);
        }

        [Test]
        public void Sync_SwitchToAnotherItem_UnequipsPreviousBeforeEquippingNew()
        {
            _inventory.TryAdd(_nozzle);
            _inventory.TryAdd(_pump);
            _equipment.Sync();
            _log.Clear();

            _inventory.Select(1);
            _equipment.Sync();

            Assert.AreSame(_pump, _equipment.Current);
            CollectionAssert.AreEqual(new[] { "unequip nozzle", "equip pump" }, _log);
        }

        [Test]
        public void Sync_SwitchToEmptySlot_UnequipsAndLeavesHandsEmpty()
        {
            _inventory.TryAdd(_nozzle);
            _equipment.Sync();
            _log.Clear();

            _inventory.Select(2);
            _equipment.Sync();

            Assert.IsNull(_equipment.Current);
            CollectionAssert.AreEqual(new[] { "unequip nozzle" }, _log);
        }

        [Test]
        public void Sync_SelectedItemRemoved_UnequipsIt()
        {
            _inventory.TryAdd(_nozzle);
            _equipment.Sync();
            _log.Clear();

            _inventory.RemoveSelected();
            _equipment.Sync();

            Assert.IsNull(_equipment.Current);
            CollectionAssert.AreEqual(new[] { "unequip nozzle" }, _log);
        }

        [Test]
        public void Sync_SwitchBack_EquipsItemAgain()
        {
            _inventory.TryAdd(_nozzle);
            _equipment.Sync();
            _inventory.Select(1);
            _equipment.Sync();
            _log.Clear();

            _inventory.Select(0);
            _equipment.Sync();

            Assert.AreSame(_nozzle, _equipment.Current);
            CollectionAssert.AreEqual(new[] { "equip nozzle" }, _log);
        }

        [Test]
        public void Sync_RepeatedSwitching_KeepsAtMostOneItemEquipped()
        {
            _inventory.TryAdd(_nozzle);
            _inventory.TryAdd(_pump);
            int equipped = 0;
            int maxEquipped = 0;
            _equipment.Equipped += _ => maxEquipped = Mathf.Max(maxEquipped, ++equipped);
            _equipment.Unequipped += _ => equipped--;

            for (int i = 0; i < 10; i++)
            {
                _inventory.Select(i % 3);
                _equipment.Sync();
            }

            Assert.AreEqual(1, maxEquipped);
            Assert.LessOrEqual(equipped, 1);
        }

        [Test]
        public void Sync_CurrentIsAlreadyClearedWhileUnequippedRuns()
        {
            _inventory.TryAdd(_nozzle);
            _equipment.Sync();
            IPickableItem currentDuringUnequip = _nozzle;
            _equipment.Unequipped += _ => currentDuringUnequip = _equipment.Current;

            _inventory.Select(1);
            _equipment.Sync();

            Assert.IsNull(currentDuringUnequip);
        }
    }
}
