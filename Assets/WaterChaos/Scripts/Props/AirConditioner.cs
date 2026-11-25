using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Props
{
    /// <summary>
    /// Wall air conditioner the player turns on and off from the interaction menu.
    /// When on: the louver opens, the status light shows and the cold-air streaks pulse.
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
        [SerializeField] private bool _startOn;

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
        private bool _isOn;
        private Action _toggle;

        public bool IsOn => _isOn;

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

            _toggle = Toggle;
            _isOn = _startOn;
            UpdateVisuals();
        }

        private void Update()
        {
            float target = _isOn ? _louverOpenAngle : 0f;
            if (!Mathf.Approximately(_louverAngle, target))
            {
                _louverAngle = Mathf.MoveTowards(_louverAngle, target, _louverSpeed * Time.deltaTime);
                _louver.localRotation = _louverClosedRotation * Quaternion.Euler(_louverAngle, 0f, 0f);
            }

            if (_isOn && _airFlow != null)
            {
                float pulse = 1f + _airPulse * Mathf.Sin(Time.time * _airPulseFrequency * 2f * Mathf.PI);
                _airFlow.localScale = _airFlowScale * pulse;
            }
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            if (!isActiveAndEnabled)
                return;

            actions.Add(new InteractionAction(_isOn ? TurnOffText : TurnOnText, this, _toggle));
        }

        public void Toggle()
        {
            _isOn = !_isOn;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_statusLight != null)
                _statusLight.SetActive(_isOn);

            if (_airFlow != null)
            {
                _airFlow.gameObject.SetActive(_isOn);
                _airFlow.localScale = _airFlowScale;
            }
        }
    }
}
