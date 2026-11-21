using NUnit.Framework;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class WaterArcTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void BuildPoints_FirstPointIsOrigin()
        {
            Vector3 origin = new Vector3(1f, 2f, 3f);
            Vector3[] points = new Vector3[5];

            WaterArc.BuildPoints(origin, Vector3.forward, 10f, 2f, points);

            AssertApproximately(origin, points[0]);
        }

        [Test]
        public void BuildPoints_LastPointIsRangeAlongAimMinusDrop()
        {
            Vector3 origin = new Vector3(0f, -2f, 10f);
            Vector3[] points = new Vector3[5];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, 2f, points);

            AssertApproximately(origin, points[4]);
        }

        [Test]
        public void BuildPoints_MidPointDropsQuarterOfDrop()
        {
            Vector3 origin = new Vector3(0f, -0.5f, 5f);
            Vector3[] points = new Vector3[3];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, 2f, points);

            // Test for: t = 0.5 → drop * t² = 2 * 0.25
            AssertApproximately(origin, points[1]);
        }

        [Test]
        public void BuildPoints_NormalizesDirection()
        {
            Vector3 origin = Vector3.zero;
            Vector3 direction = new Vector3(0f, 0f, 5f);
            Vector3[] points = new Vector3[2];

            WaterArc.BuildPoints(origin, direction, 10f, 0f, points);

            AssertApproximately(new Vector3(0f, 0f, 10f), points[1]);
        }

        [Test]
        public void BuildPoints_ZeroRangeAndDrop_AllPointsAreOrigin()
        {
            Vector3 origin = new Vector3(1f, 2f, 3f);
            Vector3[] points = new Vector3[5];

            WaterArc.BuildPoints(origin, Vector3.forward, 0f, 0f, points);

            foreach(Vector3 point in points)
            {
                AssertApproximately(origin, point);
            }
        }

        private static void AssertApproximately(Vector3 expected, Vector3 actual)
        {
            Assert.That(
                Vector3.Distance(expected, actual),
                Is.LessThan(Tolerance), 
                $"Expected {expected}, got {actual}");
        }
    }
}