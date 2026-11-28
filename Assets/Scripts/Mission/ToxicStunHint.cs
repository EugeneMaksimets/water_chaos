using UnityEngine;
using WaterChaos.Characters;

namespace WaterChaos.Mission
{
    /// <summary>
    /// The first time anyone is stunned by a toxic stream, explains it with a story card sliding in from the side.
    /// Shown once; the game keeps running.
    /// </summary>
    public sealed class ToxicStunHint : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MissionRunner _runner;

        [Header("Hint")]
        [Tooltip("Who speaks, shown above the text. Empty = no name.")]
        [SerializeField] private string _speaker = "Підказка";

        [SerializeField, TextArea(2, 5)] private string _text =
            "Токсична вода паралізує! Той, у кого влучили, на 5 секунд втрачає керування, над ним здіймається зелена хмарка, "
            + "а сам він розвертається й цілиться в того, хто в нього стріляв. Не поливай напарників токсином — і не стій під їхнім струменем!";

        private bool _shown;

        private void OnEnable()
        {
            ToxicStun.AnyStunned += OnStunned;
        }

        private void OnDisable()
        {
            ToxicStun.AnyStunned -= OnStunned;
        }

        private void OnStunned(ToxicStun target)
        {
            if (_shown || _runner == null || _runner.Story == null)
                return;

            _shown = true;
            _runner.Story.Show(_speaker, _text);
        }
    }
}
