using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Items the mission needs where they are (the tee feeding the teammates) cannot be picked up
    /// until the mission is completed.
    /// </summary>
    public sealed class MissionItemLock : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MissionRunner _runner;

        [Tooltip("Items locked in place until the mission ends.")]
        [SerializeField] private PickableItem[] _items = new PickableItem[0];

        private void Start()
        {
            SetLocked(true);
        }

        private void Update()
        {
            if (_runner == null || !_runner.IsCompleted)
                return;

            SetLocked(false);
            enabled = false;
        }

        private void SetLocked(bool locked)
        {
            foreach (PickableItem item in _items)
            {
                if (item != null)
                    item.IsLocked = locked;
            }
        }
    }
}
