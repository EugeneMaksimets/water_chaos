using UnityEngine;
using WaterChaos.Items;
using WaterChaos.Player;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Get someone out of a place (the cat out of the toxic house): done once it is put down outside the zone.
    /// Carrying it out is not enough.
    /// </summary>
    public sealed class RescueObjective : ObjectiveStep
    {
        [Header("Rescue")]
        [Tooltip("Who has to get out (a pickable item, e.g. the cat).")]
        [SerializeField] private PickableItem _target;

        [SerializeField] private PlayerInventory _inventory;

        [Tooltip("The place it has to leave (the house).")]
        [SerializeField] private MissionZone _zone;

        protected override void OnBegin()
        {
            if (_target == null || _zone == null)
                Debug.LogError($"{name}: RescueObjective needs Target and Zone references.", this);

            base.OnBegin();
        }

        protected override bool Evaluate()
        {
            if (_target == null || _zone == null)
                return false;

            bool carried = _target.IsHeld || (_inventory != null && _inventory.Model.Contains(_target));
            return !carried && _target.gameObject.activeInHierarchy && !_zone.Contains(_target.transform.position);
        }
    }
}
