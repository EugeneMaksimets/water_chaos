using System;
using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Rooms
{
    /// <summary>
    /// Pure gameplay model of one room's temperature, in degrees Celsius (°C).
    /// Every burning fire (keyed by an id the caller chooses) adds an initial heat once when it starts,
    /// then a fixed amount once per heating interval on its own timer, and lowers the room by the extinguish
    /// heat loss once when it goes out. Working coolers lower the temperature over time while fires keep heating.
    /// Only when every cooler in the room works is the room protected: fires cannot heat it above
    /// the cooling limit, it cools down to the limit faster, and fires neither spread nor re-ignite.
    /// Also keeps the re-ignition timers of extinguished fires, keyed by an id the caller chooses.
    /// </summary>
    public sealed class RoomTemperatureModel
    {
        private const float TimeEpsilon = 0.0001f;

        private readonly float _minTemperature;
        private readonly float _coolingLimit;
        private readonly float _initialFireHeat;
        private readonly float _extinguishHeatLoss;
        private readonly float _heatInterval;
        private readonly float _heatPerInterval;
        private readonly float _overheatCooling;
        private readonly float _reigniteDelay;
        // Seconds (s) left until each scheduled fire may burn again.
        private readonly Dictionary<int, float> _reigniteTimers = new Dictionary<int, float>();
        // Seconds (s) each burning fire has heated since its last heating step.
        private readonly Dictionary<int, float> _fireHeatTimers = new Dictionary<int, float>();
        private readonly List<int> _timerKeys = new List<int>();

        private float _coolingPerSecond;

        /// <summary>Current temperature (°C), never below the minimum.</summary>
        public float Temperature { get; private set; }

        public float StartTemperature { get; }

        /// <summary>Highest temperature (°C) fires can reach while any cooler works; also the re-ignition threshold.</summary>
        public float CoolingLimit => _coolingLimit;

        public int ActiveFires => _fireHeatTimers.Count;
        /// <summary>Coolers in the room, switched on or off.</summary>
        public int TotalCoolers { get; private set; }
        public int ActiveCoolers { get; private set; }

        /// <summary>At least one cooler works: the room loses heat over time.</summary>
        public bool IsCoolingActive => ActiveCoolers > 0;

        /// <summary>Every cooler in the room works: cooling protection is on.</summary>
        public bool IsProtected => TotalCoolers > 0 && ActiveCoolers >= TotalCoolers;

        /// <summary>Average heat all burning fires add, in degrees Celsius per second (°C/s).</summary>
        public float FireHeatPerSecond =>
            _heatInterval > 0f ? ActiveFires * _heatPerInterval / _heatInterval : 0f;

        /// <summary>
        /// The working coolers take away at least as much heat as the fires add, so the room is not heating up.
        /// Under full protection above the limit it always cools down.
        /// </summary>
        public bool IsCoolingWinning =>
            IsCoolingActive && ((IsProtected && IsOverheated) || _coolingPerSecond >= FireHeatPerSecond);
        public bool IsOverheated => Temperature > _coolingLimit;

        /// <summary>Fires may spread and re-ignite unless the room is protected by all its coolers.</summary>
        public bool AllowsFireSpread => !IsProtected;

        /// <param name="startTemperature">Starting temperature (°C).</param>
        /// <param name="minTemperature">Lowest possible temperature (°C).</param>
        /// <param name="coolingLimit">Temperature (°C) fires cannot heat above while cooling works.</param>
        /// <param name="initialFireHeat">Heat (°C) a fire adds once when it starts.</param>
        /// <param name="extinguishHeatLoss">Degrees (°C) the room loses once when a fire goes out or leaves it.</param>
        /// <param name="heatInterval">Seconds (s) between heating steps of one burning fire; 0 = no periodic heat.</param>
        /// <param name="heatPerInterval">Heat (°C) one burning fire adds every heating interval.</param>
        /// <param name="overheatCooling">Cooling (°C/s) while above the limit with cooling on.</param>
        /// <param name="reigniteDelay">Seconds (s) before an extinguished fire may burn again.</param>
        public RoomTemperatureModel(float startTemperature, float minTemperature, float coolingLimit,
            float initialFireHeat, float extinguishHeatLoss, float heatInterval, float heatPerInterval, float overheatCooling, float reigniteDelay)
        {
            if (float.IsNaN(startTemperature) || float.IsNaN(minTemperature) || float.IsNaN(coolingLimit))
                throw new ArgumentException("Room temperatures must be numbers.");
            if (minTemperature > coolingLimit)
                throw new ArgumentException("The minimum temperature must not exceed the cooling limit.");

            _minTemperature = minTemperature;
            _coolingLimit = coolingLimit;
            _initialFireHeat = Sanitize(initialFireHeat);
            _extinguishHeatLoss = Sanitize(extinguishHeatLoss);
            _heatInterval = Sanitize(heatInterval);
            _heatPerInterval = Sanitize(heatPerInterval);
            _overheatCooling = Sanitize(overheatCooling);
            _reigniteDelay = Sanitize(reigniteDelay);

            StartTemperature = Math.Max(startTemperature, minTemperature);
            Temperature = StartTemperature;
        }

        /// <summary>
        /// A fire started burning in the room: adds the initial heat and starts its heating timer.
        /// Returns false (and adds nothing) if this fire is already burning here.
        /// </summary>
        public bool AddFire(int fireId)
        {
            if (_fireHeatTimers.ContainsKey(fireId))
                return false;

            _fireHeatTimers.Add(fireId, 0f);
            AddHeat(_initialFireHeat);
            return true;
        }

        public bool IsFireActive(int fireId) => _fireHeatTimers.ContainsKey(fireId);

        /// <summary>
        /// Heats the room (°C); while the room is protected it never goes above the cooling limit this way.
        /// Negative and NaN values are ignored.
        /// </summary>
        public void AddHeat(float degrees)
        {
            if (!(degrees > 0f))
                return;

            float heated = Temperature + degrees;
            if (IsProtected)
                heated = Math.Max(Temperature, Math.Min(heated, _coolingLimit));

            Temperature = heated;
        }

        /// <summary>
        /// A burning fire went out or left the room: the room loses the extinguish heat loss once and the fire stops heating.
        /// Returns false (and changes nothing) if this fire was not burning here.
        /// </summary>
        public bool RemoveFire(int fireId)
        {
            if (!_fireHeatTimers.Remove(fireId))
                return false;

            Temperature = Math.Max(Temperature - _extinguishHeatLoss, _minTemperature);
            return true;
        }

        /// <param name="totalCoolers">All coolers in the room, switched on or off.</param>
        /// <param name="activeCoolers">Coolers that are switched on.</param>
        /// <param name="coolingPerSecond">Total cooling of the switched-on coolers (°C/s).</param>
        public void SetCooling(int totalCoolers, int activeCoolers, float coolingPerSecond)
        {
            TotalCoolers = Math.Max(totalCoolers, 0);
            ActiveCoolers = Math.Min(Math.Max(activeCoolers, 0), TotalCoolers);
            _coolingPerSecond = ActiveCoolers > 0 ? Sanitize(coolingPerSecond) : 0f;
        }

        /// <summary>
        /// Starts the re-ignition timer of an extinguished fire. Rejected while the room is protected,
        /// at or below the limit, or if the fire already has a timer.
        /// </summary>
        public bool ScheduleReignite(int fireId)
        {
            if (IsProtected || !IsOverheated || _reigniteTimers.ContainsKey(fireId))
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
        /// Advances fire heating, cooling and re-ignition timers.
        /// </summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        /// <param name="readyToReignite">Filled with the fires whose delay ended while the room is still above the limit.</param>
        public void Tick(float deltaTime, List<int> readyToReignite)
        {
            if (!(deltaTime > 0f))
                return;

            TickFireHeat(deltaTime);

            if (IsCoolingActive)
                Cool(deltaTime);

            if (IsProtected)
            {
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
            // Fast cooling down to the limit only under full protection.
            if (IsProtected && Temperature > _coolingLimit)
                Temperature = Math.Max(Temperature - _overheatCooling * deltaTime, _coolingLimit);
            else
                Temperature -= _coolingPerSecond * deltaTime;

            Temperature = Math.Max(Temperature, _minTemperature);
        }

        /// <summary>Every burning fire adds its heat once per interval it has burned, on its own timer.</summary>
        private void TickFireHeat(float deltaTime)
        {
            if (_fireHeatTimers.Count == 0 || _heatInterval <= 0f || _heatPerInterval <= 0f)
                return;

            _timerKeys.Clear();
            _timerKeys.AddRange(_fireHeatTimers.Keys);

            foreach (int fireId in _timerKeys)
            {
                float elapsed = _fireHeatTimers[fireId] + deltaTime;
                // Epsilon: many small frame steps must not lose a heating step to float rounding.
                int steps = (int)((elapsed + TimeEpsilon) / _heatInterval);
                if (steps > 0)
                {
                    elapsed = Math.Max(elapsed - steps * _heatInterval, 0f);
                    AddHeat(steps * _heatPerInterval);
                }

                _fireHeatTimers[fireId] = elapsed;
            }
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
