using UnityEngine;
using UnityEngine.InputSystem;
using WaterChaos.Soap;

namespace WaterChaos.Player
{
    /// <summary>
    /// In a soap bubble the player floats where they steer: W/S forward and back, A/D left and right (along the view),
    /// so they can fly over a roof and land on it when the bubble pops. The bubble itself still rises and pops on its own.
    /// Normal movement is off meanwhile (the bubble locks it), so the two never fight.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerBubbleSteering : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SoapBubbleController _bubble;

        [Tooltip("The player's view: steering goes along it.")]
        [SerializeField] private Transform _camera;

        [Tooltip("Optional: the held hose stops the bubble from flying farther than it reaches.")]
        [SerializeField] private PlayerHoseConnector _hoseConnector;

        [Header("Steering")]
        [Tooltip("Floating speed, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _speed = 3f;

        private CharacterController _body;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            if (_bubble == null || _camera == null)
            {
                Debug.LogError($"{name}: PlayerBubbleSteering needs Bubble and Camera references.", this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (Keyboard.current == null || !_body.enabled || !IsFloating(_bubble.Phase))
                return;

            Vector2 input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (input == Vector2.zero)
                return;

            Vector3 direction = _camera.forward * input.y + _camera.right * input.x;
            direction.y = 0f;
            Vector3 step = direction.normalized * (_speed * Time.deltaTime);
            if (_hoseConnector != null)
                step = _hoseConnector.ConstrainMove(step);

            _body.Move(step);
        }

        /// <summary>The bubble is closed and carries the player (not while it forms, pops or falls).</summary>
        public static bool IsFloating(SoapBubblePhase phase)
        {
            return phase == SoapBubblePhase.Encapsulated || phase == SoapBubblePhase.Rising;
        }
    }
}
