using UnityEngine;

namespace WaterChaos.Sounds
{
    /// <summary>Footsteps of a walking character: one step per stride on the ground, none in the air.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FootstepSound : MonoBehaviour
    {
        [Tooltip("Distance walked per step, in meters (m).")]
        [SerializeField, Min(0.2f)] private float _strideLength = 1.6f;

        private CharacterController _body;
        private Vector3 _lastPosition;
        private float _walked;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            _lastPosition = transform.position;
        }

        private void Update()
        {
            Vector3 position = transform.position;
            Vector3 moved = position - _lastPosition;
            _lastPosition = position;

            if (!_body.isGrounded)
                return;

            _walked += new Vector2(moved.x, moved.z).magnitude;
            if (_walked < _strideLength)
                return;

            _walked = 0f;
            Sound.Play(SoundId.Footstep, position + _body.center - Vector3.up * (_body.height * 0.5f));
        }
    }
}
