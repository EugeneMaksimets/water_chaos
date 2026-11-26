using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Bubbles on a reservoir's water surface while its water is soapy (<see cref="WaterReservoir.IsSoapy"/>),
    /// so the player sees the soap worked. Display only: reads the reservoir, never changes it.
    /// </summary>
    public sealed class SoapyReservoirView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private WaterReservoirComponent _reservoir;

        [Header("Look")]
        [Tooltip("Looping bubble effect over the surface.")]
        [SerializeField] private ParticleSystem _bubbles;

        [Tooltip("Visible water (moved by the level or surface view); the bubbles sit on its top. " +
                 "Empty = they stay where they were placed.")]
        [SerializeField] private Renderer _surface;

        private void Awake()
        {
            if (_reservoir == null || _bubbles == null)
            {
                Debug.LogError($"{name}: SoapyReservoirView needs Reservoir and Bubbles references.", this);
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            WaterReservoir reservoir = _reservoir.Reservoir;
            bool show = reservoir != null && reservoir.IsSoapy && !reservoir.IsEmpty;

            if (show)
            {
                if (_surface != null)
                {
                    Vector3 position = _bubbles.transform.position;
                    position.y = _surface.bounds.max.y;
                    _bubbles.transform.position = position;
                }

                if (!_bubbles.isEmitting)
                    _bubbles.Play(true);
            }
            else if (_bubbles.isEmitting)
            {
                _bubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
