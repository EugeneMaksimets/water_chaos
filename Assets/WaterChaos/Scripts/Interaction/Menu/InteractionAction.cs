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

        /// <param name="label">Menu text; must not be empty.</param>
        /// <param name="target">Object the action is about, or null.</param>
        /// <param name="execute">Performs the action; cache the delegate so collecting actions does not allocate.</param>
        public InteractionAction(string label, Component target, Action execute)
        {
            if (string.IsNullOrEmpty(label))
                throw new ArgumentException("An interaction action needs a label.", nameof(label));

            Label = label;
            Target = target;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public void Execute()
        {
            _execute();
        }
    }
}
