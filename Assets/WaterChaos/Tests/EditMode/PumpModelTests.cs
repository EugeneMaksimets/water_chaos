using NUnit.Framework;
using WaterChaos.Pump;

namespace WaterChaos.Tests
{
    public class PumpModelTests
    {
        private const float MaxPressure = 1000f;
        private const float MaxFlowRate = 10f;
        private const float ChangeRate = 100f;

        private PumpModel CreatePump() => new PumpModel(MaxPressure, MaxFlowRate, ChangeRate);

        [Test]
        public void NewPump_IsStoppedWithZeroOutput()
        {
            PumpModel pump = CreatePump();

            Assert.IsFalse(pump.IsRunning);
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
            Assert.IsTrue(pump.IsRunning);

            pump.Toggle();
            Assert.IsFalse(pump.IsRunning);
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
    }
}
