using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Plays an extra effect where the stream hits while it carries one particular liquid
    /// (bubbling fumes for toxic liquid), on top of the normal splash. Display only: all data comes from WaterStream,
    /// and the effect has no colliders, so it never takes part in gameplay.
    /// </summary>
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so the effect uses this frame's hit
    public sealed class LiquidImpactEffect : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private WaterStream _stream;

        [Header("Effect")]
        [Tooltip("The effect plays only while the stream carries this liquid.")]
        [SerializeField] private LiquidType _liquid = LiquidType.Toxic;

        [Tooltip("Looping effect, emitted along its +Z; moved to the hit point and turned along the surface normal.")]
        [SerializeField] private ParticleSystem _effect;

        private void Awake()
        {
            if (_stream == null || _effect == null)
            {
                Debug.LogError($"{name}: LiquidImpactEffect needs Stream and Effect references.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_effect != null)
                _effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void LateUpdate()
        {
            // Fewer than 2 points means nothing is flowing.
            bool show = _stream.VisiblePointCount >= 2 && _stream.HasHit && _stream.Flow.Liquid == _liquid;
            if (!show)
            {
                if (_effect.isPlaying)
                    _effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }

            _effect.transform.SetPositionAndRotation(_stream.HitPoint, Quaternion.LookRotation(_stream.HitNormal));
            if (!_effect.isPlaying)
                _effect.Play();
        }
    }
}
