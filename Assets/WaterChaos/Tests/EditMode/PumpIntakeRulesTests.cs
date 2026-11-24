using NUnit.Framework;
using WaterChaos.Pump;

namespace WaterChaos.Tests
{
    public class PumpIntakeRulesTests
    {
        private const float RequiredSubmersion = 0.2f;
        private const float MinWaterDepth = 0.5f;
        private const float MaxTilt = 60f;

        private readonly PumpIntakeRules _rules = new PumpIntakeRules(RequiredSubmersion, MinWaterDepth, MaxTilt);

        [Test]
        public void IntakeDeepInFullSource_IsSubmerged()
        {
            IntakeStatus status = _rules.Evaluate(true, 0.5f, 1.5f, 0f, false);

            Assert.AreEqual(IntakeStatus.Submerged, status);
        }

        [Test]
        public void NoSource_IsNotInWater()
        {
            IntakeStatus status = _rules.Evaluate(false, 0.5f, 1.5f, 0f, false);

            Assert.AreEqual(IntakeStatus.NotInWater, status);
        }

        [Test]
        public void IntakeAboveRequiredDepth_IsNotSubmerged()
        {
            IntakeStatus status = _rules.Evaluate(true, 0.1f, 1.5f, 0f, false);

            Assert.AreEqual(IntakeStatus.NotSubmerged, status);
        }

        [Test]
        public void IntakeAboveSurface_IsNotSubmerged()
        {
            IntakeStatus status = _rules.Evaluate(true, -0.3f, 1.5f, 0f, false);

            Assert.AreEqual(IntakeStatus.NotSubmerged, status);
        }

        [Test]
        public void ShallowWater_IsTooShallow()
        {
            IntakeStatus status = _rules.Evaluate(true, 0.3f, 0.4f, 0f, false);

            Assert.AreEqual(IntakeStatus.TooShallow, status);
        }

        [Test]
        public void TiltBeyondLimit_IsTilted()
        {
            IntakeStatus status = _rules.Evaluate(true, 0.5f, 1.5f, 80f, false);

            Assert.AreEqual(IntakeStatus.Tilted, status);
        }

        [Test]
        public void EmptySource_IsSourceEmpty()
        {
            IntakeStatus status = _rules.Evaluate(true, 0.5f, 1.5f, 0f, true);

            Assert.AreEqual(IntakeStatus.SourceEmpty, status);
        }

        [Test]
        public void ExactlyRequiredValues_AreAccepted()
        {
            IntakeStatus status = _rules.Evaluate(true, RequiredSubmersion, MinWaterDepth, MaxTilt, false);

            Assert.AreEqual(IntakeStatus.Submerged, status);
        }

        [Test]
        public void NaNReadings_AreNotAccepted()
        {
            IntakeStatus status = _rules.Evaluate(true, float.NaN, 1.5f, 0f, false);

            Assert.AreNotEqual(IntakeStatus.Submerged, status);
        }

        [Test]
        public void Constructor_ClampsInvalidSettings()
        {
            PumpIntakeRules rules = new PumpIntakeRules(-1f, float.NaN, 500f);

            Assert.AreEqual(0f, rules.RequiredSubmersion);
            Assert.AreEqual(0f, rules.MinWaterDepth);
            Assert.AreEqual(180f, rules.MaxTiltAngle);
        }
    }
}
