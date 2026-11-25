using NUnit.Framework;
using UnityEngine;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    public class TransferHosePlacementTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void GroundWithinReach_IsUsedAsIs()
        {
            Vector3 ground = new Vector3(3f, 0f, 4f);

            Assert.IsTrue(TransferHosePlacement.TryResolve(Vector3.zero, 10f, true, ground, out Vector3 point));
            Assert.AreEqual(ground, point);
        }

        [Test]
        public void GroundTooFar_IsPulledBackToHoseLength()
        {
            Assert.IsTrue(TransferHosePlacement.TryResolve(Vector3.zero, 5f, true, new Vector3(0f, 0f, 8f), out Vector3 point));
            Assert.AreEqual(5f, point.magnitude, Tolerance);
            Assert.AreEqual(5f, point.z, Tolerance);
        }

        [Test]
        public void NoGround_IsRejected()
        {
            Assert.IsFalse(TransferHosePlacement.TryResolve(Vector3.zero, 5f, false, new Vector3(1f, 0f, 0f), out _));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void NoHoseLeft_IsRejected(float reach)
        {
            Assert.IsFalse(TransferHosePlacement.TryResolve(Vector3.zero, reach, true, new Vector3(1f, 0f, 0f), out _));
        }

        [Test]
        public void InvalidGroundPoint_IsRejected()
        {
            Assert.IsFalse(TransferHosePlacement.TryResolve(Vector3.zero, 5f, true, new Vector3(float.NaN, 0f, 0f), out _));
        }
    }
}
