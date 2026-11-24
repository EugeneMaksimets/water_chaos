using UnityEngine;

namespace WaterChaos.Items
{
    /// <summary>World object the player can put into the inventory and throw back out.</summary>
    public interface IPickableItem
    {
        /// <summary>Thumbnail for the inventory slot; null shows the empty look.</summary>
        Sprite Icon { get; }

        /// <summary>Mass in kilograms (kg); a throw's speed is impulse / mass.</summary>
        float Mass { get; }

        /// <summary>Removes the item from the world while it is in the inventory.</summary>
        void PickUp();

        /// <summary>
        /// Returns the item to the world at <paramref name="position"/> (world space, m)
        /// and pushes it with <paramref name="impulse"/> (N·s).
        /// </summary>
        void Release(Vector3 position, Vector3 impulse);
    }
}