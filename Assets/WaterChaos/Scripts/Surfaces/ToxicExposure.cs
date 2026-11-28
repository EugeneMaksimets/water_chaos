using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Surfaces
{
    /// <summary>
    /// The one place that knows whether a character is exposed to toxic liquid: it stands in a toxic puddle or touches toxic liquid in a reservoir, breathes
    /// toxic air (<see cref="ToxicHazards"/>, e.g. a cloud from a toxic fire), or a toxic stream (a teammate's) hits it. Every source feeds the same <see cref="ToxicExposureModel"/>,
    /// so the screen effect never flickers or doubles up. Sits next to the character's collider, like every
    /// impact receiver; screen effects read <see cref="IsExposed"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToxicExposure : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Exposure")]
        [Tooltip("Direct exposure lasts this long after the last toxic hit, in seconds (s).")]
        [SerializeField, Min(0f)] private float _holdTime = 0.8f;

        [Tooltip("Seconds (s) between checks for a toxic puddle under the feet and toxic air around the body.")]
        [SerializeField, Range(0.02f, 1f)] private float _puddleCheckInterval = 0.1f;

        [Tooltip("Optional: the feet; the object's position (minus the character controller's half height) otherwise.")]
        [SerializeField] private Transform _feet;

        [Header("Reservoirs")]
        [Tooltip("Layers of the reservoirs' liquid volumes (triggers with a WaterReservoirComponent).")]
        [SerializeField] private LayerMask _liquidMask = 1 << 4;

        [Tooltip("Reach of the body (m) when checking for touching a reservoir's liquid.")]
        [SerializeField, Min(0.01f)] private float _touchRadius = 0.3f;

        private static readonly Collider[] s_liquidHits = new Collider[8];

        private ToxicExposureModel _model;
        private CharacterController _body;
        private float _nextCheck;

        /// <summary>Exposed by any source right now (never while protected, e.g. by a gas mask).</summary>
        public bool IsExposed => !IsProtected && _model != null && _model.IsExposed(Time.time);

        /// <summary>Protected from every toxic source (wears a gas mask).</summary>
        public bool IsProtected { get; private set; }

        public void SetProtected(bool isProtected)
        {
            IsProtected = isProtected;
        }
        public bool InToxicPuddle => _model != null && _model.InPuddle;
        public bool InToxicCloud => _model != null && _model.InCloud;

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
            _model.SetInPuddle(LiquidSurfaceController.IsInToxicPuddle(FeetPosition()) || TouchesToxicReservoir());
            _model.SetInCloud(ToxicHazards.AnyContains(BodyPosition()));
        }

        // Any body part from the feet to the chest inside a reservoir that holds toxic liquid.
        private bool TouchesToxicReservoir()
        {
            int count = Physics.OverlapCapsuleNonAlloc(FeetPosition(), BodyPosition(), _touchRadius, s_liquidHits,
                _liquidMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                WaterReservoirComponent tank = s_liquidHits[i].GetComponent<WaterReservoirComponent>();
                WaterReservoir reservoir = tank != null ? tank.Reservoir : null;
                if (reservoir != null && !reservoir.IsEmpty && reservoir.Liquid == LiquidType.Toxic)
                    return true;
            }

            return false;
        }

        private Vector3 BodyPosition()
        {
            return _body != null ? transform.TransformPoint(_body.center) : transform.position + Vector3.up;
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
