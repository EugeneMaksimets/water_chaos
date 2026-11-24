using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Physical side of a portable pump: each physics step checks whether the intake sits in usable water
    /// and hands that water body's reservoir to the pump (or nothing). Does not move the pump.
    /// </summary>
    [RequireComponent(typeof(PumpController))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PumpDeployment : MonoBehaviour
    {
        private const int BufferSize = 8;

        [Header("Intake")]
        [Tooltip("Point that must be under water (e.g. the bottom of the pump). Empty = this transform.")]
        [SerializeField] private Transform _intake;

        [Tooltip("How deep the intake must be below the surface, in meters (m).")]
        [SerializeField, Min(0f)] private float _requiredSubmersion = 0.15f;

        [Tooltip("Smallest water column at the intake, surface to bed, in meters (m).")]
        [SerializeField, Min(0f)] private float _minWaterDepth = 0.4f;

        [Tooltip("Largest angle between the pump's up and world up, in degrees. 180 = any angle.")]
        [SerializeField, Range(0f, 180f)] private float _maxTiltAngle = 180f;

        [Header("Detection")]
        [Tooltip("Layers of WaterBody trigger volumes.")]
        [SerializeField] private LayerMask _waterMask = 1 << 4;

        [Tooltip("Layers of the ground under water, used to measure water depth. Exclude Water and Player.")]
        [SerializeField] private LayerMask _bedMask = 1;

        private readonly Collider[] _overlaps = new Collider[BufferSize];
        private readonly RaycastHit[] _hits = new RaycastHit[BufferSize];

        private PumpController _pump;
        private Rigidbody _body;
        private PumpIntakeRules _rules;

        /// <summary>Raised when the intake status changes; argument is the new status.</summary>
        public event Action<IntakeStatus> StatusChanged;

        public IntakeStatus Status { get; private set; } = IntakeStatus.NotInWater;

        /// <summary>Water body the intake is in (even if unusable); null if none.</summary>
        public WaterBody CurrentBody { get; private set; }

        /// <summary>Depth of the intake below the surface, in meters (m); 0 when not in water.</summary>
        public float Submersion { get; private set; }

        private Transform Intake => _intake != null ? _intake : transform;

        private void Awake()
        {
            _pump = GetComponent<PumpController>();
            _body = GetComponent<Rigidbody>();
            _rules = new PumpIntakeRules(_requiredSubmersion, _minWaterDepth, _maxTiltAngle);
        }

        private void FixedUpdate()
        {
            Evaluate();
        }

        private void OnDisable()
        {
            // Picked up into the inventory: the pump leaves the water.
            CurrentBody = null;
            Submersion = 0f;
            SetStatus(IntakeStatus.NotInWater);
            if (_pump != null)
                _pump.SetReservoir(null);
        }

        private void Evaluate()
        {
            Vector3 point = Intake.position;
            CurrentBody = FindDeepestBody(point);

            IntakeStatus status;
            if (CurrentBody == null || CurrentBody.Reservoir == null)
            {
                Submersion = 0f;
                status = IntakeStatus.NotInWater;
            }
            else
            {
                Submersion = CurrentBody.GetDepth(point);
                float waterDepth = CurrentBody.SurfaceHeight - FindBedHeight(point, CurrentBody.BottomHeight);
                float tilt = Vector3.Angle(transform.up, Vector3.up);
                status = _rules.Evaluate(true, Submersion, waterDepth, tilt, CurrentBody.Reservoir.IsEmpty);
            }

            SetStatus(status);
            _pump.SetReservoir(status == IntakeStatus.Submerged ? CurrentBody.Reservoir : null);
        }

        /// <summary>Of all water bodies the intake is really inside, the one where it is deepest.</summary>
        private WaterBody FindDeepestBody(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, 0.01f, _overlaps, _waterMask, QueryTriggerInteraction.Collide);

            WaterBody best = null;
            float bestDepth = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                if (!_overlaps[i].TryGetComponent(out WaterBody body) || !body.Contains(point))
                    continue;

                float depth = body.GetDepth(point);
                if (depth > bestDepth)
                {
                    best = body;
                    bestDepth = depth;
                }
            }

            return best;
        }

        /// <summary>Height of the first solid surface under the point (ignoring the pump itself), or the volume bottom.</summary>
        private float FindBedHeight(Vector3 point, float volumeBottom)
        {
            float maxDistance = point.y - volumeBottom;
            if (maxDistance <= 0f)
                return volumeBottom;

            int count = Physics.RaycastNonAlloc(point, Vector3.down, _hits, maxDistance, _bedMask, QueryTriggerInteraction.Ignore);

            float bed = volumeBottom;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].rigidbody == _body)
                    continue;

                bed = Mathf.Max(bed, _hits[i].point.y);
            }

            return bed;
        }

        private void SetStatus(IntakeStatus status)
        {
            if (status == Status)
                return;

            Status = status;
            StatusChanged?.Invoke(status);
        }

        private void OnValidate()
        {
            _rules = new PumpIntakeRules(_requiredSubmersion, _minWaterDepth, _maxTiltAngle);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Status == IntakeStatus.Submerged ? Color.cyan : Color.yellow;
            Gizmos.DrawWireSphere(Intake.position, 0.08f);
        }
    }
}
