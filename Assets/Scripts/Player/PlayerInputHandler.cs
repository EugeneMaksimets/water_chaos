using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterChaos.Player
{
    /// <summary>
    /// Single place that reads player devices and turns presses into intent events.
    /// Gameplay components subscribe to these events instead of reading keys themselves.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        /// <summary>E is held down this frame: attach or detach the hose (hold action).</summary>
        public bool InteractHeld { get; private set; }

        /// <summary>F: turn the connected source on or off.</summary>
        public event Action TogglePressed;

        /// <summary>Mouse wheel: +1 = more pressure, -1 = less.</summary>
        public event Action<int> PressureScrolled;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                InteractHeld = false;
                return;
            }

            InteractHeld = keyboard.eKey.isPressed;

            if (keyboard.fKey.wasPressedThisFrame)
                TogglePressed?.Invoke();

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                PressureScrolled?.Invoke(scroll > 0f ? 1 : -1);
        }
    }
}
