using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.Mission
{
    /// <summary>Put a pump into one of the water bodies (toxic tank, a pool, underground tank) deep enough to draw from it.</summary>
    public sealed class PumpInWaterObjective : ObjectiveStep
    {
        [Header("Pump")]
        [Tooltip("Any of these pumps counts.")]
        [SerializeField] private PumpDeployment[] _pumps = new PumpDeployment[0];

        [Tooltip("Any of these water bodies counts.")]
        [SerializeField] private WaterBody[] _bodies = new WaterBody[0];

        protected override bool Evaluate()
        {
            foreach (PumpDeployment pump in _pumps)
            {
                if (pump != null && pump.Status == IntakeStatus.Submerged
                    && System.Array.IndexOf(_bodies, pump.CurrentBody) >= 0)
                    return true;
            }

            return false;
        }
    }
}
