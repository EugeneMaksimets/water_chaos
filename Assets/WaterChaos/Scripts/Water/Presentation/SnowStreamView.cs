using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// While the stream comes out as snow (<see cref="WaterStream.IsSnow"/>) hides the water look
    /// (tube, splash) and blows cartoon snowflakes along the arc, with a puff of powder where it lands.
    /// Display only: the arc, hits and snow state all come from WaterStream, the particles have no collision.
    /// Hides renderers (not components), so it never fights the flame view, which switches components.
    /// </summary>
    [DefaultExecutionOrder(110)] // after WaterStream.LateUpdate and the other stream views
    public sealed class SnowStreamView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private WaterStream _stream;

        [Header("Water Look")]
        [Tooltip("Renderers of the water look hidden while it snows (the tube, the splash, the nozzle spray).")]
        [SerializeField] private Renderer[] _waterRenderers = new Renderer[0];

        [Header("Snow")]
        [Tooltip("Snowflakes; world simulation space, emission off (flakes are emitted along the arc from code).")]
        [SerializeField] private ParticleSystem _snowflakes;

        [Tooltip("Looping powder puff, emitted along its +Z; moved to the hit point.")]
        [SerializeField] private ParticleSystem _impactPowder;

        [Tooltip("Flakes emitted per second along the whole arc.")]
        [SerializeField, Min(0f)] private float _flakesPerSecond = 220f;

        [Tooltip("Flake speed along the arc, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _flakeSpeed = 6f;

        [Tooltip("How far flakes scatter from the arc, in meters (m).")]
        [SerializeField, Min(0f)] private float _spread = 0.12f;

        private bool[] _wasEnabled;
        private bool _snowing;
        private float _emitDebt;

        private void Awake()
        {
            if (_stream == null || _snowflakes == null)
            {
                Debug.LogError($"{name}: SnowStreamView needs Stream and Snowflakes references.", this);
                enabled = false;
                return;
            }

            _wasEnabled = new bool[_waterRenderers.Length];
        }

        private void OnDisable()
        {
            SetSnowing(false);
        }

        private void LateUpdate()
        {
            bool snowing = _stream.IsSnow && _stream.VisiblePointCount >= 2;
            SetSnowing(snowing);
            if (!snowing)
                return;

            // Some water views switch their renderer back on every frame (the line view): keep them hidden.
            HideWater();
            EmitAlongArc();
            UpdatePowder();
        }

        private void SetSnowing(bool snowing)
        {
            if (snowing == _snowing)
                return;

            _snowing = snowing;
            for (int i = 0; i < _waterRenderers.Length; i++)
            {
                Renderer part = _waterRenderers[i];
                if (part == null)
                    continue;

                if (snowing)
                {
                    _wasEnabled[i] = part.enabled;
                    part.enabled = false;
                }
                else
                {
                    part.enabled = _wasEnabled[i];
                }
            }

            if (!snowing)
            {
                _emitDebt = 0f;
                if (_impactPowder != null)
                    _impactPowder.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void HideWater()
        {
            foreach (Renderer part in _waterRenderers)
            {
                if (part != null && part.enabled)
                    part.enabled = false;
            }
        }

        private void EmitAlongArc()
        {
            Vector3[] points = _stream.Points;
            int count = _stream.VisiblePointCount;

            _emitDebt += _flakesPerSecond * Time.deltaTime;
            int flakes = (int)_emitDebt;
            _emitDebt -= flakes;

            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < flakes; i++)
            {
                int segment = Random.Range(0, count - 1);
                Vector3 from = points[segment];
                Vector3 to = points[segment + 1];
                Vector3 direction = (to - from).normalized;

                emit.position = Vector3.Lerp(from, to, Random.value) + Random.insideUnitSphere * _spread;
                emit.velocity = direction * (_flakeSpeed * Random.Range(0.7f, 1.1f)) + Random.insideUnitSphere * 0.4f;
                _snowflakes.Emit(emit, 1);
            }
        }

        private void UpdatePowder()
        {
            if (_impactPowder == null)
                return;

            if (!_stream.HasHit)
            {
                if (_impactPowder.isEmitting)
                    _impactPowder.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            _impactPowder.transform.SetPositionAndRotation(_stream.HitPoint, Quaternion.LookRotation(_stream.HitNormal));
            if (!_impactPowder.isEmitting)
                _impactPowder.Play();
        }
    }
}
