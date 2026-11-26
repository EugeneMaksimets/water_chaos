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

        private static readonly object TargetA = new object();
        private static readonly object TargetB = new object();

        [Test]
        public void StreamIgnition_LightsOnlyOnFireWithFlammableFlow()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);

            stream.Tick(Step, false, true, null);
            Assert.IsFalse(stream.IsLit, "water touching fire");

            stream.Tick(Step, true, false, null);
            Assert.IsFalse(stream.IsLit, "gasoline not touching fire");

            stream.Tick(Step, true, true, null);
            Assert.IsTrue(stream.IsLit);
        }

        [Test]
        public void StreamIgnition_RaisesLitChangedOnceOnIgnitionAndOnceOnReset()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);
            int lit = 0;
            int unlit = 0;
            stream.LitChanged += value => { if (value) lit++; else unlit++; };

            stream.Tick(Step, true, true, null);
            stream.Tick(Step, true, true, null);
            stream.Tick(Step, false, false, null);
            stream.Tick(Step, false, false, null);

            Assert.AreEqual(1, lit);
            Assert.AreEqual(1, unlit);
        }

        [Test]
        public void StreamIgnition_StaysLitWhileFlowingAndGoesOutWhenFlowStops()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);
            stream.Tick(Step, true, true, null);

            stream.Tick(Step, true, false, null);
            Assert.IsTrue(stream.IsLit);

            stream.Tick(Step, false, false, null);
            Assert.IsFalse(stream.IsLit);
        }

        [Test]
        public void StreamIgnition_SwitchedOffAndOnAgain_StartsUnlit()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);
            stream.Tick(Step, true, true, null);

            stream.Tick(Step, false, false, null);
            stream.Tick(Step, true, false, TargetA);

            Assert.IsFalse(stream.IsLit);
        }

        [Test]
        public void StreamIgnition_SameTarget_IgnitesAtMostOncePerInterval()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);
            stream.Tick(Step, true, true, null);

            int attempts = 0;
            for (int i = 0; i < 100; i++)
            {
                if (stream.Tick(Step, true, false, TargetA))
                    attempts++;
            }

            // 100 steps of 0.02 s = 2 s: the first attempt at once, then one every 0.5 s.
            Assert.AreEqual(4, attempts);
        }

        [Test]
        public void StreamIgnition_NewTarget_IsTriedAtOnce()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);
            stream.Tick(Step, true, true, null);

            Assert.IsTrue(stream.Tick(Step, true, false, TargetA));
            Assert.IsFalse(stream.Tick(Step, true, false, TargetA));
            Assert.IsTrue(stream.Tick(Step, true, false, TargetB));
        }

        [Test]
        public void StreamIgnition_DefaultInterval_Is075Seconds()
        {
            Assert.AreEqual(0.75f, StreamIgnitionModel.DefaultInterval, 1e-6f);
        }

        [Test]
        public void StreamIgnition_UnlitStream_NeverIgnites()
        {
            StreamIgnitionModel stream = new StreamIgnitionModel(Interval);

            for (int i = 0; i < 100; i++)
                Assert.IsFalse(stream.Tick(Step, true, false, TargetA));
        }

        [Test]
        public void OutdoorFire_WithoutReigniteDelay_StaysOut()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold, 0f);
            fire.ApplyWater(1000f);

            fire.Tick(1000f);

            Assert.IsFalse(fire.IsBurning);
        }

        [Test]
        public void ExtinguishedFire_CanBeRelitAtOnce_WithoutWaiting()
        {
            FireModel fire = new FireModel(CoolingPerLiter, Threshold, 0f);
            fire.ApplyWater(1000f);

            fire.Reignite();

            Assert.IsTrue(fire.IsBurning);
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

        [Test]
        public void Flamethrower_InCooledRoom_IgnitesByDefault()
        {
            FakeArea cooledRoom = new FakeArea(false, true);

            Assert.IsTrue(FuelStreamIgnition.AllowsIgnitionIn(cooledRoom, Vector3.zero, false));
        }

        [Test]
        public void Flamethrower_InCooledRoom_CanBeSetToRespectCooling()
        {
            FakeArea cooledRoom = new FakeArea(false, true);

            Assert.IsFalse(FuelStreamIgnition.AllowsIgnitionIn(cooledRoom, Vector3.zero, true));
            Assert.IsTrue(FuelStreamIgnition.AllowsIgnitionIn(null, Vector3.zero, true));
        }

        [Test]
        public void IsFloorLike_OnlyUpwardSurfaces()
        {
            Assert.IsTrue(FuelStreamIgnition.IsFloorLike(Vector3.up));
            Assert.IsTrue(FuelStreamIgnition.IsFloorLike(new Vector3(0.3f, 1f, 0f)));
            Assert.IsFalse(FuelStreamIgnition.IsFloorLike(Vector3.right));
            Assert.IsFalse(FuelStreamIgnition.IsFloorLike(Vector3.down));
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
