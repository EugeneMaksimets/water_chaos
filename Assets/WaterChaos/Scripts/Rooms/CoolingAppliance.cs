using System;
using UnityEngine;

namespace WaterChaos.Rooms
{
    public enum CoolingApplianceKind
    {
        Refrigerator,
        AirConditioner
    }

    /// <summary>
    /// A refrigerator or air conditioner that cools the room it stands in while switched on.
    /// Owns the on/off state; the room finds it through its trigger and listens to <see cref="StateChanged"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoolingAppliance : MonoBehaviour
    {
        [Header("Cooling")]
        [SerializeField] private CoolingApplianceKind _kind = CoolingApplianceKind.Refrigerator;

        [Tooltip("Cooling while switched on, in degrees Celsius per second (°C/s).")]
        [SerializeField, Min(0f)] private float _coolingPerSecond = 1f;

        [Tooltip("Switched on when the scene starts.")]
        [SerializeField] private bool _isOn;

        public CoolingApplianceKind Kind => _kind;
        public float CoolingPerSecond => _coolingPerSecond;
        public bool IsOn => _isOn;

        /// <summary>On and enabled: actually cooling its room.</summary>
        public bool IsWorking => _isOn && isActiveAndEnabled;

        /// <summary>Room the appliance stands in; null outside every room. Set by the room.</summary>
        public RoomTemperature Room { get; internal set; }

        /// <summary>Raised when the appliance is switched, enabled or disabled.</summary>
        public event Action<CoolingAppliance> StateChanged;

        private void OnEnable()
        {
            StateChanged?.Invoke(this);
        }

        private void OnDisable()
        {
            StateChanged?.Invoke(this);
        }

        public void SetOn(bool isOn)
        {
            if (_isOn == isOn)
                return;

            _isOn = isOn;
            StateChanged?.Invoke(this);
        }

        public void Toggle()
        {
            SetOn(!_isOn);
        }

        /// <summary>Used when the component is added at runtime to a prefab set up before rooms existed.</summary>
        public void Configure(CoolingApplianceKind kind)
        {
            _kind = kind;
        }
    }
}
