using NUnit.Framework;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    public class FireModelTests
    {
        private const float CoolingPerLiter = 0.1f;
        private const float Threshold = 0.2f;

        private FireModel CreateFire() => new FireModel(CoolingPerLiter, Threshold);

        [Test]
        public void NewFire_IsBurningAtFullIntensity()
        {
            FireModel fire = CreateFire();

            Assert.IsTrue(fire.IsBurning);
            Assert.AreEqual(1f, fire.Intensity);
        }

        [Test]
        public void ApplyWater_ReducesIntensityByVolumeTimesCooling()
        {
            FireModel fire = CreateFire();

            fire.ApplyWater(2f);

            Assert.AreEqual(0.8f, fire.Intensity, 0.0001f);
            Assert.IsTrue(fire.IsBurning);
        }

        [Test]
        public void ApplyWater_ReachingThreshold_Extinguishes()
        {
            FireModel fire = CreateFire();

            fire.ApplyWater(8f); // 1 - 0.8 = 0.2 -> на пороге

            Assert.AreEqual(0f, fire.Intensity);
            Assert.IsFalse(fire.IsBurning);
        }

        [Test]
        public void ApplyWater_HugeVolume_IntensityNeverNegative()
        {
            FireModel fire = CreateFire();

            fire.ApplyWater(1000f);

            Assert.AreEqual(0f, fire.Intensity);
        }

        [TestCase(-5f)]
        [TestCase(float.NaN)]
        public void ApplyWater_InvalidVolume_IsIgnored(float volume)
        {
            FireModel fire = CreateFire();

            fire.ApplyWater(volume);

            Assert.AreEqual(1f, fire.Intensity);
        }

        [Test]
        public void ApplyWater_WhenExtinguished_StaysZero()
        {
            FireModel fire = CreateFire();
            fire.ApplyWater(1000f);

            fire.ApplyWater(1f);

            Assert.AreEqual(0f, fire.Intensity);
            Assert.IsFalse(fire.IsBurning);
        }
    }
}