using UnityEngine;

namespace WaterChaos.Water
{
    [DefaultExecutionOrder(100)] // after WaterStream.LateUpdate, so the tube uses this frame's arc
    /// <summary>
    /// Draws the water stream as a procedural tube mesh along the arc. Display only: all data comes from WaterStream.
    /// Disabled, it shows nothing (e.g. while the stream burns as a flamethrower).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WaterStreamTubeView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField]
        private WaterStream _stream;

        [Header("Shape")]
        [Tooltip("Tube radius at the nozzle, m")]
        [SerializeField, Min(0.001f)]
        private float _radius = 0.05f;
        [Tooltip("Radius at the stream end relative to the nozzle radius (>1 widens the stream)")]
        [SerializeField, Range(0f, 4f)]
        private float _tipRadiusScale = 0.6f;
        [SerializeField, Range(3, 16)]
        private int _sides = 8;

        [Header("Rendering")]
        [Tooltip("Order in Layer of the mesh renderer. Match the highest value used by the fire particles")]
        [SerializeField]
        private int _sortingOrder = 2;

        [Header("Wobble")]
        [Tooltip("Radius pulsation along the stream, fraction of the radius")]
        [SerializeField, Range(0f, 0.5f)]
        private float _wobbleAmount = 0.15f;
        [Tooltip("Pulses per metre of stream")]
        [SerializeField, Min(0f)]
        private float _wobbleFrequency = 8f;
        [Tooltip("How fast the pulses travel along the stream, rad/s")]
        [SerializeField, Min(0f)]
        private float _wobbleSpeed = 10f;

        [Header("UV")]
        [Tooltip("Stream length covered by one UV tile along V, m")]
        [SerializeField, Min(0.01f)]
        private float _metersPerTile = 0.5f;

        private Mesh _mesh;
        private Vector3[] _vertices;
        private Vector3[] _normals;
        private Vector2[] _uvs;
        private int[] _indices;
        private int _bufferRings;
        private int _bufferSides;

        private void Awake()
        {
            _mesh = new Mesh { name = "WaterStreamTube" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = _mesh;
            GetComponent<Renderer>().sortingOrder = _sortingOrder;
        }

        private void OnEnable()
        {
            GetComponent<Renderer>().enabled = true;
        }

        private void OnDisable()
        {
            GetComponent<Renderer>().enabled = false;
            if (_mesh != null)
                _mesh.Clear();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
                Destroy(_mesh);
        }

        private void LateUpdate()
        {
            int count = _stream != null ? _stream.VisiblePointCount : 0;

            // Fewer than 2 points means no water is flowing
            if (count < 2)
            {
                _mesh.Clear();
                return;
            }

            EnsureBuffers(_stream.Points.Length);
            Build(_stream.Points, count);
        }

        private void EnsureBuffers(int maxRings)
        {
            if (_vertices != null && _bufferRings == maxRings && _bufferSides == _sides)
                return;

            _bufferRings = maxRings;
            _bufferSides = _sides;

            int verticesPerRing = _sides + 1; // +1: the seam vertex is duplicated for correct UVs
            int vertexCount = maxRings * verticesPerRing;
            _vertices = new Vector3[vertexCount];
            _normals = new Vector3[vertexCount];
            _uvs = new Vector2[vertexCount];

            // Triangles for the full arc; only the first (count - 1) rings are used each frame
            _indices = new int[(maxRings - 1) * _sides * 6];
            int n = 0;
            for (int ring = 0; ring < maxRings - 1; ring++)
            {
                for (int side = 0; side < _sides; side++)
                {
                    int a = ring * verticesPerRing + side;
                    int b = a + 1;
                    int c = a + verticesPerRing;
                    int d = c + 1;

                    // Front faces look away from the tube axis (back-face culling hides the inside)
                    _indices[n++] = a; _indices[n++] = c; _indices[n++] = b;
                    _indices[n++] = b; _indices[n++] = c; _indices[n++] = d;
                }
            }
        }

        private void Build(Vector3[] points, int count)
        {
            int verticesPerRing = _sides + 1;
            float distance = 0f;

            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    distance += Vector3.Distance(points[i], points[i - 1]);

                // Tangent: the direction of the stream at this point
                Vector3 tangent = i == 0
                    ? points[1] - points[0]
                    : i == count - 1
                        ? points[i] - points[i - 1]
                        : points[i + 1] - points[i - 1];
                tangent.Normalize();

                // Two axes perpendicular to the tangent; they span the ring
                Vector3 right = Vector3.Cross(tangent, Vector3.up);
                if (right.sqrMagnitude < 0.0001f) // stream points straight up or down
                    right = Vector3.Cross(tangent, Vector3.right);
                right.Normalize();
                Vector3 up = Vector3.Cross(right, tangent);

                float t = (float) i / (count - 1);
                float wobble = 1f + _wobbleAmount * Mathf.Sin(distance * _wobbleFrequency - Time.time * _wobbleSpeed);
                float radius = _radius * Mathf.Lerp(1f, _tipRadiusScale, t) * wobble;

                for (int s = 0; s <= _sides; s++)
                {
                    float angle = (float) s / _sides * Mathf.PI * 2f;
                    Vector3 normal = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    int index = i * verticesPerRing + s;

                    // Arc points are in world space; the mesh lives in local space
                    _vertices[index] = transform.InverseTransformPoint(points[i] + normal * radius);
                    _normals[index] = transform.InverseTransformDirection(normal);
                    _uvs[index] = new Vector2((float) s / _sides, distance / _metersPerTile);
                }
            }

            int vertexCount = count * verticesPerRing;
            int indexCount = (count - 1) * _sides * 6;

            _mesh.Clear();
            _mesh.SetVertices(_vertices, 0, vertexCount);
            _mesh.SetNormals(_normals, 0, vertexCount);
            _mesh.SetUVs(0, _uvs, 0, vertexCount);
            _mesh.SetIndices(_indices, 0, indexCount, MeshTopology.Triangles, 0);
            _mesh.RecalculateBounds();
        }
    }
}
