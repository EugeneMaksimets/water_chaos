using System.Collections.Generic;
using NUnit.Framework;
using WaterChaos.Rooms;

namespace WaterChaos.Tests
{
    public class RoomTemperatureModelTests
    {
        private const float Start = 25f;
        private const float Min = -100f;
        private const float Limit = 100f;
        private const float HeatPerFire = 10f;
        private const float OverheatCooling = 2.5f;
        private const float ReigniteDelay = 15f;
        private const float Tolerance = 0.001f;

        private readonly List<int> _ready = new List<int>();

        private static RoomTemperatureModel CreateRoom()
        {
            return new RoomTemperatureModel(Start, Min, Limit, HeatPerFire, OverheatCooling, ReigniteDelay);
        }

        private static RoomTemperatureModel CreateHotRoom(int fires)
        {
            RoomTemperatureModel room = CreateRoom();
            for (int i = 0; i < fires; i++)
                room.AddFire();
            return room;
        }

        private void Tick(RoomTemperatureModel room, float seconds)
        {
            _ready.Clear();
            room.Tick(seconds, _ready);
        }

        // ── Temperature ──

        [Test]
        public void NewRoom_StartsAtStartTemperature()
        {
            RoomTemperatureModel room = CreateRoom();

            Assert.AreEqual(25f, room.Temperature, Tolerance);
            Assert.AreEqual(0, room.ActiveFires);
            Assert.IsFalse(room.IsCoolingActive);
        }

        [Test]
        public void AddFire_RaisesTemperatureByHeatPerFire()
        {
            RoomTemperatureModel room = CreateRoom();

            room.AddFire();

            Assert.AreEqual(35f, room.Temperature, Tolerance);
            Assert.AreEqual(1, room.ActiveFires);
        }

        [Test]
        public void SeveralFires_HeatProportionally()
        {
            RoomTemperatureModel room = CreateHotRoom(3);

            Assert.AreEqual(55f, room.Temperature, Tolerance);
            Assert.AreEqual(3, room.ActiveFires);
        }

        [Test]
        public void BurningFire_DoesNotAddHeatEveryTick()
        {
            RoomTemperatureModel room = CreateHotRoom(1);

            for (int i = 0; i < 100; i++)
                Tick(room, 0.02f);

            Assert.AreEqual(35f, room.Temperature, Tolerance);
        }

        [Test]
        public void RemoveFire_LowersTemperatureByHeatPerFireOnce()
        {
            RoomTemperatureModel room = CreateHotRoom(2);

            room.RemoveFire();
            Tick(room, 1f);

            Assert.AreEqual(35f, room.Temperature, Tolerance);
            Assert.AreEqual(1, room.ActiveFires);
        }

        [Test]
        public void RemoveFire_WithoutFires_DoesNothing()
        {
            RoomTemperatureModel room = CreateRoom();

            room.RemoveFire();

            Assert.AreEqual(25f, room.Temperature, Tolerance);
            Assert.AreEqual(0, room.ActiveFires);
        }

        [Test]
        public void Cooling_CanGoBelowZero()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1f);

            Tick(room, 30f);

