using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Neutralize the gasoline in a tank: pump toxic liquid in (gasoline + toxic turn into water, see
    /// <see cref="LiquidTypeExtensions.MixWith"/>) and throw soap in (it dissolves only in water).
    /// Done when the tank holds soapy water; the teammates' streams follow the tank by themselves.
    /// </summary>
    public sealed class NeutralizeObjective : ObjectiveStep
    {
        [Header("Tank")]
        [Tooltip("The tank with gasoline that feeds the teammates.")]
        [SerializeField] private WaterReservoirComponent _tank;

        [Header("Markers")]
        [Tooltip("Shown until the gasoline is neutralized (e.g. on the toxic tank).")]
        [SerializeField] private MissionMarker _toxinMarker;

        [Tooltip("Shown until the soap is dissolved in the tank (on the soap).")]
        [SerializeField] private MissionMarker _soapMarker;

        private int _doneCount;

        protected override string Progress => $"{_doneCount}/2";

        protected override bool Evaluate()
        {
            WaterReservoir tank = _tank != null ? _tank.Reservoir : null;
            bool neutralized = tank != null && !tank.Liquid.IsFlammable();
            bool soapy = tank != null && tank.IsSoapy;

            SetMarkerShown(_toxinMarker, !neutralized);
            SetMarkerShown(_soapMarker, !soapy);

            _doneCount = (neutralized ? 1 : 0) + (soapy ? 1 : 0);
            return neutralized && soapy;
        }
    }
}
