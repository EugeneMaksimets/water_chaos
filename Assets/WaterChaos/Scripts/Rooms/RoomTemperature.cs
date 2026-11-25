using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Rooms
{
    /// <summary>
    /// One room: its trigger box finds the fires, coolers and the player inside, and its
    /// <see cref="RoomTemperatureModel"/> turns them into the room temperature.
    /// Ownership rule: an object belongs to the room whose box contains its pivot (half-open box),
    /// so an object on a shared wall is never counted by two rooms.
    /// Every burning fire heats the room once when it starts and then periodically (see <see cref="RoomTemperatureModel"/>).
    /// Fires ask the room (as their <see cref="IFireArea"/>) where they may spread; the room re-ignites
    /// extinguished fires while it stays above the limit. Never touches water, pump, hose or player code.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
    public sealed class RoomTemperature : MonoBehaviour, IFireArea
    {
        [Header("Room")]
        [Tooltip("Name shown on the HUD.")]
        [SerializeField] private string _displayName = "Room";

        [Header("Temperature")]
        [Tooltip("Temperature at start, in degrees Celsius (°C).")]
        [SerializeField] private float _startTemperature = 25f;

        [Tooltip("Lowest possible temperature (°C).")]
        [SerializeField] private float _minTemperature = -100f;

        [Tooltip("While every cooler in the room is on, fires cannot heat the room above this (°C). Also the re-ignition threshold.")]
        [SerializeField] private float _coolingLimit = 100f;

        [Header("Fire Heat")]
        [Tooltip("Heat a fire adds once when it starts burning in the room (°C); the same amount goes away when it is extinguished.")]
        [SerializeField, Min(0f)] private float _initialFireHeat = 5f;

        [Tooltip("Seconds (s) between heating steps of one burning fire. Each fire runs its own timer.")]
        [SerializeField, Min(0.1f)] private float _heatInterval = 5f;

        [Tooltip("Heat one burning fire adds every heating interval (°C).")]
        [SerializeField, Min(0f)] private float _heatPerInterval = 1f;

        [Header("Cooling")]

        [Tooltip("Cooling while above the limit with every cooler on, in degrees per second (°C/s).")]
        [SerializeField, Min(0f)] private float _overheatCooling = 2.5f;

        [Header("Fire")]
        [Tooltip("Seconds (s) after which an extinguished fire burns again if the room is still above the limit.")]
        [SerializeField, Min(0f)] private float _reigniteDelay = 15f;

        // Everything seen by the trigger; owned only while its pivot is inside the box.
        private readonly HashSet<Component> _candidates = new HashSet<Component>();
        private readonly HashSet<Component> _owned = new HashSet<Component>();
        private readonly List<Component> _buffer = new List<Component>();

        // Owned fires and the id the model knows them by.
        private readonly Dictionary<FireExtinguishHandler, int> _fireIds = new Dictionary<FireExtinguishHandler, int>();
        private readonly List<CoolingAppliance> _appliances = new List<CoolingAppliance>();
        private readonly List<int> _readyToReignite = new List<int>();

        private int _nextFireId;
        private BoxCollider _box;
        private RoomTemperatureModel _model;
        private System.Action<FireExtinguishHandler> _onFireChanged;
        private System.Action<CoolingAppliance> _onApplianceChanged;

        public string DisplayName => _displayName;
        public float Temperature => _model.Temperature;
        public float StartTemperature => _model.StartTemperature;
        public float CoolingLimit => _model.CoolingLimit;
        public int ActiveFireCount => _model.ActiveFires;
        public int ActiveCoolerCount => _model.ActiveCoolers;
        public bool IsCoolingActive => _model.IsCoolingActive;
        /// <summary>Every cooler in the room is on: fires cannot heat above the limit or spread.</summary>
        public bool IsProtected => _model.IsProtected;
        /// <summary>Coolers take away at least as much heat as the fires add.</summary>
        public bool IsCoolingWinning => _model.IsCoolingWinning;
        public bool IsOverheated => _model.IsOverheated;
        public bool AllowsSpread => _model.AllowsFireSpread;

        public int RefrigeratorCount { get; private set; }
        public int AirConditionerCount { get; private set; }
        public int ActiveRefrigeratorCount { get; private set; }
        public int ActiveAirConditionerCount { get; private set; }

        private void Awake()
        {
            _box = GetComponent<BoxCollider>();
            _box.isTrigger = true;

            // Static fires and appliances only send trigger events to a trigger with a rigidbody.
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            _model = new RoomTemperatureModel(_startTemperature, Mathf.Min(_minTemperature, _coolingLimit),
                _coolingLimit, _initialFireHeat, _heatInterval, _heatPerInterval, _overheatCooling, _reigniteDelay);

            _onFireChanged = OnFireChanged;
            _onApplianceChanged = OnApplianceChanged;
        }

        private void OnDisable()
        {
            _buffer.Clear();
            _buffer.AddRange(_owned);
            foreach (Component member in _buffer)
                SetOwned(member, false);

            _candidates.Clear();
        }

        private void Update()
        {
            RefreshOwnership();

            _readyToReignite.Clear();
            _model.Tick(Time.deltaTime, _readyToReignite);
            foreach (int fireId in _readyToReignite)
                ReigniteFire(fireId);
        }

        private void OnTriggerEnter(Collider other)
        {
            Component member = Resolve(other);
            if (member != null)
                _candidates.Add(member);
        }

        private void OnTriggerExit(Collider other)
        {
            Component member = Resolve(other);
            if (member == null || Contains(member.transform.position))
                return;

            _candidates.Remove(member);
            SetOwned(member, false);
        }

        // Play-mode test helpers (component menu ⋮ in the Inspector): reach the 100 °C rules without 8 fires.
        [ContextMenu("Debug/Heat +50 °C")]
        private void DebugHeat()
        {
            if (_model != null)
                _model.AddHeat(50f);
        }

        /// <summary>True if the point belongs to this room (half-open box, so shared walls are not double counted).</summary>
        public bool Contains(Vector3 position)
        {
            if (_box == null)
                return false;

            Vector3 offset = transform.InverseTransformPoint(position) - _box.center;
            return RoomTemperatureModel.IsInsideBox(offset, _box.size);
        }

        /// <summary>Fire, cooler or visitor a collider belongs to; child colliders map to the same object.</summary>
        private static Component Resolve(Collider other)
        {
            FireExtinguishHandler fire = other.GetComponentInParent<FireExtinguishHandler>();
            if (fire != null)
                return fire;

            CoolingAppliance appliance = other.GetComponentInParent<CoolingAppliance>();
            if (appliance != null)
                return appliance;

            return other.GetComponentInParent<RoomVisitor>();
        }

        // Trigger exits are not sent for destroyed or disabled objects, and the player moves,
        // so ownership is checked again every frame (only for the few objects the trigger has seen).
        private void RefreshOwnership()
        {
            _buffer.Clear();
            _buffer.AddRange(_candidates);

            foreach (Component member in _buffer)
            {
                if (member == null)
                {
                    _candidates.Remove(member);
                    SetOwned(member, false);
                    continue;
                }

                bool present = ((Behaviour)member).isActiveAndEnabled && Contains(member.transform.position);
                SetOwned(member, present);
            }
        }

        private void SetOwned(Component member, bool owned)
        {
            if (owned == _owned.Contains(member))
                return;

            if (owned)
                _owned.Add(member);
            else
                _owned.Remove(member);

            switch (member)
            {
                case FireExtinguishHandler fire:
                    if (owned) RegisterFire(fire);
                    else UnregisterFire(fire);
                    break;

                case CoolingAppliance appliance:
                    if (owned) RegisterAppliance(appliance);
                    else UnregisterAppliance(appliance);
                    break;

                case RoomVisitor visitor:
                    // A destroyed visitor needs no update.
                    if (visitor == null)
                        break;
                    if (owned) visitor.Enter(this);
                    else visitor.Leave(this);
                    break;
            }
        }

        // ───────────── Fires ─────────────

        private void RegisterFire(FireExtinguishHandler fire)
        {
            int fireId = ++_nextFireId;
            _fireIds.Add(fire, fireId);
            fire.SetArea(this);
            fire.BurningChanged += _onFireChanged;

            if (fire.IsBurning)
                _model.AddFire(fireId);
        }

        private void UnregisterFire(FireExtinguishHandler fire)
        {
            if (_fireIds.TryGetValue(fire, out int fireId))
            {
                _model.CancelReignite(fireId);
                // A disabled, destroyed or departed fire stops heating at once.
                _model.RemoveFire(fireId);
            }

            _fireIds.Remove(fire);
            fire.BurningChanged -= _onFireChanged;

            if (fire != null && ReferenceEquals(fire.Area, this))
                fire.SetArea(null);
        }

        private void OnFireChanged(FireExtinguishHandler fire)
        {
            if (!_fireIds.TryGetValue(fire, out int fireId))
                return;

            if (fire.IsBurning)
            {
                _model.CancelReignite(fireId);
                _model.AddFire(fireId);
                return;
            }

            // Extinguished: its heat goes away once, and it may come back if the room stays too hot.
            if (_model.RemoveFire(fireId))
                _model.ScheduleReignite(fireId);
        }

        private void ReigniteFire(int fireId)
        {
            if (!_model.AllowsFireSpread)
                return;

            foreach (KeyValuePair<FireExtinguishHandler, int> entry in _fireIds)
            {
                FireExtinguishHandler fire = entry.Key;
                if (entry.Value == fireId && fire != null)
                {
                    // The same fire object on the same target: never a duplicate fire.
                    if (fire.isActiveAndEnabled && !fire.IsBurning)
                        fire.Reignite();
                    return;
                }
            }
        }

        // ───────────── Coolers ─────────────

        private void RegisterAppliance(CoolingAppliance appliance)
        {
            _appliances.Add(appliance);
            appliance.Room = this;
            appliance.StateChanged += _onApplianceChanged;
            RecountCooling();
        }

        private void UnregisterAppliance(CoolingAppliance appliance)
        {
            _appliances.Remove(appliance);
            appliance.StateChanged -= _onApplianceChanged;
            if (appliance != null && appliance.Room == this)
                appliance.Room = null;
            RecountCooling();
        }

        private void OnApplianceChanged(CoolingAppliance appliance)
        {
            RecountCooling();
        }

        private void RecountCooling()
        {
            int refrigerators = 0;
            int airConditioners = 0;
            int activeRefrigerators = 0;
            int activeAirConditioners = 0;
            float cooling = 0f;

            foreach (CoolingAppliance appliance in _appliances)
            {
                if (appliance == null)
                    continue;

                bool working = appliance.IsWorking;
                if (appliance.Kind == CoolingApplianceKind.Refrigerator)
                {
                    refrigerators++;
                    if (working) activeRefrigerators++;
                }
                else
                {
                    airConditioners++;
                    if (working) activeAirConditioners++;
                }

                if (working)
                    cooling += appliance.CoolingPerSecond;
            }

            RefrigeratorCount = refrigerators;
            AirConditionerCount = airConditioners;
            ActiveRefrigeratorCount = activeRefrigerators;
            ActiveAirConditionerCount = activeAirConditioners;
            _model.SetCooling(refrigerators + airConditioners, activeRefrigerators + activeAirConditioners, cooling);
        }
    }
}
