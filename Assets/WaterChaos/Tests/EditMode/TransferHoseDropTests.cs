using NUnit.Framework;
using UnityEngine;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    public class TransferHoseDropTests
    {
        private const float Tolerance = 0.0001f;
        private const float Clearance = 1f;

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        [Test]
        public void KeepClear_FarEnough_IsUnchanged()
        {
            Vector3 preferred = new Vector3(0f, 0.5f, 3f);

            Vector3 spot = TransferHoseDrop.KeepClear(Vector3.zero, preferred, Clearance, Vector3.right);

            Assert.AreEqual(preferred, spot);
        }

        [Test]
        public void KeepClear_TooClose_IsPushedAwayFromPlayerKeepingHeight()
        {
            Vector3 player = new Vector3(1f, 0f, 1f);
            Vector3 preferred = new Vector3(1f, 1.4f, 1.3f);

            Vector3 spot = TransferHoseDrop.KeepClear(player, preferred, Clearance, Vector3.right);

            Assert.AreEqual(Clearance, HorizontalDistance(player, spot), Tolerance);
            Assert.AreEqual(1f, spot.x, Tolerance);
            Assert.Greater(spot.z, player.z);
            Assert.AreEqual(1.4f, spot.y, Tolerance);
        }

        [Test]
        public void KeepClear_RightAbovePlayer_UsesFallbackDirection()
        {
            Vector3 spot = TransferHoseDrop.KeepClear(Vector3.zero, new Vector3(0f, 1f, 0f), Clearance, Vector3.back);

            Assert.AreEqual(-Clearance, spot.z, Tolerance);
            Assert.AreEqual(0f, spot.x, Tolerance);
        }

        [Test]
        public void GetCandidate_First_IsPreferredSpotKeptClear()
        {
            Vector3 preferred = new Vector3(0f, 0f, 0.2f);

            Vector3 spot = TransferHoseDrop.GetCandidate(0, Vector3.zero, preferred, Vector3.forward, Clearance);

            Assert.AreEqual(Clearance, HorizontalDistance(Vector3.zero, spot), Tolerance);
            Assert.Greater(spot.z, 0f);
        }

        [Test]
        public void GetCandidate_Others_GoRightLeftBackThenForward()
        {
            Vector3 preferred = new Vector3(0f, 1f, 5f);

            Vector3 right = TransferHoseDrop.GetCandidate(1, Vector3.zero, preferred, Vector3.forward, Clearance);
            Vector3 left = TransferHoseDrop.GetCandidate(2, Vector3.zero, preferred, Vector3.forward, Clearance);
            Vector3 back = TransferHoseDrop.GetCandidate(3, Vector3.zero, preferred, Vector3.forward, Clearance);
            Vector3 forward = TransferHoseDrop.GetCandidate(4, Vector3.zero, preferred, Vector3.forward, Clearance);

            Assert.AreEqual(Clearance, right.x, Tolerance);
            Assert.AreEqual(-Clearance, left.x, Tolerance);
            Assert.AreEqual(-Clearance, back.z, Tolerance);
            Assert.AreEqual(Clearance, forward.z, Tolerance);
            Assert.AreEqual(1f, right.y, Tolerance, "Side spots keep the preferred height.");
            Assert.AreEqual(5, TransferHoseDrop.CandidateCount);
        }
    }
}
