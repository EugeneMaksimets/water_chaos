using NUnit.Framework;
using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    /// <summary>Liquid type from the reservoir through the pump, the flow and the impact.</summary>
    public class LiquidFlowTests
    {
        [Test]
        public void Liquids_HaveSeparateRoles()
        {
            Assert.IsTrue(LiquidType.Water.Extinguishes());
            Assert.IsFalse(LiquidType.Water.IsFlammable());
            Assert.IsFalse(LiquidType.Gasoline.Extinguishes());
            Assert.IsTrue(LiquidType.Gasoline.IsFlammable());
        }

        [Test]
        public void Reservoir_ByDefault_HoldsWater()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f);

            Assert.AreEqual(LiquidType.Water, reservoir.Liquid);
        }

        [Test]
        public void Reservoir_CanHoldGasoline()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, LiquidType.Gasoline);

            Assert.AreEqual(LiquidType.Gasoline, reservoir.Liquid);
            Assert.AreEqual(50f, reservoir.Volume);
        }

        [Test]
        public void WaterIntoGasoline_IsAcceptedAndBecomesGasoline()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, LiquidType.Gasoline);

            float accepted = reservoir.Add(20f, LiquidType.Water);

            Assert.AreEqual(20f, accepted);
            Assert.AreEqual(70f, reservoir.Volume);
            Assert.AreEqual(LiquidType.Gasoline, reservoir.Liquid);
        }

        [Test]
        public void GasolineIntoWater_TurnsTheWholeReservoirIntoGasoline()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f);

            reservoir.Add(1f, LiquidType.Gasoline);

            Assert.AreEqual(51f, reservoir.Volume);
            Assert.AreEqual(LiquidType.Gasoline, reservoir.Liquid);
        }

        [Test]
        public void GasolineReservoir_DrainedEmpty_TakesWaterAgain()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, LiquidType.Gasoline);
            reservoir.Draw(50f);

            reservoir.Add(30f, LiquidType.Water);

            Assert.AreEqual(LiquidType.Water, reservoir.Liquid);
        }

        [Test]
        public void PartlyDrainedGasolineReservoir_StaysGasoline()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, LiquidType.Gasoline);
            reservoir.Draw(49f);

            reservoir.Add(40f, LiquidType.Water);

            Assert.AreEqual(LiquidType.Gasoline, reservoir.Liquid);
        }

        [Test]
        public void Reservoir_WithSameLiquid_AcceptsIt()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 50f, 0f, LiquidType.Gasoline);

            Assert.AreEqual(20f, reservoir.Add(20f, LiquidType.Gasoline));
            Assert.AreEqual(70f, reservoir.Volume);
        }

        [Test]
        public void EmptyReservoir_TakesTheNewLiquid()
        {
            WaterReservoir reservoir = new WaterReservoir(100f, 0f);

            Assert.AreEqual(30f, reservoir.Add(30f, LiquidType.Gasoline));
            Assert.AreEqual(LiquidType.Gasoline, reservoir.Liquid);
        }

        [Test]
        public void Mix_GasolineAlwaysWins()
        {
            Assert.AreEqual(LiquidType.Gasoline, LiquidType.Water.MixWith(LiquidType.Gasoline));
            Assert.AreEqual(LiquidType.Gasoline, LiquidType.Gasoline.MixWith(LiquidType.Water));
            Assert.AreEqual(LiquidType.Water, LiquidType.Water.MixWith(LiquidType.Water));
        }

        [Test]
        public void FlowData_ByDefault_IsWater()
        {
            Assert.AreEqual(LiquidType.Water, new WaterFlowData(1f, 1f).Liquid);
            Assert.AreEqual(LiquidType.Water, default(WaterFlowData).Liquid);
        }

        [Test]
        public void Impact_CarriesTheLiquid()
        {
            WaterImpact impact = new WaterImpact(Vector3.zero, Vector3.up, Vector3.forward, 1f, 1f, LiquidType.Gasoline);

            Assert.AreEqual(LiquidType.Gasoline, impact.Liquid);
            Assert.AreEqual(LiquidType.Water, new WaterImpact(Vector3.zero, Vector3.up, Vector3.forward, 1f, 1f).Liquid);
        }

        [Test]
        public void Pump_OutputCarriesTheReservoirLiquid()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(1000f, 1000f, 0f, LiquidType.Gasoline));

            Assert.Greater(pump.Output.FlowRate, 0f);
            Assert.AreEqual(LiquidType.Gasoline, pump.Output.Liquid);
            Assert.AreEqual(LiquidType.Gasoline, pump.Liquid);
        }

        [Test]
        public void Pump_FromWaterReservoir_GivesWater()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(1000f, 1000f));

            Assert.AreEqual(LiquidType.Water, pump.Output.Liquid);
        }

        [Test]
        public void Pump_FromEmptyReservoir_GivesNoFlow()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(1000f, 0f, 0f, LiquidType.Gasoline));

            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(0f, pump.Output.Pressure);
        }

        [Test]
        public void Settlement_WaterIntoGasolineReservoir_BecomesGasoline()
        {
            WaterReservoir source = new WaterReservoir(100f, 40f);
            WaterReservoir destination = new WaterReservoir(100f, 10f, 0f, LiquidType.Gasoline);

            TransferResult result = WaterTransferSettlement.Settle(source, destination, 10f);

            Assert.AreEqual(10f, result.Delivered);
            Assert.AreEqual(20f, destination.Volume);
            Assert.AreEqual(LiquidType.Gasoline, destination.Liquid);
            Assert.AreEqual(LiquidType.Water, source.Liquid);
        }

        [Test]
        public void Settlement_IntoEmptyReservoir_FillsItWithTheSourceLiquid()
        {
            WaterReservoir source = new WaterReservoir(100f, 40f, 0f, LiquidType.Gasoline);
            WaterReservoir destination = new WaterReservoir(100f, 0f);

            WaterTransferSettlement.Settle(source, destination, 10f);

            Assert.AreEqual(10f, destination.Volume);
            Assert.AreEqual(LiquidType.Gasoline, destination.Liquid);
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
