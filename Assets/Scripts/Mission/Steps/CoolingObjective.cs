using UnityEngine;
using WaterChaos.Rooms;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Switch on the listed fridges and air conditioners. The room temperature reacts by itself
    /// (<see cref="RoomTemperature"/>); this objective only counts the appliances that are on.
    /// </summary>
    public sealed class CoolingObjective : TargetsObjective
    {
        [Header("Cooling")]
        [Tooltip("Appliances to switch on; each shows the MissionMarker on it until it is on. Add or remove freely.")]
        [SerializeField] private CoolingAppliance[] _appliances = new CoolingAppliance[0];

        protected override int TargetCount => _appliances.Length;

        protected override Component GetTarget(int index) => _appliances[index];

        protected override bool IsTargetDone(int index) => _appliances[index].IsOn;
    }
}
