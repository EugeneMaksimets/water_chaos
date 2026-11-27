using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Done once the player has looked at a burning flamethrower for a moment: one of the listed streams burns,
    /// its nozzle is in the middle of the view and close enough, for <c>Look Time</c> without a break.
    /// Used before the "gasoline!" story, so the panel opens when the player sees what happened.
    /// </summary>
    public sealed class SeeFlamethrowersObjective : ObjectiveStep
    {
        [Header("Flamethrowers")]
        [Tooltip("The teammates' nozzles that may burn.")]
        [SerializeField] private FuelStreamIgnition[] _flamethrowers = new FuelStreamIgnition[0];

        [Tooltip("How long the player must keep looking, in seconds (s).")]
        [SerializeField, Min(0f)] private float _lookTime = 1f;

        [Tooltip("Farthest a flamethrower still counts as seen, in meters (m).")]
        [SerializeField, Min(1f)] private float _maxDistance = 45f;

        [Tooltip("Part of the screen around its center that counts as looking at it (0.8 = all but the edges).")]
        [SerializeField, Range(0.1f, 1f)] private float _viewArea = 0.8f;

        private Camera _camera;
        private float _seen;

        protected override void OnBegin()
        {
            _camera = Runner.Player.GetComponentInChildren<Camera>();
            if (_camera == null)
                _camera = Camera.main;
            _seen = 0f;
            base.OnBegin();
        }

        protected override bool Evaluate()
        {
            _seen = SeesAFlamethrower() ? _seen + Time.deltaTime : 0f;
            return _seen >= _lookTime;
        }

        private bool SeesAFlamethrower()
        {
            if (_camera == null)
                return false;

            float half = _viewArea * 0.5f;
            foreach (FuelStreamIgnition flamethrower in _flamethrowers)
            {
                if (flamethrower == null || !flamethrower.IsLit)
                    continue;

                Vector3 view = _camera.WorldToViewportPoint(flamethrower.transform.position);
                if (view.z > 0f && view.z <= _maxDistance && Mathf.Abs(view.x - 0.5f) <= half && Mathf.Abs(view.y - 0.5f) <= half)
                    return true;
            }

            return false;
        }
    }
}
