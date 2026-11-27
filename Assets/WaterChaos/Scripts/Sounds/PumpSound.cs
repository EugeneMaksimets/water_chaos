using UnityEngine;
using WaterChaos.Pump;

namespace WaterChaos.Sounds
{
    /// <summary>Sound of a pump: the motor while it runs (higher with more pressure), a start and a stop sound.</summary>
    [RequireComponent(typeof(PumpController))]
    public sealed class PumpSound : MonoBehaviour
    {
        private PumpController _pump;
        private SoundLoop _motor;
        private PumpState _lastState;

        private void Awake()
        {
            _pump = GetComponent<PumpController>();
            _motor = new SoundLoop(gameObject, SoundId.PumpMotor, 0.3f);
        }

        private void OnEnable()
        {
            _lastState = _pump.State;
            _pump.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _pump.StateChanged -= OnStateChanged;
            _motor.Stop();
        }

        private void Update()
        {
            float load = _pump.MaxPressure > 0f ? _pump.CurrentPressure / _pump.MaxPressure : 0f;
            _motor.Update(_pump.State == PumpState.Running, 1f, Mathf.Lerp(0.85f, 1.25f, load));
        }

        private void OnStateChanged(PumpState state)
        {
            if (state == PumpState.Running && _lastState != PumpState.Running)
                Sound.Play(SoundId.PumpStart, transform.position);
            else if (_lastState == PumpState.Running && state != PumpState.Running)
                Sound.Play(SoundId.PumpStop, transform.position);
            _lastState = state;
        }
    }
}
