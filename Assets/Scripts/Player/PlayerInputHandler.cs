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
        /// <summary>E: attach or detach the hose.</summary>
        public event Action InteractPressed;

        /// <summary>F: turn the connected source on or off.</summary>
        public event Action TogglePressed;

        /// <summary>Mouse wheel: +1 = more pressure, -1 = less.</summary>
        public event Action<int> PressureScrolled;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
                return;

            if (keyboard.eKey.wasPressedThisFrame)
                InteractPressed?.Invoke();

            if (keyboard.fKey.wasPressedThisFrame)
                TogglePressed?.Invoke();

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                PressureScrolled?.Invoke(scroll > 0f ? 1 : -1);
        }
    }
}
