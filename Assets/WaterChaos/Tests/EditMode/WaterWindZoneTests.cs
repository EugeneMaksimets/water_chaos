using NUnit.Framework;
using UnityEngine;
using WaterChaos.Environment;

namespace WaterChaos.Tests
{
    public class WaterWindZoneTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void CalculateEndOffset_ScalesWithStrengthAndRange()
        {
            Vector3 offset = WaterWindZone.CalculateEndOffset(Vector3.right, 0.3f, 10f);

            AssertApproximately(new Vector3(3f, 0f, 0f), offset);
        }

        [Test]
        public void CalculateEndOffset_NormalizesDirection()
        {
            Vector3 offset = WaterWindZone.CalculateEndOffset(new Vector3(5f, 0f, 0f), 0.5f, 2f);

            AssertApproximately(new Vector3(1f, 0f, 0f), offset);
        }

        [Test]
        public void CalculateEndOffset_NegativeStrength_IsZero()
        {
            Vector3 offset = WaterWindZone.CalculateEndOffset(Vector3.right, -1f, 10f);

            AssertApproximately(Vector3.zero, offset);
        }

        [Test]
        public void CalculateEndOffset_NegativeRange_IsZero()
        {
            Vector3 offset = WaterWindZone.CalculateEndOffset(Vector3.right, 0.3f, -5f);

            AssertApproximately(Vector3.zero, offset);
        }

        private static void AssertApproximately(Vector3 expected, Vector3 actual)
        {
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(Tolerance),
                $"Expected {expected}, got {actual}");
        }
    }
}