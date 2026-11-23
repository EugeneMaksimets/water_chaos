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
            Vector3 expected = new Vector3(0f, -2f, 10f);
            Vector3[] points = new Vector3[5];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, 2f, points);

            AssertApproximately(expected, points[4]);
        }

        [Test]
        public void BuildPoints_MidPointDropsQuarterOfDrop()
        {
            Vector3 expected = new Vector3(0f, -0.5f, 5f);
            Vector3[] points = new Vector3[3];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, 2f, points);

            // Test for: t = 0.5 → drop * t² = 2 * 0.25
            AssertApproximately(expected, points[1]);
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

        [Test]
        public void BuildPoints_Bend_ShiftsLastPointByFullBend()
        {
            Vector3 bend = new Vector3(3f, -2f, 0f);
            Vector3[] points = new Vector3[5];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, bend, points);

            AssertApproximately(new Vector3(3f, -2f, 10f), points[4]);
        }

        [Test]
        public void BuildPoints_Bend_MidPointShiftsByQuarterOfBend()
        {
            Vector3 bend = new Vector3(4f, 0f, 0f);
            Vector3[] points = new Vector3[3];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, bend, points);

            // t = 0.5 → bend * t² = 4 * 0.25
            AssertApproximately(new Vector3(1f, 0f, 5f), points[1]);
        }

        [Test]
        public void BuildPoints_DropOverload_MatchesDownwardBend()
        {
            Vector3[] fromDrop = new Vector3[4];
            Vector3[] fromBend = new Vector3[4];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, 2f, fromDrop);
            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, Vector3.down * 2f, fromBend);

            for (int i = 0; i < fromDrop.Length; i++)
                AssertApproximately(fromDrop[i], fromBend[i]);
        }

        [Test]
        public void BuildPoints_TEndAboveOne_ContinuesParabolaPastRange()
        {
            var points = new Vector3[5];

            WaterArc.BuildPoints(Vector3.zero, Vector3.forward, 10f, Vector3.down * 2f, points, 2f);

            // t = 2: forward = 10 * 2, drop = 2 * 2^2
            AssertApproximately(new Vector3(0f, -8f, 20f), points[4]);
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