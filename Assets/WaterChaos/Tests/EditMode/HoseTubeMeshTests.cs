using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class HoseTubeMeshTests
    {
        private const int Sides = 8;
        private const float Radius = 0.05f;
        private const float Step = 0.2f;
        private const float Tolerance = 1e-3f;

        private HoseTubeMesh _tube;

        [SetUp]
        public void SetUp()
        {
            _tube = new HoseTubeMesh();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tube.Mesh);
        }

        [Test]
        public void Build_StraightPath_HasRingsCapsAndValidTriangles()
        {
            Vector3[] points = StraightPoints(5);

            _tube.Build(points, points.Length, Radius, Sides, Matrix4x4.identity);

            Mesh mesh = _tube.Mesh;
            int rings = points.Length * (Sides + 1);
            Assert.AreEqual(rings + 2 * (Sides + 1), mesh.vertexCount);

            int[] triangles = mesh.triangles;
            Assert.AreEqual((points.Length - 1) * Sides * 6 + 2 * Sides * 3, triangles.Length);
            foreach (int index in triangles)
                Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
        }

        [Test]
        public void Build_StraightPath_RingVerticesLieOnHoseSurface()
        {
            Vector3[] points = StraightPoints(5);

            _tube.Build(points, points.Length, Radius, Sides, Matrix4x4.identity);

            Vector3[] vertices = _tube.Mesh.vertices;
            for (int i = 0; i < points.Length * (Sides + 1); i++)
            {
                Vector3 centre = points[i / (Sides + 1)];
                Assert.AreEqual(Radius, Vector3.Distance(vertices[i], centre), Tolerance, $"vertex {i}");
                Assert.AreEqual(centre.z, vertices[i].z, Tolerance, $"vertex {i}");
            }
        }

        [Test]
        public void Build_CurvedPath_RingsStayRoundAndPerpendicular()
        {
            Vector3[] points =
            {
                new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0.2f), new Vector3(0.05f, 0f, 0.4f),
                new Vector3(0.2f, 0f, 0.55f), new Vector3(0.4f, 0.1f, 0.6f), new Vector3(0.6f, 0.1f, 0.6f),
            };

            _tube.Build(points, points.Length, Radius, Sides, Matrix4x4.identity);

            Vector3[] vertices = _tube.Mesh.vertices;
            for (int i = 0; i < points.Length * (Sides + 1); i++)
            {
                Vector3 vertex = vertices[i];
                Assert.IsFalse(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z), $"vertex {i}");
                Assert.AreEqual(Radius, Vector3.Distance(vertex, points[i / (Sides + 1)]), Tolerance, $"vertex {i}");
            }
        }

        [Test]
        public void Build_TrianglesAndNormalsFaceOutward()
        {
            Vector3[] points = StraightPoints(4);

            _tube.Build(points, points.Length, Radius, Sides, Matrix4x4.identity);

            Mesh mesh = _tube.Mesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] triangles = mesh.triangles;
            int tubeIndices = (points.Length - 1) * Sides * 6;

            for (int t = 0; t < tubeIndices; t += 3)
            {
                Vector3 a = vertices[triangles[t]];
                Vector3 b = vertices[triangles[t + 1]];
                Vector3 c = vertices[triangles[t + 2]];
                Vector3 centre = (a + b + c) / 3f;
                Vector3 outward = new Vector3(centre.x, centre.y, 0f);

                // Unity front faces: Cross(b - a, c - a) points toward the viewer.
                Assert.Greater(Vector3.Dot(Vector3.Cross(b - a, c - a), outward), 0f, $"triangle {t / 3}");
            }

            for (int i = 0; i < points.Length * (Sides + 1); i++)
            {
                Vector3 outward = new Vector3(vertices[i].x, vertices[i].y, 0f);
                Assert.Greater(Vector3.Dot(normals[i], outward), 0f, $"normal {i}");
            }
        }

        [Test]
        public void Build_UvRunsAlongAndAroundTheHose()
        {
            Vector3[] points = StraightPoints(4);

            _tube.Build(points, points.Length, Radius, Sides, Matrix4x4.identity);

            var uvs = new System.Collections.Generic.List<Vector2>();
            _tube.Mesh.GetUVs(0, uvs);
            for (int ring = 0; ring < points.Length; ring++)
            {
                Assert.AreEqual(ring * Step, uvs[ring * (Sides + 1)].x, Tolerance, $"ring {ring} along");
                Assert.AreEqual(0f, uvs[ring * (Sides + 1)].y, Tolerance, $"ring {ring} seam start");
                Assert.AreEqual(1f, uvs[ring * (Sides + 1) + Sides].y, Tolerance, $"ring {ring} seam end");
            }
        }

        [Test]
        public void Build_WorldToLocal_PutsVerticesInLocalSpace()
        {
            Vector3[] points = StraightPoints(3);
            Vector3 offset = new Vector3(10f, 0f, 0f);
            Matrix4x4 worldToLocal = Matrix4x4.Translate(-offset);

            _tube.Build(points, points.Length, Radius, Sides, worldToLocal);

            Vector3 firstRingCentre = Vector3.zero;
            Vector3[] vertices = _tube.Mesh.vertices;
            for (int i = 0; i < Sides; i++)
                firstRingCentre += vertices[i];
            firstRingCentre /= Sides;

            Assert.Less(Vector3.Distance(points[0] - offset, firstRingCentre), Tolerance);
        }

        [Test]
        public void Build_FewerPointsLater_ShrinksTheMesh()
        {
            _tube.Build(StraightPoints(10), 10, Radius, Sides, Matrix4x4.identity);
            _tube.Build(StraightPoints(3), 3, Radius, Sides, Matrix4x4.identity);

            Assert.AreEqual(3 * (Sides + 1) + 2 * (Sides + 1), _tube.Mesh.vertexCount);
        }

        [Test]
        public void Build_LessThanTwoPoints_ClearsTheMesh()
        {
            _tube.Build(StraightPoints(5), 5, Radius, Sides, Matrix4x4.identity);
            _tube.Build(StraightPoints(1), 1, Radius, Sides, Matrix4x4.identity);

            Assert.AreEqual(0, _tube.Mesh.vertexCount);
        }

        private static Vector3[] StraightPoints(int count)
        {
            var points = new Vector3[count];
            for (int i = 0; i < count; i++)
                points[i] = new Vector3(0f, 0f, i * Step);
            return points;
        }
    }
}
