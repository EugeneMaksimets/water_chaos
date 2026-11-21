using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Water
{
    /// <summary>
    /// Simulates the water stream: builds the arc from the nozzle, finds what it hits
    /// and delivers a WaterImpact to every receiver on that object each physics step.
    /// </summary>
    public sealed class WaterStream : MonoBehaviour
    {
        // Reuse a fixed buffer to avoid allocating memory during physics queries.
        // Hits beyond this capacity cannot be collected by the current cast.
        private const int MaxHitsPerCast = 8;
        private static readonly RaycastHit[] s_hitBuffer = new RaycastHit[MaxHitsPerCast];

        [Header("Source")]
        [Tooltip("Component implementing IWaterFlowSource (e.g. PumpController).")]
        [SerializeField] 
        private MonoBehaviour _sourceBehaviour;

        [Header("Setup")]
        [SerializeField] 
        private WaterFlowTuning _tuning;
        // The nozzle defines both the stream origin and its aiming direction.
        [Tooltip("Stream start; its forward axis is the aim direction.")]
        [SerializeField]
        private Transform _nozzle;
        // Only objects on selected layers can interact with the stream.
        [SerializeField]
        private LayerMask _hitMask;

        [Header("Shape")]
        [SerializeField, Min(2)]
        private int _pointCount = 16;
        // Controls how much the stream drops relative to its total range.
        // A proportional value keeps the arc shape consistent at different pressures.
        [Tooltip("How far the stream end drops, as a fraction of range (0..1).")]
        [SerializeField, Range(0f, 1f)] 
        private float _dropPerRange = 0.2f;
        // SphereCast radius approximates the physical thickness of the stream.
        [Tooltip("Stream thickness for hit detection, in meters (m).")]
        [SerializeField, Min(0f)] 
        private float _radius = 0.1f;
        // Reused to collect all water-reactive components on the hit collider.
        // Keeping the list alive avoids creating a new collection for every impact.
        private readonly List<IWaterImpactReceiver> _receivers = 
            new List<IWaterImpactReceiver>();
        // The stream depends on the source contract rather than a specific pump implementation.
        private IWaterFlowSource _source;
        private Vector3[] _points;

        /// <summary>Arc points for visuals. Read only the first <see cref="VisiblePointCount"/>. Do not modify. </summary>
        public Vector3[] Points => _points;
        /// <summary>How many points are valid this step (0 if no water flows).</summary>
        public int VisiblePointCount { get; private set; }
        public bool HasHit { get; private set; }
        /// <summary>World position where the stream hit something.</summary>
        public Vector3 HitPoint { get; private set; }

        private void Awake()
        {
            _points = new Vector3[_pointCount];
            // Inspector stores a Unity component, while simulation works through the interface.
            _source = _sourceBehaviour as IWaterFlowSource; // TODO: maybe change logic, dont like this solution
        }

        private void OnValidate()
        {
            if(_sourceBehaviour != null && _sourceBehaviour is not IWaterFlowSource)
            {
                Debug.LogError($"{_sourceBehaviour.GetType().Name} does not implement IWaterFlowSource.", this);
                _sourceBehaviour = null;
            }
        }

        /// <summary>Connects the stream to another source. Null disconnects.</summary>
        public void SetSource(IWaterFlowSource source) => _source = source;

        private void FixedUpdate()
        {
            if (TryBuildStream(out RaycastHit hit, out Vector3 direction))
                DeliverImpact(hit, direction, _source.Output);
        }

        // The player moves and rotates in Update, so the arc from FixedUpdate is stale by render time.
        // Rebuild it every frame for visuals only; impacts are delivered in FixedUpdate.
        private void LateUpdate()
        {
            TryBuildStream(out _, out _);
        }

        /// <summary>
        /// Builds the arc from the current nozzle pose and cuts it at the first hit.
        /// Returns true if the stream hit something.
        /// </summary>
        private bool TryBuildStream(out RaycastHit hit, out Vector3 direction)
        {
            HasHit = false;
            VisiblePointCount = 0;
            hit = default;
            direction = default;

            if(_source == null || _tuning == null || _nozzle == null)
                return false;

            WaterFlowData flow = _source.Output;
            float range = _tuning.GetRange(flow.Pressure);
            if(flow.FlowRate <= 0 || range <= 0)
                return false;

            // Build the complete trajectory before collision detection.
            WaterArc.BuildPoints(
                _nozzle.position, 
                _nozzle.forward, 
                range, 
                range * _dropPerRange, 
                _points);
            VisiblePointCount = _points.Length;

            // The first segment containing a collision gives us the nearest hit along the stream.
            if(!TryFindHit(out hit, out int segment))
                return false;
            
            // Direction of the segment that hit — computed before the end point is moved.
            direction = (_points[segment + 1] - _points[segment]).normalized;

            // Cut the visible arc at the hit point.
            _points[segment + 1] = hit.point;
            // Only points up to the collision are valid for rendering.
            VisiblePointCount = segment + 2;
            HasHit = true;
            HitPoint = hit.point;

            DeliverImpact(hit, direction, flow);
            return true;
        }

        // Segments are checked in order from the nozzle, so the first segment with a hit is the closest one.
        private bool TryFindHit(out RaycastHit closest, out int segment)
        {
            // Each pair of adjacent points forms one segment.
            // The final point has no following point, so it cannot start a segment.
            for(int i = 0; i < VisiblePointCount - 1; i++)
            {
                Vector3 from = _points[i];
                Vector3 delta = _points[i + 1] - from;
                float length = delta.magnitude;
                if(length <= 0f)
                    continue;

                // Cast a sphere instead of a thin ray to account for stream thickness.
                // Direction is normalized because the cast distance is supplied separately.
                int count = Physics.SphereCastNonAlloc(
                    from, 
                    _radius, 
                    delta / length, 
                    s_hitBuffer, 
                    length, 
                    _hitMask, 
                    QueryTriggerInteraction.Ignore);

                // A single segment may overlap multiple colliders.
                // Select its nearest valid hit before moving to the next segment.
                if(TryGetClosest(count, out closest))
                {
                    segment = i;
                    return true;
                }    
            }
            closest = default;
            segment = -1;
            return false;
        }

        // SphereCastNonAlloc does not guarantee that results are ordered by distance.
        // We must explicitly select the nearest valid result.
        private static bool TryGetClosest(int count, out RaycastHit closest)
        {
            closest = default;
            float best = float.MaxValue;

            for(int i = 0; i < count; i++)
            {
                RaycastHit hit = s_hitBuffer[i];
                // distance == 0 means the sphere started inside the collider: hit.point is not valid.
                if(hit.distance <= 0f || hit.distance >= best)
                    continue;

                best = hit.distance;
                closest = hit;
            }
            return best < float.MaxValue;
        }

        private void DeliverImpact(in RaycastHit hit, Vector3 direction, WaterFlowData flow)
        {
            // Convert the current flow into an impact snapshot.
            // Volume represents the water delivered during this physics step,
            // while force represents the stream's gameplay impact strength.
            var impact = new WaterImpact(
                hit.point,
                hit.normal,
                direction,
                flow.FlowRate * Time.fixedDeltaTime,
                _tuning.GetImpactForce(flow.FlowRate));

            hit.collider.GetComponents(_receivers);
            for(int i = 0; i < _receivers.Count; i++)
            {
                _receivers[i].ReceiveImpact(impact);
            }
            _receivers.Clear();
        }

    }
}