            Assert.AreEqual(-5f, room.Temperature, Tolerance);
        }

        [Test]
        public void Cooling_NeverGoesBelowMinimum()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(4, 4f);

            Tick(room, 1000f);

            Assert.AreEqual(-100f, room.Temperature, Tolerance);
        }

        // ── Cooling ──

        [Test]
        public void OneActiveAppliance_CoolsOneDegreePerSecond()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1f);

            Tick(room, 1f);

            Assert.AreEqual(24f, room.Temperature, Tolerance);
            Assert.IsTrue(room.IsCoolingActive);
        }

        [Test]
        public void TwoActiveAppliances_CoolTwoDegreesPerSecond()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(2, 2f);

            Tick(room, 1f);

            Assert.AreEqual(23f, room.Temperature, Tolerance);
        }

        [Test]
        public void CoolingSwitchedOff_StopsCooling()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1f);
            Tick(room, 1f);

            room.SetCooling(0, 0f);
            Tick(room, 5f);

            Assert.AreEqual(24f, room.Temperature, Tolerance);
            Assert.IsFalse(room.IsCoolingActive);
        }

        [Test]
        public void Cooling_KeepsFiresFromHeatingAboveLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(7); // 95
            room.SetCooling(1, 1f);

            room.AddFire();
            room.AddFire();
            room.AddFire();

            Assert.AreEqual(100f, room.Temperature, Tolerance);
            Assert.AreEqual(10, room.ActiveFires);
        }

        [Test]
        public void Cooling_AboveLimit_CoolsAtOverheatRateDownToLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(9); // 115
            room.SetCooling(1, 1f);

            Tick(room, 2f);
            Assert.AreEqual(110f, room.Temperature, Tolerance);

            Tick(room, 10f);
            Assert.AreEqual(100f, room.Temperature, Tolerance);
        }

        [Test]
        public void Cooling_AtOrBelowLimit_UsesApplianceRate()
        {
            RoomTemperatureModel room = CreateHotRoom(8); // 105
            room.SetCooling(2, 2f);
            Tick(room, 2f); // 100

            Tick(room, 1f);

            Assert.AreEqual(98f, room.Temperature, Tolerance);
        }

        [Test]
        public void AddFire_WhileCoolingAboveLimit_DoesNotHeat()
        {
            RoomTemperatureModel room = CreateHotRoom(9); // 115
            room.SetCooling(1, 1f);

            room.AddFire();

            Assert.AreEqual(115f, room.Temperature, Tolerance);
        }

        [Test]
        public void Cooling_BlocksFireSpread()
        {
            RoomTemperatureModel room = CreateRoom();
            Assert.IsTrue(room.AllowsFireSpread);

            room.SetCooling(1, 1f);

            Assert.IsFalse(room.AllowsFireSpread);
        }

        [Test]
        public void AddHeat_WhileCooling_StopsAtLimit()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1f);

            room.AddHeat(500f);

            Assert.AreEqual(100f, room.Temperature, Tolerance);
        }

        [Test]
        public void AddHeat_Negative_IsIgnored()
        {
            RoomTemperatureModel room = CreateRoom();

            room.AddHeat(-50f);

            Assert.AreEqual(25f, room.Temperature, Tolerance);
        }

        // ── Re-ignition ──

        [Test]
        public void Reignite_AfterDelay_WhenStillAboveLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(9); // 115
            room.RemoveFire(); // 105
            Assert.IsTrue(room.ScheduleReignite(1));

            Tick(room, 14f);
            Assert.AreEqual(0, _ready.Count);

            Tick(room, 1f);
            CollectionAssert.AreEqual(new[] { 1 }, _ready);
        }

        [Test]
        public void ScheduleReignite_AtOrBelowLimit_IsRejected()
        {
            RoomTemperatureModel room = CreateHotRoom(3);

            Assert.IsFalse(room.ScheduleReignite(1));
        }

        [Test]
        public void Reignite_Skipped_WhenTemperatureFellToLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(9);
            room.RemoveFire(); // 105
            room.ScheduleReignite(1);
            room.RemoveFire(); // 95

            Tick(room, 15f);

            Assert.AreEqual(0, _ready.Count);
            Assert.IsFalse(room.IsReigniteScheduled(1));
        }

        [Test]
        public void Reignite_CancelledByCooling()
        {
            RoomTemperatureModel room = CreateHotRoom(9);
            room.RemoveFire();
            room.ScheduleReignite(1);

            room.SetCooling(1, 1f);
            Tick(room, 1f);
            room.SetCooling(0, 0f);
            Tick(room, 15f);

            Assert.AreEqual(0, _ready.Count);
        }

        [Test]
        public void ScheduleReignite_WhileCooling_IsRejected()
        {
            RoomTemperatureModel room = CreateHotRoom(12);
            room.SetCooling(1, 1f);

            Assert.IsFalse(room.ScheduleReignite(1));
        }

        [Test]
        public void ScheduleReignite_Twice_KeepsOneTimer()
        {
            RoomTemperatureModel room = CreateHotRoom(9);
            room.RemoveFire();

            Assert.IsTrue(room.ScheduleReignite(1));
            Tick(room, 10f);
            Assert.IsFalse(room.ScheduleReignite(1));

            Tick(room, 5f);
            CollectionAssert.AreEqual(new[] { 1 }, _ready);
        }

        [Test]
        public void CancelReignite_RemovesTimer()
        {
            RoomTemperatureModel room = CreateHotRoom(9);
            room.RemoveFire();
            room.ScheduleReignite(1);

            room.CancelReignite(1);
            Tick(room, 15f);

            Assert.AreEqual(0, _ready.Count);
        }

        // ── Boundaries ──

        [Test]
        public void IsInsideBox_IsHalfOpen_SoSharedFacesBelongToOneRoom()
        {
            UnityEngine.Vector3 size = new UnityEngine.Vector3(2f, 2f, 2f);

            Assert.IsTrue(RoomTemperatureModel.IsInsideBox(new UnityEngine.Vector3(-1f, 0f, 0f), size));
            Assert.IsFalse(RoomTemperatureModel.IsInsideBox(new UnityEngine.Vector3(1f, 0f, 0f), size));
            Assert.IsFalse(RoomTemperatureModel.IsInsideBox(new UnityEngine.Vector3(0f, 0f, 1.5f), size));
        }
    }
}
