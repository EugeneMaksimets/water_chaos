using NUnit.Framework;
using WaterChaos.Surfaces;

namespace WaterChaos.Tests
{
    public class IceSlipModelTests
    {
        private const float Chance = 0.3f;

        [Test]
        public void MovingOntoIce_RollBelowChance_Slips()
        {
            var ice = new IceSlipModel(Chance);

            Assert.IsTrue(ice.Step(onIce: true, moving: true, roll: 0.1f));
        }

        [Test]
        public void MovingOntoIce_RollAtOrAboveChance_DoesNotSlip()
        {
            var ice = new IceSlipModel(Chance);

            Assert.IsFalse(ice.Step(onIce: true, moving: true, roll: 0.3f));
        }

        [Test]
        public void StandingStillOnIce_NeverRolls_UntilMoving()
        {
            var ice = new IceSlipModel(Chance);

            Assert.IsFalse(ice.Step(onIce: true, moving: false, roll: 0f));
            Assert.IsTrue(ice.Step(onIce: true, moving: true, roll: 0f));
        }

        [Test]
        public void OneRollPerVisit_NoMoreChecksUntilLeavingIce()
        {
            var ice = new IceSlipModel(Chance);

            Assert.IsFalse(ice.Step(onIce: true, moving: true, roll: 0.9f));
            Assert.IsFalse(ice.Step(onIce: true, moving: true, roll: 0f));

            ice.Step(onIce: false, moving: true, roll: 0f);

            Assert.IsTrue(ice.Step(onIce: true, moving: true, roll: 0f));
        }

        [Test]
        public void OffIce_NeverSlips()
        {
            var ice = new IceSlipModel(Chance);

            Assert.IsFalse(ice.Step(onIce: false, moving: true, roll: 0f));
        }

        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(5f, 1f)]
        public void Chance_IsClampedToZeroOne(float chance, float expected)
        {
            Assert.AreEqual(expected, new IceSlipModel(chance).Chance);
        }
    }
}
