using UnityEngine;
using WaterChaos.Pump;

namespace WaterChaos.Sounds
{
    /// <summary>
    /// Sound of a pump: the motor while it is switched on (higher with more pressure), a start and a stop sound.
    /// Follows the switch, not <see cref="PumpState.Running"/>: a pump turned on before its hose is laid already roars.
    /// </summary>
    [RequireComponent(typeof(PumpController))]
    public sealed class PumpSound : MonoBehaviour
    {
        private PumpController _pump;
        private SoundLoop _motor;
        private bool _wasOn;

        private void Awake()
        {
            _pump = GetComponent<PumpController>();
            _motor = new SoundLoop(gameObject, SoundId.PumpMotor, 0.3f);
        }

        private void OnEnable()
        {
            _wasOn = _pump.isTurnOn;
        }

        private void OnDisable()
        {
            _motor.Stop();
        }

        private void Update()
        {
            float load = _pump.MaxPressure > 0f ? _pump.CurrentPressure / _pump.MaxPressure : 0f;
            bool on = _pump.isTurnOn;
            _motor.Update(on, 1f, Mathf.Lerp(0.85f, 1.25f, load));

            if (on != _wasOn)
                Sound.Play(on ? SoundId.PumpStart : SoundId.PumpStop, transform.position);
            _wasOn = on;
        }
    }
}
