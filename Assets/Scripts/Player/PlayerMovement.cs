using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterChaos.Player
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _sprintSpeed = 8f;
        [SerializeField] private float _jumpHeight = 1.5f;
        [SerializeField] private float _gravity = -9.81f;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;

        [Tooltip("Optional: stops moving farther while the held hose is fully extended.")]
        [SerializeField] private PlayerHoseConnector _hoseConnector;

        private CharacterController _characterController;
        private Vector3 _velocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            Move();
        }

        private void Move()
        {
            Vector2 input = Vector2.zero;

            if (Keyboard.current.wKey.isPressed)
                input.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                input.y -= 1f;

            if (Keyboard.current.aKey.isPressed)
                input.x -= 1f;

            if (Keyboard.current.dKey.isPressed)
                input.x += 1f;

            input = input.normalized;

            Vector3 direction = _cameraTransform.forward * input.y
                              + _cameraTransform.right * input.x;

            direction.y = 0f;
            direction.Normalize();

            float speed = Keyboard.current.leftShiftKey.isPressed
                ? _sprintSpeed
                : _moveSpeed;

            Vector3 movement = direction * speed;

            if (_characterController.isGrounded && _velocity.y < 0f)
            {
                _velocity.y = -2f;
            }

            if (_characterController.isGrounded &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                _velocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
            }

            _velocity.y += _gravity * Time.deltaTime;

            movement.y = _velocity.y;

            Vector3 step = movement * Time.deltaTime;
            if (_hoseConnector != null)
                step = _hoseConnector.ConstrainMove(step);

            _characterController.Move(step);
        }
    }
}