using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Flamethrower look: while the stream is lit (<see cref="FuelStreamIgnition"/>), fills its arc with flame
    /// particles and lights the end with a flickering light. Display only: builds its own particle system and light
    /// on start, so it needs only the references and a flame material.
    /// </summary>
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so the flames follow this frame's arc
    public sealed class FlameStreamView : MonoBehaviour
    {
        // Emission is capped per frame, so a frame hitch never dumps a huge burst.
        private const int MaxEmitPerFrame = 200;
        private const int MaxParticles = 4000;

        [Header("Source")]
        [SerializeField] private WaterStream _stream;
        [SerializeField] private FuelStreamIgnition _ignition;

        [Header("Flames")]
        [Tooltip("Particle material for the flames (e.g. the fire material of the Cartoon Fire prefab).")]
        [SerializeField] private Material _flameMaterial;

        [Tooltip("Flame particles per meter of stream per second.")]
        [SerializeField, Min(0f)] private float _particlesPerMeter = 60f;

        [Tooltip("Particle size at the nozzle and at the stream end, in meters (m).")]
        [SerializeField] private Vector2 _size = new Vector2(0.2f, 0.9f);

        [Tooltip("Particle lifetime, in seconds (s).")]
        [SerializeField, Min(0.05f)] private float _lifetime = 0.35f;

        [Tooltip("Speed along the stream, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _forwardSpeed = 3f;

        [Tooltip("Upward drift of the flames, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _riseSpeed = 1.5f;

        [Header("Light")]
        [SerializeField] private Color _lightColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField, Min(0f)] private float _lightIntensity = 4f;
        [SerializeField, Min(0f)] private float _lightRange = 6f;

        private ParticleSystem _particles;
        private Light _light;
        private float _emitDebt;

        private void Awake()
        {
            if (_stream == null || _ignition == null)
            {
                Debug.LogError($"{name}: FlameStreamView needs Stream and Ignition references.", this);
                enabled = false;
                return;
            }

            if (_flameMaterial == null)
                Debug.LogWarning($"{name}: FlameStreamView has no Flame Material; flames use the default particle look.", this);

            _particles = CreateParticles();
            _light = CreateLight();
        }

        private void OnDisable()
        {
            if (_light != null)
                _light.enabled = false;
            _emitDebt = 0f;
        }

        private void LateUpdate()
        {
            Vector3[] points = _stream.Points;
            int count = _stream.VisiblePointCount;
            bool lit = _ignition.IsLit && points != null && count >= 2;

            if (_light.enabled != lit)
                _light.enabled = lit;

            if (!lit)
            {
                _emitDebt = 0f;
                return;
            }

            float length = 0f;
            for (int i = 0; i < count - 1; i++)
                length += Vector3.Distance(points[i], points[i + 1]);

            Vector3 end = points[count - 1];
            _light.transform.position = end + Vector3.up * 0.3f;
            _light.intensity = _lightIntensity * (0.75f + 0.5f * Mathf.PerlinNoise(Time.time * 8f, 0f));

            _emitDebt += _particlesPerMeter * length * Time.deltaTime;
            int emit = Mathf.Min((int)_emitDebt, MaxEmitPerFrame);
            _emitDebt -= emit;

            for (int i = 0; i < emit; i++)
                EmitAlong(points, count, length, Random.value * length);
        }

        /// <summary>Emits one flame particle at <paramref name="distance"/> meters along the arc; bigger toward the end.</summary>
        private void EmitAlong(Vector3[] points, int count, float length, float distance)
        {
            float along = length > 0f ? Mathf.Clamp01(distance / length) : 0f;

            for (int i = 0; i < count - 1; i++)
            {
                Vector3 from = points[i];
                Vector3 to = points[i + 1];
                float segment = Vector3.Distance(from, to);
                if (distance > segment && i < count - 2)
                {
                    distance -= segment;
                    continue;
                }

                float t = segment > 0f ? Mathf.Clamp01(distance / segment) : 0f;
                Vector3 direction = segment > 0f ? (to - from) / segment : Vector3.zero;

                ParticleSystem.EmitParams particle = new ParticleSystem.EmitParams
                {
                    position = Vector3.Lerp(from, to, t) + Random.insideUnitSphere * (0.1f + 0.2f * along),
                    velocity = direction * _forwardSpeed + Vector3.up * _riseSpeed + Random.insideUnitSphere * 0.5f,
                    startSize = Mathf.Lerp(_size.x, _size.y, along) * Random.Range(0.7f, 1.3f),
                    startLifetime = _lifetime * Random.Range(0.7f, 1.3f),
                    rotation = Random.Range(0f, 360f),
                    applyShapeToPosition = false,
                };
                _particles.Emit(particle, 1);
                return;
            }
        }

        private ParticleSystem CreateParticles()
        {
            GameObject holder = new GameObject("Flame Stream");
            holder.transform.SetParent(transform, false);
            holder.SetActive(false);

            ParticleSystem particles = holder.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = _lifetime;
            main.gravityModifier = 0f;
            main.maxParticles = MaxParticles;

            // Particles come only from EmitAlong.
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule color = particles.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.35f),
                    new GradientColorKey(new Color(0.6f, 0.12f, 0.05f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.1f),
                    new GradientAlphaKey(0.8f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = gradient;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 3;
            if (_flameMaterial != null)
                renderer.sharedMaterial = _flameMaterial;

            holder.SetActive(true);
            particles.Play();
            return particles;
        }

        private Light CreateLight()
        {
            GameObject holder = new GameObject("Flame Stream Light");
            holder.transform.SetParent(transform, false);

            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = _lightColor;
            light.range = _lightRange;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }
    }
}
