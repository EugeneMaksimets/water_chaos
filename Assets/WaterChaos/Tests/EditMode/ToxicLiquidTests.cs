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
    /// it corrodes, never puts fire out (it reacts with it, see ToxicReactionTests) and never feeds it.
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
        public void ExtinguishingPower_WaterFull_ToxicAndGasolineNone_ToxicReacts()
        {
            Assert.AreEqual(1f, LiquidType.Water.ExtinguishingPower());
            Assert.AreEqual(0f, LiquidType.Toxic.ExtinguishingPower());
            Assert.AreEqual(0f, LiquidType.Gasoline.ExtinguishingPower());
            Assert.IsFalse(LiquidType.Toxic.Extinguishes(), "toxic liquid reacts with fire instead of putting it out");
            Assert.IsTrue(LiquidType.Toxic.ReactsWithFire());
            Assert.IsFalse(LiquidType.Water.ReactsWithFire());
            Assert.IsFalse(LiquidType.Gasoline.ReactsWithFire());
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
        public void Mix_GasolineAndToxic_IsToxic()
        {
            Assert.AreEqual(LiquidType.Toxic, LiquidType.Toxic.MixWith(LiquidType.Gasoline));
            Assert.AreEqual(LiquidType.Toxic, LiquidType.Gasoline.MixWith(LiquidType.Toxic));
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
        public void Reservoir_GasolineAndToxic_TurnTheWholeTankToxic(LiquidType stored, LiquidType added)
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, stored);

            float accepted = reservoir.Add(1f, added);

            Assert.AreEqual(1f, accepted);
            Assert.AreEqual(51f, reservoir.Volume, "nothing is lost, the liquid just changes");
            Assert.AreEqual(LiquidType.Toxic, reservoir.Liquid);
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
        public void Toxic_NeverCoolsOrPutsOutFire()
        {
            FireExtinguishHandler toxicFire = CreateFire();

            toxicFire.ReceiveImpact(Impact(LiquidType.Toxic, 10f));
            Assert.AreEqual(1f, toxicFire.Intensity, Tolerance);

            StepsToExtinguish(toxicFire, LiquidType.Toxic);
            Assert.IsTrue(toxicFire.IsBurning, "any amount of toxic liquid leaves the fire burning");
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
