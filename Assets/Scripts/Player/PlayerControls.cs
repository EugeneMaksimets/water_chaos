using UnityEngine.InputSystem;

namespace WaterChaos.Player
{
    /// <summary>Player buttons read by <see cref="PlayerInputHandler"/>.</summary>
    public enum PlayerControl
    {
        /// <summary>Pick up the item under the crosshair; otherwise open or close the interaction menu.</summary>
        Interact,

        /// <summary>Close an open menu.</summary>
        Cancel,

        /// <summary>Main action: the first primary-button interaction action (Connect, Water On/Off...). Never picks up or drops an item.</summary>
        Primary,

        /// <summary>Put the held item down in front of the player. The only control that takes an item out of the hands.</summary>
        Drop,

        /// <summary>Secondary action (right mouse button): take the nozzle hose off its source.</summary>
        Secondary,

        Slot1,
        Slot2,
        Slot3,
    }

    /// <summary>
    /// The single source of player button bindings (Input System control paths).
    /// The input handler builds its actions from these paths and prompts show the same paths,
    /// so a hint always names the button that really does the action.
    /// </summary>
    public static class PlayerControls
    {
        public const int Count = (int)PlayerControl.Slot3 + 1;

        private const string MousePrefix = "<Mouse>/";
        private const string LeftButton = "<Mouse>/leftButton";
        private const string RightButton = "<Mouse>/rightButton";

        public static string GetBindingPath(PlayerControl control)
        {
            switch (control)
            {
                case PlayerControl.Interact: return "<Keyboard>/e";
                case PlayerControl.Cancel: return "<Keyboard>/escape";
                case PlayerControl.Primary: return LeftButton;
                case PlayerControl.Drop: return "<Keyboard>/f";
                case PlayerControl.Secondary: return RightButton;
                case PlayerControl.Slot1: return "<Keyboard>/1";
                case PlayerControl.Slot2: return "<Keyboard>/2";
                case PlayerControl.Slot3: return "<Keyboard>/3";
                default: return string.Empty;
            }
        }

        /// <summary>Short readable name of the binding: "F", "E", "LMB".</summary>
        public static string GetDisplayName(PlayerControl control)
        {
            return InputControlPath.ToHumanReadableString(
                GetBindingPath(control),
                InputControlPath.HumanReadableStringOptions.OmitDevice | InputControlPath.HumanReadableStringOptions.UseShortNames);
        }

        /// <summary>True if the control is a mouse button; <paramref name="isLeft"/>/<paramref name="isRight"/> tell which one.</summary>
        public static bool IsMouseButton(PlayerControl control, out bool isLeft, out bool isRight)
        {
            string path = GetBindingPath(control);
            isLeft = path == LeftButton;
            isRight = path == RightButton;
            return path.StartsWith(MousePrefix);
        }
    }
}
