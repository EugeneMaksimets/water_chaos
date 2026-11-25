using UnityEngine;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Water pouring out of the hose end into a reservoir: particle emission follows the real flow.
    /// Look only; the volume is counted by the transfer, not by particles.
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

        private void Awake()
        {
            _particles = GetComponent<ParticleSystem>();
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
        }
    }
}
