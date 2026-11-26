using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Mission
{
    /// <summary>Break the door with the axe (the door's own mechanic); done once it is broken.</summary>
    public sealed class BreakDoorObjective : ObjectiveStep
    {
        [Header("Door")]
        [SerializeField] private BreakableDoor _door;

        protected override string Progress =>
            _door != null && _door.Progress > 0f && !_door.IsBroken ? $"{Mathf.RoundToInt(_door.Progress * 100f)}%" : null;

        protected override bool Evaluate() => _door == null || _door.IsBroken;

        protected override void OnBegin()
        {
            if (_door == null)
                Debug.LogError($"{name}: BreakDoorObjective has no Door; the objective is skipped.", this);

            base.OnBegin();
        }
    }
}
