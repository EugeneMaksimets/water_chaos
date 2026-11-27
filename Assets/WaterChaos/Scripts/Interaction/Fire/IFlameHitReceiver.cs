using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Something that reacts to a burning flamethrower stream touching it (a teammate catches fire,
    /// the player's screen burns). Called every physics step the lit stream hits its collider or a child of it.
    /// </summary>
    public interface IFlameHitReceiver
    {
        /// <param name="point">Where the flame touches, in world space.</param>
        void ReceiveFlame(Vector3 point);
    }
}
