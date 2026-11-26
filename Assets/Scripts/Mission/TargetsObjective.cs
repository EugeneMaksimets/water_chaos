using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Objective made of several targets (items to pick up, appliances to switch on).
    /// A target counts once it is done and stays counted; its own marker disappears at that moment.
    /// A target's marker is the <see cref="MissionMarker"/> on the target object or its children.
    /// </summary>
    public abstract class TargetsObjective : ObjectiveStep
    {
        [Header("Targets")]
        [Tooltip("How many targets must be done; 0 = all of them.")]
        [SerializeField, Min(0)] private int _requiredCount;

        private bool[] _done;
        private MissionMarker[] _targetMarkers;
        private int _doneCount;

        private int Required => _requiredCount > 0 ? Mathf.Min(_requiredCount, TargetCount) : TargetCount;

        protected abstract int TargetCount { get; }

        protected abstract bool IsTargetDone(int index);

        /// <summary>The target object; its <see cref="MissionMarker"/> is shown until the target is done.</summary>
        protected abstract Component GetTarget(int index);

        protected override string Progress => Required > 1 ? $"{_doneCount}/{Required}" : null;

        protected override void OnBegin()
        {
            base.OnBegin();

            _done = new bool[TargetCount];
            _targetMarkers = new MissionMarker[TargetCount];
            for (int i = 0; i < TargetCount; i++)
            {
                Component target = GetTarget(i);
                _targetMarkers[i] = target != null ? target.GetComponentInChildren<MissionMarker>(true) : null;
                ShowMarker(_targetMarkers[i]);
            }
        }

        protected override bool Evaluate()
        {
            _doneCount = 0;
            for (int i = 0; i < _done.Length; i++)
            {
                if (!_done[i] && GetTarget(i) != null && IsTargetDone(i))
                {
                    _done[i] = true;
                    HideMarker(_targetMarkers[i]);
                }

                if (_done[i])
                    _doneCount++;
            }

            return _doneCount >= Required;
        }
    }
}
