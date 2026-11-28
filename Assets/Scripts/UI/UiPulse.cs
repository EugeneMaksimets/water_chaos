using UnityEngine;

namespace WaterChaos.UI
{
    /// <summary>
    /// Gently scales a UI element up and down so it catches the eye (e.g. mouse prompts).
    /// Runs on real time, so it keeps pulsing while the game is paused.
    /// </summary>
    public sealed class UiPulse : MonoBehaviour
    {
        [Tooltip("Extra size at the top of a pulse, as a share of the size (0.08 = 8 %).")]
        [SerializeField, Range(0f, 0.5f)] private float _amount = 0.08f;

        [Tooltip("Pulses per second (Hz).")]
        [SerializeField, Min(0f)] private float _frequency = 1.2f;

        private Vector3 _baseScale = Vector3.one;

        private void Awake()
        {
            _baseScale = transform.localScale;
        }

        private void OnDisable()
        {
            transform.localScale = _baseScale;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.unscaledTime * _frequency * 2f * Mathf.PI) * 0.5f + 0.5f;
            transform.localScale = _baseScale * (1f + _amount * wave);
        }
    }
}
