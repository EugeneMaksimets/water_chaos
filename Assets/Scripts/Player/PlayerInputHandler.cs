using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterChaos.Player
{
    /// <summary>
    /// Single place that reads player input and turns presses into intent events.
    /// Buttons are Input System actions built from the bindings in <see cref="PlayerControls"/>.
    /// Gameplay components subscribe to these events instead of reading keys themselves.
    /// While <see cref="GameplayBlocked"/> (a menu is open) only Interact, Cancel and pointer events are sent.
    /// Primary (left mouse button) and Drop (F) are separate actions with separate events:
    /// only <see cref="DropPressed"/> may take an item out of the hands.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        private readonly InputAction[] _actions = new InputAction[PlayerControls.Count];

        /// <summary>Mouse cursor position in screen pixels, for menus that are picked with the mouse.</summary>
        public Vector2 PointerPosition { get; private set; }

        /// <summary>Set by an open menu: gameplay buttons, scroll and primary presses are not sent.</summary>
        public bool GameplayBlocked { get; set; }

        /// <summary>E: open or close the interaction menu.</summary>
        public event Action InteractPressed;

        /// <summary>Escape: close an open menu.</summary>
        public event Action CancelPressed;

        /// <summary>F went down: put the held item down. Once per press; holding F does not repeat it.</summary>
        public event Action DropPressed;

        /// <summary>Left mouse button went down for gameplay: perform the first interaction action. Never drops an item.</summary>
        public event Action PrimaryPressed;

        /// <summary>Right mouse button went down for gameplay: the secondary action (take the nozzle hose off).</summary>
        public event Action SecondaryPressed;

        /// <summary>Left mouse button went up, blocked or not: a click for menus.</summary>
        public event Action PointerReleased;

        /// <summary>Mouse wheel: +1 = more pressure, -1 = less.</summary>
        public event Action<int> PressureScrolled;

        /// <summary>1, 2, 3: select an inventory slot; the argument is the slot index from 0.</summary>
        public event Action<int> SlotSelected;

        private void Awake()
        {
            for (int i = 0; i < _actions.Length; i++)
            {
                var control = (PlayerControl)i;
                _actions[i] = new InputAction(control.ToString(), InputActionType.Button, PlayerControls.GetBindingPath(control));
            }
        }

        private void OnEnable()
        {
            foreach (InputAction action in _actions)
                action.Enable();
        }

        private void OnDisable()
        {
            foreach (InputAction action in _actions)
                action.Disable();
        }

        private void OnDestroy()
        {
            foreach (InputAction action in _actions)
                action.Dispose();
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
                PointerPosition = mouse.position.ReadValue();

            if (WasPressed(PlayerControl.Interact))
                InteractPressed?.Invoke();

            if (WasPressed(PlayerControl.Cancel))
                CancelPressed?.Invoke();

            if (Get(PlayerControl.Primary).WasReleasedThisFrame())
                PointerReleased?.Invoke();

            if (!GameplayBlocked)
                ReadGameplayButtons(mouse);
        }

        private void ReadGameplayButtons(Mouse mouse)
        {
            if (WasPressed(PlayerControl.Drop))
                DropPressed?.Invoke();

            if (WasPressed(PlayerControl.Slot1))
                SlotSelected?.Invoke(0);
            if (WasPressed(PlayerControl.Slot2))
                SlotSelected?.Invoke(1);
            if (WasPressed(PlayerControl.Slot3))
                SlotSelected?.Invoke(2);

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (scroll != 0f)
                    PressureScrolled?.Invoke(scroll > 0f ? 1 : -1);
            }

            if (WasPressed(PlayerControl.Primary))
                PrimaryPressed?.Invoke();

            if (WasPressed(PlayerControl.Secondary))
                SecondaryPressed?.Invoke();
        }

        private InputAction Get(PlayerControl control) => _actions[(int)control];

        private bool WasPressed(PlayerControl control) => Get(control).WasPressedThisFrame();
    }
}
