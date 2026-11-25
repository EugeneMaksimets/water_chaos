using NUnit.Framework;
using WaterChaos.UI;

namespace WaterChaos.Tests
{
    public class HeatWarningHintTests
    {
        private const float Threshold = 80f;
        private const float Duration = 3f;
        private const float RearmMargin = 5f;

        private static HeatWarningHint CreateHint() => new HeatWarningHint(Threshold, Duration, RearmMargin);

        [Test]
        public void BelowThreshold_IsHidden()
        {
            HeatWarningHint hint = CreateHint();

            hint.Tick(79f, 0.1f);

            Assert.IsFalse(hint.IsVisible);
        }

        [Test]
        public void ReachingThreshold_ShowsHint()
        {
            HeatWarningHint hint = CreateHint();
            hint.Tick(60f, 0.1f);

            hint.Tick(80f, 0.1f);

            Assert.IsTrue(hint.IsVisible);
        }

        [Test]
        public void Hint_HidesAfterDuration_EvenIfStillHot()
        {
            HeatWarningHint hint = CreateHint();
            hint.Tick(85f, 0.1f);

            hint.Tick(90f, 2.9f);
            Assert.IsTrue(hint.IsVisible);

            hint.Tick(95f, 0.2f);
            Assert.IsFalse(hint.IsVisible);
        }

        [Test]
        public void StayingHot_DoesNotShowAgain()
        {
            HeatWarningHint hint = CreateHint();
            hint.Tick(85f, 0.1f);
            hint.Tick(85f, Duration);

            hint.Tick(99f, 10f);

            Assert.IsFalse(hint.IsVisible);
        }

        [Test]
        public void SmallDipAroundThreshold_DoesNotShowAgain()
        {
            HeatWarningHint hint = CreateHint();
            hint.Tick(80f, 0.1f);
            hint.Tick(80f, Duration);

            hint.Tick(77f, 0.1f);
            hint.Tick(81f, 0.1f);

            Assert.IsFalse(hint.IsVisible);
        }

        [Test]
        public void CoolingWellBelowThreshold_LetsHintShowAgain()
        {
            HeatWarningHint hint = CreateHint();
            hint.Tick(85f, 0.1f);
            hint.Tick(85f, Duration);

            hint.Tick(70f, 0.1f);
            hint.Tick(80f, 0.1f);

            Assert.IsTrue(hint.IsVisible);
        }

        [Test]
        public void Reset_HidesHint_AndLetsItShowAgain()
        {
            HeatWarningHint hint = CreateHint();
            hint.Tick(85f, 0.1f);

            hint.Reset();
            Assert.IsFalse(hint.IsVisible);

            hint.Tick(85f, 0.1f);
            Assert.IsTrue(hint.IsVisible);
        }

        [Test]
        public void InvalidDeltaTime_DoesNotShortenHint()
        {
            HeatWarningHint hint = CreateHint();
            hint.Tick(85f, 0.1f);

            hint.Tick(85f, float.NaN);
            hint.Tick(85f, -10f);

            Assert.IsTrue(hint.IsVisible);
        }
    }
}
