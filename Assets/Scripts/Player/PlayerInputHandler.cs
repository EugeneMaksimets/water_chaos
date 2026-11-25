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
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        private readonly InputAction[] _actions = new InputAction[PlayerControls.Count];
        private bool _primaryActive;

        /// <summary>Primary is held down for gameplay: aiming a throw. False after the press is consumed.</summary>
        public bool PrimaryHeld { get; private set; }

        /// <summary>Mouse cursor position in screen pixels, for menus that are picked with the mouse.</summary>
        public Vector2 PointerPosition { get; private set; }

        /// <summary>Set by an open menu: gameplay buttons, scroll and primary presses are not sent.</summary>
        public bool GameplayBlocked { get; set; }

        /// <summary>E: open or close the interaction menu.</summary>
        public event Action InteractPressed;

        /// <summary>Escape: close an open menu.</summary>
        public event Action CancelPressed;

        /// <summary>F: put the held item down.</summary>
        public event Action DropPressed;

        /// <summary>
        /// Left mouse button went down for gameplay. A subscriber that performs an action with it calls
        /// <see cref="ConsumePrimary"/>, so the same press does not also aim and throw.
        /// </summary>
        public event Action PrimaryPressed;

        /// <summary>Left mouse button went up after a gameplay press that nobody consumed: throw the selected item.</summary>
        public event Action PrimaryReleased;

        /// <summary>Left mouse button went up, blocked or not: a click for menus.</summary>
        public event Action PointerReleased;

        /// <summary>Mouse wheel: +1 = more pressure, -1 = less.</summary>
        public event Action<int> PressureScrolled;

        /// <summary>1, 2, 3: select an inventory slot; the argument is the slot index from 0.</summary>
        public event Action<int> SlotSelected;

        /// <summary>The current primary press has done its job: no hold or release follows it.</summary>
        public void ConsumePrimary()
        {
            _primaryActive = false;
            PrimaryHeld = false;
        }

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

            _primaryActive = false;
            PrimaryHeld = false;
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

            bool primaryReleased = Get(PlayerControl.Primary).WasReleasedThisFrame();
            if (primaryReleased)
                PointerReleased?.Invoke();

            if (GameplayBlocked)
            {
                // A press made while a menu is open never reaches gameplay, even if the menu closes before release.
                _primaryActive = false;
            }
            else
            {
                ReadGameplayButtons(mouse);
            }

            PrimaryHeld = _primaryActive && Get(PlayerControl.Primary).IsPressed();
            if (primaryReleased && _primaryActive)
            {
                _primaryActive = false;
                PrimaryReleased?.Invoke();
            }
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
            {
                _primaryActive = true;
                PrimaryPressed?.Invoke();
            }
        }

        private InputAction Get(PlayerControl control) => _actions[(int)control];

        private bool WasPressed(PlayerControl control) => Get(control).WasPressedThisFrame();
    }
}
