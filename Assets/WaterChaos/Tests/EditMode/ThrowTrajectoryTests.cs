using NUnit.Framework;
using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Tests
{
    public class ThrowTrajectoryTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Fill_FirstPointIsOrigin()
        {
            Vector3[] points = new Vector3[4];
            Vector3 origin = new Vector3(1f, 2f, 3f);

            ThrowTrajectory.Fill(points, origin, new Vector3(0f, 0f, 10f), Physics.gravity, 0.1f);

            AssertNear(origin, points[0]);
        }

        [Test]
        public void Fill_NoGravity_IsStraightLineAtGivenSpeed()
        {
            Vector3[] points = new Vector3[4];

            ThrowTrajectory.Fill(points, Vector3.zero, new Vector3(0f, 0f, 10f), Vector3.zero, 0.5f);

            AssertNear(new Vector3(0f, 0f, 5f), points[1]);
            AssertNear(new Vector3(0f, 0f, 15f), points[3]);
        }

        [Test]
        public void Fill_WithGravity_DropsByHalfGTSquared()
        {
            Vector3[] points = new Vector3[3];
            Vector3 gravity = new Vector3(0f, -10f, 0f);

            ThrowTrajectory.Fill(points, Vector3.zero, new Vector3(4f, 0f, 0f), gravity, 1f);

            // t = 2 s: x = 4 * 2 = 8 m, y = -0.5 * 10 * 2^2 = -20 m.
            AssertNear(new Vector3(8f, -20f, 0f), points[2]);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Fill_InvalidTimeStep_PutsAllPointsAtOrigin(float timeStep)
        {
            Vector3[] points = new Vector3[3];
            Vector3 origin = new Vector3(1f, 1f, 1f);

            ThrowTrajectory.Fill(points, origin, new Vector3(0f, 0f, 10f), Physics.gravity, timeStep);

            AssertNear(origin, points[2]);
        }

        [Test]
        public void Fill_NullArray_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => ThrowTrajectory.Fill(null, Vector3.zero, Vector3.forward, Physics.gravity, 0.1f));
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance);
            Assert.AreEqual(expected.y, actual.y, Tolerance);
            Assert.AreEqual(expected.z, actual.z, Tolerance);
        }
    }
}
