using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Stops the whole game (e.g. while a story panel is open) and resumes it exactly where it was.
    /// Time.timeScale = 0 freezes physics, fire, pumps, coroutines with scaled waits and timers on Time.deltaTime;
    /// the listed player components (input, look, movement) are switched off, because they read devices directly.
    /// UI that must keep animating uses Time.unscaledDeltaTime.
    /// </summary>
    public sealed class GamePause : MonoBehaviour
    {
        [Header("Frozen While Paused")]
        [Tooltip("Player components switched off while paused: PlayerInputHandler, PlayerLook, PlayerMovement...")]
        [SerializeField] private Behaviour[] _disableWhilePaused = new Behaviour[0];

        [Tooltip("Pauses all audio sources too.")]
        [SerializeField] private bool _pauseAudio = true;

        [Tooltip("Locks and hides the cursor again on resume (Esc in the Editor releases it).")]
        [SerializeField] private bool _lockCursorOnResume = true;

        private bool[] _wasEnabled;
        private float _timeScaleBefore = 1f;

        public bool IsPaused { get; private set; }

        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            _timeScaleBefore = Time.timeScale;
            Time.timeScale = 0f;

            if (_pauseAudio)
                AudioListener.pause = true;

            _wasEnabled = new bool[_disableWhilePaused.Length];
            for (int i = 0; i < _disableWhilePaused.Length; i++)
            {
                Behaviour part = _disableWhilePaused[i];
                if (part == null)
                    continue;

                _wasEnabled[i] = part.enabled;
                part.enabled = false;
            }
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            Time.timeScale = _timeScaleBefore;

            if (_pauseAudio)
                AudioListener.pause = false;

            for (int i = 0; i < _disableWhilePaused.Length; i++)
            {
                Behaviour part = _disableWhilePaused[i];
                if (part != null && _wasEnabled[i])
                    part.enabled = true;
            }

            if (_lockCursorOnResume)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        // Never leave the game frozen when this object goes away (scene change, exiting Play Mode).
        private void OnDisable()
        {
            Resume();
        }
    }
}
