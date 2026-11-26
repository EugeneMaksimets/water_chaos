using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Player;

namespace WaterChaos.Mission
{
    /// <summary>Pick up the listed items (axe, nozzle, pump, hose...). Each counts once it is in the inventory.</summary>
    public sealed class PickUpObjective : TargetsObjective
    {
        [Header("Pick Up")]
        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("Items to pick up; each shows the MissionMarker on it until picked.")]
        [SerializeField] private PickableItem[] _items = new PickableItem[0];

        protected override int TargetCount => _items.Length;

        protected override Component GetTarget(int index) => _items[index];

        protected override bool IsTargetDone(int index) => _inventory != null && _inventory.Model.Contains(_items[index]);

        protected override void OnBegin()
        {
            if (_inventory == null)
                Debug.LogError($"{name}: PickUpObjective needs an Inventory reference.", this);

            base.OnBegin();
        }
    }
}
