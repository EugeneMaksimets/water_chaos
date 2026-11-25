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
        /// <summary>Left mouse button is held down this frame: aiming a throw.</summary>
        public bool ThrowHeld { get; private set; }

        /// <summary>Mouse cursor position in screen pixels, for menus that are picked with the mouse.</summary>
        public Vector2 PointerPosition { get; private set; }

        /// <summary>E was pressed this frame: open or close the interaction menu.</summary>
        public event Action InteractPressed;

        /// <summary>Escape was pressed this frame: close an open menu.</summary>
        public event Action CancelPressed;

        /// <summary>F: turn the connected source on or off.</summary>
        public event Action TogglePressed;

        /// <summary>Mouse wheel: +1 = more pressure, -1 = less.</summary>
        public event Action<int> PressureScrolled;

        /// <summary>1, 2, 3: select an inventory slot; the argument is the slot index from 0.</summary>
        public event Action<int> SlotSelected;

        /// <summary>Left mouse button was released this frame: throw the selected item.</summary>
        public event Action ThrowReleased;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                ThrowHeld = false;
                return;
            }

            PointerPosition = mouse.position.ReadValue();

            if (keyboard.eKey.wasPressedThisFrame)
                InteractPressed?.Invoke();

            if (keyboard.escapeKey.wasPressedThisFrame)
                CancelPressed?.Invoke();

            if (keyboard.fKey.wasPressedThisFrame)
                TogglePressed?.Invoke();

            if (keyboard.digit1Key.wasPressedThisFrame)
                SlotSelected?.Invoke(0);
            if (keyboard.digit2Key.wasPressedThisFrame)
                SlotSelected?.Invoke(1);
            if (keyboard.digit3Key.wasPressedThisFrame)
                SlotSelected?.Invoke(2);

            ThrowHeld = mouse.leftButton.isPressed;
            if (mouse.leftButton.wasReleasedThisFrame)
                ThrowReleased?.Invoke();

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                PressureScrolled?.Invoke(scroll > 0f ? 1 : -1);
        }
    }
}
