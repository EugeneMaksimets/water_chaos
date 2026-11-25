using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Makes every collider under this object (tank walls, floor, water volume) count as the given reservoir
    /// for <see cref="WaterReservoirDetector"/>. Only a link: the liters stay in the reservoir.
    /// </summary>
    public sealed class WaterReservoirTarget : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reservoir shown when any collider under this object is looked at.")]
        [SerializeField] private WaterReservoirComponent _reservoir;

        public WaterReservoirComponent Reservoir => _reservoir;
    }
}
