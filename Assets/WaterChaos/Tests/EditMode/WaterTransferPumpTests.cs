using NUnit.Framework;
using WaterChaos.Pump;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    /// <summary>
    /// The existing pump model together with the transfer rules, step by step the way
    /// WaterTransferController runs them: pump ticks → settle its output → decide whether to deliver next step.
    /// </summary>
    public class WaterTransferPumpTests
    {
        private const float MaxPressure = 1000f;
        private const float MaxFlowRate = 10f;
        private const float InstantChangeRate = 1_000_000_000f;
        private const float Step = 0.1f;
        private const float Tolerance = 0.001f;

        private sealed class Rig
        {
            public PumpModel Pump;
            public WaterReservoir Source;
            public WaterReservoir Destination;
            public TransferHoseState HoseState = TransferHoseState.Placed;
            public bool OutletBusy;
            public bool Delivering;

            /// <summary>One physics step; returns the water that left into the environment.</summary>
            public float Tick()
            {
                Pump.SetOutletConnected(Delivering || OutletBusy);
                Pump.Tick(Step);

                float discharged = 0f;
                if (Delivering && !OutletBusy)
                    discharged = WaterTransferSettlement.Settle(Pump.LastDrawnSource, Destination, Pump.LastDrawnVolume).Discharged;

                Delivering = TransferRules.CanDeliver(HoseState, OutletBusy, Destination != null && Destination.IsFull);
                return discharged;
            }

            public float Run(float seconds)
            {
                float discharged = 0f;
                int steps = (int)System.Math.Round(seconds / Step);
                for (int i = 0; i < steps; i++)
                    discharged += Tick();
                return discharged;
            }
        }

        /// <summary>Hose placed, pump switched on at full pressure; delivery starts on the first step.</summary>
        private static Rig CreateRig(float sourceVolume, WaterReservoir destination, LiquidType sourceLiquid = LiquidType.Water)
        {
            Rig rig = new Rig
            {
                Pump = new PumpModel(MaxPressure, MaxFlowRate, InstantChangeRate),
                Source = new WaterReservoir(10_000f, sourceVolume, 0f, sourceLiquid),
                Destination = destination,
                Delivering = true
            };

            rig.Pump.SetSource(rig.Source);
            rig.Pump.SetOutletConnected(true);
            rig.Pump.SetTargetPressure(MaxPressure);
            rig.Pump.Start();
            return rig;
        }

        // ───────────── Liquids ─────────────

        [Test]
        public void ToxicPumpedIntoWaterReservoir_TurnsItToxic()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 300f), LiquidType.Toxic);

            rig.Tick();

            Assert.AreEqual(LiquidType.Toxic, rig.Destination.Liquid, "the first step already spoils the water");
            rig.Run(5f);
            Assert.AreEqual(LiquidType.Toxic, rig.Destination.Liquid);
            Assert.AreEqual(LiquidType.Toxic, rig.Source.Liquid, "the source keeps its own liquid");
            Assert.AreEqual(800f, rig.Source.Volume + rig.Destination.Volume, Tolerance);
        }

        [TestCase(LiquidType.Toxic, LiquidType.Gasoline)]
        [TestCase(LiquidType.Gasoline, LiquidType.Toxic)]
        public void GasolineAndToxicMeeting_TurnTheWholeTankIntoWater(LiquidType pumped, LiquidType stored)
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 300f, 0f, stored), pumped);

            rig.Tick();

            Assert.AreEqual(LiquidType.Water, rig.Destination.Liquid);
            Assert.AreEqual(800f, rig.Source.Volume + rig.Destination.Volume, Tolerance);
        }

        [TestCase(LiquidType.Toxic, LiquidType.Gasoline)]
        [TestCase(LiquidType.Gasoline, LiquidType.Toxic)]
        public void PumpingOnAfterNeutralizing_SpoilsTheWaterWithThePumpedLiquid(LiquidType pumped, LiquidType stored)
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 300f, 0f, stored), pumped);
            rig.Tick();

            rig.Tick();

            Assert.AreEqual(pumped, rig.Destination.Liquid);
        }

        [Test]
        public void FiveSecondsAtTenLitersPerSecond_MovesFiftyLiters()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 100f));

            rig.Run(5f);

            Assert.AreEqual(450f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(150f, rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void TotalVolume_IsConserved()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 100f));

            rig.Run(3.7f);

            Assert.AreEqual(600f, rig.Source.Volume + rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void DestinationFills_ThenPumpStopsDrawing()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(130f, 100f));

            rig.Run(10f);

            Assert.AreEqual(130f, rig.Destination.Volume, Tolerance);
            Assert.AreEqual(470f, rig.Source.Volume, Tolerance);
            Assert.IsFalse(rig.Delivering);
            Assert.AreEqual(0f, rig.Pump.Output.FlowRate);
        }

        [Test]
        public void FullDestination_NoWaterLeavesSource()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(100f, 100f));

            rig.Run(5f);

            Assert.AreEqual(500f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(100f, rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void NoDestination_SourceDrainsIntoEnvironment()
        {
            Rig rig = CreateRig(500f, null);

            float discharged = rig.Run(5f);

            Assert.AreEqual(450f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(50f, discharged, Tolerance);
        }

        [Test]
        public void EmptySource_StopsTransfer()
        {
            Rig rig = CreateRig(12f, new WaterReservoir(1000f, 0f));

            rig.Run(5f);

            Assert.AreEqual(0f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(12f, rig.Destination.Volume, Tolerance);
            Assert.AreEqual(0f, rig.Pump.Output.FlowRate);
        }

        [Test]
        public void ZeroPressure_TransfersNothing()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 100f));
            rig.Pump.SetTargetPressure(0f);

            rig.Run(5f);

            Assert.AreEqual(500f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(100f, rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void StoppingPump_StopsTransfer()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 100f));
            rig.Run(1f);

            rig.Pump.Stop();
            rig.Run(5f);

            Assert.AreEqual(490f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(110f, rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void DisconnectingHose_StopsTransfer()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 100f));
            rig.Run(1f);

            rig.HoseState = TransferHoseState.Stored;
            rig.Delivering = false;
            rig.Run(5f);

            Assert.AreEqual(490f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(110f, rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void PickingUpEnd_PausesTransfer()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 100f));
            rig.Run(1f);

            rig.HoseState = TransferHoseState.Carried;
            rig.Run(5f);

            // One step was already decided before the end was picked up.
            Assert.AreEqual(489f, rig.Source.Volume, Tolerance);
            Assert.AreEqual(111f, rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void NozzleHoseOnRegularOutlet_TransferGetsNothing()
        {
            Rig rig = CreateRig(500f, new WaterReservoir(1000f, 100f));
            rig.OutletBusy = true;

            rig.Run(5f);

            Assert.AreEqual(100f, rig.Destination.Volume, Tolerance);
        }

        [Test]
        public void Chain_LakeToTankOneToTankTwo_TankTwoFillsAndWaterIsConserved()
        {
            WaterReservoir lake = new WaterReservoir(5000f, 5000f);
            WaterReservoir tankOne = new WaterReservoir(2000f, 100f);
            WaterReservoir tankTwo = new WaterReservoir(200f, 10f);
            PumpModel first = CreateRunningPump(lake);
            PumpModel second = CreateRunningPump(tankOne);

            // Both pumps tick, then both transfers settle what their pump drew, as in one physics step.
            for (int i = 0; i < 50; i++)
            {
                first.Tick(Step);
                second.Tick(Step);
                WaterTransferSettlement.Settle(first.LastDrawnSource, tankOne, first.LastDrawnVolume);
                WaterTransferSettlement.Settle(second.LastDrawnSource, tankTwo, second.LastDrawnVolume);
            }

            Assert.AreEqual(60f, tankTwo.Volume, Tolerance);
            Assert.AreEqual(100f, tankOne.Volume, Tolerance);
            Assert.AreEqual(4950f, lake.Volume, Tolerance);
            Assert.AreEqual(5110f, lake.Volume + tankOne.Volume + tankTwo.Volume, Tolerance);
        }

        [Test]
        public void LastDrawn_IsResetEveryTick()
        {
            PumpModel pump = CreateRunningPump(new WaterReservoir(1000f, 500f));
            pump.Tick(Step);
            Assert.AreEqual(1f, pump.LastDrawnVolume, Tolerance);

            pump.Stop();
            pump.Tick(Step);

            Assert.AreEqual(0f, pump.LastDrawnVolume);
        }

        private static PumpModel CreateRunningPump(WaterReservoir source)
        {
            PumpModel pump = new PumpModel(MaxPressure, MaxFlowRate, InstantChangeRate);
            pump.SetSource(source);
            pump.SetOutletConnected(true);
            pump.SetTargetPressure(MaxPressure);
            pump.Start();
            return pump;
        }
    }
}
