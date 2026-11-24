using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Turns the nozzle lever with the stream pressure: closed at 0%, fully open at 100%.
    /// Display only: the pressure comes from WaterStream.
    /// </summary>
    public sealed class NozzleLeverView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private WaterStream _stream;

        [Tooltip("Lever mesh; its rotation in the prefab is the closed (0%) pose.")]
        [SerializeField] private Transform _lever;

        [Header("Motion")]
        [Tooltip("Local axis the lever turns around.")]
        [SerializeField] private Vector3 _axis = Vector3.right;

        [Tooltip("Lever angle at 100% pressure, in degrees. Negative turns the other way.")]
        [SerializeField] private float _openAngle = 45f;

        [Tooltip("How fast the lever follows the pressure, in degrees per second (deg/s).")]
        [SerializeField, Min(0f)] private float _speed = 180f;

        private Quaternion _closedRotation;
        private float _angle;

        private void Awake()
        {
            if (_stream == null || _lever == null)
            {
                Debug.LogError($"{name}: NozzleLeverView needs Stream and Lever references.", this);
                enabled = false;
                return;
            }

            _closedRotation = _lever.localRotation;
        }

        private void LateUpdate()
        {
            float target = _openAngle * _stream.PressureRatio;
            _angle = Mathf.MoveTowards(_angle, target, _speed * Time.deltaTime);
            _lever.localRotation = _closedRotation * Quaternion.AngleAxis(_angle, _axis);
        }
    }
}
