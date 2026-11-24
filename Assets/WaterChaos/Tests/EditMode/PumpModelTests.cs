using System.Collections.Generic;
using NUnit.Framework;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class PumpModelTests
    {
        private const float MaxPressure = 1000f;
        private const float MaxFlowRate = 10f;
        private const float ChangeRate = 100f;

        private const float EndlessVolume = 1_000_000f;

        /// <summary>Pump with no source and no hose, as it lands in the world.</summary>
        private PumpModel CreateBarePump() => new PumpModel(MaxPressure, MaxFlowRate, ChangeRate);

        /// <summary>Pump drawing from a huge source with the hose attached: every condition to run is met.</summary>
        private PumpModel CreatePump()
        {
            PumpModel pump = CreateBarePump();
            pump.SetSource(new WaterReservoir(EndlessVolume, EndlessVolume));
            pump.SetOutletConnected(true);
            return pump;
        }

        [Test]
        public void NewPump_IsStoppedWithZeroOutput()
        {
            PumpModel pump = CreatePump();

            Assert.IsFalse(pump.IsTurnOn);
            Assert.AreEqual(0f, pump.Output.Pressure);
            Assert.AreEqual(0f, pump.Output.FlowRate);
        }

        [Test]
        public void Tick_WhenStopped_PressureStaysZero()
        {
            PumpModel pump = CreatePump();
            pump.SetTargetPressure(500f);

            pump.Tick(10f);

            Assert.AreEqual(0f, pump.CurrentPressure);
        }

        [Test]
        public void Tick_WhenRunning_PressureRampsByChangeRate()
        {
            PumpModel pump = CreatePump();
            pump.SetTargetPressure(500f);
            pump.Start();

            pump.Tick(1f);

            Assert.AreEqual(100f, pump.CurrentPressure, 0.001f);
        }

        [Test]
        public void Tick_WhenRunning_DoesNotOvershootTarget()
        {
            PumpModel pump = CreatePump();
            pump.SetTargetPressure(150f);
            pump.Start();

            pump.Tick(10f);

            Assert.AreEqual(150f, pump.CurrentPressure);
        }

        [Test]
        public void Stop_PressureDecaysToZero()
        {
            PumpModel pump = CreatePump();
            pump.SetTargetPressure(200f);
            pump.Start();
            pump.Tick(10f);

            pump.Stop();
            pump.Tick(1f);
            Assert.AreEqual(100f, pump.CurrentPressure, 0.001f);

            pump.Tick(10f);
            Assert.AreEqual(0f, pump.CurrentPressure);
        }

        [Test]
        public void Toggle_SwitchesRunningState()
        {
            PumpModel pump = CreatePump();

            pump.Toggle();
            Assert.IsTrue(pump.IsTurnOn);

            pump.Toggle();
            Assert.IsFalse(pump.IsTurnOn);
        }

        [TestCase(5000f, MaxPressure)]
        [TestCase(-50f, 0f)]
        [TestCase(float.NaN, 0f)]
        public void SetTargetPressure_IsClampedToValidRange(float requested, float expected)
        {
            PumpModel pump = CreatePump();

            pump.SetTargetPressure(requested);

            Assert.AreEqual(expected, pump.TargetPressure);
        }

        [Test]
        public void Output_FlowRateScalesWithPressure()
        {
            PumpModel pump = CreatePump();
            pump.SetTargetPressure(MaxPressure / 2f);
            pump.Start();
            pump.Tick(100f);

            Assert.AreEqual(MaxFlowRate / 2f, pump.Output.FlowRate, 0.001f);
            Assert.AreEqual(MaxPressure / 2f, pump.Output.Pressure, 0.001f);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Tick_WithNonPositiveDeltaTime_DoesNothing(float deltaTime)
        {
            PumpModel pump = CreatePump();
            pump.SetTargetPressure(500f);
            pump.Start();

            pump.Tick(deltaTime);

            Assert.AreEqual(0f, pump.CurrentPressure);
        }
        private PumpModel CreateRunningPumpAtMaxPressure()
        {
            PumpModel pump = CreatePump();
            pump.SetTargetPressure(MaxPressure);
            pump.Start();
            pump.Tick(100f);
            return pump;
        }

        [Test]
        public void NewPump_HasFullSupply()
        {
            Assert.AreEqual(1f, CreatePump().SupplyRatio);
        }

        // --- Water source and hose conditions ---

        private PumpModel CreateRunningPump(WaterReservoir source)
        {
            PumpModel pump = CreateBarePump();
            pump.SetSource(source);
            pump.SetOutletConnected(true);
            pump.SetTargetPressure(MaxPressure);
            pump.Start();
            return pump;
        }

        [Test]
        public void NewBarePump_HasNoWaterState()
        {
            PumpModel pump = CreateBarePump();

            Assert.AreEqual(PumpState.NoWater, pump.State);
            Assert.IsFalse(pump.CanRun);
        }

        [Test]
        public void Start_WithoutSource_DoesNotRun()
        {
            PumpModel pump = CreateBarePump();
            pump.SetOutletConnected(true);
            pump.SetTargetPressure(MaxPressure);

            pump.Start();
            pump.Tick(10f);

            Assert.IsFalse(pump.IsTurnOn);
            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(PumpState.NoWater, pump.State);
        }

        [Test]
        public void Start_WithoutHose_DoesNotRunOrDrawWater()
        {
            WaterReservoir lake = new WaterReservoir(1000f, 1000f);
            PumpModel pump = CreateBarePump();
            pump.SetSource(lake);
            pump.SetTargetPressure(MaxPressure);

            pump.Start();
            pump.Tick(10f);

            Assert.IsFalse(pump.IsTurnOn);
            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(1000f, lake.Volume);
            Assert.AreEqual(PumpState.Disconnected, pump.State);
        }

        [Test]
        public void SourceAndHose_MakePumpReady()
        {
            PumpModel pump = CreateBarePump();

            pump.SetSource(new WaterReservoir(100f, 100f));
            Assert.AreEqual(PumpState.Disconnected, pump.State);

            pump.SetOutletConnected(true);
            Assert.AreEqual(PumpState.Ready, pump.State);
            Assert.IsTrue(pump.CanRun);
        }

        [Test]
        public void StateTransitions_FollowConditions()
        {
            PumpModel pump = CreateBarePump();
            List<PumpState> states = new List<PumpState>();
            pump.StateChanged += states.Add;

            pump.SetSource(new WaterReservoir(100f, 100f));
            pump.SetOutletConnected(true);
            pump.Start();
            pump.Stop();
            pump.SetSource(null);

            CollectionAssert.AreEqual(
                new[] { PumpState.Disconnected, PumpState.Ready, PumpState.Running, PumpState.Ready, PumpState.NoWater },
                states);
        }

        [Test]
        public void StateChanged_NotRaisedWhenStateStaysTheSame()
        {
            PumpModel pump = CreatePump();
            int raised = 0;
            pump.StateChanged += _ => raised++;

            pump.SetOutletConnected(true);
            pump.Tick(1f);

            Assert.AreEqual(0, raised);
        }

        [Test]
        public void Running_DrawsFlowTimesTimeFromSource()
        {
            WaterReservoir lake = new WaterReservoir(10_000f, 10_000f);
            PumpModel pump = CreateRunningPump(lake);
            pump.Tick(10f); // reach max pressure: 10 L/s

            float before = lake.Volume;
            pump.Tick(2f);

            Assert.AreEqual(MaxFlowRate * 2f, before - lake.Volume, 0.01f);
            Assert.AreEqual(MaxFlowRate, pump.Output.FlowRate, 0.001f);
        }

        [Test]
        public void SourceDepleted_PumpStopsSupplying()
        {
            WaterReservoir lake = new WaterReservoir(15f, 15f);
            PumpModel pump = CreateRunningPump(lake);
            pump.Tick(10f); // max pressure from the first step: demand 100 L, lake gives 15

            Assert.IsTrue(lake.IsEmpty);

            pump.Tick(1f);

            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(0f, pump.Output.Pressure);
            Assert.AreEqual(PumpState.NoWater, pump.State);
        }

        [Test]
        public void SourceRemoved_SupplyStopsAtOnce()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(EndlessVolume, EndlessVolume));
            pump.Tick(100f);

            pump.SetSource(null);

            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(PumpState.NoWater, pump.State);
        }

        [Test]
        public void SourceReturns_PausedPumpResumes()
        {
            WaterReservoir lake = new WaterReservoir(EndlessVolume, EndlessVolume);
            PumpModel pump = CreateRunningPump(lake);
            pump.Tick(100f);

            pump.SetSource(null);
            pump.Tick(1f);
            pump.SetSource(lake);
            pump.Tick(100f);

            Assert.AreEqual(PumpState.Running, pump.State);
            Assert.AreEqual(MaxFlowRate, pump.Output.FlowRate, 0.001f);
        }

        [Test]
        public void HoseDisconnected_StopsDeliveryAndConsumption()
        {
            WaterReservoir lake = new WaterReservoir(1000f, 1000f);
            PumpModel pump = CreateRunningPump(lake);
            pump.Tick(10f);

            pump.SetOutletConnected(false);
            float before = lake.Volume;
            pump.Tick(1f);

            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(0f, pump.Output.Pressure);
            Assert.AreEqual(before, lake.Volume);
            Assert.AreEqual(PumpState.Disconnected, pump.State);
        }

        [Test]
        public void SwitchingSource_DrawsOnlyFromNewSource()
        {
            WaterReservoir lake = new WaterReservoir(1000f, 1000f);
            WaterReservoir pond = new WaterReservoir(1000f, 1000f);
            PumpModel pump = CreateRunningPump(lake);
            pump.Tick(10f);
            float lakeAfterFirst = lake.Volume;

            pump.SetSource(pond);
            pump.Tick(10f);

            Assert.AreSame(pond, pump.Source);
            Assert.AreEqual(lakeAfterFirst, lake.Volume);
            Assert.Less(pond.Volume, 1000f);
        }

        [Test]
        public void Output_IsNeverNegative()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(1000f, 1000f));
            pump.SetTargetPressure(-500f);
            pump.SetSupplyRatio(-1f);

            pump.Tick(1f);

            Assert.GreaterOrEqual(pump.Output.FlowRate, 0f);
            Assert.GreaterOrEqual(pump.Output.Pressure, 0f);
            Assert.GreaterOrEqual(pump.DemandFlowRate, 0f);
        }

        [Test]
        public void Consumption_NeverExceedsDelivery()
        {
            WaterReservoir lake = new WaterReservoir(1000f, 1000f);
            PumpModel pump = CreateRunningPump(lake);
            float delivered = 0f;

            for (int i = 0; i < 50; i++)
            {
                pump.Tick(0.1f);
                delivered += pump.Output.FlowRate * 0.1f;
            }

            Assert.AreEqual(1000f - lake.Volume, delivered, 0.01f);
        }

        [Test]
        public void DemandFlowRate_ScalesWithPressure_IgnoresSupply()
        {
            PumpModel pump = CreateRunningPumpAtMaxPressure();

            pump.SetSupplyRatio(0f);

            Assert.AreEqual(MaxFlowRate, pump.DemandFlowRate, 0.0001f);
        }

        [Test]
        public void SetSupplyRatio_Half_HalvesOutputFlowAndPressure()
        {
            PumpModel pump = CreateRunningPumpAtMaxPressure();

            pump.SetSupplyRatio(0.5f);

            Assert.AreEqual(MaxFlowRate * 0.5f, pump.Output.FlowRate, 0.0001f);
            Assert.AreEqual(MaxPressure * 0.5f, pump.Output.Pressure, 0.0001f);
        }

        [Test]
        public void SetSupplyRatio_Zero_OutputIsZero_MotorPressureKept()
        {
            PumpModel pump = CreateRunningPumpAtMaxPressure();

            pump.SetSupplyRatio(0f);

            Assert.AreEqual(0f, pump.Output.FlowRate);
            Assert.AreEqual(0f, pump.Output.Pressure);
            Assert.AreEqual(MaxPressure, pump.CurrentPressure);
        }

        [TestCase(2f, 1f)]
        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        public void SetSupplyRatio_IsClampedToZeroOne(float input, float expected)
        {
            PumpModel pump = CreatePump();

            pump.SetSupplyRatio(input);

            Assert.AreEqual(expected, pump.SupplyRatio);
        }
    }
}
