using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Props
{
    /// <summary>
    /// Fridge whose door the player opens and closes from the interaction menu.
    /// The door swings smoothly around its hinge (the door object's pivot, local Y axis).
    /// </summary>
    public class Fridge : MonoBehaviour, IInteractionProvider
    {
        private const string OpenText = "Open";
        private const string CloseText = "Close";

        [Header("References")]
        [Tooltip("Door object; its pivot sits on the hinge.")]
        [SerializeField] private Transform _door;

        [Tooltip("Optional light inside, on only while the door is open.")]
        [SerializeField] private Light _interiorLight;

        [Header("Settings")]
        [Tooltip("Door angle when fully open, in degrees around the hinge (local Y). Flip the sign if it opens inward.")]
        [SerializeField] private float _openAngle = -110f;

        [Tooltip("Door swing speed, in degrees per second.")]
        [SerializeField, Min(1f)] private float _swingSpeed = 240f;

        private Quaternion _closedRotation;
        private float _currentAngle;
        private bool _isOpen;
        private Action _toggle;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_door == null)
            {
                Debug.LogError($"{name}: Fridge needs a Door reference.", this);
                enabled = false;
                return;
            }

            _closedRotation = _door.localRotation;
            _toggle = Toggle;
            UpdateLight();
        }

        private void Update()
        {
            float target = _isOpen ? _openAngle : 0f;
            if (Mathf.Approximately(_currentAngle, target))
                return;

            _currentAngle = Mathf.MoveTowards(_currentAngle, target, _swingSpeed * Time.deltaTime);
            _door.localRotation = _closedRotation * Quaternion.Euler(0f, _currentAngle, 0f);
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            if (!isActiveAndEnabled)
                return;

            actions.Add(new InteractionAction(_isOpen ? CloseText : OpenText, this, _toggle));
        }

        public void Toggle()
        {
            _isOpen = !_isOpen;
            UpdateLight();
        }

        private void UpdateLight()
        {
            if (_interiorLight != null)
                _interiorLight.enabled = _isOpen;
        }
    }
}
