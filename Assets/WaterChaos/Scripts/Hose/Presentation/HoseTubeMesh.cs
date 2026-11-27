using UnityEngine;
using UnityEngine.Rendering;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Builds a round 3D tube along drawn hose points: a ring of vertices at every point, rings joined by
    /// triangles, both ends closed. Rings are oriented with parallel transport, so the tube never twists at bends.
    /// UV: U = distance along the hose in meters (m) from the first point, V = 0..1 around it.
    /// Look only; reuses its mesh and buffers, so building every frame allocates nothing.
    /// </summary>
    public sealed class HoseTubeMesh
    {
        /// <summary>Most rings (drawn points) one tube may have.</summary>
        public const int MaxRings = 256;

        /// <summary>Allowed vertices around the hose.</summary>
        public const int MinSides = 3;
        public const int MaxSides = 32;

        private const int MaxVertices = (MaxRings + 2) * (MaxSides + 1);
        private const int MaxIndices = (MaxRings - 1) * MaxSides * 6 + 2 * MaxSides * 3;

        private readonly Vector3[] _vertices = new Vector3[MaxVertices];
        private readonly Vector3[] _normals = new Vector3[MaxVertices];
        private readonly Vector2[] _uvs = new Vector2[MaxVertices];
        private readonly int[] _indices = new int[MaxIndices];

        private int _builtRings = -1;
        private int _builtSides = -1;

        public HoseTubeMesh()
        {
            Mesh = new Mesh { name = "Hose Tube" };
            Mesh.MarkDynamic();
        }

        /// <summary>The tube; owned by this object, destroy it together with the owner.</summary>
        public Mesh Mesh { get; }

        /// <summary>
        /// Shows <see cref="Mesh"/> on a new child of owner, so the owner's own renderers stay untouched,
        /// and turns off an old LineRenderer on owner if it still has one.
        /// </summary>
        public MeshRenderer CreateRenderer(Transform owner, Material material)
        {
            LineRenderer oldLine = owner.GetComponent<LineRenderer>();
            if (oldLine != null)
            {
                if (material == null)
                    material = oldLine.sharedMaterial;
                oldLine.enabled = false;
            }

            var child = new GameObject("Hose Tube");
            child.layer = owner.gameObject.layer;
            child.transform.SetParent(owner, false);
            child.AddComponent<MeshFilter>().sharedMesh = Mesh;

            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.enabled = false;
            return renderer;
        }

        /// <param name="points">Hose centre line, world space; only the first count are used.</param>
        /// <param name="radius">Hose radius, in meters (m).</param>
        /// <param name="sides">Vertices around the hose.</param>
        /// <param name="worldToLocal">World to the space of the object that draws the mesh.</param>
        public void Build(Vector3[] points, int count, float radius, int sides, Matrix4x4 worldToLocal)
        {
            count = Mathf.Min(count, Mathf.Min(points.Length, MaxRings));
            sides = Mathf.Clamp(sides, MinSides, MaxSides);

            if (count < 2)
            {
                if (_builtRings != 0)
                    Mesh.Clear();
                _builtRings = 0;
                return;
            }

            int ringSize = sides + 1;
            int vertexCount = (count + 2) * ringSize;
            FillVertices(points, count, radius, sides, worldToLocal);

            bool topologyChanged = count != _builtRings || sides != _builtSides;
            if (topologyChanged)
                Mesh.Clear();

            Mesh.SetVertices(_vertices, 0, vertexCount);
            Mesh.SetNormals(_normals, 0, vertexCount);
            Mesh.SetUVs(0, _uvs, 0, vertexCount);

            if (topologyChanged)
            {
                int indexCount = FillIndices(count, sides);
                Mesh.SetTriangles(_indices, 0, indexCount, 0, false);
                _builtRings = count;
                _builtSides = sides;
            }

            Mesh.RecalculateBounds();

            // The shader swells the hose under pressure: leave room so it is never culled too early.
            Bounds bounds = Mesh.bounds;
            bounds.Expand(radius * 2f);
            Mesh.bounds = bounds;
        }

        private void FillVertices(Vector3[] points, int count, float radius, int sides, Matrix4x4 worldToLocal)
        {
            int ringSize = sides + 1;
            Vector3 tangent = Tangent(points, count, 0, Vector3.forward);

            // Start the seam at the bottom of the hose, where it is least visible.
            Vector3 normal = Vector3.ProjectOnPlane(Vector3.down, tangent);
            if (normal.sqrMagnitude < 1e-6f)
                normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
            normal.Normalize();

            float along = 0f;
            for (int ring = 0; ring < count; ring++)
            {
                if (ring > 0)
                {
                    along += Vector3.Distance(points[ring - 1], points[ring]);

                    // Parallel transport: turn the previous frame by exactly the tangent's turn, so it never spins.
                    Vector3 next = Tangent(points, count, ring, tangent);
                    normal = Quaternion.FromToRotation(tangent, next) * normal;
                    tangent = next;
                    normal = Vector3.ProjectOnPlane(normal, tangent).normalized;
                }

                Vector3 binormal = Vector3.Cross(tangent, normal);
                Vector3 centre = points[ring];
                for (int side = 0; side <= sides; side++)
                {
                    float around = (float)side / sides;
                    float angle = around * 2f * Mathf.PI;
                    Vector3 direction = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);

                    int index = ring * ringSize + side;
                    _vertices[index] = worldToLocal.MultiplyPoint3x4(centre + direction * radius);
                    _normals[index] = worldToLocal.MultiplyVector(direction).normalized;
                    _uvs[index] = new Vector2(along, around);
                }
            }

            FillCap(points[0], -Tangent(points, count, 0, Vector3.forward), count * ringSize, 0, sides, 0f, worldToLocal);
            FillCap(points[count - 1], tangent, (count + 1) * ringSize, (count - 1) * ringSize, sides, along, worldToLocal);
        }

        /// <summary>Flat end cap: a centre vertex plus a copy of the end ring with normals along the hose.</summary>
        private void FillCap(Vector3 centre, Vector3 facing, int start, int ringStart, int sides, float along, Matrix4x4 worldToLocal)
        {
            Vector3 normal = worldToLocal.MultiplyVector(facing).normalized;
            _vertices[start] = worldToLocal.MultiplyPoint3x4(centre);
            _normals[start] = normal;
            _uvs[start] = new Vector2(along, 0.5f);

            for (int side = 0; side < sides; side++)
            {
                _vertices[start + 1 + side] = _vertices[ringStart + side];
                _normals[start + 1 + side] = normal;
                _uvs[start + 1 + side] = _uvs[ringStart + side];
            }
        }

        private int FillIndices(int count, int sides)
        {
            int ringSize = sides + 1;
            int index = 0;

            for (int ring = 0; ring < count - 1; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    int a = ring * ringSize + side;
                    int b = a + 1;
                    int c = a + ringSize;
                    int d = c + 1;

                    _indices[index++] = a;
                    _indices[index++] = b;
                    _indices[index++] = c;
                    _indices[index++] = b;
                    _indices[index++] = d;
                    _indices[index++] = c;
                }
            }

            int startCap = count * ringSize;
            int endCap = (count + 1) * ringSize;
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;

                // Start cap faces back along the hose, end cap forward.
                _indices[index++] = startCap;
                _indices[index++] = startCap + 1 + next;
                _indices[index++] = startCap + 1 + side;

                _indices[index++] = endCap;
                _indices[index++] = endCap + 1 + side;
                _indices[index++] = endCap + 1 + next;
            }

            return index;
        }

        /// <summary>Direction of the hose at a point; fallback when neighbouring points coincide.</summary>
        private static Vector3 Tangent(Vector3[] points, int count, int index, Vector3 fallback)
        {
            Vector3 from = points[Mathf.Max(0, index - 1)];
            Vector3 to = points[Mathf.Min(count - 1, index + 1)];
            Vector3 direction = to - from;
            return direction.sqrMagnitude > 1e-10f ? direction.normalized : fallback;
        }
    }
}
