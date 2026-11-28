using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Dissolve a metal object (the grate) with toxic liquid; done once it is destroyed,
    /// or once the player got past it another way (e.g. through a dissolved fence) into the skip zone.
    /// </summary>
    public sealed class CorrodeObjective : ObjectiveStep
    {
        [Header("Corrosion")]
        [SerializeField] private CorrodibleObject _target;

        [Tooltip("Optional: the objective also counts as done once the player is inside this zone (got there another way).")]
        [SerializeField] private MissionZone _skipZone;

        protected override string Progress =>
            _target != null && _target.Progress > 0f && !_target.IsDestroyed ? $"{Mathf.RoundToInt(_target.Progress * 100f)}%" : null;

        protected override bool Evaluate() =>
            _target == null || _target.IsDestroyed || (_skipZone != null && _skipZone.Contains(Runner.Player.position));

        protected override void OnBegin()
        {
            if (_target == null)
                Debug.LogError($"{name}: CorrodeObjective has no Target; the objective is skipped.", this);

            base.OnBegin();
        }
    }
}
