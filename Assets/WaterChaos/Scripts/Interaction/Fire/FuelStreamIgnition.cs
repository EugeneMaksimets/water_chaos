using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Turns a stream of flammable liquid into a flamethrower: it lights when it reaches a burning fire
    /// and, while lit, sets flammable objects and ground it hits alight under the normal fire rules
    /// (fire limit, exclusion radius, water, protected rooms). Reads the stream; never changes it.
    /// </summary>
    [DefaultExecutionOrder(10)] // after WaterStream.FixedUpdate has found this step's hit
    public sealed class FuelStreamIgnition : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaterStream _stream;

        [Tooltip("Shared fire tuning: the fire prefab and the rules where a new fire may appear.")]
        [SerializeField] private FireSpreadSettings _settings;

        [Header("Ignition")]
        [Tooltip("Seconds (s) between attempts of the lit stream to set what it hits alight.")]
        [SerializeField, Min(StreamIgnitionModel.MinInterval)] private float _ignitionInterval = 0.75f;

        private StreamIgnitionModel _model;

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
        }

        private void OnDisable()
        {
            // No flow is seen while disabled: the flame goes out.
            _model?.Tick(0f, false, false, false);
        }

        private void FixedUpdate()
        {
            WaterFlowData flow = _stream.Flow;
            bool flammableFlowing = flow.FlowRate > 0f && flow.Liquid.IsFlammable() && _stream.VisiblePointCount >= 2;

            Collider hit = _stream.HasHit ? _stream.HitCollider : null;
            FireExtinguishHandler fire = hit != null ? hit.GetComponentInParent<FireExtinguishHandler>() : null;
            FlammableObject flammable = hit != null && fire == null ? hit.GetComponentInParent<FlammableObject>() : null;
            bool flammableGround = hit != null && fire == null && flammable == null
                && hit.GetComponentInParent<FlammableSurface>() != null;

            bool touchingFire = fire != null && fire.IsBurning;
            bool touchingFlammable = (flammable != null && flammable.CanIgnite) || flammableGround;

            if (_model.Tick(Time.fixedDeltaTime, flammableFlowing, touchingFire, touchingFlammable))
                Ignite(flammable, _stream.HitPoint);
        }

        private void Ignite(FlammableObject flammable, Vector3 hitPoint)
        {
            if (flammable == null)
            {
                FireSpreader.TryStartFireAt(_settings.GetFirePosition(hitPoint), _settings);
                return;
            }

            Vector3 firePosition = _settings.GetFirePosition(flammable.IgnitionPosition);
            if (FireSpreader.AllowsIgnition(FireSpreader.FindArea(firePosition, _settings), firePosition))
                flammable.TryIgnite(_settings);
        }
    }
}
