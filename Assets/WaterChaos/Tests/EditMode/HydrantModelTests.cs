using NUnit.Framework;
using WaterChaos.Hydrant;

namespace WaterChaos.Tests
{
    public class HydrantModelTests
    {
        private const float Pressure = 400f;
        private const float FlowRate = 8f;
        private const float ValveSpeed = 2f; // fully opens in 0.5 s
        private const float Tolerance = 0.0001f;

        private HydrantModel CreateHydrant() => new HydrantModel(Pressure, FlowRate, ValveSpeed);

        [Test]
        public void NewHydrant_IsClosedWithZeroOutput()
        {
            HydrantModel hydrant = CreateHydrant();

            Assert.IsFalse(hydrant.IsOpen);
            Assert.AreEqual(0f, hydrant.Output.FlowRate);
            Assert.AreEqual(0f, hydrant.Output.Pressure);
        }

        [Test]
        public void Open_ThenTick_ValveOpensByValveSpeed()
        {
            HydrantModel hydrant = CreateHydrant();

            hydrant.Open();
            hydrant.Tick(0.25f);

            Assert.IsTrue(hydrant.IsOpen);
            Assert.AreEqual(0.5f, hydrant.OpenRatio, Tolerance);
            Assert.AreEqual(FlowRate * 0.5f, hydrant.Output.FlowRate, Tolerance);
            Assert.AreEqual(Pressure * 0.5f, hydrant.Output.Pressure, Tolerance);
        }

        [Test]
        public void Open_LongTick_GivesFullNetworkOutput()
        {
            HydrantModel hydrant = CreateHydrant();

            hydrant.Open();
            hydrant.Tick(10f);

            Assert.AreEqual(1f, hydrant.OpenRatio);
            Assert.AreEqual(FlowRate, hydrant.Output.FlowRate, Tolerance);
            Assert.AreEqual(Pressure, hydrant.Output.Pressure, Tolerance);
        }

        [Test]
        public void Close_AfterOpen_ValveClosesGradually()
        {
            HydrantModel hydrant = CreateHydrant();
            hydrant.Open();
            hydrant.Tick(10f);

            hydrant.Close();
            hydrant.Tick(0.25f);

            Assert.IsFalse(hydrant.IsOpen);
            Assert.AreEqual(0.5f, hydrant.OpenRatio, Tolerance);
        }

        [Test]
        public void Toggle_SwitchesValveState()
        {
            HydrantModel hydrant = CreateHydrant();

            hydrant.Toggle();
            Assert.IsTrue(hydrant.IsOpen);

            hydrant.Toggle();
            Assert.IsFalse(hydrant.IsOpen);
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        public void Tick_InvalidDeltaTime_IsIgnored(float invalid)
        {
            HydrantModel hydrant = CreateHydrant();
            hydrant.Open();

            hydrant.Tick(invalid);

            Assert.AreEqual(0f, hydrant.OpenRatio);
        }

        [Test]
        public void InvalidSettings_BecomeZero()
        {
            HydrantModel hydrant = new HydrantModel(-5f, float.NaN, ValveSpeed);
            hydrant.Open();
            hydrant.Tick(10f);

            Assert.AreEqual(0f, hydrant.Output.FlowRate);
            Assert.AreEqual(0f, hydrant.Output.Pressure);
        }
    }
}