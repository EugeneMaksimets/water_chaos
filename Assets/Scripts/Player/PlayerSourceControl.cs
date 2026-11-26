using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Pump;
using WaterChaos.Splitter;
using WaterChaos.Water;

namespace WaterChaos.Player
{
    /// <summary>
    /// Controls whatever source the hose is attached to; does nothing when detached.
    /// Turning it on or off is a primary-button action only (left mouse button); it is not listed in the radial menu,
    /// the mouse wheel steps the pressure.
    /// Hints: the primary prompt for "Water On/Off" shows until the first toggle,
    /// then <see cref="ShowPressureHint"/> until the first pressure change.
    /// While a connected pump is out of water (<see cref="PumpNeedsWater"/>) neither hint is shown.
    /// </summary>
    public class PlayerSourceControl : MonoBehaviour, IInteractionProvider
    {
        private const string ToggleText = "Вода: увімк./вимк.";

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerHoseConnector _hose;

        private readonly SourceControlHints _hints = new SourceControlHints();
        private Action _toggle;
        private IWaterFlowSource _deploymentOwner;
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
                actions.Add(new InteractionAction(ToggleText, null, _toggle, _hints.ShowToggleHint && !PumpNeedsWater,
                    InteractionButton.Primary, false));
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

        /// <summary>Deployment of the connected pump (also behind a splitter); null for other sources. Looked up once per source.</summary>
        private PumpDeployment GetConnectedDeployment()
        {
            WaterConnectionPoint connected = _hose.Connected;
            IWaterFlowSource origin = connected != null ? SplitterOutlet.ResolveOrigin(connected.Source) : null;
            if (!ReferenceEquals(origin, _deploymentOwner))
            {
                _deploymentOwner = origin;
                _deployment = origin is PumpController pump ? pump.GetComponent<PumpDeployment>() : null;
            }

            return _deployment;
        }

        private IWaterSourceControl GetConnectedControl()
        {
            return _hose.Connected != null ? _hose.Connected.Control : null;
        }
    }
}
