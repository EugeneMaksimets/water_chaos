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
        private const float InitialFireHeat = 5f;
        private const float HeatInterval = 5f;
        private const float HeatPerInterval = 1f;
        private const float OverheatCooling = 2.5f;
        private const float ReigniteDelay = 15f;
        private const float Tolerance = 0.001f;

        private readonly List<int> _ready = new List<int>();

        private static RoomTemperatureModel CreateRoom(float heatPerInterval = HeatPerInterval,
            float extinguishHeatLoss = InitialFireHeat)
        {
            return new RoomTemperatureModel(Start, Min, Limit, InitialFireHeat, extinguishHeatLoss, HeatInterval,
                heatPerInterval, OverheatCooling, ReigniteDelay);
        }

        /// <summary>Room with fires 1..<paramref name="fires"/> burning; each adds 5 °C at once.</summary>
        private static RoomTemperatureModel CreateHotRoom(int fires, float heatPerInterval = HeatPerInterval)
        {
            RoomTemperatureModel room = CreateRoom(heatPerInterval);
            for (int i = 1; i <= fires; i++)
                room.AddFire(i);
            return room;
        }

        [Test]
        public void RemoveHeat_CoolsTheRoom_NeverBelowMinimum_IgnoresBadValues()
        {
            RoomTemperatureModel room = CreateRoom();

            room.RemoveHeat(30f);
            Assert.AreEqual(Start - 30f, room.Temperature, Tolerance);

            room.RemoveHeat(-10f);
            room.RemoveHeat(float.NaN);
            Assert.AreEqual(Start - 30f, room.Temperature, Tolerance);

            room.RemoveHeat(10000f);
            Assert.AreEqual(Min, room.Temperature, Tolerance);
        }

        private void Tick(RoomTemperatureModel room, float seconds)
        {
            _ready.Clear();
            room.Tick(seconds, _ready);
        }

        // ── Initial fire heat ──

        [Test]
        public void NewRoom_StartsAtStartTemperature()
        {
            RoomTemperatureModel room = CreateRoom();

            Assert.AreEqual(25f, room.Temperature, Tolerance);
            Assert.AreEqual(0, room.ActiveFires);
            Assert.IsFalse(room.IsCoolingActive);
        }

        [Test]
        public void OneFire_AddsFiveDegreesInitially()
        {
            RoomTemperatureModel room = CreateRoom();

            Assert.IsTrue(room.AddFire(1));

            Assert.AreEqual(30f, room.Temperature, Tolerance);
            Assert.AreEqual(1, room.ActiveFires);
        }

        [Test]
        public void TwoFires_AddTenDegreesInitially()
        {
            RoomTemperatureModel room = CreateHotRoom(2);

            Assert.AreEqual(35f, room.Temperature, Tolerance);
            Assert.AreEqual(2, room.ActiveFires);
        }

        [Test]
        public void AddFire_SameFireTwice_AddsInitialHeatOnce()
        {
            RoomTemperatureModel room = CreateRoom();
            room.AddFire(1);

            Assert.IsFalse(room.AddFire(1));

            Assert.AreEqual(30f, room.Temperature, Tolerance);
            Assert.AreEqual(1, room.ActiveFires);
        }

        // ── Periodic fire heat ──

        [Test]
        public void OneFire_AddsOneDegreeAfterFiveSeconds()
        {
            RoomTemperatureModel room = CreateHotRoom(1);

            Tick(room, 5f);

            Assert.AreEqual(31f, room.Temperature, Tolerance);
        }

        [Test]
        public void OneFire_BeforeInterval_AddsNothing()
        {
            RoomTemperatureModel room = CreateHotRoom(1);

            Tick(room, 4.9f);

            Assert.AreEqual(30f, room.Temperature, Tolerance);
        }

        [Test]
        public void ThreeFires_AddThreeDegreesEveryFiveSeconds()
        {
            RoomTemperatureModel room = CreateHotRoom(3);
            Assert.AreEqual(40f, room.Temperature, Tolerance);

            Tick(room, 5f);
            Assert.AreEqual(43f, room.Temperature, Tolerance);

            Tick(room, 5f);
            Assert.AreEqual(46f, room.Temperature, Tolerance);

            Tick(room, 5f);
            Assert.AreEqual(49f, room.Temperature, Tolerance);
        }

        [Test]
        public void Heating_DoesNotDependOnFrameRate()
        {
            RoomTemperatureModel slow = CreateHotRoom(1);
            RoomTemperatureModel fast = CreateHotRoom(1);

            Tick(slow, 10f);
            for (int i = 0; i < 40; i++)
                Tick(fast, 0.25f);

            Assert.AreEqual(32f, slow.Temperature, Tolerance);
            Assert.AreEqual(32f, fast.Temperature, Tolerance);
        }

        [Test]
        public void Fires_HeatOnTheirOwnTimers()
        {
            RoomTemperatureModel room = CreateRoom();
            room.AddFire(1);           // 30
            Tick(room, 2f);
            room.AddFire(2);           // 35

            Tick(room, 3f);            // fire 1 reaches 5 s
            Assert.AreEqual(36f, room.Temperature, Tolerance);

            Tick(room, 2f);            // fire 2 reaches 5 s
            Assert.AreEqual(37f, room.Temperature, Tolerance);
        }

        [Test]
        public void RemoveFire_StopsItsFutureHeating()
        {
            RoomTemperatureModel room = CreateHotRoom(2); // 35
            Tick(room, 5f);                               // 37

            room.RemoveFire(1);                           // 32
            Tick(room, 5f);                               // only fire 2 heats

            Assert.AreEqual(33f, room.Temperature, Tolerance);
            Assert.AreEqual(1, room.ActiveFires);
        }

        // ── Removing fires ──

        [Test]
        public void RemoveFire_LowersTemperatureByInitialHeatOnce()
        {
            RoomTemperatureModel room = CreateHotRoom(2);

            Assert.IsTrue(room.RemoveFire(1));
            Assert.IsFalse(room.RemoveFire(1));

            Assert.AreEqual(30f, room.Temperature, Tolerance);
            Assert.AreEqual(1, room.ActiveFires);
        }

        [Test]
        public void RemoveFire_LowersTemperatureByExtinguishHeatLoss()
        {
            RoomTemperatureModel room = CreateRoom(extinguishHeatLoss: 12f);
            room.AddFire(1);                              // 30

            room.RemoveFire(1);

            Assert.AreEqual(18f, room.Temperature, Tolerance);
        }

        [Test]
        public void RemoveFire_UnknownFire_DoesNothing()
        {
            RoomTemperatureModel room = CreateRoom();

            Assert.IsFalse(room.RemoveFire(7));

            Assert.AreEqual(25f, room.Temperature, Tolerance);
            Assert.AreEqual(0, room.ActiveFires);
        }

        [Test]
        public void FireAddedAgainAfterRemoval_DoesNotDuplicateHeat()
        {
            RoomTemperatureModel room = CreateRoom();

            room.AddFire(1);
            room.RemoveFire(1);
            room.AddFire(1);

            Assert.AreEqual(30f, room.Temperature, Tolerance);
            Assert.AreEqual(1, room.ActiveFires);
        }

        // ── Cooling ──

        [Test]
        public void Cooling_CanGoBelowZero()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1, 1f);

            Tick(room, 30f);

            Assert.AreEqual(-5f, room.Temperature, Tolerance);
        }

        [Test]
        public void Cooling_NeverGoesBelowMinimum()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(4, 4, 4f);

            Tick(room, 1000f);

            Assert.AreEqual(-100f, room.Temperature, Tolerance);
        }

        [Test]
        public void OneActiveAppliance_CoolsOneDegreePerSecond()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1, 1f);

            Tick(room, 1f);

            Assert.AreEqual(24f, room.Temperature, Tolerance);
            Assert.IsTrue(room.IsCoolingActive);
        }

        [Test]
        public void TwoActiveAppliances_CoolTwoDegreesPerSecond()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(2, 2, 2f);

            Tick(room, 1f);

            Assert.AreEqual(23f, room.Temperature, Tolerance);
        }

        [Test]
        public void CoolingSwitchedOff_StopsCooling()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1, 1f);
            Tick(room, 1f);

            room.SetCooling(0, 0, 0f);
            Tick(room, 5f);

            Assert.AreEqual(24f, room.Temperature, Tolerance);
            Assert.IsFalse(room.IsCoolingActive);
        }

        [Test]
        public void Cooling_KeepsNewFiresFromHeatingAboveLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(14); // 95
            room.SetCooling(1, 1, 1f);

            room.AddFire(15);
            room.AddFire(16);
            room.AddFire(17);

            Assert.AreEqual(100f, room.Temperature, Tolerance);
            Assert.AreEqual(17, room.ActiveFires);
        }

        [Test]
        public void Cooling_KeepsPeriodicHeatFromGoingAboveLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(15); // 100
            room.SetCooling(1, 1, 0f);

            Tick(room, 10f);

            Assert.AreEqual(100f, room.Temperature, Tolerance);
        }

        [Test]
        public void Cooling_AboveLimit_CoolsAtOverheatRateDownToLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(18); // 115
            room.SetCooling(1, 1, 1f);

            Tick(room, 2f);
            Assert.AreEqual(110f, room.Temperature, Tolerance);

            Tick(room, 10f);
            Assert.AreEqual(100f, room.Temperature, Tolerance);
        }

        [Test]
        public void Cooling_AtOrBelowLimit_UsesApplianceRate()
        {
            RoomTemperatureModel room = CreateHotRoom(16); // 105
            room.SetCooling(2, 2, 2f);
            Tick(room, 2f); // 100

            Tick(room, 1f);

            Assert.AreEqual(98f, room.Temperature, Tolerance);
        }

        [Test]
        public void AddFire_WhileCoolingAboveLimit_DoesNotHeat()
        {
            RoomTemperatureModel room = CreateHotRoom(18); // 115
            room.SetCooling(1, 1, 1f);

            room.AddFire(19);

            Assert.AreEqual(115f, room.Temperature, Tolerance);
        }

        [Test]
        public void Cooling_BlocksFireSpread_AndSwitchingOffAllowsItAgain()
        {
            RoomTemperatureModel room = CreateHotRoom(3);
            Assert.IsTrue(room.AllowsFireSpread);

            room.SetCooling(1, 1, 1f);
            Assert.IsFalse(room.AllowsFireSpread);

            room.SetCooling(0, 0, 0f);
            Assert.IsTrue(room.AllowsFireSpread);
        }

        [Test]
        public void ExtinguishingAFire_DoesNotBlockFireSpread()
        {
            RoomTemperatureModel room = CreateHotRoom(3);

            room.RemoveFire(1);

            Assert.IsTrue(room.AllowsFireSpread);
        }

        // ── Partial cooling: only some of the room's coolers are on ──

        [Test]
        public void PartialCooling_FiresHeatAndCoolerCoolsTogether()
        {
            RoomTemperatureModel room = CreateHotRoom(1); // 30
            room.SetCooling(2, 1, 1f);

            Tick(room, 5f); // +1 from the fire, -5 from the fridge

            Assert.AreEqual(26f, room.Temperature, Tolerance);
            Assert.IsTrue(room.IsCoolingActive);
            Assert.IsFalse(room.IsProtected);
        }

        [Test]
        public void PartialCooling_ManyFires_StillHeatTheRoom()
        {
            RoomTemperatureModel room = CreateHotRoom(8); // 65
            room.SetCooling(2, 1, 1f);

            Tick(room, 5f); // +8 from fires, -5 from the fridge

            Assert.AreEqual(68f, room.Temperature, Tolerance);
        }

        [Test]
        public void PartialCooling_DoesNotCapFiresAtLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(15); // 100
            room.SetCooling(2, 1, 0f);

            room.AddFire(16);

            Assert.AreEqual(105f, room.Temperature, Tolerance);
        }

        [Test]
        public void PartialCooling_AllowsSpreadAndReignition()
        {
            RoomTemperatureModel room = CreateHotRoom(18, 0f); // 115
            room.SetCooling(2, 1, 0f);
            room.RemoveFire(1);

            Assert.IsTrue(room.AllowsFireSpread);
            Assert.IsTrue(room.ScheduleReignite(1));
        }

        // ── Which wins: cooling or fire heat ──

        [Test]
        public void FireHeatPerSecond_IsFiresTimesHeatPerInterval()
        {
            RoomTemperatureModel room = CreateHotRoom(8);

            Assert.AreEqual(1.6f, room.FireHeatPerSecond, Tolerance); // 8 × 1 °C / 5 s
        }

        [Test]
        public void NoCoolerOn_CoolingDoesNotWin()
        {
            RoomTemperatureModel room = CreateRoom();

            Assert.IsFalse(room.IsCoolingWinning);
        }

        [Test]
        public void CoolerFasterThanFires_CoolingWins()
        {
            RoomTemperatureModel room = CreateHotRoom(1); // 0.2 °C/s
            room.SetCooling(2, 1, 1f);

            Assert.IsTrue(room.IsCoolingWinning);
        }

        [Test]
        public void FiresFasterThanCooler_CoolingDoesNotWin()
        {
            RoomTemperatureModel room = CreateHotRoom(8); // 1.6 °C/s
            room.SetCooling(2, 1, 1f);

            Assert.IsFalse(room.IsCoolingWinning);
        }

        [Test]
        public void ExtinguishingFires_LetsCoolingWin()
        {
            RoomTemperatureModel room = CreateHotRoom(8);
            room.SetCooling(2, 1, 1f);

            for (int i = 1; i <= 4; i++)
                room.RemoveFire(i); // 4 fires: 0.8 °C/s

            Assert.IsTrue(room.IsCoolingWinning);
        }

        [Test]
        public void Protected_AboveLimit_CoolingWins()
        {
            RoomTemperatureModel room = CreateHotRoom(30); // 175, 6 °C/s of fire heat
            room.SetCooling(2, 2, 2f);

            Assert.IsTrue(room.IsCoolingWinning);
        }

        [Test]
        public void AllCoolersOn_ProtectTheRoom()
        {
            RoomTemperatureModel room = CreateRoom();

            room.SetCooling(2, 2, 2f);

            Assert.IsTrue(room.IsProtected);
            Assert.IsFalse(room.AllowsFireSpread);
        }

        [Test]
        public void NoCoolersInRoom_NoProtection()
        {
            RoomTemperatureModel room = CreateRoom();

            room.SetCooling(0, 0, 0f);

            Assert.IsFalse(room.IsProtected);
            Assert.IsTrue(room.AllowsFireSpread);
        }

        [Test]
        public void AddHeat_WhileCooling_StopsAtLimit()
        {
            RoomTemperatureModel room = CreateRoom();
            room.SetCooling(1, 1, 1f);

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

        // ── Re-ignition (periodic heat off, so only the timers matter) ──

        [Test]
        public void Reignite_AfterDelay_WhenStillAboveLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(18, 0f); // 115
            room.RemoveFire(1);                                // 110
            Assert.IsTrue(room.ScheduleReignite(1));

            Tick(room, 14f);
            Assert.AreEqual(0, _ready.Count);

            Tick(room, 1f);
            CollectionAssert.AreEqual(new[] { 1 }, _ready);
        }

        [Test]
        public void ScheduleReignite_AtOrBelowLimit_IsRejected()
        {
            RoomTemperatureModel room = CreateHotRoom(3, 0f);

            Assert.IsFalse(room.ScheduleReignite(1));
        }

        [Test]
        public void Reignite_Skipped_WhenTemperatureFellToLimit()
        {
            RoomTemperatureModel room = CreateHotRoom(17, 0f); // 110
            room.RemoveFire(1);                                // 105
            room.ScheduleReignite(1);
            room.RemoveFire(2);                                // 100

            Tick(room, 15f);

            Assert.AreEqual(0, _ready.Count);
            Assert.IsFalse(room.IsReigniteScheduled(1));
        }

        [Test]
        public void Reignite_CancelledByCooling()
        {
            RoomTemperatureModel room = CreateHotRoom(18, 0f);
            room.RemoveFire(1);
            room.ScheduleReignite(1);

            room.SetCooling(1, 1, 1f);
            Tick(room, 1f);
            room.SetCooling(0, 0, 0f);
            Tick(room, 15f);

            Assert.AreEqual(0, _ready.Count);
        }

        [Test]
        public void ScheduleReignite_WhileCooling_IsRejected()
        {
            RoomTemperatureModel room = CreateHotRoom(20, 0f);
            room.SetCooling(1, 1, 1f);

            Assert.IsFalse(room.ScheduleReignite(1));
        }

        [Test]
        public void ScheduleReignite_Twice_KeepsOneTimer()
        {
            RoomTemperatureModel room = CreateHotRoom(18, 0f);
            room.RemoveFire(1);

            Assert.IsTrue(room.ScheduleReignite(1));
            Tick(room, 10f);
            Assert.IsFalse(room.ScheduleReignite(1));

            Tick(room, 5f);
            CollectionAssert.AreEqual(new[] { 1 }, _ready);
        }

        [Test]
        public void CancelReignite_RemovesTimer()
        {
            RoomTemperatureModel room = CreateHotRoom(18, 0f);
            room.RemoveFire(1);
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


        [Test]
        public void NoFireLimit_ByDefault_AllowsSpreadWithManyFires()
        {
            RoomTemperatureModel room = CreateHotRoom(20);

            Assert.IsTrue(room.AllowsFireSpread);
        }

        [Test]
        public void FireLimit_Reached_StopsSpreading()
        {
            RoomTemperatureModel room = CreateHotRoom(3);

            room.SetMaxFires(3);

            Assert.IsFalse(room.AllowsFireSpread);
        }

        [Test]
        public void FireLimit_NotReached_AllowsSpreading()
        {
            RoomTemperatureModel room = CreateHotRoom(2);

            room.SetMaxFires(3);

            Assert.IsTrue(room.AllowsFireSpread);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void FireLimit_ZeroOrLess_MeansNoLimit(int max)
        {
            RoomTemperatureModel room = CreateHotRoom(10);

            room.SetMaxFires(max);

            Assert.IsTrue(room.AllowsFireSpread);
            Assert.AreEqual(0, room.MaxFires);
        }
    }
}
