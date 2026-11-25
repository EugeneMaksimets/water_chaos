using UnityEngine;

namespace WaterChaos.Rooms
{
    /// <summary>
    /// Put on the player: rooms report here when the player enters or leaves them,
    /// so the HUD knows which room's temperature to show.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomVisitor : MonoBehaviour
    {
        /// <summary>Room the visitor is in; null outside every room.</summary>
        public RoomTemperature CurrentRoom { get; private set; }

        internal void Enter(RoomTemperature room)
        {
            CurrentRoom = room;
        }

        internal void Leave(RoomTemperature room)
        {
            if (CurrentRoom == room)
                CurrentRoom = null;
        }
    }
}
