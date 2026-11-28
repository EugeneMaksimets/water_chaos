using System;
using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Look-only physics of a hose (Verlet rope): a chain of nodes pinned at both ends that falls, lies on the ground,
    /// slides with friction, resists sharp bends and never passes through colliders. The rope length follows the
    /// gameplay path, so the drawn hose pays out as the player walks. Nodes only ever move with collision checks (also
    /// when the rope is pulled taut), so the hose never passes through a fence or a wall.
    /// Gameplay (length limit, wraps) stays in <see cref="HoseModel"/>; this only decides where the tube is drawn.
    /// </summary>
    public sealed class HoseVerletSimulation
    {
        // Layer 2 = Ignore Raycast: the probe is never found by our own queries.
        private const int ProbeLayer = 2;

        // Passes that push the middles of the pieces out of obstacles.
        private const int EdgePasses = 2;

        private struct Node
        {
            public Vector3 Position;
            public Vector3 Previous;
        }

        private Node[] _nodes;
        // Node positions before the constraints moved them, to sweep that move too.
        private Vector3[] _beforeConstraints;

        private readonly Collider[] _overlaps = new Collider[16];
        private readonly SphereCollider _probe;
        private readonly Func<Collider, bool> _isIgnored;
        private readonly LayerMask _mask;
        private readonly float _collisionRadius;
        private readonly float _skin;
        private readonly float _damping;
        private readonly float _friction;
        private readonly float _bendStiffness;
        private readonly int _iterations;
        private float _segmentLength;

        /// <param name="segments">Rope pieces (nodes - 1).</param>
        /// <param name="owner">Gets a hidden trigger probe needed for collision checks.</param>
        /// <param name="mask">Layers the hose lies on and collides with.</param>
        /// <param name="isIgnored">Colliders that never block the hose (the player, the source, the nozzle).</param>
        /// <param name="collisionRadius">Radius of each node for collisions, in meters (m).</param>
        /// <param name="damping">0..1: share of speed kept per step.</param>
        /// <param name="friction">0..1: share of sliding along a surface lost per touch.</param>
        /// <param name="bendStiffness">0..1: 1 = never bends, 0.9 = a joint bends about 50° at most.</param>
        /// <param name="iterations">Constraint passes per step; more = less stretchy.</param>
        public HoseVerletSimulation(int segments, Transform owner, LayerMask mask, Func<Collider, bool> isIgnored,
            float collisionRadius, float damping, float friction, float bendStiffness, int iterations)
        {
            _nodes = new Node[Mathf.Max(2, segments) + 1];
            _beforeConstraints = new Vector3[_nodes.Length];
            _mask = mask & ~(1 << ProbeLayer);
            _isIgnored = isIgnored;
            _collisionRadius = Mathf.Max(0.001f, collisionRadius);
            _skin = _collisionRadius * 0.25f;
            _damping = Mathf.Clamp01(damping);
            _friction = Mathf.Clamp01(friction);
            _bendStiffness = Mathf.Clamp01(bendStiffness);
            _iterations = Mathf.Max(1, iterations);

            GameObject probe = new GameObject("Hose Collision Probe");
            probe.transform.SetParent(owner, false);
            probe.layer = ProbeLayer;
            _probe = probe.AddComponent<SphereCollider>();
            _probe.isTrigger = true;
            _probe.radius = _collisionRadius;
        }

        public int NodeCount => _nodes.Length;
        public int Segments => _nodes.Length - 1;
        public Vector3 Start => _nodes[0].Position;
        public Vector3 End => _nodes[_nodes.Length - 1].Position;

        /// <summary>Length of the rope as it lies now, in meters (m): above the set length when it is stretched (caught on something).</summary>
        public float CurrentLength
        {
            get
            {
                float length = 0f;
                for (int i = 1; i < _nodes.Length; i++)
                    length += Vector3.Distance(_nodes[i - 1].Position, _nodes[i].Position);
                return length;
            }
        }

        /// <summary>Lays the nodes evenly along the gameplay path (on connect, or after the hose was reset or teleported).</summary>
        public void Reset(HoseModel path, Vector3 start, Vector3 end)
        {
            float total = 0f;
            for (int i = 1; i < path.PointCount; i++)
                total += Vector3.Distance(PathPoint(path, i - 1, start, end), PathPoint(path, i, start, end));

            int segment = 1;
            float walked = 0f;
            for (int n = 0; n < _nodes.Length; n++)
            {
                float target = total * n / (_nodes.Length - 1);
                while (segment < path.PointCount - 1
                       && walked + Vector3.Distance(PathPoint(path, segment - 1, start, end), PathPoint(path, segment, start, end)) < target)
                {
                    walked += Vector3.Distance(PathPoint(path, segment - 1, start, end), PathPoint(path, segment, start, end));
                    segment++;
                }

                Vector3 a = PathPoint(path, segment - 1, start, end);
                Vector3 b = PathPoint(path, segment, start, end);
                float piece = Vector3.Distance(a, b);
                Vector3 position = piece > 1e-5f ? Vector3.Lerp(a, b, Mathf.Clamp01((target - walked) / piece)) : a;
                _nodes[n] = new Node { Position = position, Previous = position };
            }

            _segmentLength = total / (_nodes.Length - 1);
        }

        /// <summary>
        /// Changes the number of pieces, keeping the rope where it lies: new nodes are spread evenly along its current shape,
        /// at rest. Used when the hose gets much longer or shorter, so pieces keep a sensible length.
        /// </summary>
        public void Resample(int segments)
        {
            segments = Mathf.Max(2, segments);
            if (segments == Segments)
                return;

            float total = CurrentLength;
            var resampled = new Node[segments + 1];
            int piece = 1;
            float walked = 0f;
            for (int n = 0; n <= segments; n++)
            {
                float target = total * n / segments;
                while (piece < _nodes.Length - 1
                       && walked + Vector3.Distance(_nodes[piece - 1].Position, _nodes[piece].Position) < target)
                {
                    walked += Vector3.Distance(_nodes[piece - 1].Position, _nodes[piece].Position);
                    piece++;
                }

                Vector3 a = _nodes[piece - 1].Position;
                Vector3 b = _nodes[piece].Position;
                float length = Vector3.Distance(a, b);
                Vector3 position = length > 1e-5f ? Vector3.Lerp(a, b, Mathf.Clamp01((target - walked) / length)) : a;
                resampled[n] = new Node { Position = position, Previous = position };
            }

            _nodes = resampled;
            _beforeConstraints = new Vector3[_nodes.Length];
        }

        /// <summary>Advances the rope by <paramref name="deltaTime"/> (s); its total length becomes <paramref name="length"/> (m).</summary>
        public void Step(Vector3 start, Vector3 end, float length, Vector3 gravity, float deltaTime)
        {
            _segmentLength = Mathf.Max(0.01f, length / (_nodes.Length - 1));
            float deltaTimeSquared = deltaTime * deltaTime;

            for (int i = 1; i < _nodes.Length - 1; i++)
            {
                Node node = _nodes[i];
                Vector3 from = node.Position;
                Vector3 velocity = (node.Position - node.Previous) * _damping;

                node.Previous = node.Position;
                node.Position += velocity + gravity * deltaTimeSquared;
                _nodes[i] = node;

                Sweep(i, from);
            }

            for (int i = 0; i < _nodes.Length; i++)
                _beforeConstraints[i] = _nodes[i].Position;

            for (int iteration = 0; iteration < _iterations; iteration++)
            {
                Pin(start, end);
                for (int i = 0; i < _nodes.Length - 1; i++)
                    SolveDistance(i, i + 1, _segmentLength, exact: true);
                for (int i = 0; i < _nodes.Length - 2; i++)
                    SolveDistance(i, i + 2, 2f * _segmentLength * _bendStiffness, exact: false);
            }

            // A taut rope must not be pulled through a thin wall: the constraints' move is swept like a fall.
            for (int i = 1; i < _nodes.Length - 1; i++)
            {
                Sweep(i, _beforeConstraints[i]);
                Depenetrate(i);
            }

            // The pieces between nodes must not cut through a thin fence or a corner either.
            for (int pass = 0; pass < EdgePasses; pass++)
            {
                for (int i = 0; i < _nodes.Length - 1; i++)
                    DepenetrateEdge(i);
            }

            Pin(start, end);
        }

        /// <summary>
        /// Fills <paramref name="output"/> with points along a smooth (Catmull-Rom) curve through the nodes,
        /// <paramref name="smoothing"/> per piece; returns how many were written.
        /// </summary>
        public int BuildSmoothPoints(Vector3[] output, int smoothing)
        {
            int last = _nodes.Length - 1;
            int steps = Mathf.Max(1, Mathf.Min(smoothing, (output.Length - 1) / last));
            int count = 0;

            for (int i = 0; i < last; i++)
            {
                Vector3 p0 = _nodes[Mathf.Max(i - 1, 0)].Position;
                Vector3 p1 = _nodes[i].Position;
                Vector3 p2 = _nodes[i + 1].Position;
                Vector3 p3 = _nodes[Mathf.Min(i + 2, last)].Position;

                for (int step = 0; step < steps; step++)
                    output[count++] = CatmullRom(p0, p1, p2, p3, (float)step / steps);
            }

            output[count++] = _nodes[last].Position;
            return count;
        }

        public void Destroy()
        {
            if (_probe != null)
                UnityEngine.Object.Destroy(_probe.gameObject);
        }

        // The path's ends are its live start and end (the model keeps them laid near the ground).
        private static Vector3 PathPoint(HoseModel path, int index, Vector3 start, Vector3 end)
        {
            if (index == 0)
                return start;
            return index == path.PointCount - 1 ? end : path.GetPoint(index);
        }

        private void Pin(Vector3 start, Vector3 end)
        {
            int last = _nodes.Length - 1;
            _nodes[0] = new Node { Position = start, Previous = start };
            _nodes[last] = new Node { Position = end, Previous = end };
        }

        // exact: keeps the distance (a piece of hose); otherwise only pushes apart below it (bending stiffness).
        private void SolveDistance(int a, int b, float target, bool exact)
        {
            Vector3 difference = _nodes[b].Position - _nodes[a].Position;
            float distance = difference.magnitude;
            if (distance <= Mathf.Epsilon || (!exact && distance >= target))
                return;

            Vector3 correction = difference * ((distance - target) / distance);
            bool aFixed = a == 0;
            bool bFixed = b == _nodes.Length - 1;
            if (aFixed && bFixed)
                return;

            if (aFixed)
                _nodes[b].Position -= correction;
            else if (bFixed)
                _nodes[a].Position += correction;
            else
            {
                _nodes[a].Position += correction * 0.5f;
                _nodes[b].Position -= correction * 0.5f;
            }
        }

        // Stops the node on the first surface on its way, so it never skips through a floor;
        // keeps sliding along the surface (minus friction) and drops the speed into it.
        private void Sweep(int index, Vector3 from)
        {
            Node node = _nodes[index];
            Vector3 delta = node.Position - from;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
                return;

            Vector3 direction = delta / distance;
            if (!Physics.SphereCast(from, _collisionRadius, direction, out RaycastHit hit, distance + _skin, _mask, QueryTriggerInteraction.Ignore)
                || _isIgnored(hit.collider))
                return;

            Vector3 stop = from + direction * Mathf.Max(0f, hit.distance - _skin);
            Vector3 slide = Vector3.ProjectOnPlane(node.Position - node.Previous, hit.normal) * (1f - _friction);
            node.Position = stop;
            node.Previous = stop - slide;
            _nodes[index] = node;
        }

        // Pushes the node out of any collider it ended up inside (mesh and terrain colliders too).
        private void Depenetrate(int index)
        {
            Node node = _nodes[index];
            int count = Physics.OverlapSphereNonAlloc(node.Position, _collisionRadius + _skin, _overlaps, _mask, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < count; k++)
            {
                Collider other = _overlaps[k];
                if (_isIgnored(other))
                    continue;

                if (Physics.ComputePenetration(_probe, node.Position, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation, out Vector3 push, out float depth))
                {
                    Vector3 move = push * (depth + _skin);
                    node.Position += move;
                    node.Previous += move;
                }
            }

            _nodes[index] = node;
        }

        // Pushes the middle of the piece between nodes a and a+1 out of any obstacle; both ends move with it
        // (a pinned end does not, the other takes the whole move).
        private void DepenetrateEdge(int a)
        {
            int b = a + 1;
            int last = _nodes.Length - 1;
            float weightA = a == 0 ? 0f : 0.5f;
            float weightB = b == last ? 0f : 0.5f;
            float weights = weightA * weightA + weightB * weightB;
            if (weights <= 0f)
                return;

            Vector3 middle = (_nodes[a].Position + _nodes[b].Position) * 0.5f;
            int count = Physics.OverlapSphereNonAlloc(middle, _collisionRadius, _overlaps, _mask, QueryTriggerInteraction.Ignore);
            Vector3 push = Vector3.zero;
            for (int k = 0; k < count; k++)
            {
                Collider other = _overlaps[k];
                if (_isIgnored(other))
                    continue;

                if (Physics.ComputePenetration(_probe, middle + push, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation, out Vector3 direction, out float depth))
                    push += direction * (depth + _skin);
            }

            if (push == Vector3.zero)
                return;

            // Moving the ends this much moves the middle by exactly the push; each move is swept, so no end jumps a wall.
            MoveSwept(a, push * (weightA / weights));
            MoveSwept(b, push * (weightB / weights));
        }

        // Moves a free node, stopping just before any obstacle on the way; its speed is not changed.
        private void MoveSwept(int index, Vector3 move)
        {
            if (index == 0 || index == _nodes.Length - 1)
                return;

            float distance = move.magnitude;
            if (distance < 1e-5f)
                return;

            Vector3 direction = move / distance;
            Vector3 from = _nodes[index].Position;
            if (Physics.SphereCast(from, _collisionRadius, direction, out RaycastHit hit, distance + _skin, _mask, QueryTriggerInteraction.Ignore)
                && !_isIgnored(hit.collider))
                move = direction * Mathf.Max(0f, hit.distance - _skin);

            _nodes[index].Position += move;
            _nodes[index].Previous += move;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }
    }
}
