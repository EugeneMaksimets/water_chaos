using System;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Turns a stream of flammable liquid into a flamethrower (gameplay only, no visuals):
    /// it lights as soon as it reaches a burning fire, whatever the room temperature, and raises <see cref="LitChanged"/>.
    /// While lit it sets what it hits alight, once per interval per target: flammable objects (also re-lighting
    /// their extinguished fire at once) and flammable ground, under the normal fire rules
    /// (fire limit, exclusion radius, water, rooms protected by coolers).
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

        [Header("Ignition")]
        [Tooltip("Seconds (s) between attempts of the lit stream to set the same target alight; a new target is tried at once.")]
        [SerializeField, Min(StreamIgnitionModel.MinInterval)] private float _ignitionInterval = StreamIgnitionModel.DefaultInterval;

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

            object target = null;
            if (flammable != null && (flammable.CanIgnite || flammable.CanRekindle))
                target = flammable;
            else if (flammableGround)
                target = hit;

            bool touchingFire = fire != null && fire.IsBurning;
            if (_model.Tick(Time.fixedDeltaTime, IsFlammableFlowing(), touchingFire, target))
                Ignite(flammable, _stream.HitPoint);
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

        private void Ignite(FlammableObject flammable, Vector3 hitPoint)
        {
            if (flammable == null)
            {
                FireSpreader.TryStartFireAt(_settings.GetFirePosition(hitPoint), _settings);
                return;
            }

            Vector3 firePosition = _settings.GetFirePosition(flammable.IgnitionPosition);
            if (!FireSpreader.AllowsIgnition(FireSpreader.FindArea(firePosition, _settings), firePosition))
                return;

            if (!flammable.TryRekindle())
                flammable.TryIgnite(_settings);
        }

        private void OnModelLitChanged(bool lit)
        {
            LitChanged?.Invoke(lit);
        }
    }
}
