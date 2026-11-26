using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Runs its child steps at the same time (e.g. "switch on the coolers" while "put out the fires").
    /// Done when every non-optional child is done; optional children still running are then dropped.
    /// </summary>
    public sealed class StepGroup : MissionStep
    {
        private MissionStep[] _children;

        protected override void OnBegin()
        {
            _children = CollectChildren(transform);
            if (_children.Length == 0)
                Debug.LogWarning($"{name}: StepGroup has no child steps.", this);

            foreach (MissionStep child in _children)
                child.Begin(Runner);
        }

        protected override void OnTick()
        {
            bool hasRequired = false;
            bool requiredDone = true;
            bool allDone = true;

            foreach (MissionStep child in _children)
            {
                child.Tick();

                allDone &= child.IsDone;
                if (!child.IsOptional)
                {
                    hasRequired = true;
                    requiredDone &= child.IsDone;
                }
            }

            if (hasRequired ? requiredDone : allDone)
                Complete();
        }

        protected override void OnEnd(bool completed)
        {
            foreach (MissionStep child in _children)
                child.Abort();
        }
    }
}
