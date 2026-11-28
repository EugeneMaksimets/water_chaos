using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Runs the hose while it is connected: follows the player end, wraps around obstacles,
    /// and tells player movement how far it may go. Does not move the player itself.
    /// The hose lies on the ground, so its path (and its wrapping) runs a little above the ground below
    /// the source and the nozzle, not through the air between them: it goes round a car or a post at its foot.
    /// </summary>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class HoseController : MonoBehaviour
    {
        /// <summary>The path updates in LateUpdate after other scripts; the view runs right after this.</summary>
        public const int ExecutionOrder = 50;

        [Header("References")]
        [Tooltip("Where the hose meets the player, e.g. the nozzle.")]
        [SerializeField] private Transform _endPoint;

        [Tooltip("Player root: its colliders never block the hose.")]
        [SerializeField] private Transform _playerRoot;

        [Header("Length")]
        [Tooltip("Physical hose length, in meters (m).")]
        [SerializeField, Min(1f)] private float _maxLength = 15f;

        [Tooltip("Free hose needed before the 'length reached' state clears, in meters (m). Stops flickering.")]
        [SerializeField, Min(0f)] private float _releaseMargin = 0.3f;

        [Header("Wrapping")]
        [Tooltip("Layers the hose wraps around. Leave out the ground, Player and Water layers.")]
        [SerializeField] private LayerMask _obstacleMask = ~0;

        [Tooltip("Gap between a wrap point and the obstacle surface, in meters (m). Keep it above half the hose width.")]
        [SerializeField, Min(0.01f)] private float _surfaceOffset = 0.08f;

        [Tooltip("Wrap points the hose may hold at once.")]
        [SerializeField, Range(1, 64)] private int _maxWrapPoints = 32;

        [Header("Ground")]
        [Tooltip("Height above the ground where the hose looks for obstacles, in meters (m). Above curbs, below car hoods and railings.")]
        [SerializeField, Range(0.05f, 1.5f)] private float _sweepHeight = 0.4f;

        [Tooltip("How far below the source and the nozzle the ground is searched, in meters (m). No ground: the path stays at their height.")]
        [SerializeField, Min(0.1f)] private float _groundProbe = 3f;

        private HoseModel _model;
        private HosePathSolver _solver;
        private PhysicsHoseObstacleQuery _query;
        private Transform _anchor;

        // The item the hose end belongs to (e.g. the nozzle): when it lies in the world, its own colliders never block the hose.
        private Transform _owner;
        private bool _blockedThisFrame;

        /// <summary>Raised when the hose starts or stops holding the player back.</summary>
        public event Action<bool> LimitReachedChanged;

        public bool IsConnected => _anchor != null && _model != null;

        /// <summary>The player tried to go past the full length and has not yet given some hose back.</summary>
        public bool IsLimitReached { get; private set; }

        /// <summary>Current path and length, laid near the ground (see the class summary); null until Awake.</summary>
        public HoseModel Model => _model;

        /// <summary>Where the hose really starts (the connection point), world space; meaningful while connected.</summary>
        public Vector3 StartPosition => _anchor != null ? _anchor.position : _model.Anchor;

        /// <summary>True if the collider belongs to the source, the player or the nozzle: it never blocks this hose.</summary>
        public bool Ignores(Collider collider) => _query != null && _query.IsIgnored(collider);

        /// <summary>Where the hose really ends (e.g. the nozzle), world space.</summary>
        public Vector3 EndPosition => _endPoint.position;

        /// <summary>Water source the hose start is attached to (pump, hydrant); null when disconnected.</summary>
        public IWaterFlowSource Source { get; private set; }

        private void Awake()
        {
            if (_endPoint == null)
            {
                Debug.LogError($"{name}: HoseController needs an End Point reference.", this);
                enabled = false;
                return;
            }

            Rigidbody ownerBody = _endPoint.GetComponentInParent<Rigidbody>();
            _owner = ownerBody != null ? ownerBody.transform : null;

            _model = new HoseModel(_maxLength);
            _query = new PhysicsHoseObstacleQuery(_obstacleMask);
            _solver = new HosePathSolver(_query, _surfaceOffset, _maxWrapPoints);
        }

        /// <summary>Attaches the hose start to this connection point; null detaches.</summary>
        public void Connect(WaterConnectionPoint point)
        {
            if (point == null || _model == null)
            {
                Disconnect();
                return;
            }

            _anchor = point.HoseAnchor;
            Source = point.Source;

            // The object the point belongs to (pump, hydrant, the whole splitter) never blocks its own hose.
            Component source = point.Source as Component;
            Rigidbody sourceBody = source != null ? source.GetComponentInParent<Rigidbody>() : null;
            Transform ignored = sourceBody != null ? sourceBody.transform : source != null ? source.transform : _anchor;
            _query.SetIgnored(ignored, _playerRoot, _owner);

            _model.Reset(OnGround(_anchor.position), OnGround(_endPoint.position));
            SetLimitReached(false);
        }

        /// <summary>
        /// Lays the hose again in a straight line from its start to its end, dropping the bends it had
        /// (e.g. after the teammate holding it was lifted over a roof and landed again).
        /// </summary>
        public void ResetPath()
        {
            if (IsConnected && _model != null)
                _model.Reset(OnGround(_anchor.position), OnGround(_endPoint.position));
        }

        public void Disconnect()
        {
            _anchor = null;
            Source = null;
            _blockedThisFrame = false;
            SetLimitReached(false);
        }

        /// <summary>
        /// Called by player movement with this frame's move, in meters (m).
        /// Returns the part of it the hose allows; unchanged when disconnected.
        /// </summary>
        public Vector3 ConstrainMove(Vector3 delta)
        {
            if (!enabled || !IsConnected)
                return delta;

            Vector3 allowed = HoseMovementConstraint.Constrain(
                _model.LastFixedPoint, _model.FreeSegmentLength, OnGround(_endPoint.position), delta, out bool blocked);

            _blockedThisFrame |= blocked;
            return allowed;
        }

        private void LateUpdate()
        {
            if (!IsConnected)
                return;

            // The source left the world (e.g. the pump was picked up): the hose cannot stay attached to nothing.
            if (!_anchor.gameObject.activeInHierarchy)
            {
                Disconnect();
                return;
            }

            _model.SetAnchor(OnGround(_anchor.position));
            Vector3 hands = _endPoint.position;
            _solver.Step(_model, HosePathEnd.Choose(_query, _model.LastFixedPoint, hands, OnGround(hands)));

            if (_blockedThisFrame)
                SetLimitReached(true);
            else if (_model.RemainingLength > _releaseMargin)
                SetLimitReached(false);

            _blockedThisFrame = false;
        }

        /// <summary>The point moved down to the sweep height above the ground below it; unchanged without ground.</summary>
        private Vector3 OnGround(Vector3 point)
        {
            if (_query.Linecast(point, point + Vector3.down * _groundProbe, out Vector3 ground, out _))
                point.y = Mathf.Min(point.y, ground.y + _sweepHeight);

            return point;
        }

        private void SetLimitReached(bool value)
        {
            if (IsLimitReached == value)
                return;

            IsLimitReached = value;
            LimitReachedChanged?.Invoke(value);
        }
    }
}
