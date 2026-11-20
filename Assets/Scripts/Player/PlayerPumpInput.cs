using UnityEngine;
using UnityEngine.InputSystem;
using WaterChaos.Pump;

namespace WaterChaos.Player
{
    /// <summary>
    /// Maps player input to pump commands: F toggles the pump, mouse wheel changes target pressure.
    /// </summary>
    public class PlayerPumpInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PumpController _pump;

        [Header("Settings")]
        [Tooltip("Target pressure change per mouse wheel notch, in pascals (Pa).")]
        [SerializeField, Min(0f)] private float _pressureStep = 50_000f;

        private void Update()
        {
            if (Keyboard.current == null || Mouse.current == null)
                return;

            if (Keyboard.current.fKey.wasPressedThisFrame)
                _pump.Toggle();

            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0f)
                _pump.SetTargetPressure(_pump.TargetPressure + Mathf.Sign(scroll) * _pressureStep);
        }
    }
}
