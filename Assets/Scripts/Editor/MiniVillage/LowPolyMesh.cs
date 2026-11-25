using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaterChaos.EditorTools.MiniVillage
{
    /// <summary>
    /// Flat-shaded low-poly mesh assembled from simple convex solids (boxes, cylinders, cones, spheres, prisms).
    /// Every solid names its material; the finished mesh has one submesh per material.
    /// Faces are turned outwards automatically, so solids may list their corners in any order.
    /// UVs are planar in meters, so tiling textures keep the same scale on every model.
    /// </summary>
    internal sealed class LowPolyMesh
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<string> _materials = new List<string>();
        private readonly List<List<int>> _triangles = new List<List<int>>();
        private Matrix4x4 _matrix = Matrix4x4.identity;

        public bool IsEmpty => _vertices.Count == 0;

        /// <summary>Places the following solids with this offset and rotation (in mesh space).</summary>
        public void SetTransform(Vector3 position, Quaternion rotation)
        {
            _matrix = Matrix4x4.TRS(position, rotation, Vector3.one);
        }

        public void ResetTransform()
        {
            _matrix = Matrix4x4.identity;
        }

        public void Box(Vector3 center, Vector3 size, string material)
        {
            Vector3 h = size * 0.5f;
            var c = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                c[i] = center + new Vector3(
                    (i & 1) == 0 ? -h.x : h.x,
                    (i & 2) == 0 ? -h.y : h.y,
                    (i & 4) == 0 ? -h.z : h.z);
            }

            Quad(c[0], c[2], c[6], c[4], center, material);
            Quad(c[1], c[5], c[7], c[3], center, material);
            Quad(c[0], c[4], c[5], c[1], center, material);
            Quad(c[2], c[3], c[7], c[6], center, material);
            Quad(c[0], c[1], c[3], c[2], center, material);
            Quad(c[4], c[6], c[7], c[5], center, material);
        }

        /// <summary>Upright cylinder, frustum or cone (topRadius 0) standing on <paramref name="baseCenter"/>.</summary>
        public void Cylinder(Vector3 baseCenter, float bottomRadius, float topRadius, float height, int segments,
            string material, bool caps = true, float angleOffset = 0f)
        {
            segments = Mathf.Max(3, segments);
            Vector3 center = baseCenter + Vector3.up * (height * 0.5f);
            Vector3 top = baseCenter + Vector3.up * height;
            var bottomRing = new Vector3[segments];
            var topRing = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                float angle = angleOffset + i * Mathf.PI * 2f / segments;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                bottomRing[i] = baseCenter + direction * bottomRadius;
                topRing[i] = top + direction * topRadius;
            }

            for (int i = 0; i < segments; i++)
            {
                int j = (i + 1) % segments;
                if (topRadius <= 0.0001f)
                    Tri(bottomRing[i], bottomRing[j], top, center, material);
                else if (bottomRadius <= 0.0001f)
                    Tri(topRing[i], topRing[j], baseCenter, center, material);
                else
                    Quad(bottomRing[i], bottomRing[j], topRing[j], topRing[i], center, material);

                if (!caps)
                    continue;
                if (bottomRadius > 0.0001f)
                    Tri(baseCenter, bottomRing[i], bottomRing[j], center, material);
                if (topRadius > 0.0001f)
                    Tri(top, topRing[j], topRing[i], center, material);
            }
        }

        /// <summary>Low-poly ellipsoid (a sphere when all radii are equal).</summary>
        public void Sphere(Vector3 center, Vector3 radii, int longitude, int latitude, string material)
        {
            longitude = Mathf.Max(3, longitude);
            latitude = Mathf.Max(2, latitude);
            var rings = new Vector3[latitude + 1, longitude];

            for (int y = 0; y <= latitude; y++)
            {
                float polar = Mathf.PI * y / latitude;
                for (int x = 0; x < longitude; x++)
                {
                    float azimuth = Mathf.PI * 2f * x / longitude + (y % 2) * Mathf.PI / longitude;
                    var unit = new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Cos(polar), Mathf.Sin(polar) * Mathf.Sin(azimuth));
                    rings[y, x] = center + Vector3.Scale(unit, radii);
                }
            }

            for (int y = 0; y < latitude; y++)
            {
                for (int x = 0; x < longitude; x++)
                {
                    int next = (x + 1) % longitude;
                    if (y == 0)
                        Tri(rings[0, 0], rings[1, x], rings[1, next], center, material);
                    else if (y == latitude - 1)
                        Tri(rings[y, x], rings[y, next], rings[latitude, 0], center, material);
                    else
                        Quad(rings[y, x], rings[y, next], rings[y + 1, next], rings[y + 1, x], center, material);
                }
            }
        }

        /// <summary>
        /// Triangular prism with its ridge along X: base <paramref name="width"/> × <paramref name="depth"/>
        /// at <paramref name="baseCenter"/>, ridge <paramref name="height"/> above it.
        /// </summary>
        public void Gable(Vector3 baseCenter, float width, float depth, float height, string material)
        {
            float hx = width * 0.5f;
            float hz = depth * 0.5f;
            Vector3 a = baseCenter + new Vector3(-hx, 0f, -hz);
            Vector3 b = baseCenter + new Vector3(hx, 0f, -hz);
            Vector3 c = baseCenter + new Vector3(hx, 0f, hz);
            Vector3 d = baseCenter + new Vector3(-hx, 0f, hz);
            Vector3 r0 = baseCenter + new Vector3(-hx, height, 0f);
            Vector3 r1 = baseCenter + new Vector3(hx, height, 0f);
            Vector3 center = baseCenter + Vector3.up * (height * 0.33f);

            Quad(a, b, r1, r0, center, material);
            Quad(d, c, r1, r0, center, material);
            Tri(a, d, r0, center, material);
            Tri(b, c, r1, center, material);
            Quad(a, b, c, d, center, material);
        }

        /// <summary>Convex outline in the XY plane extruded along Z from <paramref name="zMin"/> to <paramref name="zMax"/>.</summary>
        public void Extrude(Vector2[] outline, float zMin, float zMax, string material)
        {
            Vector2 middle = Vector2.zero;
            foreach (Vector2 point in outline)
                middle += point;
            middle /= outline.Length;
            var center = new Vector3(middle.x, middle.y, (zMin + zMax) * 0.5f);

            for (int i = 0; i < outline.Length; i++)
            {
                Vector2 a = outline[i];
                Vector2 b = outline[(i + 1) % outline.Length];
                Quad(new Vector3(a.x, a.y, zMin), new Vector3(b.x, b.y, zMin),
                    new Vector3(b.x, b.y, zMax), new Vector3(a.x, a.y, zMax), center, material);
                Tri(new Vector3(middle.x, middle.y, zMin), new Vector3(a.x, a.y, zMin), new Vector3(b.x, b.y, zMin), center, material);
                Tri(new Vector3(middle.x, middle.y, zMax), new Vector3(a.x, a.y, zMax), new Vector3(b.x, b.y, zMax), center, material);
            }
        }

        /// <summary>Ring of boxes lying flat (torus-like), e.g. a swim ring.</summary>
        public void Ring(Vector3 center, float radius, float thickness, int segments, string material)
        {
            Matrix4x4 saved = _matrix;
            float length = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * 360f / segments;
                Quaternion rotation = Quaternion.Euler(0f, -angle, 0f);
                Vector3 position = center + rotation * Vector3.right * radius;
                _matrix = saved * Matrix4x4.TRS(position, rotation, Vector3.one);
                Box(Vector3.zero, new Vector3(thickness, thickness, length), material);
            }

            _matrix = saved;
        }

        public Mesh ToMesh(string name, out string[] materials)
        {
            var mesh = new Mesh { name = name };
            if (_vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;

            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.subMeshCount = _triangles.Count;
            for (int i = 0; i < _triangles.Count; i++)
                mesh.SetTriangles(_triangles[i], i);

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            materials = _materials.ToArray();
            return mesh;
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 solidCenter, string material)
        {
            a = _matrix.MultiplyPoint3x4(a);
            b = _matrix.MultiplyPoint3x4(b);
            c = _matrix.MultiplyPoint3x4(c);
            d = _matrix.MultiplyPoint3x4(d);
            solidCenter = _matrix.MultiplyPoint3x4(solidCenter);

            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f)
                normal = Vector3.Cross(c - a, d - a);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            Vector3 faceCenter = (a + b + c + d) * 0.25f;
            if (Vector3.Dot(normal, faceCenter - solidCenter) < 0f)
            {
                Vector3 t = b;
                b = d;
                d = t;
                normal = -normal;
            }

            normal.Normalize();
            List<int> triangles = Slot(material);
            int start = _vertices.Count;
            AddVertex(a, normal);
            AddVertex(b, normal);
            AddVertex(c, normal);
            AddVertex(d, normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 solidCenter, string material)
        {
            a = _matrix.MultiplyPoint3x4(a);
            b = _matrix.MultiplyPoint3x4(b);
            c = _matrix.MultiplyPoint3x4(c);
            solidCenter = _matrix.MultiplyPoint3x4(solidCenter);

            Vector3 normal = Vector3.Cross(b - a, c - a);
            if (normal.sqrMagnitude < 1e-12f)
                return;

            if (Vector3.Dot(normal, (a + b + c) / 3f - solidCenter) < 0f)
            {
                Vector3 t = b;
                b = c;
                c = t;
                normal = -normal;
            }

            normal.Normalize();
            List<int> triangles = Slot(material);
            int start = _vertices.Count;
            AddVertex(a, normal);
            AddVertex(b, normal);
            AddVertex(c, normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private void AddVertex(Vector3 position, Vector3 normal)
        {
            _vertices.Add(position);
            _normals.Add(normal);

            float ax = Mathf.Abs(normal.x);
            float ay = Mathf.Abs(normal.y);
            float az = Mathf.Abs(normal.z);
            if (ay >= ax && ay >= az)
                _uvs.Add(new Vector2(position.x, position.z));
            else if (ax >= az)
                _uvs.Add(new Vector2(position.z, position.y));
            else
                _uvs.Add(new Vector2(position.x, position.y));
        }

        private List<int> Slot(string material)
        {
            int index = _materials.IndexOf(material);
            if (index >= 0)
                return _triangles[index];

            _materials.Add(material);
            var triangles = new List<int>();
            _triangles.Add(triangles);
            return triangles;
        }
    }
}
