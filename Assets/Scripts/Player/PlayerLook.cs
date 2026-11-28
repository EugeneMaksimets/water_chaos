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

        /// <summary>
        /// Turns the view toward a point at most <paramref name="maxDegrees"/> this call (e.g. while stunned);
        /// works while this component is switched off and keeps its pitch, so mouse look goes on from there.
        /// </summary>
        public void TurnTowards(Vector3 point, float maxDegrees)
        {
            Vector3 toPoint = point - _cameraTransform.position;
            Vector3 flat = new Vector3(toPoint.x, 0f, toPoint.z);
            if (flat.sqrMagnitude < 0.0001f)
                return;

            Quaternion yaw = Quaternion.LookRotation(flat, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, yaw, maxDegrees);

            float pitch = -Mathf.Atan2(toPoint.y, flat.magnitude) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(pitch, -_maxLookAngle, _maxLookAngle);
            _verticalRotation = Mathf.MoveTowards(_verticalRotation, pitch, maxDegrees);
            _cameraTransform.localRotation = Quaternion.Euler(_verticalRotation, 0f, 0f);
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