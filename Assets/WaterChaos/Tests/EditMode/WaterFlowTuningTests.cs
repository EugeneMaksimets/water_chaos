using NUnit.Framework;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class WaterFlowTuningTests
    {
        private WaterFlowTuning _tuning;

        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<WaterFlowTuning>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_tuning);
        }

        [TestCase(0f)]
        [TestCase(-100f)]
        [TestCase(float.NaN)]
        public void GetRange_WithoutValidPressure_ReturnsZero(float pressure)
        {
            Assert.AreEqual(0f, _tuning.GetRange(pressure));
        }

        [Test]
        public void GetRange_AboveReference_IsClampedToMaxRange()
        {
            Assert.AreEqual(_tuning.MaxRange, _tuning.GetRange(float.MaxValue), 0.001f);
        }

        [TestCase(0f)]
        [TestCase(-100f)]
        [TestCase(float.NaN)]
        public void GetImpactForce_WithoutValidForce_ReturnZero(float force)
        {
            Assert.AreEqual(0f, _tuning.GetImpactForce(force));
        }

        [Test]
        public void GetImpactForce_AboveReference_IsClampedToMaxForce()
        {
            Assert.AreEqual(_tuning.MaxImpactForce, _tuning.GetImpactForce(float.MaxValue), 0.001f);
        }

        [TestCase(0f)]
        [TestCase(-100f)]
        [TestCase(float.NaN)]
        public void GetPressureRatio_WithoutValidPressure_ReturnsZero(float pressure)
        {
            Assert.AreEqual(0f, _tuning.GetPressureRatio(pressure));
        }

        [Test]
        public void GetPressureRatio_HalfReference_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, _tuning.GetPressureRatio(_tuning.ReferencePressure * 0.5f), 0.001f);
        }

        [Test]
        public void GetPressureRatio_AboveReference_IsClampedToOne()
        {
            Assert.AreEqual(1f, _tuning.GetPressureRatio(float.MaxValue));
        }

    }
}
