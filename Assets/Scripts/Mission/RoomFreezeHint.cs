using UnityEngine;
using WaterChaos.Rooms;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Once a room gets down to the freezing point, tells the player what changes there: water from the nozzle turns to
    /// snow (a weak extinguisher) and puddles freeze to slippery ice. Shown once, without stopping the mission.
    /// </summary>
    public sealed class RoomFreezeHint : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MissionRunner _runner;
        [SerializeField] private RoomTemperature _room;

        [Header("Hint")]
        [Tooltip("Shown when the room reaches this temperature, in degrees Celsius (°C).")]
        [SerializeField] private float _temperature;

        [SerializeField, TextArea(2, 5)] private string _text =
            "Температура впала до нуля! Вода з брандспойта тут перетворюється на сніг — він гасить вогонь значно слабше. "
            + "А калюжі замерзають у лід — на ньому можна посковзнутися!";

        [Tooltip("How long the hint stays, in seconds (s).")]
        [SerializeField, Min(1f)] private float _seconds = 9f;

        private bool _shown;

        private void Update()
        {
            if (_shown || _runner == null || _room == null || _runner.Hints == null || _room.Temperature > _temperature)
                return;

            _shown = true;
            _runner.Hints.ShowFor(_text, _seconds);
        }
    }
}
