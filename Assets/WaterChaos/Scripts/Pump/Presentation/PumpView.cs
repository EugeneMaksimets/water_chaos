using UnityEngine;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Visual and audio feedback of the pump: status light, vibration, pressure gauge, intake bubbles, motor sound.
    /// Only reads the pump; never decides whether water is available. Every reference is optional.
    /// </summary>
    public sealed class PumpView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("References")]
        [SerializeField] private PumpController _pump;

        [Header("Status Light")]
        [Tooltip("Small lamp on the pump; its color shows the state.")]
        [SerializeField] private Renderer _statusLight;
        [SerializeField] private Color _noWaterColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] private Color _disconnectedColor = new Color(1f, 0.75f, 0.1f);
        [SerializeField] private Color _readyColor = new Color(0.2f, 0.9f, 0.3f);
        [SerializeField] private Color _runningColor = new Color(0.2f, 0.7f, 1f);

        [Header("Vibration")]
        [Tooltip("Visual-only child (no collider) that shakes while running.")]
        [SerializeField] private Transform _vibrationTarget;

        [Tooltip("Shake amplitude at max pressure, in meters (m).")]
        [SerializeField, Min(0f)] private float _vibrationAmplitude = 0.01f;

        [Header("Pressure Gauge")]
        [Tooltip("Needle rotated around its local Z by the output pressure.")]
        [SerializeField] private Transform _gaugeNeedle;
        [SerializeField] private float _needleZeroAngle = 120f;
        [SerializeField] private float _needleMaxAngle = -120f;

        [Header("Effects")]
        [Tooltip("Bubbles at the intake; play while the pump draws water.")]
        [SerializeField] private ParticleSystem _intakeBubbles;

        [Header("Audio")]
        [Tooltip("Looping motor sound; pitch follows pressure.")]
        [SerializeField] private AudioSource _motorLoop;

        [Tooltip("Plays the start and stop clips.")]
        [SerializeField] private AudioSource _sfx;
        [SerializeField] private AudioClip _startClip;
        [SerializeField] private AudioClip _stopClip;
        [SerializeField, Min(0f)] private float _minPitch = 0.8f;
        [SerializeField, Min(0f)] private float _maxPitch = 1.3f;

        private MaterialPropertyBlock _lightBlock;
        private Vector3 _vibrationRest;
        private bool _started;
        private bool _wasRunning;

        private void Awake()
        {
            if (_pump == null)
                _pump = GetComponentInParent<PumpController>();

            _lightBlock = new MaterialPropertyBlock();
            if (_vibrationTarget != null)
                _vibrationRest = _vibrationTarget.localPosition;
        }

        private void Start()
        {
            _started = true;
            OnEnable();
        }

        // The pump model is created in the pump's Awake, so the first sync waits for Start.
        private void OnEnable()
        {
            if (!_started || _pump == null)
                return;

            _pump.StateChanged -= OnStateChanged;
            _pump.StateChanged += OnStateChanged;
            _wasRunning = _pump.State == PumpState.Running;
            ShowState(_pump.State);
        }

        private void OnDisable()
        {
            if (_pump != null)
                _pump.StateChanged -= OnStateChanged;

            if (_motorLoop != null)
                _motorLoop.Stop();
        }

        private void Update()
        {
            if (_pump == null)
                return;

            float pressureRatio = _pump.MaxPressure > 0f ? _pump.Output.Pressure / _pump.MaxPressure : 0f;

            if (_vibrationTarget != null)
                _vibrationTarget.localPosition = _vibrationRest + Random.insideUnitSphere * (_vibrationAmplitude * pressureRatio);

            if (_gaugeNeedle != null)
                _gaugeNeedle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_needleZeroAngle, _needleMaxAngle, pressureRatio));

            if (_motorLoop != null)
                _motorLoop.pitch = Mathf.Lerp(_minPitch, _maxPitch, pressureRatio);

            UpdateBubbles(_pump.Output.FlowRate > 0f);
        }

        private void OnStateChanged(PumpState state)
        {
            bool running = state == PumpState.Running;
            if (running != _wasRunning)
                PlayOneShot(running ? _startClip : _stopClip);

            _wasRunning = running;
            ShowState(state);
        }

        private void ShowState(PumpState state)
        {
            SetLightColor(GetColor(state));

            if (_motorLoop == null)
                return;

            if (state == PumpState.Running && !_motorLoop.isPlaying)
                _motorLoop.Play();
            else if (state != PumpState.Running && _motorLoop.isPlaying)
                _motorLoop.Stop();
        }

        private void UpdateBubbles(bool drawing)
        {
            if (_intakeBubbles == null || drawing == _intakeBubbles.isEmitting)
                return;

            if (drawing)
                _intakeBubbles.Play();
            else
                _intakeBubbles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void PlayOneShot(AudioClip clip)
        {
            if (clip != null && _sfx != null)
                _sfx.PlayOneShot(clip);
        }

        private Color GetColor(PumpState state)
        {
            switch (state)
            {
                case PumpState.Running: return _runningColor;
                case PumpState.Ready: return _readyColor;
                case PumpState.Disconnected: return _disconnectedColor;
                default: return _noWaterColor;
            }
        }

        private void SetLightColor(Color color)
        {
            if (_statusLight == null)
                return;

            _statusLight.GetPropertyBlock(_lightBlock);
            _lightBlock.SetColor(BaseColorId, color);
            _lightBlock.SetColor(EmissionColorId, color * 2f);
            _statusLight.SetPropertyBlock(_lightBlock);
        }
    }
}
