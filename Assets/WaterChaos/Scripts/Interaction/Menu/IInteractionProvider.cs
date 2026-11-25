using System.Collections.Generic;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Offers interaction actions to the menu. Implemented by player components (on the same object as
    /// <see cref="PlayerInteraction"/>, e.g. the hose connector) and by world objects under the crosshair.
    /// To give the player a new action, add it here; the menu shows it without any new UI.
    /// </summary>
    public interface IInteractionProvider
    {
        /// <summary>Adds the actions that are valid right now; adds nothing when none are.</summary>
        /// <param name="player">The interacting player; world objects can read player parts from it.</param>
        void CollectActions(PlayerInteraction player, List<InteractionAction> actions);
    }
}
