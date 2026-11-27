using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Cartoon steam where water (plain, soapy or toxic) meets a burning fire: puffs at the contact point
    /// while the stream keeps hitting, for a moment more as the fire goes out. Display only.
    /// Sits next to the fire's collider, like every impact receiver.
    /// </summary>
    [RequireComponent(typeof(FireExtinguishHandler))]
    public sealed class FireSteamEffect : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Look")]
        [Tooltip("Looping steam particles, instantiated once per fire.")]
        [SerializeField] private ParticleSystem _steamPrefab;

        [Header("Timing")]
        [Tooltip("Steam stops this long after the last hit, in seconds (s).")]
        [SerializeField, Min(0.02f)] private float _holdTime = 0.15f;

        [Tooltip("A fire just put out still steams when hit for this long, in seconds (s).")]
        [SerializeField, Min(0f)] private float _afterOutTime = 0.6f;

        private FireExtinguishHandler _fire;
        private ParticleSystem _steam;
        private float _lastHit = float.NegativeInfinity;
        private float _lastBurning;

        /// <summary>Steam is coming out now (water keeps hitting the fire).</summary>
        public bool IsSteaming => _steam != null && _steam.isEmitting;

        private void Awake()
        {
            _fire = GetComponent<FireExtinguishHandler>();
            _lastBurning = Time.time;
        }

        public void ReceiveImpact(in WaterImpact impact)
        {
            if (!(impact.Liquid.ExtinguishingPower() > 0f) || _steamPrefab == null)
                return;

            if (!_fire.IsBurning && Time.time - _lastBurning > _afterOutTime)
                return;

            if (_steam == null)
            {
                _steam = Instantiate(_steamPrefab, transform);
                ParticleSystem.MainModule main = _steam.main;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
            }

            _steam.transform.position = impact.ContactPoint;
            _lastHit = Time.time;
            if (!_steam.isPlaying)
                _steam.Play(true);
        }

        private void Update()
        {
            if (_fire.IsBurning)
                _lastBurning = Time.time;

            if (_steam != null && _steam.isPlaying && Time.time - _lastHit > _holdTime)
                _steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
