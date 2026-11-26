using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Rooms;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Put out every fire inside the zones (fires that spread there during the step count too).
    /// With a time limit, fires still burning when it runs out go out by themselves; the timer starts once,
    /// with the step, and runs in game time (it stops while the game is paused).
    /// On completion the listed rooms are settled: they stop heating and re-lighting fires, so the area stays safe.
    /// </summary>
    public sealed class ExtinguishFiresObjective : ObjectiveStep
    {
        [Header("Fires")]
        [Tooltip("A fire belongs to the objective if it is inside any of these zones.")]
        [SerializeField] private MissionZone[] _zones = new MissionZone[0];

        [Tooltip("Seconds (s) after which the remaining fires go out by themselves; 0 = no limit.")]
        [SerializeField, Min(0f)] private float _timeLimit;

        [Header("After Completion")]
        [Tooltip("Rooms switched off when the objective is done, so they never re-light its fires (the house).")]
        [SerializeField] private RoomTemperature[] _settleRooms = new RoomTemperature[0];

        private float _elapsed;
        private bool _timedOut;
        private int _burning;

        protected override string Progress => _burning > 0 ? $"осередків: {_burning}" : null;

        protected override void OnBegin()
        {
            _elapsed = 0f;
            _timedOut = false;
            if (_zones.Length == 0)
                Debug.LogError($"{name}: ExtinguishFiresObjective has no zones; it completes at once.", this);

            base.OnBegin();
        }

        protected override bool Evaluate()
        {
            if (_timeLimit > 0f && !_timedOut)
            {
                _elapsed += Time.deltaTime;
                if (_elapsed >= _timeLimit)
                {
                    _timedOut = true;
                    ExtinguishAll();
                }
            }

            _burning = CountBurning();
            return _burning == 0;
        }

        protected override void OnEnd(bool completed)
        {
            base.OnEnd(completed);
            if (!completed)
                return;

            // Rooms first: a switched-off room hands its fires back as outdoor fires and never re-lights them.
            foreach (RoomTemperature room in _settleRooms)
            {
                if (room != null)
                    room.enabled = false;
            }

            // Then the put-out fires themselves stop, so nothing brings them back.
            for (int i = FireExtinguishHandler.All.Count - 1; i >= 0; i--)
            {
                FireExtinguishHandler fire = FireExtinguishHandler.All[i];
                if (!fire.IsBurning && IsInZones(fire.transform.position))
                    fire.enabled = false;
            }
        }

        private void ExtinguishAll()
        {
            for (int i = FireExtinguishHandler.All.Count - 1; i >= 0; i--)
            {
                FireExtinguishHandler fire = FireExtinguishHandler.All[i];
                if (fire.IsBurning && IsInZones(fire.transform.position))
                    fire.Extinguish();
            }
        }

        private int CountBurning()
        {
            int count = 0;
            foreach (FireExtinguishHandler fire in FireExtinguishHandler.All)
            {
                if (fire.IsBurning && IsInZones(fire.transform.position))
                    count++;
            }

            return count;
        }

        private bool IsInZones(Vector3 position)
        {
            foreach (MissionZone zone in _zones)
            {
                if (zone != null && zone.Contains(position))
                    return true;
            }

            return false;
        }
    }
}
