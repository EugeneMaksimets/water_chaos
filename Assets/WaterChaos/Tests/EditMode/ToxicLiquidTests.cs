using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Pump;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    /// <summary>
    /// Toxic liquid is a real liquid: stored, pumped and mixed like the others, never turned into water;
    /// it corrodes but puts fire out far worse than water and never feeds it.
    /// Splitter, hose and nozzle propagation are covered in <see cref="HoseSplitterTests"/>.
    /// </summary>
    public class ToxicLiquidTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            _created.Clear();
        }

        // ───────────── Roles ─────────────

        [Test]
        public void Toxic_CorrodesButDoesNotBurn()
        {
            Assert.IsTrue(LiquidType.Toxic.Corrodes());
            Assert.IsFalse(LiquidType.Toxic.IsFlammable());
            Assert.IsFalse(LiquidType.Water.Corrodes());
            Assert.IsFalse(LiquidType.Gasoline.Corrodes());
        }

        [Test]
        public void ExtinguishingPower_WaterFull_ToxicWeak_GasolineNone()
        {
            Assert.AreEqual(1f, LiquidType.Water.ExtinguishingPower());
            Assert.AreEqual(0.15f, LiquidType.Toxic.ExtinguishingPower(), Tolerance);
            Assert.AreEqual(0f, LiquidType.Gasoline.ExtinguishingPower());
            Assert.IsTrue(LiquidType.Toxic.Extinguishes(), "toxic liquid still puts fire out, just slowly");
        }

        [Test]
        public void ToxicLabel_IsGreen()
        {
            Color color = LiquidLabelColors.Get(LiquidType.Toxic);

            Assert.Greater(color.g, 0.8f);
            Assert.Less(color.r, color.g);
            Assert.Less(color.b, 0.4f);
            Assert.AreNotEqual(LiquidLabelColors.Get(LiquidType.Water), color);
            Assert.AreNotEqual(LiquidLabelColors.Get(LiquidType.Gasoline), color);
        }

        // ───────────── Storage and mixing ─────────────

        [Test]
        public void Reservoir_CanHoldToxic()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, LiquidType.Toxic);

            Assert.AreEqual(LiquidType.Toxic, reservoir.Liquid);
            Assert.AreEqual(50f, reservoir.Volume);
        }

        [Test]
        public void Reservoir_KeepsToxicWhileDrawingAndRefillingWithToxic()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, LiquidType.Toxic);

            reservoir.Draw(30f);
            reservoir.Add(20f, LiquidType.Toxic);

            Assert.AreEqual(LiquidType.Toxic, reservoir.Liquid);
        }

        [Test]
        public void Mix_ToxicSpoilsWater()
        {
            Assert.AreEqual(LiquidType.Toxic, LiquidType.Water.MixWith(LiquidType.Toxic));
            Assert.AreEqual(LiquidType.Toxic, LiquidType.Toxic.MixWith(LiquidType.Water), "water does not dilute toxic back to water");
            Assert.AreEqual(LiquidType.Toxic, LiquidType.Toxic.MixWith(LiquidType.Toxic));
        }

        [Test]
        public void Mix_GasolineAndToxic_NeutralizeIntoWater()
        {
            Assert.AreEqual(LiquidType.Water, LiquidType.Toxic.MixWith(LiquidType.Gasoline));
            Assert.AreEqual(LiquidType.Water, LiquidType.Gasoline.MixWith(LiquidType.Toxic));
        }

        [Test]
        public void Reservoir_ToxicIntoWater_TurnsTheWholeTankToxic()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f);

            reservoir.Add(1f, LiquidType.Toxic);

            Assert.AreEqual(LiquidType.Toxic, reservoir.Liquid);
            Assert.AreEqual(51f, reservoir.Volume);
        }

        [TestCase(LiquidType.Gasoline, LiquidType.Toxic)]
        [TestCase(LiquidType.Toxic, LiquidType.Gasoline)]
        public void Reservoir_GasolineAndToxic_TurnTheWholeTankIntoWater(LiquidType stored, LiquidType added)
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, stored);

            float accepted = reservoir.Add(1f, added);

            Assert.AreEqual(1f, accepted);
            Assert.AreEqual(51f, reservoir.Volume, "nothing is lost, the liquid just changes");
            Assert.AreEqual(LiquidType.Water, reservoir.Liquid);
        }

        [Test]
        public void Settlement_ToxicIntoEmptyReservoir_FillsItWithToxic()
        {
            WaterReservoir source = new WaterReservoir(100f, 40f, 0f, LiquidType.Toxic);
            WaterReservoir destination = new WaterReservoir(100f, 0f);

            WaterTransferSettlement.Settle(source, destination, 10f);

            Assert.AreEqual(LiquidType.Toxic, destination.Liquid);
        }

        // ───────────── Pump and impact ─────────────

        [Test]
        public void Pump_PreservesToxic()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(1000f, 1000f, 0f, LiquidType.Toxic));

            Assert.Greater(pump.Output.FlowRate, 0f);
            Assert.AreEqual(LiquidType.Toxic, pump.Output.Liquid);
            Assert.AreEqual(LiquidType.Toxic, pump.Liquid);
        }

        [Test]
        public void Pump_FromEmptyToxicReservoir_StillReportsToxic()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(1000f, 0f, 0f, LiquidType.Toxic));

            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(LiquidType.Toxic, pump.Output.Liquid, "no flow is not a reason to fall back to water");
        }

        [Test]
        public void FlowAndImpact_CarryToxic()
        {
            Assert.AreEqual(LiquidType.Toxic, new WaterFlowData(1f, 1f, LiquidType.Toxic).Liquid);
            Assert.AreEqual(LiquidType.Toxic,
                new WaterImpact(Vector3.zero, Vector3.up, Vector3.forward, 1f, 1f, LiquidType.Toxic).Liquid);
        }

        // ───────────── Fire ─────────────

        [Test]
        public void Water_KeepsItsExtinguishingEffectiveness()
        {
            FireExtinguishHandler fire = CreateFire();

            fire.ReceiveImpact(Impact(LiquidType.Water, 10f));

            // Default cooling: 0.033 intensity per liter.
            Assert.AreEqual(1f - 10f * 0.033f, fire.Intensity, Tolerance);
        }

        [Test]
        public void Toxic_CoolsFireAtFifteenPercentOfWater()
        {
            FireExtinguishHandler waterFire = CreateFire();
            FireExtinguishHandler toxicFire = CreateFire();

            waterFire.ReceiveImpact(Impact(LiquidType.Water, 10f));
            toxicFire.ReceiveImpact(Impact(LiquidType.Toxic, 10f));

            float waterCooling = 1f - waterFire.Intensity;
            float toxicCooling = 1f - toxicFire.Intensity;
            Assert.Greater(toxicCooling, 0f);
            Assert.AreEqual(waterCooling * 0.15f, toxicCooling, Tolerance);
        }

        [Test]
        public void Toxic_PutsFireOutOnlyWithMuchMoreLiquid()
        {
            FireExtinguishHandler waterFire = CreateFire();
            FireExtinguishHandler toxicFire = CreateFire();
            int waterSteps = StepsToExtinguish(waterFire, LiquidType.Water);
            int toxicSteps = StepsToExtinguish(toxicFire, LiquidType.Toxic);

            Assert.IsFalse(toxicFire.IsBurning);
            Assert.GreaterOrEqual(toxicSteps, waterSteps * 6, "toxic needs about 1 / 0.15 = 6.7 times the liquid");
        }

        [Test]
        public void Toxic_DoesNotFeedFireLikeGasoline()
        {
            FireExtinguishHandler fire = CreateFire();
            FireFuelHandler fuel = fire.gameObject.AddComponent<FireFuelHandler>();
            CallAwake(fuel);

            fuel.ReceiveImpact(Impact(LiquidType.Toxic, 10f));

            Assert.LessOrEqual(fire.Intensity, 1f, "toxic liquid never overburns a fire");
        }

        [Test]
        public void Toxic_CannotBeLitIntoAFlame()
        {
            // FuelStreamIgnition lights only flammable flow.
            Assert.IsFalse(new WaterFlowData(5f, 100_000f, LiquidType.Toxic).Liquid.IsFlammable());
        }

        // ───────────── Helpers ─────────────

        private static WaterImpact Impact(LiquidType liquid, float volume)
        {
            return new WaterImpact(Vector3.zero, Vector3.up, Vector3.forward, volume, 10f, liquid);
        }

        private static int StepsToExtinguish(FireExtinguishHandler fire, LiquidType liquid)
        {
            int steps = 0;
            while (fire.IsBurning && steps < 10_000)
            {
                fire.ReceiveImpact(Impact(liquid, 1f));
                steps++;
            }

            return steps;
        }

        private FireExtinguishHandler CreateFire()
        {
            GameObject go = new GameObject("Fire");
            _created.Add(go);
            FireExtinguishHandler fire = go.AddComponent<FireExtinguishHandler>();
            CallAwake(fire);
            return fire;
        }

        // Edit mode does not run Awake, where these handlers build their models from inspector settings.
        private static void CallAwake(MonoBehaviour component)
        {
            component.GetType()
                .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(component, null);
        }

        private static PumpModel CreateRunningPump(WaterReservoir reservoir)
        {
            PumpModel pump = new PumpModel(100_000f, 10f, 1_000_000f);
            pump.SetSource(reservoir);
            pump.SetOutletConnected(true);
            pump.SetTargetPressure(100_000f);
            pump.Start();
            pump.Tick(0.1f);
            return pump;
        }
    }
}
