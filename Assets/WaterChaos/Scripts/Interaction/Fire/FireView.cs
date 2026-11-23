using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Scales all child fire particles and the light by the fire intensity. Presentation only.
    /// </summary>
    public sealed class FireView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        private FireExtinguishHandler _fire;

        [Header("Visuals")]
        [SerializeField]
        private Light _light;

        private ParticleSystem[] _particles;
        private float[] _baseEmissionRates;
        private float _baseLightIntensity;

        private void Awake()
        {
            _particles = GetComponentsInChildren<ParticleSystem>();
            _baseEmissionRates = new float[_particles.Length];
            for (int i = 0; i < _particles.Length; i++)
                _baseEmissionRates[i] = _particles[i].emission.rateOverTimeMultiplier;

            if (_light != null)
                _baseLightIntensity = _light.intensity;
        }

        private void Update()
        {
            float intensity = _fire.Intensity;

            for (int i = 0; i < _particles.Length; i++)
            {
                ParticleSystem.EmissionModule emission = _particles[i].emission;
                emission.rateOverTimeMultiplier = _baseEmissionRates[i] * intensity;
            }

            if (_light != null)
                _light.intensity = _baseLightIntensity * intensity;
        }
    }
}