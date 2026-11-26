using UnityEngine;

namespace WaterChaos.Water
{
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so the splash uses this frame's hit
    /// <summary>
    /// Plays a splash where the water stream hits a surface. Display only: all data comes from WaterStream.
    /// Disabled, it shows nothing (e.g. while the stream burns as a flamethrower).
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

        [Tooltip("One-shot burst at the nozzle when water starts flowing; should be a child of the nozzle. Looping off.")]
        [SerializeField]
        private ParticleSystem _startBurst;

        private bool _wasFlowing;

        private void OnDisable()
        {
            Hide(_nozzleSpray);
            Hide(_startBurst);
            Hide(_splashParticles);
            _wasFlowing = false;
        }

        private static void Hide(ParticleSystem particles)
        {
            if (particles != null)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            // Fewer than 2 points means no water is flowing
            bool flowing = _stream != null && _stream.VisiblePointCount >= 2;

            if (flowing && !_wasFlowing && _startBurst != null)
                _startBurst.Play();
            _wasFlowing = flowing;

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
