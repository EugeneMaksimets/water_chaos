using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// The one place that knows whether a character is exposed to toxic liquid: it stands in a toxic puddle, or a
    /// toxic stream (a teammate's) hits it directly. Every source feeds the same <see cref="ToxicExposureModel"/>,
    /// so the screen effect never flickers or doubles up. Sits next to the character's collider, like every
    /// impact receiver; screen effects read <see cref="IsExposed"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToxicExposure : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Exposure")]
        [Tooltip("Direct exposure lasts this long after the last toxic hit, in seconds (s).")]
        [SerializeField, Min(0f)] private float _holdTime = 0.8f;

        [Tooltip("Seconds (s) between checks for a toxic puddle under the feet.")]
        [SerializeField, Range(0.02f, 1f)] private float _puddleCheckInterval = 0.1f;

        [Tooltip("Optional: the feet; the object's position (minus the character controller's half height) otherwise.")]
        [SerializeField] private Transform _feet;

        private ToxicExposureModel _model;
        private CharacterController _body;
        private float _nextCheck;

        /// <summary>Exposed by any source right now.</summary>
        public bool IsExposed => _model != null && _model.IsExposed(Time.time);
        public bool InToxicPuddle => _model != null && _model.InPuddle;

        private void Awake()
        {
            _model = new ToxicExposureModel(_holdTime);
            _body = GetComponent<CharacterController>();
        }

        // Called during FixedUpdate by WaterStream.
        public void ReceiveImpact(in WaterImpact impact)
        {
            if (impact.Liquid.Corrodes())
                _model.Hit(Time.time);
        }

        private void Update()
        {
            if (Time.time < _nextCheck)
                return;

            _nextCheck = Time.time + _puddleCheckInterval;
            _model.SetInPuddle(LiquidSurfaceController.IsInToxicPuddle(FeetPosition()));
        }

        private Vector3 FeetPosition()
        {
            if (_feet != null)
                return _feet.position;

            if (_body != null)
                return transform.TransformPoint(_body.center) - Vector3.up * (_body.height * 0.5f);

            return transform.position;
        }
    }
}
