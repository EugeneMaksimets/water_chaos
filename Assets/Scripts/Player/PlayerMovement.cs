using UnityEngine;
using UnityEngine.InputSystem;
using WaterChaos.Characters;
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

        [Tooltip("Optional: makes the player fall when slipping on ice; the one on this object otherwise.")]
        [SerializeField] private CharacterKnockdown _knockdown;

        [Header("Ice")]
        [Tooltip("Chance (0..1) to slip and fall each time the player walks onto ice.")]
        [SerializeField, Range(0f, 1f)] private float _slipChance = 0.3f;

        private readonly Collider[] _ladderHits = new Collider[8];
        private IceSlipModel _ice;
        private CharacterController _characterController;
        private Vector3 _velocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _ice = new IceSlipModel(_slipChance);
            if (_knockdown == null)
                _knockdown = GetComponent<CharacterKnockdown>();
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
            CheckIceSlip(direction);

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

        /// <summary>Walking onto ice may make the player slip and fall (see <see cref="IceSlipModel"/>).</summary>
        private void CheckIceSlip(Vector3 direction)
        {
            bool onIce = _characterController.isGrounded && LiquidSurfaceController.TryGetIceAt(FeetPosition(), out _);
            bool moving = direction.sqrMagnitude > 0.01f;

            if (_ice.Step(onIce, moving, Random.value) && _knockdown != null)
                _knockdown.Slip(direction);
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