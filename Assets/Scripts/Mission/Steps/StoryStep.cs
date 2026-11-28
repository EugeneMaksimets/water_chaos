using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>Shows a story card (the game keeps running) and is done once its text is fully typed out.</summary>
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
            if (!Runner.Story.IsReading)
                Complete();
        }
    }
}
