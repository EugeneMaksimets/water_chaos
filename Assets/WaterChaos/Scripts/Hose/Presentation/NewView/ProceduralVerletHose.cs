using UnityEngine;

namespace WaterChaos.Hose
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class ProceduralVerletHose : MonoBehaviour
    {
        [Header("Attachments")]
        [SerializeField] private Transform _startAnchor;
        [SerializeField] private Transform _endAnchor;

        [Tooltip("Where the hose attaches, in the start anchor's local space (m): lift it off the ground or move it to the side of the object.")]
        [SerializeField] private Vector3 _startOffset = new Vector3(0f, 0.05f, 0f);

        [Tooltip("Where the hose attaches, in the end anchor's local space (m).")]
        [SerializeField] private Vector3 _endOffset = new Vector3(0f, 0.05f, 0f);

        [Header("Simulation")]
        [SerializeField, Min(2)] private int _segmentCount = 30;
        [SerializeField, Min(0.01f)] private float _segmentLength = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _damping = 0.99f;
        [SerializeField, Min(1)] private int _constraintIterations = 12;
        [SerializeField] private Vector3 _gravity = new(0f, -9.81f, 0f);
        [Tooltip("Bending stiffness: 1 = never bends, 0 = bends freely. 0.9 lets a joint bend about 50° at most.")]
        [SerializeField, Range(0f, 1f)] private float _bendStiffness = 0.9f;
        [Tooltip("Extra rings drawn between two nodes along a smooth curve. 1 = straight pieces.")]
        [SerializeField, Range(1, 8)] private int _smoothing = 4;
        [Tooltip("Hose length compared to the distance between the anchors at start: 1 = taut, 1.1 = lies with a little slack. Sets Segment Length.")]
        [SerializeField, Min(1f)] private float _slack = 1.1f;

        [Header("Collision")]
        [SerializeField] private bool _enableCollision = true;
        [SerializeField] private LayerMask _collisionMask = ~0;
        [SerializeField, Min(0.001f)] private float _collisionRadius = 0.04f;
        [SerializeField, Min(0f)] private float _collisionSkin = 0.01f;
        [Tooltip("How much sliding along a surface is lost per touch (0 = ice, 1 = sticks where it lands).")]
        [SerializeField, Range(0f, 1f)] private float _friction = 0.3f;

        [Header("Mesh")]
        [SerializeField, Min(0.001f)] private float _radius = 0.05f;
        [SerializeField, Range(3, 16)] private int _radialSegments = 10;
        [SerializeField, Min(0.01f)] private float _uvTilingLength = 1f;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos;

        // Layer 2 = Ignore Raycast: the probe is never found by our own queries.
        private const int ProbeLayer = 2;

        private SphereCollider _probe;
        private readonly Collider[] _overlaps = new Collider[16];

        private struct RopeNode
        {
            public Vector3 Position;
            public Vector3 PreviousPosition;

            public RopeNode(Vector3 position)
            {
                Position = position;
                PreviousPosition = position;
            }
        }

        private RopeNode[] _nodes;
        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private int[] _triangles;
        private Mesh _mesh;
        // Smooth points the tube is drawn through: _smoothing per segment plus the last node.
        private Vector3[] _renderPoints;

        private int RingSize => _radialSegments + 1;
        private int RingCount => _segmentCount * _smoothing + 1;

        private void Awake()
        {
            _segmentCount = Mathf.Max(2, _segmentCount);
            _radialSegments = Mathf.Max(3, _radialSegments);

            InitializeNodes();
            InitializeMesh();
            CreateProbe();
        }

        private void FixedUpdate()
        {
            if (!IsReady())
                return;

            Simulate();
            SolveConstraints();
        }

        private void LateUpdate()
        {
            if (!IsReady())
                return;

            UpdateMesh();
        }

        private bool IsReady()
        {
            return _nodes != null &&
                   _startAnchor != null &&
                   _endAnchor != null;
        }

        private void InitializeNodes()
        {
            _nodes = new RopeNode[_segmentCount + 1];

            Vector3 start = GetStartPosition();
            Vector3 end = GetEndPosition();

            Vector3 direction = end - start;
            float distance = direction.magnitude;

            // The hose is as long as the gap between its anchors plus the slack, split evenly between segments.
            if (distance > Mathf.Epsilon)
                _segmentLength = distance * _slack / _segmentCount;

            if (distance > Mathf.Epsilon)
                direction /= distance;
            else
                direction = Vector3.down;

            for (int i = 0; i <= _segmentCount; i++)
            {
                Vector3 position = start +
                                   direction * (distance * i / _segmentCount);

                _nodes[i] = new RopeNode(position);
            }
        }

        private Vector3 GetStartPosition()
        {
            return _startAnchor != null
                ? _startAnchor.TransformPoint(_startOffset)
                : transform.position;
        }

        private Vector3 GetEndPosition()
        {
            return _endAnchor != null
                ? _endAnchor.TransformPoint(_endOffset)
                : transform.position;
        }

        private void Simulate()
        {
            float deltaTime = Time.fixedDeltaTime;
            float deltaTimeSquared = deltaTime * deltaTime;

            for (int i = 1; i < _nodes.Length - 1; i++)
            {
                RopeNode node = _nodes[i];
                Vector3 from = node.Position;

                Vector3 velocity = (node.Position - node.PreviousPosition) * _damping;

                node.PreviousPosition = node.Position;
                node.Position = node.Position + velocity + _gravity * deltaTimeSquared;
                _nodes[i] = node;

                if (_enableCollision)
                {
                    Sweep(i, from);
                    Depenetrate(i);
                }
            }

            PinEndpoints();
        }

        // Stops the node on the first surface between where it was and where it moved, so it never skips through a floor.
        private void Sweep(int index, Vector3 from)
        {
            RopeNode node = _nodes[index];
            Vector3 delta = node.Position - from;
            float distance = delta.magnitude;
            if (distance < 0.0001f)
                return;

            Vector3 direction = delta / distance;
            int mask = _collisionMask & ~(1 << ProbeLayer);

            if (!Physics.SphereCast(from, _collisionRadius, direction, out RaycastHit hit,
                    distance + _collisionSkin, mask, QueryTriggerInteraction.Ignore))
                return;

            if (IsIgnored(hit.collider))
                return;

            Vector3 stop = from + direction * Mathf.Max(0f, hit.distance - _collisionSkin);

            // Keep the slide along the surface (minus friction), drop the speed into it: the hose lies down and can straighten.
            Vector3 velocity = node.Position - node.PreviousPosition;
            Vector3 slide = Vector3.ProjectOnPlane(velocity, hit.normal) * (1f - _friction);

            node.Position = stop;
            node.PreviousPosition = stop - slide;
            _nodes[index] = node;
        }

        // Pushes the node out of any collider it ended up inside (works with mesh and terrain colliders too).
        private void Depenetrate(int index)
        {
            RopeNode node = _nodes[index];
            float radius = _collisionRadius + _collisionSkin;
            int mask = _collisionMask & ~(1 << ProbeLayer);

            int count = Physics.OverlapSphereNonAlloc(node.Position, radius, _overlaps, mask, QueryTriggerInteraction.Ignore);
            for (int k = 0; k < count; k++)
            {
                Collider other = _overlaps[k];
                if (IsIgnored(other))
                    continue;

                if (Physics.ComputePenetration(_probe, node.Position, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 pushDirection, out float pushDistance))
                {
                    Vector3 push = pushDirection * (pushDistance + _collisionSkin);
                    node.Position += push;
                    node.PreviousPosition += push;
                }
            }

            _nodes[index] = node;
        }

        // The hose never collides with what it is attached to (the player, the nozzle, the source) or with itself.
        private bool IsIgnored(Collider other)
        {
            Transform target = other.transform;
            return target.IsChildOf(transform)
                   || (_startAnchor != null && target.IsChildOf(_startAnchor))
                   || (_endAnchor != null && target.IsChildOf(_endAnchor));
        }

        private void SolveConstraints()
        {
            float targetLength = _segmentLength;

            for (int iteration = 0;
                 iteration < _constraintIterations;
                 iteration++)
            {
                PinEndpoints();

                for (int i = 0; i < _nodes.Length - 1; i++)
                {
                    SolveDistanceConstraint(i, targetLength);
                }

                for (int i = 0; i < _nodes.Length - 2; i++)
                    SolveBendConstraint(i, targetLength);

                if (_enableCollision)
                {
                    for (int i = 1; i < _nodes.Length - 1; i++)
                        Depenetrate(i);
                }
            }

            PinEndpoints();
        }

        // Keeps nodes i and i+2 at least this far apart, so the joint between them never folds into a sharp corner.
        private void SolveBendConstraint(int index, float segmentLength)
        {
            RopeNode first = _nodes[index];
            RopeNode third = _nodes[index + 2];

            Vector3 difference = third.Position - first.Position;
            float distance = difference.magnitude;
            float minDistance = 2f * segmentLength * _bendStiffness;

            if (distance >= minDistance || distance <= Mathf.Epsilon)
                return;

            Vector3 correction = difference * ((distance - minDistance) / distance);

            bool firstIsFixed = index == 0;
            bool thirdIsFixed = index + 2 == _nodes.Length - 1;

            if (firstIsFixed && thirdIsFixed)
                return;

            if (firstIsFixed)
            {
                third.Position -= correction;
            }
            else if (thirdIsFixed)
            {
                first.Position += correction;
            }
            else
            {
                first.Position += correction * 0.5f;
                third.Position -= correction * 0.5f;
            }

            _nodes[index] = first;
            _nodes[index + 2] = third;
        }

        private void SolveDistanceConstraint(int index, float targetLength)
        {
            RopeNode first = _nodes[index];
            RopeNode second = _nodes[index + 1];

            Vector3 difference = second.Position - first.Position;
            float distance = difference.magnitude;

            if (distance <= Mathf.Epsilon)
                return;

            float error = (distance - targetLength) / distance;
            Vector3 correction = difference * error;

            bool firstIsFixed = index == 0;
            bool secondIsFixed = index + 1 == _nodes.Length - 1;

            if (firstIsFixed && secondIsFixed)
                return;

            if (firstIsFixed)
            {
                second.Position -= correction;
            }
            else if (secondIsFixed)
            {
                first.Position += correction;
            }
            else
            {
                first.Position += correction * 0.5f;
                second.Position -= correction * 0.5f;
            }

            _nodes[index] = first;
            _nodes[index + 1] = second;
        }

        private void PinEndpoints()
        {
            RopeNode first = _nodes[0];
            first.Position = GetStartPosition();
            first.PreviousPosition = first.Position;
            _nodes[0] = first;

            int lastIndex = _nodes.Length - 1;

            RopeNode last = _nodes[lastIndex];
            last.Position = GetEndPosition();
            last.PreviousPosition = last.Position;
            _nodes[lastIndex] = last;
        }

        private void InitializeMesh()
        {
            int rings = RingCount;
            _renderPoints = new Vector3[rings];

            int vertexCount = rings * RingSize;
            int triangleCount = (rings - 1) * _radialSegments * 6;

            _vertices = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];
            _triangles = new int[triangleCount];

            int triangleIndex = 0;

            for (int i = 0; i < rings - 1; i++)
            {
                for (int j = 0; j < _radialSegments; j++)
                {
                    int current = i * RingSize + j;
                    int next = current + RingSize;

                    _triangles[triangleIndex++] = current;
                    _triangles[triangleIndex++] = next;
                    _triangles[triangleIndex++] = current + 1;

                    _triangles[triangleIndex++] = current + 1;
                    _triangles[triangleIndex++] = next;
                    _triangles[triangleIndex++] = next + 1;
                }
            }

            _mesh = new Mesh
            {
                name = "ProceduralHoseMesh"
            };

            _mesh.MarkDynamic();
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;

            GetComponent<MeshFilter>().sharedMesh = _mesh;
        }

        // Points along a Catmull-Rom curve through the nodes: the drawn hose bends smoothly between them.
        private void BuildRenderPoints()
        {
            int last = _nodes.Length - 1;
            int point = 0;

            for (int i = 0; i < last; i++)
            {
                Vector3 p0 = _nodes[Mathf.Max(i - 1, 0)].Position;
                Vector3 p1 = _nodes[i].Position;
                Vector3 p2 = _nodes[i + 1].Position;
                Vector3 p3 = _nodes[Mathf.Min(i + 2, last)].Position;

                for (int step = 0; step < _smoothing; step++)
                    _renderPoints[point++] = CatmullRom(p0, p1, p2, p3, (float)step / _smoothing);
            }

            _renderPoints[point] = _nodes[last].Position;
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * (2f * p1
                           + (p2 - p0) * t
                           + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                           + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        private void UpdateMesh()
        {
            BuildRenderPoints();

            int rings = RingCount;
            Vector3 previousNormal = Vector3.up;
            float currentV = 0f;

            for (int i = 0; i < rings; i++)
            {
                Vector3 tangent = GetTangent(i);

                // Parallel transport: the ring keeps its roll from the previous one, so the tube does not twist.
                Vector3 normal = Vector3.ProjectOnPlane(previousNormal, tangent);
                if (normal.sqrMagnitude < 0.0001f)
                {
                    normal = Vector3.Cross(tangent, Vector3.right);
                    if (normal.sqrMagnitude < 0.0001f)
                        normal = Vector3.Cross(tangent, Vector3.forward);
                }

                normal.Normalize();
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                previousNormal = normal;

                if (i > 0)
                    currentV += Vector3.Distance(_renderPoints[i], _renderPoints[i - 1]) / _uvTilingLength;

                for (int j = 0; j <= _radialSegments; j++)
                {
                    float angle = (float)j / _radialSegments * Mathf.PI * 2f;
                    Vector3 offset = (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle)) * _radius;

                    int vertexIndex = i * RingSize + j;
                    _vertices[vertexIndex] = transform.InverseTransformPoint(_renderPoints[i] + offset);
                    _uvs[vertexIndex] = new Vector2((float)j / _radialSegments, currentV);
                }
            }

            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
        }

        // Direction of the drawn tube at a ring, from its neighbours on the smooth curve.
        private Vector3 GetTangent(int index)
        {
            int last = RingCount - 1;
            Vector3 tangent;

            if (index == 0)
                tangent = _renderPoints[1] - _renderPoints[0];
            else if (index == last)
                tangent = _renderPoints[index] - _renderPoints[index - 1];
            else
                tangent = _renderPoints[index + 1] - _renderPoints[index - 1];

            return tangent.sqrMagnitude > Mathf.Epsilon ? tangent.normalized : Vector3.forward;
        }

        // A trigger sphere used only to measure overlaps (ComputePenetration needs a collider of our own).
        private void CreateProbe()
        {
            GameObject probe = new GameObject("Hose Collision Probe");
            probe.transform.SetParent(transform, false);
            probe.layer = ProbeLayer;

            _probe = probe.AddComponent<SphereCollider>();
            _probe.isTrigger = true;
            _probe.radius = _collisionRadius;
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _nodes == null)
                return;

            Gizmos.color = Color.cyan;

            for (int i = 0; i < _nodes.Length; i++)
            {
                Gizmos.DrawSphere(_nodes[i].Position, 0.025f);

                if (i < _nodes.Length - 1)
                {
                    Gizmos.DrawLine(
                        _nodes[i].Position,
                        _nodes[i + 1].Position);
                }
            }
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying)
                    Destroy(_mesh);
                else
                    DestroyImmediate(_mesh);
            }
        }
    }
}
