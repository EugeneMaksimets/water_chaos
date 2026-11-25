namespace WaterChaos.Interaction
{
    /// <summary>
    /// One-time hints for a connected water source: first "Water On/Off" on the primary button,
    /// then, after the first toggle, "the mouse wheel changes pressure" until the pressure is changed once.
    /// Pure C#: the player component reports what the player did.
    /// </summary>
    public sealed class SourceControlHints
    {
        private bool _toggled;
        private bool _pressureChanged;

        /// <summary>The player has never turned the water on or off.</summary>
        public bool ShowToggleHint => !_toggled;

        /// <summary>The player has toggled the water but never changed the pressure.</summary>
        public bool ShowPressureHint => _toggled && !_pressureChanged;

        public void NotifyToggled()
        {
            _toggled = true;
        }

        public void NotifyPressureChanged()
        {
            _pressureChanged = true;
        }
    }
}
