using UnityEngine;

namespace WaterChaos.UI
{
    /// <summary>
    /// Slow side-to-side sway of the menu camera around a point of interest, so the background feels alive.
    /// </summary>
    public sealed class MenuCameraDrift : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform _lookAt;

        [Header("Motion")]
        [Tooltip("Sideways swing, in metres.")]
        [SerializeField] private float _amplitude = 1.5f;

        [Tooltip("One full swing, in seconds.")]
        [SerializeField, Min(1f)] private float _period = 24f;

        private Vector3 _startPosition;
        private Vector3 _right;

        private void Start()
        {
            _startPosition = transform.position;
            _right = transform.right;
        }

        private void LateUpdate()
        {
            float swing = Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / _period);
            transform.position = _startPosition + _right * (swing * _amplitude);

            if (_lookAt != null)
                transform.rotation = Quaternion.LookRotation(_lookAt.position - transform.position);
        }
    }
}
