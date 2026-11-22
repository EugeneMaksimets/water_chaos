using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Scales fire particles and light by the fire intensity. Presentation only.
    /// </summary>
    public sealed class FireView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        private FireExtinguishHandler _fire;

        [Header("Visuals")]
        [SerializeField]
        private ParticleSystem _particles;
        [SerializeField]
        private Light _light;

        private ParticleSystem.EmissionModule _emission;
        private float _baseEmissionRate;
        private float _baseLightIntensity;

        private void Awake()
        {
            if(_particles != null)
            {
                _emission = _particles.emission;
                _baseEmissionRate = _emission.rateOverTimeMultiplier;
            }

            if(_light != null)
                _baseLightIntensity = _light.intensity;
        }

        private void Update()
        {
            float intensity = _fire.Intensity;

            if(_particles != null)
                _emission.rateOverTimeMultiplier = _baseEmissionRate * intensity;

            if(_light != null)
                _light.intensity = _baseLightIntensity * intensity;
        }
    }
}