using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Extra soap bubbles while the stream carries soapy water (<see cref="WaterFlowData.IsSoapy"/>):
    /// small bubbles drifting off the arc and a foam of bubbles where it hits, which builds up the longer you spray.
    /// The normal water stream stays as it is. Display only: all data comes from <see cref="WaterStream"/>,
    /// and the particles have no collision, so they never take part in gameplay.
    /// </summary>
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so bubbles follow this frame's arc
    public sealed class SoapBubbleStreamView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private WaterStream _stream;

        [Header("Intensity")]
        [Tooltip("Multiplier of every bubble rate below; 0 = no bubbles.")]
        [SerializeField, Min(0f)] private float _intensity = 1f;

        [Header("Stream bubbles")]
        [Tooltip("World-space particle system the stream bubbles are emitted into (its own emission is not used).")]
        [SerializeField] private ParticleSystem _streamBubbles;

        [Tooltip("Bubbles per second along the whole stream.")]
        [SerializeField, Min(0f)] private float _spawnRate = 45f;

        [Tooltip("Bubble diameter range, in meters (m).")]
        [SerializeField] private Vector2 _sizeRange = new Vector2(0.03f, 0.1f);

        [Tooltip("Bubble lifetime range, in seconds (s).")]
        [SerializeField] private Vector2 _lifetimeRange = new Vector2(0.6f, 1.3f);

        [Tooltip("How far from the stream center bubbles appear, in meters (m).")]
        [SerializeField, Min(0f)] private float _spread = 0.08f;

        [Tooltip("Speed bubbles keep along the stream, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _carrySpeed = 1.5f;

        [Tooltip("Random drift speed of the bubbles, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _drift = 0.35f;

        [Header("Impact bubbles")]
        [Tooltip("Looping effect moved to the hit point and turned along the surface normal (+Z).")]
        [SerializeField] private ParticleSystem _impactBubbles;

        [Tooltip("Impact bubbles per second when spraying starts.")]
        [SerializeField, Min(0f)] private float _impactRate = 25f;

        [Tooltip("Rate multiplier after spraying the same spot for a while (foam builds up).")]
        [SerializeField, Min(1f)] private float _maxFoamMultiplier = 3f;

        [Tooltip("Time of continuous spraying to reach the full foam, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _foamBuildUpTime = 3f;

        private float _emitBudget;
        private float _foam;

        private void Awake()
        {
            if (_stream == null || _streamBubbles == null)
            {
                Debug.LogError($"{name}: SoapBubbleStreamView needs Stream and Stream Bubbles references.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_impactBubbles != null)
                _impactBubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void LateUpdate()
        {
            WaterFlowData flow = _stream.Flow;
            // Fewer than 2 points means nothing is flowing.
            bool soapy = flow.IsSoapy && flow.FlowRate > 0f && _stream.VisiblePointCount >= 2 && _intensity > 0f;

            if (soapy)
                EmitAlongStream(Time.deltaTime);
            else
                _emitBudget = 0f;

            UpdateImpact(soapy && _stream.HasHit, Time.deltaTime);
        }

        private void EmitAlongStream(float deltaTime)
        {
            _emitBudget += _spawnRate * _intensity * deltaTime;
            int count = Mathf.FloorToInt(_emitBudget);
            if (count <= 0)
                return;

            _emitBudget -= count;
            Vector3[] points = _stream.Points;
            int segments = _stream.VisiblePointCount - 1;

            for (int i = 0; i < count; i++)
            {
                int segment = Random.Range(0, segments);
                Vector3 a = points[segment];
                Vector3 b = points[segment + 1];
                Vector3 along = b - a;

                var emit = new ParticleSystem.EmitParams
                {
                    position = Vector3.Lerp(a, b, Random.value) + Random.insideUnitSphere * _spread,
                    velocity = along.normalized * _carrySpeed + Random.insideUnitSphere * _drift + Vector3.up * (_drift * 0.5f),
                    startSize = Random.Range(_sizeRange.x, _sizeRange.y),
                    startLifetime = Random.Range(_lifetimeRange.x, _lifetimeRange.y),
                    applyShapeToPosition = false,
                };
                _streamBubbles.Emit(emit, 1);
            }
        }

        private void UpdateImpact(bool show, float deltaTime)
        {
            _foam = Mathf.Clamp01(_foam + (show ? deltaTime : -deltaTime) / _foamBuildUpTime);
            if (_impactBubbles == null)
                return;

            if (!show)
            {
                if (_impactBubbles.isEmitting)
                    _impactBubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            _impactBubbles.transform.SetPositionAndRotation(_stream.HitPoint, Quaternion.LookRotation(_stream.HitNormal));
            ParticleSystem.EmissionModule emission = _impactBubbles.emission;
            emission.rateOverTime = _impactRate * _intensity * Mathf.Lerp(1f, _maxFoamMultiplier, _foam);
            if (!_impactBubbles.isEmitting)
                _impactBubbles.Play(true);
        }
    }
}
