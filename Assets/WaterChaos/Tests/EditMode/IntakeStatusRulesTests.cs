using NUnit.Framework;
using WaterChaos.Pump;

namespace WaterChaos.Tests
{
    public class IntakeStatusRulesTests
    {
        [TestCase(IntakeStatus.NotInWater, true)]
        [TestCase(IntakeStatus.NotSubmerged, true)]
        [TestCase(IntakeStatus.TooShallow, true)]
        [TestCase(IntakeStatus.Submerged, false)]
        [TestCase(IntakeStatus.Tilted, false)]
        [TestCase(IntakeStatus.SourceEmpty, false)]
        public void NeedsWater_OnlyWhenIntakeIsNotProperlyInWater(IntakeStatus status, bool expected)
        {
            Assert.AreEqual(expected, IntakeStatusRules.NeedsWater(status));
        }
    }
}
