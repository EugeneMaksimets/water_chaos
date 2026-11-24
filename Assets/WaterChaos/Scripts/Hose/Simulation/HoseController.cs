using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Runs the hose while it is connected: follows the player end, wraps around obstacles,
    /// and tells player movement how far it may go. Does not move the player itself.
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

        private HoseModel _model;
        private HosePathSolver _solver;
        private PhysicsHoseObstacleQuery _query;
        private Transform _anchor;
        private bool _blockedThisFrame;

        /// <summary>Raised when the hose starts or stops holding the player back.</summary>
        public event Action<bool> LimitReachedChanged;

        public bool IsConnected => _anchor != null && _model != null;

        /// <summary>The player tried to go past the full length and has not yet given some hose back.</summary>
        public bool IsLimitReached { get; private set; }

        /// <summary>Current path and length; null until Awake.</summary>
        public HoseModel Model => _model;

        private void Awake()
        {
            if (_endPoint == null)
            {
                Debug.LogError($"{name}: HoseController needs an End Point reference.", this);
                enabled = false;
                return;
            }

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

            // The source the point belongs to (pump, hydrant) never blocks its own hose.
            Component source = point.Source as Component;
            _query.SetIgnored(source != null ? source.transform : _anchor, _playerRoot);

            _model.Reset(_anchor.position, _endPoint.position);
            SetLimitReached(false);
        }

        public void Disconnect()
        {
            _anchor = null;
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
                _model.LastFixedPoint, _model.FreeSegmentLength, _endPoint.position, delta, out bool blocked);

            _blockedThisFrame |= blocked;
            return allowed;
        }

        private void LateUpdate()
        {
            if (!IsConnected)
                return;

            _model.SetAnchor(_anchor.position);
            _solver.Step(_model, _endPoint.position);

            if (_blockedThisFrame)
                SetLimitReached(true);
            else if (_model.RemainingLength > _releaseMargin)
                SetLimitReached(false);

            _blockedThisFrame = false;
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
