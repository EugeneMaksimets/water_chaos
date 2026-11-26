using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Mission
{
    /// <summary>Story event: empties a tank at once (the fire truck after the house fire). Happens once.</summary>
    public sealed class DrainReservoirStep : MissionStep
    {
        [Header("Tank")]
        [SerializeField] private WaterReservoirComponent _tank;

        protected override void OnTick()
        {
            if (_tank != null && _tank.Reservoir != null)
                _tank.Reservoir.Empty();
            else
                Debug.LogError($"{name}: DrainReservoirStep needs a Tank with a reservoir.", this);

            Complete();
        }
    }
}
