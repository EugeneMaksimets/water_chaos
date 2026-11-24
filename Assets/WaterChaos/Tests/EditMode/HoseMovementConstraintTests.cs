using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class HoseMovementConstraintTests
    {
        private const float FreeLength = 10f;
        private const float Tolerance = 1e-4f;

        private static readonly Vector3 Pivot = Vector3.zero;
        private static readonly Vector3 AtLimit = new Vector3(FreeLength, 0f, 0f);

        [Test]
        public void WithSlack_MoveIsUnchanged()
        {
            Vector3 delta = new Vector3(0.1f, 0f, 0.05f);

            Vector3 allowed = HoseMovementConstraint.Constrain(Pivot, FreeLength, new Vector3(5f, 0f, 0f), delta, out bool blocked);

            Assert.AreEqual(delta, allowed);
            Assert.IsFalse(blocked);
        }

        [Test]
        public void AtLimit_MoveTowardPivot_IsAllowed()
        {
            Vector3 delta = new Vector3(-0.1f, 0f, 0f);

            Vector3 allowed = HoseMovementConstraint.Constrain(Pivot, FreeLength, AtLimit, delta, out bool blocked);

            Assert.AreEqual(delta, allowed);
            Assert.IsFalse(blocked);
        }

        [Test]
        public void AtLimit_MoveAway_IsStoppedAndReported()
        {
            Vector3 allowed = HoseMovementConstraint.Constrain(Pivot, FreeLength, AtLimit, new Vector3(0.1f, 0f, 0f), out bool blocked);

            Assert.AreEqual(0f, allowed.magnitude, Tolerance);
            Assert.IsTrue(blocked);
        }

        [Test]
        public void NearLimit_MoveAway_StopsExactlyAtMaxLength()
        {
            Vector3 end = new Vector3(9.95f, 0f, 0f);

            Vector3 allowed = HoseMovementConstraint.Constrain(Pivot, FreeLength, end, new Vector3(0.5f, 0f, 0f), out _);

            Assert.AreEqual(FreeLength, (end + allowed - Pivot).magnitude, Tolerance);
        }

        [Test]
        public void AtLimit_SidewaysMove_SlidesAlongLimit()
        {
            Vector3 allowed = HoseMovementConstraint.Constrain(Pivot, FreeLength, AtLimit, new Vector3(0f, 0f, 0.1f), out bool blocked);

            Assert.Greater(allowed.z, 0.09f);
            Assert.LessOrEqual((AtLimit + allowed - Pivot).magnitude, FreeLength + Tolerance);
            Assert.IsFalse(blocked);
        }

        [Test]
        public void AtLimit_VerticalMove_IsNeverBlocked()
        {
            Vector3 delta = new Vector3(0.1f, -0.2f, 0f);

            Vector3 allowed = HoseMovementConstraint.Constrain(Pivot, FreeLength, AtLimit, delta, out _);

            Assert.AreEqual(-0.2f, allowed.y, Tolerance);
        }

        [Test]
        public void BeyondLimit_DoesNotPullBack()
        {
            Vector3 end = new Vector3(12f, 0f, 0f);

            Vector3 standing = HoseMovementConstraint.Constrain(Pivot, FreeLength, end, Vector3.zero, out _);
            Vector3 away = HoseMovementConstraint.Constrain(Pivot, FreeLength, end, new Vector3(0.1f, 0f, 0f), out _);

            Assert.AreEqual(Vector3.zero, standing);
            Assert.AreEqual(0f, away.magnitude, Tolerance);
        }

        [Test]
        public void NaNDelta_GivesNoMove()
        {
            Vector3 allowed = HoseMovementConstraint.Constrain(Pivot, FreeLength, AtLimit, new Vector3(float.NaN, 0f, 0f), out bool blocked);

            Assert.AreEqual(Vector3.zero, allowed);
            Assert.IsFalse(blocked);
        }

        [Test]
        public void NaNFreeLength_StillAllowsMovingBack()
        {
            Vector3 back = HoseMovementConstraint.Constrain(Pivot, float.NaN, AtLimit, new Vector3(-0.1f, 0f, 0f), out _);
            Vector3 away = HoseMovementConstraint.Constrain(Pivot, float.NaN, AtLimit, new Vector3(0.1f, 0f, 0f), out _);

            Assert.AreEqual(-0.1f, back.x, Tolerance);
            Assert.AreEqual(0f, away.magnitude, Tolerance);
        }
    }
}
