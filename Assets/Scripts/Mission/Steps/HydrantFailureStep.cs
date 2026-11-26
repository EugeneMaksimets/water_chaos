using UnityEngine;
using WaterChaos.Hydrant;
using WaterChaos.Splitter;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Story event: the hydrant network runs dry. Its supply falls smoothly to zero, so everything fed from it
    /// (the splitter and the teammates' nozzles) weakens and stops; then the fixed hose from the hydrant
    /// comes off the splitter, so the player can plug their own hose in. Happens once.
    /// </summary>
    public sealed class HydrantFailureStep : MissionStep
    {
        [Header("Hydrant")]
        [SerializeField] private HydrantController _hydrant;

        [Tooltip("Time for the supply to fall from full to zero, in seconds (s).")]
        [SerializeField, Min(0f)] private float _duration = 4f;

        [Tooltip("Optional: the hose from the hydrant to the splitter, taken off at the end.")]
        [SerializeField] private FixedHoseFeed _releaseFeed;

        private float _elapsed;
        private float _startSupply;

        protected override void OnBegin()
        {
            if (_hydrant == null)
                Debug.LogError($"{name}: HydrantFailureStep needs a Hydrant.", this);

            _elapsed = 0f;
            _startSupply = _hydrant != null ? _hydrant.SupplyRatio : 0f;
        }

        protected override void OnTick()
        {
            _elapsed += Time.deltaTime;
            float t = _duration > 0f ? Mathf.Clamp01(_elapsed / _duration) : 1f;

            if (_hydrant != null)
                _hydrant.SetSupplyRatio(Mathf.Lerp(_startSupply, 0f, t));

            if (t < 1f)
                return;

            if (_releaseFeed != null)
                _releaseFeed.Disconnect();
            Complete();
        }
    }
}
