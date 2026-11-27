using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>
    /// A tip shown for a few seconds (e.g. "toxic water dissolves walls too"); the mission goes on at once,
    /// the next objective's own hint replaces it.
    /// </summary>
    public sealed class HintStep : MissionStep
    {
        [Header("Hint")]
        [Tooltip("Tip text (Ukrainian).")]
        [SerializeField, TextArea(1, 3)] private string _text;

        [Tooltip("How long the tip stays, in seconds (s).")]
        [SerializeField, Min(0.5f)] private float _seconds = 6f;

        protected override void OnBegin()
        {
            Runner.Hints.ShowFor(_text, _seconds);
        }

        protected override void OnTick()
        {
            Complete();
        }
    }
}
