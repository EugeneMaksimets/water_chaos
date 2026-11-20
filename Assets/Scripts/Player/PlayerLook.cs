using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterChaos.Player
{
    public class PlayerLook : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _sensitivity = 0.15f;
        [SerializeField] private float _maxLookAngle = 85f;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;

        private float _verticalRotation;

        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            if (Mouse.current == null)
                return;

            Look();
        }

        private void Look()
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();

            float mouseX = mouseDelta.x * _sensitivity;
            float mouseY = mouseDelta.y * _sensitivity;

            transform.Rotate(Vector3.up * mouseX);

            _verticalRotation -= mouseY;
            _verticalRotation = Mathf.Clamp(
                _verticalRotation,
                -_maxLookAngle,
                _maxLookAngle
            );

            _cameraTransform.localRotation =
                Quaternion.Euler(_verticalRotation, 0f, 0f);
        }
    }
}