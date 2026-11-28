using NUnit.Framework;
using WaterChaos.FireTruck;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class FireTruckModelTests
    {
        private const float MaxPressure = 500_000f;
        private const float MaxFlow = 10f;
        private const float ValveSpeed = 2f;
        private const float Step = 0.5f;
        private const float Tolerance = 0.001f;

        private static FireTruckModel CreateTruck(WaterReservoir tank)
        {
            return new FireTruckModel(MaxPressure, MaxFlow, ValveSpeed, tank);
        }

        [Test]
        public void LowerTargetPressure_GivesLessPressureAndFlow()
        {
            var tank = new WaterReservoir(1000f, 1000f);
            FireTruckModel truck = CreateTruck(tank);
            truck.Toggle();

            truck.SetTargetPressure(MaxPressure * 0.5f);
            truck.Tick(Step);
            truck.Tick(Step);

            Assert.AreEqual(MaxPressure * 0.5f, truck.Output.Pressure, Tolerance);
            Assert.AreEqual(MaxFlow * 0.5f, truck.Output.FlowRate, Tolerance);
        }

        [TestCase(900_000f, MaxPressure)]
        [TestCase(-1f, 0f)]
        public void TargetPressure_IsClampedToZeroAndMax(float requested, float expected)
        {
            FireTruckModel truck = CreateTruck(new WaterReservoir(1000f, 1000f));

            truck.SetTargetPressure(requested);

            Assert.AreEqual(expected, truck.TargetPressure, Tolerance);
        }

        [Test]
        public void ClosedValve_GivesNothingAndKeepsWater()
        {
            var tank = new WaterReservoir(1000f, 1000f);
            FireTruckModel truck = CreateTruck(tank);

            truck.Tick(Step);

            Assert.AreEqual(0f, truck.Output.FlowRate, Tolerance);
            Assert.AreEqual(1000f, tank.Volume, Tolerance);
        }

        [Test]
        public void OpenValve_GivesMaxPressureAndFlow()
        {
            var tank = new WaterReservoir(1000f, 1000f);
            FireTruckModel truck = CreateTruck(tank);
            truck.Toggle();

            truck.Tick(Step); // valve fully open after 0.5 s
            truck.Tick(Step);

            Assert.AreEqual(MaxPressure, truck.Output.Pressure, Tolerance);
            Assert.AreEqual(MaxFlow, truck.Output.FlowRate, Tolerance);
        }

        [Test]
        public void Flow_DrainsTheTank()
        {
            var tank = new WaterReservoir(1000f, 1000f);
            FireTruckModel truck = CreateTruck(tank);
            truck.Toggle();
            truck.Tick(Step);

            truck.Tick(1f); // 10 L/s for 1 s

            Assert.AreEqual(1000f - MaxFlow * Step - MaxFlow, tank.Volume, Tolerance);
        }

        [Test]
        public void EmptyTank_GivesNothing()
        {
            var tank = new WaterReservoir(1000f, 0f);
            FireTruckModel truck = CreateTruck(tank);
            truck.Toggle();

            truck.Tick(Step);
            truck.Tick(Step);

            Assert.AreEqual(0f, truck.Output.FlowRate, Tolerance);
            Assert.AreEqual(0f, truck.Output.Pressure, Tolerance);
        }

        [Test]
        public void AlmostEmptyTank_GivesOnlyWhatIsLeft()
        {
            var tank = new WaterReservoir(1000f, 2f);
            FireTruckModel truck = CreateTruck(tank);
            truck.Toggle();

            truck.Tick(Step); // wants 10 L/s * 0.5 s = 5 L, gets 2 L: 40 % of the stream

            Assert.AreEqual(0f, tank.Volume, Tolerance);
            Assert.AreEqual(4f, truck.Output.FlowRate, Tolerance);
            Assert.AreEqual(MaxPressure * 0.4f, truck.Output.Pressure, Tolerance);
        }

        [Test]
        public void TankRunDry_StopsTheStream()
        {
            var tank = new WaterReservoir(1000f, 2f);
            FireTruckModel truck = CreateTruck(tank);
            truck.Toggle();
            truck.Tick(Step);

            truck.Tick(Step);

            Assert.AreEqual(0f, truck.Output.FlowRate, Tolerance);
            Assert.AreEqual(0f, truck.Output.Pressure, Tolerance);
        }

        [Test]
        public void ClosingValve_StopsFlow()
        {
            var tank = new WaterReservoir(1000f, 1000f);
            FireTruckModel truck = CreateTruck(tank);
            truck.Toggle();
            truck.Tick(Step);

            truck.Close();
            truck.Tick(Step);

            Assert.IsFalse(truck.IsOpen);
            Assert.AreEqual(0f, truck.Output.FlowRate, Tolerance);
        }

        [Test]
        public void NoTank_GivesNothing()
        {
            FireTruckModel truck = CreateTruck(null);
            truck.Toggle();

            truck.Tick(Step);

            Assert.AreEqual(0f, truck.Output.FlowRate, Tolerance);
        }
    }
}
