using UnityEngine;
using WaterChaos.Pump;

namespace WaterChaos.Mission
{
    /// <summary>Switch a pump on.</summary>
    public sealed class PumpRunningObjective : ObjectiveStep
    {
        [Header("Pump")]
        [Tooltip("Any of these pumps counts.")]
        [SerializeField] private PumpController[] _pumps = new PumpController[0];

        protected override bool Evaluate()
        {
            foreach (PumpController pump in _pumps)
            {
                if (pump != null && pump.isTurnOn)
                    return true;
            }

            return false;
        }
    }
}
