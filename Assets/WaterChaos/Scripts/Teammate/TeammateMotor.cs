using UnityEngine;

namespace WaterChaos.Teammate
{
    /// <summary>
    /// Body of an AI teammate: keeps it on the ground with normal gravity through its <see cref="CharacterController"/>.
    /// Other systems (e.g. the soap bubble) pause it by disabling it; it starts falling from rest when re-enabled.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class TeammateMotor : MonoBehaviour
    {
        [Header("Gravity")]
        [Tooltip("Multiplier of Physics.gravity; 1 = the same gravity as everything else.")]
        [SerializeField, Min(0f)] private float _gravityScale = 1f;

        [Tooltip("Fastest fall, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _maxFallSpeed = 40f;

        [Tooltip("Small downward speed while grounded, so the character stays on slopes and steps, in m/s.")]
        [SerializeField, Min(0f)] private float _groundStick = 2f;

        private CharacterController _character;
        private float _verticalSpeed;

        /// <summary>Vertical speed, in meters per second (m/s); negative = falling.</summary>
        public float VerticalSpeed => _verticalSpeed;

        private void Awake()
        {
            _character = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            _verticalSpeed = 0f;
        }

        private void Update()
        {
            if (!_character.enabled)
                return;

            if (_character.isGrounded && _verticalSpeed <= 0f)
                _verticalSpeed = -_groundStick;
            else
                _verticalSpeed = Mathf.Max(_verticalSpeed + Physics.gravity.y * _gravityScale * Time.deltaTime, -_maxFallSpeed);

            _character.Move(Vector3.up * (_verticalSpeed * Time.deltaTime));
        }
    }
}
