namespace WaterChaos.Player
{
    /// <summary>
    /// Item behavior that works only while the item is in the player's hands (nozzle, extinguisher...).
    /// Put it on the item object next to its PickableItem; PlayerEquipment calls it.
    /// </summary>
    public interface IEquippable
    {
        /// <summary>The item came into the hands: start listening to <paramref name="owner"/>'s input.</summary>
        void Equip(PlayerEquipment owner);

        /// <summary>The item left the hands: stop listening and stop anything it was doing.</summary>
        void Unequip();
    }
}
