using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Water pouring out of the hose end into a reservoir: particle emission follows the real flow.
    /// Coloured like the liquid the pump draws (gasoline, toxin). Look only; the volume is counted by the transfer.
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class TransferDischargeView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaterTransferController _transfer;

        [Header("Look")]
        [Tooltip("Particles per second for each liter per second of flow.")]
        [SerializeField, Min(0f)] private float _particlesPerLiter = 6f;

        [Tooltip("Particle start speed at full flow, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _maxSpeed = 2.5f;

        [Tooltip("Flow that shows the full speed, in liters per second (L/s).")]
        [SerializeField, Min(0.01f)] private float _fullFlowRate = 10f;

        private ParticleSystem _particles;
        private ParticleSystemRenderer _renderer;
        private MaterialPropertyBlock _block;
        private LiquidType? _shown;

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
            _renderer = GetComponent<ParticleSystemRenderer>();
            _block = new MaterialPropertyBlock();
            if (_transfer == null)
            {
                Debug.LogError($"{name}: TransferDischargeView needs a Transfer reference.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            float flow = _transfer.Destination != null ? _transfer.FlowRate : 0f;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.rateOverTime = flow * _particlesPerLiter;

            ParticleSystem.MainModule main = _particles.main;
            main.startSpeedMultiplier = _maxSpeed * Mathf.Clamp01(flow / _fullFlowRate);

            if (flow > 0f && !_particles.isPlaying)
                _particles.Play();

            LiquidType liquid = _transfer.Pump != null ? _transfer.Pump.Liquid : LiquidType.Water;
            if (_shown == liquid)
                return;

            _shown = liquid;
            bool tinted = LiquidStreamTint.TryGetDefaultColor(liquid, out Color color);
            LiquidStreamTint.Apply(_renderer, _block, tinted, color);
        }
    }
}
