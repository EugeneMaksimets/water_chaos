using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>A pause between steps in game time (frozen while the game is paused).</summary>
    public sealed class WaitStep : MissionStep
    {
        [Header("Wait")]
        [Tooltip("Duration in seconds (s).")]
        [SerializeField, Min(0f)] private float _seconds = 2f;

        private float _elapsed;

        protected override void OnBegin()
        {
            _elapsed = 0f;
        }

        protected override void OnTick()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _seconds)
                Complete();
        }
    }
}
