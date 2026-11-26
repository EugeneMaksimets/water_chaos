using UnityEngine;
using WaterChaos.Player;
using WaterChaos.Water;

namespace WaterChaos.Mission
{
    /// <summary>Connect the nozzle hose to one of the listed points (the truck outlet, a pump).</summary>
    public sealed class NozzleConnectObjective : ObjectiveStep
    {
        [Header("Connect")]
        [SerializeField] private PlayerHoseConnector _connector;

        [Tooltip("Any of these points counts.")]
        [SerializeField] private WaterConnectionPoint[] _points = new WaterConnectionPoint[0];

        protected override bool Evaluate()
        {
            if (_connector == null || _connector.Connected == null)
                return false;

            return System.Array.IndexOf(_points, _connector.Connected) >= 0;
        }
    }
}
