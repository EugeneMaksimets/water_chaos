using UnityEngine;
using UnityEngine.InputSystem;
using WaterChaos.Environment;
using WaterChaos.Surfaces;

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

        [Header("Ice")]
        [Tooltip("How fast input speeds up a slide on ice, in meters per second squared (m/s²).")]
        [SerializeField, Range(0f, 30f)] private float _iceAcceleration = 6f;

        [Tooltip("Fastest slide on ice, in meters per second (m/s).")]
        [SerializeField, Range(0f, 20f)] private float _iceMaxSpeed = 9f;

        [Tooltip("Braking on normal ground, in m/s²; on ice it is scaled by the ice friction, so slides run out slowly.")]
        [SerializeField, Range(0f, 100f)] private float _iceGroundDeceleration = 20f;

        [Tooltip("How fast a slide turns toward where the player faces, in degrees per second (°/s).")]
        [SerializeField, Range(0f, 720f)] private float _iceTurnSpeed = 120f;

        private readonly Collider[] _ladderHits = new Collider[8];
        private IceSlideModel _ice;
        // Horizontal velocity of the last frame, carried onto ice (m/s).
        private Vector3 _lastHorizontal;
        private CharacterController _characterController;
        private Vector3 _velocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _ice = new IceSlideModel(new IceSlideConfig(_iceAcceleration, _iceMaxSpeed, _iceGroundDeceleration, _iceTurnSpeed));
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

            if (TryGetLadder(out Ladder ladder))
            {
                _ice.Exit();
                Climb(input, ladder);
                return;
            }

            Vector3 direction = _cameraTransform.forward * input.y
                              + _cameraTransform.right * input.x;

            direction.y = 0f;
            direction.Normalize();

            float speed = Keyboard.current.leftShiftKey.isPressed
                ? _sprintSpeed
                : _moveSpeed;

            Vector3 movement = direction * speed;
            movement = ApplyIce(movement, direction);
            _lastHorizontal = new Vector3(movement.x, 0f, movement.z);

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

        /// <summary>
        /// On ice the slide (<see cref="IceSlideModel"/>) replaces the horizontal movement: it heads where the camera
        /// faces and slowly runs out without input. Off the ice the normal movement comes back at once.
        /// In the air the slide keeps its momentum. The movement settings above are never changed.
        /// </summary>
        private Vector3 ApplyIce(Vector3 movement, Vector3 direction)
        {
            bool grounded = _characterController.isGrounded;
            if (!grounded)
                return _ice.IsSliding ? _ice.Velocity : movement;

            if (!LiquidSurfaceController.TryGetIceAt(FeetPosition(), out float friction))
            {
                if (_ice.IsSliding)
                    _ice.Exit();
                return movement;
            }

            if (!_ice.IsSliding)
                _ice.Enter(_lastHorizontal);

            Vector3 facing = _cameraTransform.forward;
            facing.y = 0f;
            return _ice.Step(direction, facing, friction, Time.deltaTime);
        }

        private Vector3 FeetPosition()
        {
            return transform.TransformPoint(_characterController.center) - Vector3.up * (_characterController.height * 0.5f);
        }

        /// <summary>On a ladder: no gravity, forward/back climbs (see <see cref="LadderMotion"/>).</summary>
        private void Climb(Vector2 input, Ladder ladder)
        {
            _velocity = Vector3.zero;
            Vector3 step = LadderMotion.Velocity(input, _cameraTransform.forward, _cameraTransform.right,
                ladder.ClimbSpeed, ladder.MoveSpeed) * Time.deltaTime;

            if (_hoseConnector != null)
                step = _hoseConnector.ConstrainMove(step);

            _characterController.Move(step);
        }

        /// <summary>The ladder whose climb zone the player's body is in, if any.</summary>
        private bool TryGetLadder(out Ladder ladder)
        {
            Vector3 center = transform.TransformPoint(_characterController.center);
            float half = Mathf.Max(0f, _characterController.height * 0.5f - _characterController.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * half, center - Vector3.up * half,
                _characterController.radius, _ladderHits, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                if (_ladderHits[i].TryGetComponent(out ladder))
                    return true;
            }

            ladder = null;
            return false;
        }
    }
}