using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Rooms;

namespace WaterChaos.Props
{
    /// <summary>
    /// Wall air conditioner the player turns on and off from the interaction menu.
    /// When on: the louver opens, the status light shows and the cold-air streaks pulse.
    /// The on/off state lives in <see cref="CoolingAppliance"/>, which also cools the room.
    /// </summary>
    public class AirConditioner : MonoBehaviour, IInteractionProvider
    {
        private const string TurnOnText = "Turn On";
        private const string TurnOffText = "Turn Off";

        [Header("References")]
        [Tooltip("Louver flap; its pivot sits on the hinge (local X axis).")]
        [SerializeField] private Transform _louver;

        [Tooltip("Display and LED shown only while the unit is on.")]
        [SerializeField] private GameObject _statusLight;

        [Tooltip("Cold-air streaks shown only while the unit is on; scaled from its pivot at the outlet.")]
        [SerializeField] private Transform _airFlow;

        [Header("Settings")]
        [Tooltip("Louver angle when open, in degrees around its hinge (local X). Flip the sign if it opens inward.")]
        [SerializeField] private float _louverOpenAngle = -55f;

        [Tooltip("Louver speed, in degrees per second.")]
        [SerializeField, Min(1f)] private float _louverSpeed = 90f;

        [Tooltip("How much the air streaks stretch while pulsing (0.1 = ±10%).")]
        [SerializeField, Range(0f, 0.5f)] private float _airPulse = 0.12f;

        [Tooltip("Air pulse speed, in cycles per second (Hz).")]
        [SerializeField, Min(0f)] private float _airPulseFrequency = 1.5f;

        private Quaternion _louverClosedRotation;
        private Vector3 _airFlowScale;
        private float _louverAngle;
        private CoolingAppliance _cooling;
        private Action _toggle;
        private Action<CoolingAppliance> _onCoolingChanged;

        public bool IsOn => _cooling != null && _cooling.IsOn;

        private void Awake()
        {
            if (_louver == null)
            {
                Debug.LogError($"{name}: AirConditioner needs a Louver reference.", this);
                enabled = false;
                return;
            }

            _louverClosedRotation = _louver.localRotation;
            if (_airFlow != null)
                _airFlowScale = _airFlow.localScale;

            // Prefabs made before rooms existed get the cooler added here.
            if (!TryGetComponent(out _cooling))
            {
                _cooling = gameObject.AddComponent<CoolingAppliance>();
                _cooling.Configure(CoolingApplianceKind.AirConditioner);
            }

            _toggle = Toggle;
            _onCoolingChanged = OnCoolingChanged;
            _cooling.StateChanged += _onCoolingChanged;
            UpdateVisuals();
        }

        private void OnDestroy()
        {
            if (_cooling != null)
                _cooling.StateChanged -= _onCoolingChanged;
        }

        private void Update()
        {
            bool isOn = IsOn;
            float target = isOn ? _louverOpenAngle : 0f;
            if (!Mathf.Approximately(_louverAngle, target))
            {
                _louverAngle = Mathf.MoveTowards(_louverAngle, target, _louverSpeed * Time.deltaTime);
                _louver.localRotation = _louverClosedRotation * Quaternion.Euler(_louverAngle, 0f, 0f);
            }

            if (isOn && _airFlow != null)
            {
                float pulse = 1f + _airPulse * Mathf.Sin(Time.time * _airPulseFrequency * 2f * Mathf.PI);
                _airFlow.localScale = _airFlowScale * pulse;
            }
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            if (!isActiveAndEnabled)
                return;

            actions.Add(new InteractionAction(IsOn ? TurnOffText : TurnOnText, this, _toggle));
        }

        public void Toggle()
        {
            _cooling.Toggle();
        }

        private void OnCoolingChanged(CoolingAppliance cooling)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            bool isOn = IsOn;
            if (_statusLight != null)
                _statusLight.SetActive(isOn);

            if (_airFlow != null)
            {
                _airFlow.gameObject.SetActive(isOn);
                _airFlow.localScale = _airFlowScale;
            }
        }
    }
}
