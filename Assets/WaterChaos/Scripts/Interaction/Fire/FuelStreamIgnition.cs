using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Turns a stream of flammable liquid into a flamethrower (gameplay only, no visuals):
    /// it lights as soon as it reaches a burning fire, whatever the room temperature, and raises <see cref="LitChanged"/>.
    /// While lit the stream is cut to <c>Flame Length</c>, so only the nearest thing in front of the nozzle burns.
    /// While lit it sets what it hits alight, once per interval per target: flammable objects (also re-lighting
    /// their extinguished fire at once), flammable ground, and inside a room any surface (a fire hitting a wall
    /// starts on the floor below). The normal fire rules apply (fire limit, exclusion radius, water);
    /// room cooling protection only if <c>Respect Room Cooling</c> is on.
    /// It goes out at once when the nozzle is switched off, put down, set to zero pressure or runs dry;
    /// switched on again it needs a fire again.
    /// </summary>
    [DefaultExecutionOrder(10)] // after WaterStream.FixedUpdate has found this step's hit
    public sealed class FuelStreamIgnition : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaterStream _stream;

        [Tooltip("Shared fire tuning: the fire prefab and the rules where a new fire may appear.")]
        [SerializeField] private FireSpreadSettings _settings;

        [Header("Flame")]
        [Tooltip("While lit the stream reaches only this far, in meters (m), and sets alight the nearest thing within it.")]
        [SerializeField, Min(0.1f)] private float _flameLength = 3f;

        [Header("Ignition")]
        [Tooltip("Seconds (s) between attempts of the lit stream to set the same target alight; a new target is tried at once.")]
        [SerializeField, Min(StreamIgnitionModel.MinInterval)] private float _ignitionInterval = StreamIgnitionModel.DefaultInterval;

        [Tooltip("Off: the flamethrower lights fires in a room even while all its coolers work. " +
                 "On: a room protected by its coolers cannot be set alight by it (like fire spreading).")]
        [SerializeField] private bool _respectRoomCooling;

        [Tooltip("How far below a hit on a wall the floor is searched for the new fire, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDropToFloor = 3f;

        // A surface counts as floor when its normal is within about 45° of straight up.
        private const float FloorMinUp = 0.7f;
        // A drop ray starts this far out of the wall, so it does not start inside it, in meters (m).
        private const float WallClearance = 0.2f;

        private enum TargetKind
        {
            None,
            Object,
            Ground,
            Room,
        }

        private StreamIgnitionModel _model;

        /// <summary>Raised when the stream lights (true) or goes out (false).</summary>
        public event Action<bool> LitChanged;

        /// <summary>The stream burns (flamethrower).</summary>
        public bool IsLit => _model != null && _model.IsLit;

        private void Awake()
        {
            if (_stream == null || _settings == null)
            {
                Debug.LogError($"{name}: FuelStreamIgnition needs Stream and Settings references.", this);
                enabled = false;
                return;
            }

            _model = new StreamIgnitionModel(_ignitionInterval);
            _model.LitChanged += OnModelLitChanged;
        }

        private void OnDestroy()
        {
            if (_model != null)
                _model.LitChanged -= OnModelLitChanged;
        }

        private void OnDisable()
        {
            // No flow is seen while disabled: the flame goes out.
            _model?.Tick(0f, false, false, null);
        }

        private void FixedUpdate()
        {
            Collider hit = _stream.HasHit ? _stream.HitCollider : null;
            FireExtinguishHandler fire = hit != null ? hit.GetComponentInParent<FireExtinguishHandler>() : null;
            FlammableObject flammable = hit != null && fire == null ? hit.GetComponentInParent<FlammableObject>() : null;
            bool flammableGround = hit != null && fire == null && flammable == null
                && hit.GetComponentInParent<FlammableSurface>() != null;

            TargetKind kind = TargetKind.None;
            object target = null;
            if (flammable != null && (flammable.CanIgnite || flammable.CanRekindle))
            {
                kind = TargetKind.Object;
                target = flammable;
            }
            else if (flammableGround)
            {
                kind = TargetKind.Ground;
                target = hit;
            }
            else if (_model.IsLit && hit != null && fire == null && flammable == null && IsInRoom(_stream.HitPoint))
            {
                // Rooms have no flammable surfaces of their own: inside one, whatever the flame hits can burn.
                kind = TargetKind.Room;
                target = hit;
            }

            bool touchingFire = fire != null && fire.IsBurning;
            if (_model.Tick(Time.fixedDeltaTime, IsFlammableFlowing(), touchingFire, target))
                Ignite(kind, flammable, _stream.HitPoint, _stream.HitNormal);
        }

        /// <summary>
        /// True if a flamethrower may light a fire at <paramref name="firePosition"/> in <paramref name="area"/>:
        /// always, unless <paramref name="respectRoomCooling"/> is on and the room is protected by its coolers.
        /// </summary>
        public static bool AllowsIgnitionIn(IFireArea area, Vector3 firePosition, bool respectRoomCooling)
        {
            return !respectRoomCooling || FireSpreader.AllowsIgnition(area, firePosition);
        }

        /// <summary>The surface faces up enough to hold a fire (floor, table top), not a wall or a ceiling.</summary>
        public static bool IsFloorLike(Vector3 normal)
        {
            return normal.sqrMagnitude > 0f && normal.normalized.y >= FloorMinUp;
        }

        private bool IsInRoom(Vector3 hitPoint)
        {
            return FireSpreader.FindArea(_settings.GetFirePosition(hitPoint), _settings) != null;
        }

        /// <summary>The nozzle is active (source on, pressure above zero) and flammable liquid flows.</summary>
        private bool IsFlammableFlowing()
        {
            WaterFlowData flow = _stream.Flow;
            if (!(flow.FlowRate > 0f) || !flow.Liquid.IsFlammable() || _stream.VisiblePointCount < 2)
                return false;

            // Switched off or turned down to zero: the flame goes out at once, not when the pressure has run down.
            return !(_stream.Source is IWaterSourceControl control) || control.IsActive;
        }

        private void Ignite(TargetKind kind, FlammableObject flammable, Vector3 hitPoint, Vector3 hitNormal)
        {
            switch (kind)
            {
                case TargetKind.Object:
                    Vector3 firePosition = _settings.GetFirePosition(flammable.IgnitionPosition);
                    if (!AllowsIgnitionIn(FireSpreader.FindArea(firePosition, _settings), firePosition, _respectRoomCooling))
                        return;

                    if (!flammable.TryRekindle())
                        flammable.TryIgnite(_settings);
                    return;

                case TargetKind.Ground:
                    FireSpreader.TryStartFireAt(_settings.GetFirePosition(hitPoint), _settings, _respectRoomCooling);
                    return;

                case TargetKind.Room:
                    Vector3 burnPoint = GetBurnPoint(hitPoint, hitNormal);
                    FireSpreader.TryStartFireAt(_settings.GetFirePosition(burnPoint), _settings, _respectRoomCooling);
                    return;
            }
        }

        /// <summary>Where a fire from this hit stands: the hit itself on a floor, else the floor below it.</summary>
        private Vector3 GetBurnPoint(Vector3 hitPoint, Vector3 hitNormal)
        {
            if (IsFloorLike(hitNormal))
                return hitPoint;

            Vector3 from = hitPoint + hitNormal.normalized * WallClearance;
            return Physics.Raycast(from, Vector3.down, out RaycastHit floor, _maxDropToFloor, _settings.TargetMask, QueryTriggerInteraction.Ignore)
                ? floor.point
                : hitPoint;
        }

        private void OnModelLitChanged(bool lit)
        {
            // A flame is short; unlit liquid flies as far as water.
            _stream.MaxLength = lit ? _flameLength : 0f;
            LitChanged?.Invoke(lit);
        }
    }
}
