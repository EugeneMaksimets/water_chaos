using System;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.ToxicReaction
{
    /// <summary>
    /// Toxic liquid hitting this burning fire does not put it out: the fire reacts for a few seconds
    /// (<see cref="ToxicReactionModel"/>), gives off a spreading toxic cloud and splashes toxic puddles around.
    /// A cloud reaching another burning fire sets it off too (<see cref="TryReact"/> with a higher generation):
    /// a small blast and a smaller cloud, up to the chain limit. A cooldown after each reaction stops endless clouds.
    /// Gameplay only; <see cref="FireToxicReactionView"/> shows it. Sits next to the fire's collider, like every receiver.
    /// </summary>
    [RequireComponent(typeof(FireExtinguishHandler))]
    public sealed class FireToxicReaction : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Setup")]
        [SerializeField] private ToxicReactionSettings _settings;

        private FireExtinguishHandler _fire;
        private ToxicReactionModel _model;
        private bool _wasReacting;

        /// <summary>Raised when a reaction starts; the argument is its generation (0 = toxic liquid, 1+ = chain).</summary>
        public event Action<int> ReactionStarted;

        /// <summary>Raised when the reaction ends (time is up or the fire went out).</summary>
        public event Action ReactionEnded;

        public bool IsReacting => _model != null && _model.IsReacting(Time.time);
        public FireExtinguishHandler Fire => _fire;

        private void Awake()
        {
            _fire = GetComponent<FireExtinguishHandler>();
            if (_settings == null)
            {
                Debug.LogError($"{name}: FireToxicReaction needs Toxic Reaction Settings.", this);
                enabled = false;
                return;
            }

            _model = new ToxicReactionModel(_settings.Duration, _settings.Cooldown);
        }

        // Called during FixedUpdate by WaterStream.
        public void ReceiveImpact(in WaterImpact impact)
        {
            if (enabled && impact.Liquid.ReactsWithFire())
                TryReact(0);
        }

        /// <summary>
        /// Starts a reaction if the fire burns and is not reacting or cooling down. Returns true if it started.
        /// </summary>
        /// <param name="generation">0 for toxic liquid, 1+ when another fire's cloud sets this one off.</param>
        public bool TryReact(int generation)
        {
            if (_model == null || !_fire.IsBurning || !_model.TryStart(Time.time, generation))
                return false;

            _wasReacting = true;
            SpawnCloud(generation);
            if (generation == 0)
                Splash();
            else
                SpawnBlast();

            ReactionStarted?.Invoke(generation);
            return true;
        }

        private void Update()
        {
            if (!_wasReacting)
                return;

            if (!_fire.IsBurning)
                _model.Stop(Time.time);

            if (_model.IsReacting(Time.time))
                return;

            _wasReacting = false;
            ReactionEnded?.Invoke();
        }

        private void SpawnCloud(int generation)
        {
            if (_settings.CloudPrefab == null)
                return;

            Vector3 center = transform.position + Vector3.up * _settings.CloudHeight;
            ToxicCloud cloud = Instantiate(_settings.CloudPrefab, center, Quaternion.identity);
            cloud.Init(_settings.CreateCloud(center, generation), _settings, _fire);
        }

        private void SpawnBlast()
        {
            if (_settings.ChainBlastPrefab != null)
                Instantiate(_settings.ChainBlastPrefab, transform.position + Vector3.up * 0.5f, Quaternion.identity);
        }

        /// <summary>Boiling toxic liquid splashes out of the fire and lands as small puddles around it.</summary>
        private void Splash()
        {
            LiquidSurfaceController surfaces = LiquidSurfaceController.Instance;
            if (surfaces == null)
                return;

            for (int i = 0; i < _settings.SplashCount; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.5f, 1f) * _settings.SplashRadius;
                Vector3 point = transform.position + new Vector3(offset.x, 0.5f, offset.y);
                surfaces.AddLiquidAt(point, LiquidType.Toxic, _settings.SplashLiters);
            }
        }
    }
}
