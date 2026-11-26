using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    /// <summary>Gasoline feeds fire instead of putting it out, within limits; the lit stream ignites at a steady rate.</summary>
    public class FuelFireTests
    {
        private const float CoolingPerLiter = 0.1f;
        private const float Threshold = 0.05f;
        private const float MaxIntensity = 2f;
        private const float Interval = 0.5f;
        private const float Step = 0.02f;

        [Test]
        public void Water_StillCoolsTheFire()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold);

            fire.ApplyWater(5f);

            Assert.AreEqual(0.5f, fire.Intensity, 1e-4f);
        }

        [Test]
        public void Fuel_RaisesBurningFireIntensity()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold);

            fire.AddFuel(0.5f, MaxIntensity);

            Assert.AreEqual(1.5f, fire.Intensity, 1e-4f);
        }

        [Test]
        public void Fuel_NeverExceedsTheMaximum()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold);

            for (int i = 0; i < 1000; i++)
                fire.AddFuel(0.5f, MaxIntensity);

            Assert.AreEqual(MaxIntensity, fire.Intensity, 1e-4f);
        }

        [Test]
        public void Fuel_OnExtinguishedFire_DoesNothing()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold);
            fire.ApplyWater(1000f);

            fire.AddFuel(0.5f, MaxIntensity);

            Assert.IsFalse(fire.IsBurning);
        }

        [Test]
        public void Overburn_FadesBackToNormalButNotBelow()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold);
            fire.AddFuel(0.5f, MaxIntensity);

            fire.ReduceOverburn(0.3f);
            Assert.AreEqual(1.2f, fire.Intensity, 1e-4f);

            fire.ReduceOverburn(5f);
            Assert.AreEqual(FireModel.FullIntensity, fire.Intensity, 1e-4f);
        }

        [Test]
        public void OverburnedFire_NeedsMoreWater()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold);
            fire.AddFuel(1f, MaxIntensity);

            fire.ApplyWater(9f);

            Assert.IsTrue(fire.IsBurning);
        }

        [Test]
        public void StreamIgnition_LightsOnlyOnFireWithFlammableFlow()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);

            stream.Tick(Step, false, true, false);
            Assert.IsFalse(stream.IsLit, "water touching fire");

            stream.Tick(Step, true, false, false);
            Assert.IsFalse(stream.IsLit, "gasoline not touching fire");

            stream.Tick(Step, true, true, false);
            Assert.IsTrue(stream.IsLit);
        }

        [Test]
        public void StreamIgnition_StaysLitWhileFlowingAndGoesOutWhenFlowStops()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);
            stream.Tick(Step, true, true, false);

            stream.Tick(Step, true, false, false);
            Assert.IsTrue(stream.IsLit);

            stream.Tick(Step, false, false, false);
            Assert.IsFalse(stream.IsLit);
        }

        [Test]
        public void StreamIgnition_RepeatedHits_IgniteAtMostOncePerInterval()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);
            stream.Tick(Step, true, true, false);

            int attempts = 0;
            for (int i = 0; i < 100; i++)
            {
                if (stream.Tick(Step, true, false, true))
                    attempts++;
            }

            // 100 steps of 0.02 s = 2 s: the first attempt at once, then one every 0.5 s.
            Assert.AreEqual(4, attempts);
        }

        [Test]
        public void StreamIgnition_UnlitStream_NeverIgnites()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);

            for (int i = 0; i < 100; i++)
                Assert.IsFalse(stream.Tick(Step, true, false, true));
        }

        [Test]
        public void Ignition_OutdoorsOrInUnprotectedRoom_IsAllowed()
        {
            Assert.IsTrue(FireSpreader.AllowsIgnition(null, Vector3.zero));
            Assert.IsTrue(FireSpreader.AllowsIgnition(new FakeArea(true, true), Vector3.zero));
        }

        [Test]
        public void Ignition_InCooledRoom_IsBlocked()
        {
            Assert.IsFalse(FireSpreader.AllowsIgnition(new FakeArea(false, true), Vector3.zero));
        }

        [Test]
        public void Ignition_OutsideTheRoomVolume_IsNotBlockedByIt()
        {
            Assert.IsTrue(FireSpreader.AllowsIgnition(new FakeArea(false, false), Vector3.zero));
        }

        private sealed class FakeArea : IFireArea
        {
            private readonly bool _contains;

            public FakeArea(bool allowsSpread, bool contains)
            {
                AllowsSpread = allowsSpread;
                _contains = contains;
            }

            public bool AllowsSpread { get; }

            public bool Contains(Vector3 firePosition) => _contains;
        }
    }
}
