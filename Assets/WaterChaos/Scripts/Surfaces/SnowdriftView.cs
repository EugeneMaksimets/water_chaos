using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// Draws one snowdrift: a squashed snow mound sized by the drift, growing and melting smoothly.
    /// Display only, no colliders.
    /// </summary>
    public sealed class SnowdriftView : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("Mound of diameter 1 m and height 1 m, its pivot at the bottom; scaled to the drift.")]
        [SerializeField] private Transform _mound;

        [Tooltip("Optional: a puff of powder each time the drift grows.")]
        [SerializeField] private ParticleSystem _powder;

        [Header("Look")]
        [Tooltip("How fast the mound follows the drift's size, in meters per second (m/s).")]
        [SerializeField, Min(0.01f)] private float _resizeSpeed = 0.8f;

        [Tooltip("Lowest shown height, in meters (m), so a thin drift is still visible.")]
        [SerializeField, Min(0f)] private float _minHeight = 0.03f;

        private SnowdriftModel _model;
        private Vector2 _shown;

        public void Bind(SnowdriftModel model)
        {
            _model = model;
            _model.Changed += OnChanged;
            _shown = new Vector2(model.Radius * 0.5f, 0f);
            Apply();
        }

        private void OnDestroy()
        {
            if (_model != null)
                _model.Changed -= OnChanged;
        }

        private void OnChanged(SnowdriftModel model)
        {
            if (_powder != null && !_powder.isEmitting && model.Fill < 1f)
                _powder.Play();
        }

        private void Update()
        {
            if (_model == null)
                return;

            var target = new Vector2(_model.Radius, Mathf.Max(_minHeight, _model.Height));
            if (_shown == target)
                return;

            float step = _resizeSpeed * Time.deltaTime;
            _shown = new Vector2(Mathf.MoveTowards(_shown.x, target.x, step), Mathf.MoveTowards(_shown.y, target.y, step));
            Apply();
        }

        private void Apply()
        {
            if (_mound != null)
                _mound.localScale = new Vector3(_shown.x * 2f, _shown.y, _shown.x * 2f);
        }
    }
}
