using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>Go to a place (into the house, to the teammates); done once the player is inside the zone.</summary>
    public sealed class EnterZoneObjective : ObjectiveStep
    {
        [Header("Zone")]
        [SerializeField] private MissionZone _zone;

        protected override bool Evaluate() => _zone != null && _zone.Contains(Runner.Player.position);

        protected override void OnBegin()
        {
            if (_zone == null)
                Debug.LogError($"{name}: EnterZoneObjective needs a Zone.", this);

            base.OnBegin();
        }
    }
}
