using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Rooms
{
    /// <summary>
    /// Pure gameplay model of one room's temperature, in degrees Celsius (°C).
    /// Every burning fire adds its heat once when it starts and takes it back once when it goes out,
    /// so a fire never heats the room again every frame. Working coolers lower the temperature over time
    /// and keep fires from heating the room above the cooling limit.
    /// Also keeps the re-ignition timers of extinguished fires, keyed by an id the caller chooses.
    /// </summary>
    public sealed class RoomTemperatureModel
    {
        private readonly float _minTemperature;
        private readonly float _coolingLimit;
        private readonly float _heatPerFire;
        private readonly float _overheatCooling;
        private readonly float _reigniteDelay;
        // Seconds (s) left until each scheduled fire may burn again.
        private readonly Dictionary<int, float> _reigniteTimers = new Dictionary<int, float>();
        private readonly List<int> _timerKeys = new List<int>();

        private float _coolingPerSecond;

        /// <summary>Current temperature (°C), never below the minimum.</summary>
        public float Temperature { get; private set; }

        public float StartTemperature { get; }

        /// <summary>Highest temperature (°C) fires can reach while any cooler works; also the re-ignition threshold.</summary>
        public float CoolingLimit => _coolingLimit;

        public int ActiveFires { get; private set; }
        public int ActiveCoolers { get; private set; }

        public bool IsCoolingActive => ActiveCoolers > 0;
        public bool IsOverheated => Temperature > _coolingLimit;

        /// <summary>Fires may spread and re-ignite only while no cooler works.</summary>
        public bool AllowsFireSpread => !IsCoolingActive;

        /// <param name="startTemperature">Starting temperature (°C).</param>
        /// <param name="minTemperature">Lowest possible temperature (°C).</param>
        /// <param name="coolingLimit">Temperature (°C) fires cannot heat above while cooling works.</param>
        /// <param name="heatPerFire">Heat (°C) one burning fire adds; also removed when it goes out.</param>
        /// <param name="overheatCooling">Cooling (°C/s) while above the limit with cooling on.</param>
        /// <param name="reigniteDelay">Seconds (s) before an extinguished fire may burn again.</param>
        public RoomTemperatureModel(float startTemperature, float minTemperature, float coolingLimit,
            float heatPerFire, float overheatCooling, float reigniteDelay)
        {
            if (float.IsNaN(startTemperature) || float.IsNaN(minTemperature) || float.IsNaN(coolingLimit))
                throw new ArgumentException("Room temperatures must be numbers.");
            if (minTemperature > coolingLimit)
                throw new ArgumentException("The minimum temperature must not exceed the cooling limit.");

            _minTemperature = minTemperature;
            _coolingLimit = coolingLimit;
            _heatPerFire = Sanitize(heatPerFire);
            _overheatCooling = Sanitize(overheatCooling);
            _reigniteDelay = Sanitize(reigniteDelay);

            StartTemperature = Math.Max(startTemperature, minTemperature);
            Temperature = StartTemperature;
        }

        /// <summary>A fire started burning in the room.</summary>
        public void AddFire()
        {
            ActiveFires++;
            AddHeat(_heatPerFire);
        }

        /// <summary>
        /// Heats the room (°C); while cooling works it never goes above the cooling limit this way.
        /// Negative and NaN values are ignored.
        /// </summary>
        public void AddHeat(float degrees)
        {
            if (!(degrees > 0f))
                return;

            float heated = Temperature + degrees;
            if (IsCoolingActive)
                heated = Math.Max(Temperature, Math.Min(heated, _coolingLimit));

            Temperature = heated;
        }

        /// <summary>A burning fire went out or left the room: its heat goes away once.</summary>
        public void RemoveFire()
        {
            if (ActiveFires == 0)
                return;

            ActiveFires--;
            Temperature = Math.Max(Temperature - _heatPerFire, _minTemperature);
        }

        /// <param name="activeCoolers">Coolers that are switched on.</param>
        /// <param name="coolingPerSecond">Their total cooling (°C/s).</param>
        public void SetCooling(int activeCoolers, float coolingPerSecond)
        {
            ActiveCoolers = Math.Max(activeCoolers, 0);
            _coolingPerSecond = ActiveCoolers > 0 ? Sanitize(coolingPerSecond) : 0f;
        }

        /// <summary>
        /// Starts the re-ignition timer of an extinguished fire. Rejected while cooling works,
        /// at or below the limit, or if the fire already has a timer.
        /// </summary>
        public bool ScheduleReignite(int fireId)
        {
            if (IsCoolingActive || !IsOverheated || _reigniteTimers.ContainsKey(fireId))
                return false;

            _reigniteTimers.Add(fireId, _reigniteDelay);
            return true;
        }

        public void CancelReignite(int fireId)
        {
            _reigniteTimers.Remove(fireId);
        }

        public bool IsReigniteScheduled(int fireId) => _reigniteTimers.ContainsKey(fireId);

        /// <summary>
        /// Advances cooling and re-ignition timers.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        /// <param name="readyToReignite">Filled with the fires whose delay ended while the room is still above the limit.</param>
        public void Tick(float deltaTime, List<int> readyToReignite)
        {
            if (!(deltaTime > 0f))
                return;

            if (IsCoolingActive)
            {
                Cool(deltaTime);
                // Cooling protection: nothing re-ignites.
                _reigniteTimers.Clear();
                return;
            }

            TickReignition(deltaTime, readyToReignite);
        }

        /// <summary>
        /// True if a point lies in a box centred on the origin. Half-open (min inclusive, max exclusive),
        /// so a point on a face shared by two rooms belongs to exactly one of them.
        /// </summary>
        /// <param name="offset">Point relative to the box centre, in the box's local space.</param>
        /// <param name="size">Box size in the same space.</param>
        public static bool IsInsideBox(Vector3 offset, Vector3 size)
        {
            Vector3 half = size * 0.5f;
            return offset.x >= -half.x && offset.x < half.x
                && offset.y >= -half.y && offset.y < half.y
                && offset.z >= -half.z && offset.z < half.z;
        }

        private void Cool(float deltaTime)
        {
            if (Temperature > _coolingLimit)
                Temperature = Math.Max(Temperature - _overheatCooling * deltaTime, _coolingLimit);
            else
                Temperature -= _coolingPerSecond * deltaTime;

            Temperature = Math.Max(Temperature, _minTemperature);
        }

        private void TickReignition(float deltaTime, List<int> readyToReignite)
        {
            if (_reigniteTimers.Count == 0)
                return;

            _timerKeys.Clear();
            _timerKeys.AddRange(_reigniteTimers.Keys);

            foreach (int fireId in _timerKeys)
            {
                float left = _reigniteTimers[fireId] - deltaTime;
                if (left > 0f)
                {
                    _reigniteTimers[fireId] = left;
                    continue;
                }

                _reigniteTimers.Remove(fireId);
                if (IsOverheated)
                    readyToReignite?.Add(fireId);
            }
        }

        private static float Sanitize(float value)
        {
            return float.IsNaN(value) ? 0f : Math.Max(0f, value);
        }
    }
}
