using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.Player
{
    /// <summary>
    /// Controls whatever source the hose is attached to; does nothing when detached.
    /// Turning it on or off is offered as an interaction action (primary button or the menu),
    /// the mouse wheel steps the pressure.
    /// Hints: the primary prompt for "Water On/Off" shows until the first toggle,
    /// then <see cref="ShowPressureHint"/> until the first pressure change.
    /// While a connected pump is out of water (<see cref="PumpNeedsWater"/>) neither hint is shown.
    /// </summary>
    public class PlayerSourceControl : MonoBehaviour, IInteractionProvider
    {
        private const string ToggleText = "Water On/Off";

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerHoseConnector _hose;

        private readonly SourceControlHints _hints = new SourceControlHints();
        private Action _toggle;
        private WaterConnectionPoint _deploymentOwner;
        private PumpDeployment _deployment;

        /// <summary>A source is connected and the player has toggled it, but never changed the pressure.</summary>
        public bool ShowPressureHint => _hints.ShowPressureHint && GetConnectedControl() != null && !PumpNeedsWater;

        /// <summary>The connected source is a pump whose intake is not properly in water.</summary>
        public bool PumpNeedsWater
        {
            get
            {
                PumpDeployment deployment = GetConnectedDeployment();
                return deployment != null && IntakeStatusRules.NeedsWater(deployment.Status);
            }
        }

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
                actions.Add(new InteractionAction(ToggleText, null, _toggle, _hints.ShowToggleHint && !PumpNeedsWater));
        }

        private void Toggle()
        {
            IWaterSourceControl control = GetConnectedControl();
            if (control == null)
                return;

            control.Toggle();
            _hints.NotifyToggled();
        }

        private void OnPressureScrolled(int direction)
        {
            IWaterSourceControl control = GetConnectedControl();
            if (control == null)
                return;

            control.AdjustPressure(direction);
            _hints.NotifyPressureChanged();
        }

        /// <summary>Deployment of the connected pump; null for other sources. Looked up once per connection.</summary>
        private PumpDeployment GetConnectedDeployment()
        {
            WaterConnectionPoint connected = _hose.Connected;
            if (connected != _deploymentOwner)
            {
                _deploymentOwner = connected;
                _deployment = connected != null && connected.Source is PumpController pump
                    ? pump.GetComponent<PumpDeployment>()
                    : null;
            }

            return _deployment;
        }

        private IWaterSourceControl GetConnectedControl()
        {
            return _hose.Connected != null ? _hose.Connected.Control : null;
        }
    }
}
