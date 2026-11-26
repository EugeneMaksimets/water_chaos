using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>Shows a story panel (the game pauses) and is done once the player closes it with Esc.</summary>
    public sealed class StoryStep : MissionStep
    {
        [Header("Story")]
        [Tooltip("Who speaks, shown above the text (e.g. \"Напарник\"). Empty = no name.")]
        [SerializeField] private string _speaker;

        [Tooltip("Story text (Ukrainian).")]
        [SerializeField, TextArea(3, 10)] private string _text;

        protected override void OnBegin()
        {
            Runner.Story.Show(_speaker, _text);
        }

        protected override void OnTick()
        {
            if (!Runner.Story.IsOpen)
                Complete();
        }
    }
}
