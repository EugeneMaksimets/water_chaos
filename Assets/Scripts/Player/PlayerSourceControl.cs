using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Player
{
    /// <summary>
    /// Applies player intents to whatever source the hose is attached to; does nothing when detached.
    /// </summary>
    public class PlayerSourceControl : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerHoseConnector _hose;

        private void OnEnable()
        {
            _input.TogglePressed += OnTogglePressed;
            _input.PressureScrolled += OnPressureScrolled;
        }

        private void OnDisable()
        {
            _input.TogglePressed -= OnTogglePressed;
            _input.PressureScrolled -= OnPressureScrolled;
        }

        private void OnTogglePressed()
        {
            IWaterSourceControl control = GetConnectedControl();
            if (control != null)
                control.Toggle();
        }

        private void OnPressureScrolled(int direction)
        {
            IWaterSourceControl control = GetConnectedControl();
            if (control != null)
                control.AdjustPressure(direction);
        }

        private IWaterSourceControl GetConnectedControl()
        {
            return _hose.Connected != null ? _hose.Connected.Control : null;
        }
    }
}