using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>Snow puts fire out worse than water, piles into drifts without duplicates, and drifts stop new fires.</summary>
    public class SnowdriftTests
    {
        private static readonly SnowdriftConfig Config = SnowdriftConfig.Default;
        private static readonly Vector3 Floor = Vector3.zero;
        // A fire starts a little above the floor.
        private static readonly Vector3 FireSpot = new Vector3(0.05f, 0.3f, 0f);

        private sealed class FieldBlocker : IFireIgnitionBlocker
        {
            private readonly SnowdriftField _field;
            public FieldBlocker(SnowdriftField field) { _field = field; }
            public bool BlocksIgnitionAt(Vector3 firePosition) => _field.BlocksIgnitionAt(firePosition);
        }

        [Test]
        public void Snow_ExtinguishesLessThanWater()
        {
            float water = LiquidType.Water.ExtinguishingPower(false, 0.2f);
            float snow = LiquidType.Water.ExtinguishingPower(true, 0.2f);

            Assert.AreEqual(1f, water);
            Assert.AreEqual(0.2f, snow, 1e-5f);

            var waterFire = new FireModel(0.033f, 0.05f, 0f);
            var snowFire = new FireModel(0.033f, 0.05f, 0f);
            waterFire.ApplyWater(10f * water);
            snowFire.ApplyWater(10f * snow);
            Assert.Greater(snowFire.Intensity, waterFire.Intensity);
        }

        [Test]
        public void SnowMultiplier_IsClamped_AndOnlyForFreezingLiquids()
        {
            Assert.AreEqual(1f, LiquidType.Water.ExtinguishingPower(true, 5f));
            Assert.AreEqual(0f, LiquidType.Water.ExtinguishingPower(true, -1f));
            Assert.AreEqual(0f, LiquidType.Toxic.ExtinguishingPower(true, 0.2f));
        }

        [Test]
        public void Snowdrifts_Accumulate_WithoutDuplicates()
        {
            var field = new SnowdriftField(Config, 20);
            SnowdriftModel drift = field.AddSnow(Floor, 1f);
            float height = drift.Height;

            for (int i = 0; i < 30; i++)
                field.AddSnow(Floor + new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f)), 0.5f);

            Assert.AreEqual(1, field.Count);
            Assert.Greater(drift.Height, height);
        }

        [Test]
        public void Snowdrift_RespectsMaximumSize()
        {
            var field = new SnowdriftField(Config, 20);
            SnowdriftModel drift = field.AddSnow(Floor, 10000f);

            Assert.AreEqual(Config.MaxRadius, drift.Radius, 1e-4f);
            Assert.AreEqual(Config.MaxHeight, drift.Height, 1e-4f);
            Assert.AreEqual(0f, drift.AddSnow(5f));
        }

        [Test]
        public void SnowdriftLimit_RemovesOldestDrift()
        {
            var field = new SnowdriftField(Config, 1);
            SnowdriftModel oldest = field.AddSnow(Floor, 5f);
            SnowdriftModel removed = null;
            field.Removed += d => removed = d;

            Assert.IsNotNull(field.AddSnow(Floor + new Vector3(10f, 0f, 0f), 5f));
            Assert.AreSame(oldest, removed);
            Assert.AreEqual(1, field.Count);
            Assert.IsFalse(field.BlocksIgnitionAt(FireSpot), "the removed drift no longer blocks fire");
        }

        [Test]
        public void Snowdrifts_AtSeparateLocations_AreSeparate()
        {
            var field = new SnowdriftField(Config, 20);
            field.AddSnow(Floor, 1f);
            field.AddSnow(Floor + new Vector3(5f, 0f, 0f), 1f);

            Assert.AreEqual(2, field.Count);
        }

        [Test]
        public void Snowdrift_BlocksNewFireIgnition()
        {
            var field = new SnowdriftField(Config, 20);
            field.AddSnow(Floor, 5f);

            Assert.IsTrue(field.BlocksIgnitionAt(FireSpot));
            Assert.IsFalse(field.BlocksIgnitionAt(FireSpot + new Vector3(5f, 0f, 0f)), "outside the drift fire is unchanged");

            var blocker = new FieldBlocker(field);
            FireIgnitionRules.Register(blocker);
            try
            {
                Assert.IsTrue(FireIgnitionRules.IsBlocked(FireSpot));
                Assert.IsFalse(FireIgnitionRules.IsBlocked(FireSpot + new Vector3(5f, 0f, 0f)));
            }
            finally
            {
                FireIgnitionRules.Unregister(blocker);
            }

            Assert.IsFalse(FireIgnitionRules.IsBlocked(FireSpot));
        }

        [Test]
        public void FireSpreader_RespectsIgnitionRules()
        {
            var settings = ScriptableObject.CreateInstance<FireSpreadSettings>();
            var field = new SnowdriftField(Config, 20);
            field.AddSnow(Floor, 5f);
            var blocker = new FieldBlocker(field);
            try
            {
                bool before = FireSpreader.CanBurnAt(FireSpot, settings);
                FireIgnitionRules.Register(blocker);
                Assert.IsFalse(FireSpreader.CanBurnAt(FireSpot, settings));
                FireIgnitionRules.Unregister(blocker);
                Assert.AreEqual(before, FireSpreader.CanBurnAt(FireSpot, settings));
            }
            finally
            {
                FireIgnitionRules.Unregister(blocker);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void FireCanIgniteAgain_AfterSnowdriftMelts()
        {
            var zones = new FakeTemperatureZones();
            FakeZone room = zones.Add(Vector3.zero, new Vector3(10f, 4f, 10f), -5f);
            var field = new SnowdriftField(Config, 20);
            field.AddSnow(Floor, 5f);
            int removed = 0;
            field.Removed += _ => removed++;

            field.Tick(30f, zones);
            Assert.IsTrue(field.BlocksIgnitionAt(FireSpot), "cold room: the drift stays");

            room.Temperature = 10f;
            for (int i = 0; i < 100; i++)
                field.Tick(1f, zones);

            Assert.AreEqual(0, field.Count);
            Assert.AreEqual(1, removed);
            Assert.IsFalse(field.BlocksIgnitionAt(FireSpot));
        }

        [Test]
        public void SnowdriftOutdoors_MeltsAway()
        {
            var field = new SnowdriftField(Config, 20);
            field.AddSnow(new Vector3(50f, 0f, 0f), 5f);

            for (int i = 0; i < 100; i++)
                field.Tick(1f, new FakeTemperatureZones());

            Assert.AreEqual(0, field.Count);
        }
    }
}
