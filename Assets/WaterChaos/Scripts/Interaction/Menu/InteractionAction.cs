using System;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// One thing the player can do right now, as shown in the interaction menu.
    /// Offered only while it is valid; <see cref="Execute"/> still checks again, so a stale action does nothing.
    /// </summary>
    public readonly struct InteractionAction
    {
        private readonly Action _execute;

        /// <summary>Text in the menu, e.g. "Turn On". Actions with the same label are shown once.</summary>
        public string Label { get; }

        /// <summary>Object the action is about (its whole object names the menu); null for hands-only actions like placing a carried hose.</summary>
        public Component Target { get; }

        /// <summary>
        /// Shows the on-screen primary-button prompt while this is the first action. False hides only the prompt:
        /// the primary button and the menu still perform the action.
        /// </summary>
        public bool ShowPrompt { get; }

        /// <summary>Button that performs this action directly while the menu is closed; the menu can perform it too unless <see cref="InMenu"/> is false.</summary>
        public InteractionButton Button { get; }

        /// <summary>Listed in the radial menu. False for button-only actions (e.g. "Water On/Off" on the primary button).</summary>
        public bool InMenu { get; }

        /// <param name="label">Menu text; must not be empty.</param>
        /// <param name="target">Object the action is about, or null.</param>
        /// <param name="execute">Performs the action; cache the delegate so collecting actions does not allocate.</param>
        public InteractionAction(string label, Component target, Action execute)
            : this(label, target, execute, true)
        {
        }

        /// <param name="label">Menu text; must not be empty.</param>
        /// <param name="target">Object the action is about, or null.</param>
        /// <param name="execute">Performs the action; cache the delegate so collecting actions does not allocate.</param>
        /// <param name="showPrompt">False once the player has learned the action, see <see cref="ShowPrompt"/>.</param>
        /// <param name="button">Button that performs the action directly, see <see cref="Button"/>.</param>
        /// <param name="inMenu">False keeps the action out of the radial menu, see <see cref="InMenu"/>.</param>
        public InteractionAction(string label, Component target, Action execute, bool showPrompt,
            InteractionButton button = InteractionButton.Primary, bool inMenu = true)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("An interaction action needs a label.", nameof(label));

            Label = label;
            Target = target;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            ShowPrompt = showPrompt;
            Button = button;
            InMenu = inMenu;
        }

        public void Execute()
        {
            _execute();
        }
    }
}
