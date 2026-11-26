using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Mission
{
    /// <summary>Dissolve a metal object (the grate) with toxic liquid; done once it is destroyed.</summary>
    public sealed class CorrodeObjective : ObjectiveStep
    {
        [Header("Corrosion")]
        [SerializeField] private CorrodibleObject _target;

        protected override string Progress =>
            _target != null && _target.Progress > 0f && !_target.IsDestroyed ? $"{Mathf.RoundToInt(_target.Progress * 100f)}%" : null;

        protected override bool Evaluate() => _target == null || _target.IsDestroyed;

        protected override void OnBegin()
        {
            if (_target == null)
                Debug.LogError($"{name}: CorrodeObjective has no Target; the objective is skipped.", this);

            base.OnBegin();
        }
    }
}
