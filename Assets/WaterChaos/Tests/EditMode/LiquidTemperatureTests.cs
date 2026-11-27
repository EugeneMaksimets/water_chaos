using NUnit.Framework;
using UnityEngine;
using WaterChaos.Environment;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// Freezing needs a real temperature zone: no zone (outdoors) is never "0 °C". Water turns to snow and ice
    /// only in a cold zone; gasoline and toxic liquid never do; snow keeps the liquid type and the soap.
    /// </summary>
    public class LiquidTemperatureTests
    {
        private static readonly PuddleConfig Config = PuddleConfig.Default;
        private static readonly Vector3 Indoor = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 Outdoor = new Vector3(50f, 0f, 0f);
        private const float LongTime = 60f;

        private static FakeTemperatureZones ColdBuilding(float temperature = -5f)
        {
            var zones = new FakeTemperatureZones();
            zones.Add(Indoor, new Vector3(10f, 4f, 10f), temperature);
            return zones;
        }

        private static void TickFor(PuddleField field, ITemperatureProvider zones, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.25f)
                field.Tick(0.25f, zones);
        }

        // ───────────── Zones ─────────────

        [Test]
        public void Resolver_NoZone_HasNoTemperature()
        {
            var resolver = new SurfaceTemperatureResolver();
            resolver.Add(new FakeZone(new Bounds(Indoor, Vector3.one * 10f), -5f));

            Assert.IsTrue(resolver.TryGetTemperature(Indoor, out float inside));
            Assert.AreEqual(-5f, inside);
            Assert.IsFalse(resolver.TryGetTemperature(Outdoor, out _));
        }

        [Test]
        public void FreezingRules_NoZone_NeverFreezes_EvenBelowThreshold()
        {
            Assert.IsFalse(FreezingRules.IsFreezing(false, -50f, 0f));
            Assert.IsFalse(FreezingRules.IsFreezing(false, 0f, 0f));
            Assert.IsFalse(FreezingRules.IsFreezingAt(null, Indoor, 0f));
            Assert.IsTrue(FreezingRules.IsFreezing(true, -0.1f, 0f));
            Assert.IsFalse(FreezingRules.IsFreezing(true, 0f, 0f), "Exactly 0 °C is not below zero.");
        }

        // ───────────── Puddles ─────────────

        [Test]
        public void Water_FreezesOnlyInValidZoneBelowZero()
        {
            var field = new PuddleField(Config, 10);
            PuddleModel cold = field.AddLiquid(Indoor, LiquidType.Water, 10f);
            TickFor(field, ColdBuilding(-5f), LongTime);
            Assert.IsTrue(cold.IsFrozen);

            var warmField = new PuddleField(Config, 10);
            PuddleModel warm = warmField.AddLiquid(Indoor, LiquidType.Water, 10f);
            TickFor(warmField, ColdBuilding(5f), LongTime);
            Assert.IsFalse(warm.IsFrozen);
        }

        [Test]
        public void Water_WithoutTemperatureProvider_StaysLiquid()
        {
            var field = new PuddleField(Config, 10);
            PuddleModel puddle = field.AddLiquid(Indoor, LiquidType.Water, 10f);

            TickFor(field, null, LongTime);

            Assert.IsFalse(puddle.IsFrozen);
        }

        [Test]
        public void OutdoorPuddle_WithoutZone_NeverFreezes()
        {
            var field = new PuddleField(Config, 10);
            PuddleModel outdoor = field.AddLiquid(Outdoor, LiquidType.Water, 10f);

            TickFor(field, ColdBuilding(-40f), LongTime);

            Assert.IsFalse(outdoor.IsFrozen);
        }

        [Test]
        public void NoZone_IsNotZeroDegrees_EvenWithThresholdAboveZero()
        {
            // With a freeze threshold above 0 °C, treating "no zone" as 0 °C would freeze everything outdoors.
            var warmThreshold = new PuddleConfig(0.02f, 0.15f, 1.5f, 0.4f, 2f, 3f, 0.5f, 1f, 0.05f);
            var field = new PuddleField(warmThreshold, 10);
            PuddleModel outdoor = field.AddLiquid(Outdoor, LiquidType.Water, 10f);
            PuddleModel indoor = field.AddLiquid(Indoor, LiquidType.Water, 10f);

            TickFor(field, ColdBuilding(1f), LongTime);

            Assert.IsFalse(outdoor.IsFrozen, "outdoors there is no temperature, not 0 °C");
            Assert.IsTrue(indoor.IsFrozen, "1 °C is below this threshold");
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Water, ColdBuilding(1f), Outdoor, 2f));
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Water, null, Outdoor, 2f));
            Assert.IsTrue(WaterStream.EmitsSnow(LiquidType.Water, ColdBuilding(1f), Indoor, 2f));
        }

        [Test]
        public void IndoorPuddle_FreezesBelowZero_AfterDelay()
        {
            var field = new PuddleField(Config, 10);
            PuddleModel puddle = field.AddLiquid(Indoor, LiquidType.Water, 10f);
            FakeTemperatureZones zones = ColdBuilding(-5f);

            field.Tick(Config.FreezeDelay * 0.5f, zones);
            Assert.IsFalse(puddle.IsFrozen, "Freezing takes the freeze delay.");

            field.Tick(Config.FreezeDelay, zones);
            Assert.IsTrue(puddle.IsFrozen);
        }

        [Test]
        public void Ice_MeltsWhenZoneWarmsUp()
        {
            var zones = new FakeTemperatureZones();
            FakeZone room = zones.Add(Indoor, new Vector3(10f, 4f, 10f), -5f);
            var field = new PuddleField(Config, 10);
            PuddleModel puddle = field.AddLiquid(Indoor, LiquidType.Water, 10f);
            TickFor(field, zones, 5f);
            Assert.IsTrue(puddle.IsFrozen);

            room.Temperature = 0.2f; // between freeze and melt temperatures: stays frozen
            TickFor(field, zones, LongTime);
            Assert.IsTrue(puddle.IsFrozen);

            room.Temperature = 10f;
            TickFor(field, zones, Config.MeltDelay + 1f);
            Assert.IsFalse(puddle.IsFrozen);
        }

        [Test]
        public void IndoorAndOutdoor_ResolveLocalState()
        {
            FakeTemperatureZones zones = ColdBuilding(-5f);
            var field = new PuddleField(Config, 10);
            PuddleModel inside = field.AddLiquid(Indoor, LiquidType.Water, 10f);
            PuddleModel outside = field.AddLiquid(Outdoor, LiquidType.Water, 10f);

            TickFor(field, zones, LongTime);

            Assert.IsTrue(inside.IsFrozen);
            Assert.IsFalse(outside.IsFrozen);
        }

        // ───────────── Snow ─────────────

        [Test]
        public void Water_TurnsToSnow_OnlyInValidColdZone()
        {
            FakeTemperatureZones cold = ColdBuilding(-5f);
            FakeTemperatureZones warm = ColdBuilding(5f);

            Assert.IsTrue(WaterStream.EmitsSnow(LiquidType.Water, cold, Indoor, 0f));
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Water, warm, Indoor, 0f));
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Water, cold, Outdoor, 0f));
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Water, null, Indoor, 0f));
        }

        [Test]
        public void MovingBetweenOutdoorAndColdBuilding_SwitchesSnowAndBack()
        {
            FakeTemperatureZones zones = ColdBuilding(-5f);

            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Water, zones, Outdoor, 0f), "outdoors: water");
            Assert.IsTrue(WaterStream.EmitsSnow(LiquidType.Water, zones, Indoor, 0f), "cold floor: snow");
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Water, zones, Outdoor, 0f), "outdoors again: water");
        }

        // ───────────── Liquid types ─────────────

        [Test]
        public void Gasoline_NeverBecomesSnow()
        {
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Gasoline, ColdBuilding(-50f), Indoor, 0f));
            Assert.IsFalse(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Gasoline, false, true).IsSnow);
        }

        [Test]
        public void Gasoline_NeverFreezes()
        {
            var field = new PuddleField(Config, 10);
            PuddleModel puddle = field.AddLiquid(Indoor, LiquidType.Gasoline, 10f);

            TickFor(field, ColdBuilding(-50f), LongTime);

            Assert.IsFalse(puddle.IsFrozen);
            Assert.AreEqual(LiquidType.Gasoline, puddle.Liquid);
        }

        [Test]
        public void Toxic_NeverBecomesSnow()
        {
            Assert.IsFalse(WaterStream.EmitsSnow(LiquidType.Toxic, ColdBuilding(-50f), Indoor, 0f));
            Assert.IsFalse(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Toxic, false, true).IsSnow);
        }

        [Test]
        public void Toxic_NeverFreezes()
        {
            var field = new PuddleField(Config, 10);
            PuddleModel puddle = field.AddLiquid(Indoor, LiquidType.Toxic, 10f);

            TickFor(field, ColdBuilding(-50f), LongTime);

            Assert.IsFalse(puddle.IsFrozen);
            Assert.AreEqual(LiquidType.Toxic, puddle.Liquid);
        }

        [Test]
        public void ToxicPouredOnIce_MeltsIt()
        {
            var field = new PuddleField(Config, 10);
            PuddleModel puddle = field.AddLiquid(Indoor, LiquidType.Water, 10f);
            TickFor(field, ColdBuilding(-5f), LongTime);
            Assert.IsTrue(puddle.IsFrozen);

            field.AddLiquid(Indoor, LiquidType.Toxic, 1f);

            Assert.AreEqual(LiquidType.Toxic, puddle.Liquid);
            Assert.IsFalse(puddle.IsFrozen);
        }

        [Test]
        public void SoapyWater_KeepsSoap_WhenItArrivesAsSnow()
        {
            var impact = new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Water, true, true);

            Assert.IsTrue(impact.IsSnow);
            Assert.IsTrue(impact.IsSoapy);
            Assert.AreEqual(LiquidType.Water, impact.Liquid);
        }

        [Test]
        public void ExistingLiquidRoles_Unchanged()
        {
            Assert.AreEqual(1f, LiquidType.Water.ExtinguishingPower());
            Assert.AreEqual(LiquidTypeExtensions.ToxicExtinguishingPower, LiquidType.Toxic.ExtinguishingPower());
            Assert.AreEqual(0f, LiquidType.Gasoline.ExtinguishingPower());
            Assert.IsTrue(LiquidType.Gasoline.IsFlammable());
            Assert.IsTrue(LiquidType.Toxic.Corrodes());
            Assert.IsTrue(LiquidType.Water.CanBeSoapy());
            Assert.IsFalse(LiquidType.Toxic.CanBeSoapy());
            Assert.AreEqual(LiquidType.Water, LiquidType.Gasoline.MixWith(LiquidType.Toxic));
            Assert.AreEqual(LiquidType.Gasoline, LiquidType.Water.MixWith(LiquidType.Gasoline));

            Assert.IsTrue(LiquidType.Water.CanFreeze());
            Assert.IsFalse(LiquidType.Gasoline.CanFreeze());
            Assert.IsFalse(LiquidType.Toxic.CanFreeze());

            // Without snow the power is exactly the old one.
            foreach (LiquidType liquid in new[] { LiquidType.Water, LiquidType.Gasoline, LiquidType.Toxic })
                Assert.AreEqual(liquid.ExtinguishingPower(), liquid.ExtinguishingPower(false, 0.2f));

            // Default impact and flow data keep their old meaning.
            var impact = new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f);
            Assert.AreEqual(LiquidType.Water, impact.Liquid);
            Assert.IsFalse(impact.IsSnow);
            Assert.IsFalse(impact.IsSoapy);
        }
    }
}
