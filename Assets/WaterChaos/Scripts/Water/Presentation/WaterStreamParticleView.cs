using UnityEngine;

namespace WaterChaos.Water
{
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so the splash uses this frame's hit
    /// <summary>
    /// Plays a splash where the water stream hits a surface. Display only: all data comes from WaterStream.
    /// </summary>
    public sealed class WaterStreamParticleView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        private WaterStream _stream;

        [Header("Particles")]
        [SerializeField]
        private ParticleSystem _splashParticles;

        [Tooltip("Spray at the nozzle; should be a child of the nozzle so it follows it")]
        [SerializeField]
        private ParticleSystem _nozzleSpray;

        private void LateUpdate()
        {
            // Fewer than 2 points means no water is flowing
            bool flowing = _stream != null && _stream.VisiblePointCount >= 2;

            SetPlaying(_nozzleSpray, flowing);

            if (_splashParticles == null)
                return;

            if (!flowing || !_stream.HasHit)
            {
                SetPlaying(_splashParticles, false);
                return;
            }

            // The splash cone is emitted along +Z, so look along the surface normal
            _splashParticles.transform.SetPositionAndRotation(
                _stream.HitPoint,
                Quaternion.LookRotation(_stream.HitNormal));
            SetPlaying(_splashParticles, true);
        }

        private static void SetPlaying(ParticleSystem particles, bool play)
        {
            if (particles == null)
                return;

            if (play && !particles.isPlaying)
                particles.Play();
            else if (!play && particles.isPlaying)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
