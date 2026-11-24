using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class HoseModelTests
    {
        private const float MaxLength = 15f;
        private const float Tolerance = 1e-4f;

        private static HoseModel CreateHose(Vector3 anchor, Vector3 end)
        {
            HoseModel hose = new HoseModel(MaxLength);
            hose.Reset(anchor, end);
            return hose;
        }

        [Test]
        public void Reset_EndAtAnchor_LengthIsZero()
        {
            HoseModel hose = CreateHose(Vector3.zero, Vector3.zero);

            Assert.AreEqual(0f, hose.Length, Tolerance);
            Assert.AreEqual(MaxLength, hose.RemainingLength, Tolerance);
            Assert.AreEqual(0, hose.WrapCount);
            Assert.IsFalse(hose.IsFullyExtended);
        }

        [Test]
        public void Length_SumsAllSegments_NotStraightDistance()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(3f, 0f, 4f));
            hose.AddWrap(new Vector3(3f, 0f, 0f), Vector3.up);

            // 3 m to the wrap point + 4 m to the end; the straight distance would be 5 m.
            Assert.AreEqual(7f, hose.Length, Tolerance);
        }

        [Test]
        public void RemainingLength_IsMaxMinusLength()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(10f, 0f, 0f));

            Assert.AreEqual(5f, hose.RemainingLength, Tolerance);
        }

        [Test]
        public void FreeSegmentLength_ExcludesFixedPart()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(3f, 0f, 4f));
            hose.AddWrap(new Vector3(3f, 0f, 0f), Vector3.up);

            Assert.AreEqual(MaxLength - 3f, hose.FreeSegmentLength, Tolerance);
        }

        [Test]
        public void IsFullyExtended_OnlyAtMaxLength()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(14.9f, 0f, 0f));
            Assert.IsFalse(hose.IsFullyExtended);

            hose.SetEnd(new Vector3(MaxLength, 0f, 0f));
            Assert.IsTrue(hose.IsFullyExtended);
        }

        [Test]
        public void AddWrap_IncreasesLength()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(10f, 0f, 0f));
            float straight = hose.Length;

            hose.AddWrap(new Vector3(5f, 0f, 2f), Vector3.up);

            Assert.Greater(hose.Length, straight);
            Assert.AreEqual(1, hose.WrapCount);
        }

        [Test]
        public void RemoveLastWrap_ReducesLength()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(10f, 0f, 0f));
            hose.AddWrap(new Vector3(5f, 0f, 2f), Vector3.up);
            float wrapped = hose.Length;

            Assert.IsTrue(hose.RemoveLastWrap());

            Assert.Less(hose.Length, wrapped);
            Assert.AreEqual(10f, hose.Length, Tolerance);
            Assert.IsFalse(hose.RemoveLastWrap());
        }

        [Test]
        public void SetEnd_MovingBackTowardAnchor_RestoresRemainingLength()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(MaxLength, 0f, 0f));
            Assert.AreEqual(0f, hose.RemainingLength, Tolerance);

            hose.SetEnd(new Vector3(6f, 0f, 0f));

            Assert.AreEqual(9f, hose.RemainingLength, Tolerance);
        }

        [Test]
        public void RemainingLength_BeyondMax_IsZeroNotNegative()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(20f, 0f, 0f));

            Assert.AreEqual(0f, hose.RemainingLength);
            Assert.IsTrue(hose.IsFullyExtended);
        }

        [Test]
        public void NaNInputs_AreIgnored()
        {
            HoseModel hose = CreateHose(Vector3.zero, new Vector3(5f, 0f, 0f));
            Vector3 nan = new Vector3(float.NaN, 0f, 0f);

            Assert.IsFalse(hose.SetEnd(nan));
            Assert.IsFalse(hose.SetAnchor(nan));
            Assert.IsFalse(hose.AddWrap(nan, Vector3.up));
            Assert.IsFalse(hose.AddWrap(Vector3.one, Vector3.zero));
            Assert.IsFalse(hose.Reset(nan, Vector3.zero));

            Assert.AreEqual(5f, hose.Length, Tolerance);
            Assert.AreEqual(0, hose.WrapCount);
        }

        [Test]
        public void Constructor_InvalidMaxLength_UsesMinimum()
        {
            Assert.AreEqual(HoseModel.MinMaxLength, new HoseModel(float.NaN).MaxLength);
            Assert.AreEqual(HoseModel.MinMaxLength, new HoseModel(-3f).MaxLength);
        }
    }
}
