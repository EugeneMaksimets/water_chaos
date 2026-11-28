using System;
using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>How a hose view decides the shape of the drawn tube.</summary>
    public enum HoseShapeMode
    {
        /// <summary>Laid along the gameplay path, on the ground (<see cref="HoseGroundDrape"/>).</summary>
        Drape,

        /// <summary>A physical rope: falls, slides, bends softly and collides with the world (<see cref="HoseVerletSimulation"/>).</summary>
        Physics,
    }

    /// <summary>
    /// The physical shape of a drawn hose, shared by the hose views: settings shown in the Inspector, the rope
    /// simulation, and when to lay it again along the gameplay path (first use, teleports, resets).
    /// The rope length follows the gameplay path, so the hose pays out as the player walks. Look only.
    /// </summary>
    [Serializable]
    public sealed class HoseRope
    {
        [Tooltip("Distance between simulated nodes, in meters (m): the node count follows the hose length. Smaller = rounder bends, more cost.")]
        [SerializeField, Range(0.1f, 1f)] private float _nodeSpacing = 0.3f;

        [Tooltip("Drawn hose length compared to the gameplay path: 1 = taut, 1.1 = lies with a little slack.")]
        [SerializeField, Range(1f, 1.5f)] private float _slack = 1.15f;

        [Tooltip("0..1: 1 = never bends, 0.9 = a joint bends about 50° at most.")]
        [SerializeField, Range(0f, 1f)] private float _bendStiffness = 0.9f;

        [Tooltip("0..1: share of sliding along the ground lost per touch (0 = ice, 1 = sticks where it lands).")]
        [SerializeField, Range(0f, 1f)] private float _friction = 0.3f;

        [Tooltip("0..1: share of speed kept per physics step.")]
        [SerializeField, Range(0f, 1f)] private float _damping = 0.98f;

        [Tooltip("Constraint passes per physics step; more = less stretchy.")]
        [SerializeField, Range(1, 30)] private int _iterations = 12;

        [Tooltip("Smooth points drawn between two nodes.")]
        [SerializeField, Range(1, 8)] private int _smoothing = 4;

        [Tooltip("If an end jumps farther than this in one step (teleport, reset), the rope is laid again along the path, in meters (m).")]
        [SerializeField, Min(0.5f)] private float _resetJump = 2f;

        // Most pieces a rope gets, so the smooth curve (Smoothing per piece) still fits the tube mesh.
        private const int MaxSegments = 60;

        private HoseVerletSimulation _simulation;
        private Vector3[] _points;

        /// <summary>The rope has a shape to draw (it was laid and is being simulated).</summary>
        public bool IsLaid { get; private set; }

        /// <summary>Advances the rope one physics step between <paramref name="start"/> and <paramref name="end"/>.</summary>
        /// <param name="owner">Gets the rope's hidden collision probe.</param>
        /// <param name="mask">Layers the hose lies on and collides with.</param>
        /// <param name="ignores">Colliders that never block the hose (the player, the source, the nozzle).</param>
        /// <param name="radius">Hose radius, in meters (m).</param>
        public void Step(Transform owner, HoseModel path, Vector3 start, Vector3 end, LayerMask mask, Func<Collider, bool> ignores, float radius)
        {
            float length = path.Length * _slack;
            if (_simulation == null)
            {
                _simulation = new HoseVerletSimulation(SegmentsFor(length), owner, mask, ignores, radius, _damping, _friction, _bendStiffness, _iterations);
                _points = new Vector3[HoseTubeMesh.MaxRings];
            }

            float jump = _resetJump * _resetJump;
            if (!IsLaid || (_simulation.Start - start).sqrMagnitude > jump || (_simulation.End - end).sqrMagnitude > jump)
            {
                _simulation.Resample(SegmentsFor(length));
                _simulation.Reset(path, start, end);
                IsLaid = true;
            }
            else
            {
                // Pieces stay about Node Spacing long as the hose pays out or is taken back.
                int wanted = SegmentsFor(length);
                int current = _simulation.Segments;
                if (wanted > current * 1.3f || wanted < current / 1.3f)
                    _simulation.Resample(wanted);
            }

            _simulation.Step(start, end, length, Physics.gravity, Time.fixedDeltaTime);
        }

        private int SegmentsFor(float length)
        {
            return Mathf.Clamp(Mathf.RoundToInt(length / _nodeSpacing), 4, MaxSegments);
        }

        /// <summary>Smooth points along the rope to build the tube from; returns how many.</summary>
        public int BuildPoints(out Vector3[] points)
        {
            points = _points;
            return _simulation.BuildSmoothPoints(_points, _smoothing);
        }

        /// <summary>
        /// Smooth points for drawing this frame: between physics steps the rope is blended forward, and its ends are
        /// where <paramref name="start"/> and <paramref name="end"/> are now, so a hose carried at a run does not jerk.
        /// </summary>
        public int BuildPoints(Vector3 start, Vector3 end, out Vector3[] points)
        {
            points = _points;
            float blend = Time.fixedDeltaTime > 0f ? (Time.time - Time.fixedTime) / Time.fixedDeltaTime : 1f;
            return _simulation.BuildSmoothPoints(_points, _smoothing, blend, start, end);
        }

        /// <summary>The hose was disconnected or switched to another shape: lay it again next time.</summary>
        public void Clear()
        {
            IsLaid = false;
        }

        public void Dispose()
        {
            _simulation?.Destroy();
            _simulation = null;
        }
    }
}
