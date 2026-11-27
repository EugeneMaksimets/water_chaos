using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Surfaces;

namespace WaterChaos.ToxicReaction
{
    /// <summary>
    /// A spreading toxic cloud from a fire's toxic reaction. Characters inside it are exposed
    /// (it is a <see cref="IToxicHazardArea"/>), and while it may continue the chain it sets off every other burning
    /// fire it reaches, once each. Its particles follow the cloud's radius and thin out at the end.
    /// </summary>
    public sealed class ToxicCloud : MonoBehaviour, IToxicHazardArea
    {
        // Seconds (s) between checks for other fires inside the cloud.
        private const float ChainCheckInterval = 0.2f;
        // Seconds (s) the particles get to fade after the cloud stops being toxic.
        private const float FadeTime = 2.5f;

        [Header("Look")]
        [Tooltip("Cloud particles; the sphere shape radius follows the cloud.")]
        [SerializeField] private ParticleSystem _particles;

        private readonly HashSet<FireExtinguishHandler> _touched = new HashSet<FireExtinguishHandler>();
        private ToxicCloudModel _model;
        private ToxicReactionSettings _settings;
        private float _bornAt;
        private float _nextChainCheck;
        private bool _registered;
        private float _baseRate;

        public ToxicCloudModel Model => _model;
        public float Age => Time.time - _bornAt;

        /// <param name="source">The fire that made the cloud; it never sets itself off again.</param>
        public void Init(ToxicCloudModel model, ToxicReactionSettings settings, FireExtinguishHandler source)
        {
            _model = model;
            _settings = settings;
            _bornAt = Time.time;
            if (source != null)
                _touched.Add(source);

            if (_particles != null)
            {
                _baseRate = _particles.emission.rateOverTimeMultiplier;
                ApplyRadius();
                _particles.Play();
            }

            ToxicHazards.Register(this);
            _registered = true;
        }

        public bool Contains(Vector3 position) => _model != null && _model.Contains(position, Age);

        private void OnDisable()
        {
            Unregister();
        }

        private void Update()
        {
            if (_model == null)
                return;

            if (!_model.IsAlive(Age))
            {
                Unregister();
                if (_particles != null && _particles.isEmitting)
                    _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (Age > _model.Lifetime + FadeTime)
                    Destroy(gameObject);
                return;
            }

            ApplyRadius();

            if (Time.time >= _nextChainCheck && ToxicChainRules.CanTriggerChain(_model.Generation, _settings.MaxChainGeneration))
            {
                _nextChainCheck = Time.time + ChainCheckInterval;
                SetOffFiresInside();
            }
        }

        private void SetOffFiresInside()
        {
            IReadOnlyList<FireExtinguishHandler> fires = FireExtinguishHandler.All;
            for (int i = 0; i < fires.Count; i++)
            {
                FireExtinguishHandler fire = fires[i];
                if (fire == null || !fire.IsBurning || _touched.Contains(fire) || !Contains(fire.transform.position))
                    continue;

                _touched.Add(fire);
                if (fire.TryGetComponent(out FireToxicReaction reaction))
                    reaction.TryReact(_model.Generation + 1);
            }
        }

        private void ApplyRadius()
        {
            if (_particles == null)
                return;

            float radius = Mathf.Max(0.05f, _model.Radius(Age));
            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.radius = radius;
            // Bigger cloud, more puffs, so it stays as dense while it spreads.
            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTimeMultiplier = _baseRate * Mathf.Clamp(radius * radius / (_model.MaxRadius * _model.MaxRadius), 0.25f, 1f);
        }

        private void Unregister()
        {
            if (!_registered)
                return;

            _registered = false;
            ToxicHazards.Unregister(this);
        }
    }
}
