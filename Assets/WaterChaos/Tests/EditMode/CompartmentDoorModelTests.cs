using NUnit.Framework;
using WaterChaos.FireTruck;

namespace WaterChaos.Tests
{
    public class CompartmentDoorModelTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void NewDoor_IsClosed()
        {
            var door = new CompartmentDoorModel(2f);

            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual(0f, door.OpenRatio, Tolerance);
        }

        [Test]
        public void Toggle_OpensSmoothly()
        {
            var door = new CompartmentDoorModel(2f); // fully open in 0.5 s
            door.Toggle();

            door.Tick(0.25f);
            Assert.AreEqual(0.5f, door.OpenRatio, Tolerance);

            door.Tick(1f);
            Assert.AreEqual(1f, door.OpenRatio, Tolerance);
        }

        [Test]
        public void ToggleAgain_Closes()
        {
            var door = new CompartmentDoorModel(2f);
            door.Toggle();
            door.Tick(1f);

            door.Toggle();
            door.Tick(0.1f);

            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual(0.8f, door.OpenRatio, Tolerance);
        }

        [Test]
        public void InvalidTime_IsIgnored()
        {
            var door = new CompartmentDoorModel(2f);
            door.Toggle();

            door.Tick(-1f);
            door.Tick(float.NaN);

            Assert.AreEqual(0f, door.OpenRatio, Tolerance);
        }
    }
}
