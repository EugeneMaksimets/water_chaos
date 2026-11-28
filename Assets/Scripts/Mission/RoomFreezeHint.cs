using UnityEngine;
using WaterChaos.Rooms;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Once a room gets down to the freezing point, tells the player what changes there: water from the nozzle turns to
    /// snow (a weak extinguisher) and puddles freeze to slippery ice. Shown once as a story card sliding in from the side; the game keeps running.
    /// </summary>
    public sealed class RoomFreezeHint : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MissionRunner _runner;
        [SerializeField] private RoomTemperature _room;

        [Header("Hint")]
        [Tooltip("Shown when the room reaches this temperature, in degrees Celsius (°C).")]
        [SerializeField] private float _temperature;

        [Tooltip("Who speaks, shown above the text. Empty = no name.")]
        [SerializeField] private string _speaker = "Підказка";

        [SerializeField, TextArea(2, 5)] private string _text =
            "Температура впала до нуля! Вода з брандспойта тут перетворюється на сніг — він гасить вогонь значно слабше. "
            + "А калюжі замерзають у лід — на ньому можна посковзнутися!";

        private bool _shown;

        private void Update()
        {
            if (_shown || _runner == null || _room == null || _runner.Story == null || _room.Temperature > _temperature)
                return;

            _shown = true;
            _runner.Story.Show(_speaker, _text);
        }
    }
}
