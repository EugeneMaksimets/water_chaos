using NUnit.Framework;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    public class FireSpreadModelTests
    {
        private const float Interval = 4f;
        private const float MinIntensity = 0.5f;
        private const float Success = 0f;
        private const float Failure = 0.99f;

        private FireSpreadModel CreateModel(float chance = 1f) => new FireSpreadModel(Interval, MinIntensity, chance);

        [Test]
        public void StrongFire_BeforeInterval_DoesNotSpread()
        {
            FireSpreadModel model = CreateModel();

            Assert.IsFalse(model.Tick(Interval - 0.1f, 1f, Success));
        }

        [Test]
        public void StrongFire_AfterInterval_Spreads()
        {
            FireSpreadModel model = CreateModel();

            model.Tick(Interval - 0.1f, 1f, Success);

            Assert.IsTrue(model.Tick(0.2f, 1f, Success));
        }

        [Test]
        public void AfterSpreading_WaitsFullIntervalAgain()
        {
            FireSpreadModel model = CreateModel();
            model.Tick(Interval, 1f, Success);

            Assert.IsFalse(model.Tick(Interval - 0.1f, 1f, Success));
        }

        [Test]
        public void WeakFire_NeverSpreads()
        {
            FireSpreadModel model = CreateModel();

            Assert.IsFalse(model.Tick(Interval * 10f, MinIntensity - 0.01f, Success));
        }

        [Test]
        public void ExtinguishedFire_NeverSpreads()
        {
            FireSpreadModel model = CreateModel();

            Assert.IsFalse(model.Tick(Interval * 10f, 0f, Success));
        }

        [Test]
        public void ZeroMinIntensity_ExtinguishedFireStillDoesNotSpread()
        {
            FireSpreadModel model = new FireSpreadModel(Interval, 0f, 1f);

            Assert.IsFalse(model.Tick(Interval * 10f, 0f, Success));
        }

        [Test]
        public void WeakeningFire_RestartsTheWait()
        {
            FireSpreadModel model = CreateModel();
            model.Tick(Interval - 0.1f, 1f, Success);

            model.Tick(0.1f, 0f, Success);

            Assert.IsFalse(model.Tick(0.2f, 1f, Success));
        }

        [Test]
        public void FailedChance_DoesNotSpread_AndWaitsFullIntervalAgain()
        {
            FireSpreadModel model = CreateModel(0.5f);

            Assert.IsFalse(model.Tick(Interval, 1f, Failure));
            Assert.IsFalse(model.Tick(Interval - 0.1f, 1f, Success));
            Assert.IsTrue(model.Tick(0.2f, 1f, Success));
        }

        [Test]
        public void ZeroChance_NeverSpreads()
        {
            FireSpreadModel model = CreateModel(0f);

            Assert.IsFalse(model.Tick(Interval, 1f, Success));
        }

        [Test]
        public void ZeroInterval_StillWaitsMinimumInterval()
        {
            FireSpreadModel model = new FireSpreadModel(0f, MinIntensity, 1f);

            Assert.IsFalse(model.Tick(0.01f, 1f, Success));
            Assert.IsTrue(model.Tick(FireSpreadModel.MinInterval, 1f, Success));
        }

        [Test]
        public void InvalidDeltaTime_IsIgnored()
        {
            FireSpreadModel model = CreateModel();

            Assert.IsFalse(model.Tick(float.NaN, 1f, Success));
            Assert.IsFalse(model.Tick(-Interval * 2f, 1f, Success));
            Assert.IsFalse(model.Tick(Interval - 0.1f, 1f, Success));
        }
    }
}
