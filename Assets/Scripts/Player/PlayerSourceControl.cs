using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Player
{
    /// <summary>
    /// Controls whatever source the hose is attached to; does nothing when detached.
    /// Turning it on or off is offered as an interaction action (primary button or the menu),
    /// the mouse wheel steps the pressure.
    /// </summary>
    public class PlayerSourceControl : MonoBehaviour, IInteractionProvider
    {
        private const string ToggleText = "Water On/Off";

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerHoseConnector _hose;

        private Action _toggle;

        private void Awake()
        {
            _toggle = Toggle;
        }

        private void OnEnable()
        {
            _input.PressureScrolled += OnPressureScrolled;
        }

        private void OnDisable()
        {
            _input.PressureScrolled -= OnPressureScrolled;
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            if (GetConnectedControl() != null)
                actions.Add(new InteractionAction(ToggleText, null, _toggle));
        }

        private void Toggle()
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
