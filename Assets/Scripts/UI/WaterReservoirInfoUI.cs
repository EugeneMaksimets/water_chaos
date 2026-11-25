using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Water;

namespace WaterChaos.UI
{
    /// <summary>
    /// Compact panel "WATER RESERVOIR / 750 L / 2000 L" for the reservoir under the crosshair.
    /// Only reads the reservoir; builds its own overlay canvas on start, so it only needs the detector.
    /// </summary>
    public sealed class WaterReservoirInfoUI : MonoBehaviour
    {
        private static readonly Vector2 PanelSize = new Vector2(300f, 84f);

        [Header("References")]
        [Tooltip("Gives the reservoir under the crosshair; the panel shows only while it has one.")]
        [SerializeField] private WaterReservoirDetector _detector;

        [Tooltip("Interaction menu; the panel hides while it is open, so they do not overlap. Optional.")]
        [SerializeField] private RadialInteractionMenu _menu;

        [Header("Layout")]
        [Tooltip("Panel center relative to the screen center, in reference pixels (1920x1080).")]
        [SerializeField] private Vector2 _panelPosition = new Vector2(0f, 150f);

        private GameObject _panel;
        private Text _value;
        private int _shownVolume = int.MinValue;
        private int _shownCapacity = int.MinValue;

        private void Awake()
        {
            if (_detector == null)
            {
                Debug.LogError($"{name}: WaterReservoirInfoUI needs a Detector reference.", this);
                enabled = false;
                return;
            }

            Transform canvas = HudWidgets.CreateCanvas(transform, "Reservoir Info Canvas", 4).transform;

            Image panel = HudWidgets.CreateImage("Reservoir Info", canvas, _panelPosition, PanelSize, null, HudWidgets.PanelColor);
            _panel = panel.gameObject;

            Text title = HudWidgets.CreateText("Title", panel.transform, new Vector2(0f, 18f), new Vector2(PanelSize.x, 28f), 18, TextAnchor.MiddleCenter);
            title.text = WaterReservoirInfoText.Title;
            title.fontStyle = FontStyle.Bold;

            _value = HudWidgets.CreateText("Value", panel.transform, new Vector2(0f, -14f), new Vector2(PanelSize.x, 34f), 26, TextAnchor.MiddleCenter);

            _panel.SetActive(false);
        }

        private void OnDisable()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }

        // LateUpdate: the detector picks its target in Update.
        private void LateUpdate()
        {
            WaterReservoirComponent target = _detector.Target;
            WaterReservoir reservoir = target != null ? target.Reservoir : null;

            bool visible = reservoir != null && (_menu == null || !_menu.IsOpen);
            if (_panel.activeSelf != visible)
                _panel.SetActive(visible);

            if (!visible)
                return;

            int volume = WaterReservoirInfoText.ToLiters(reservoir.Volume);
            int capacity = WaterReservoirInfoText.ToLiters(reservoir.Capacity);
            if (volume == _shownVolume && capacity == _shownCapacity)
                return;

            // Rebuild the string only when the rounded liters change.
            _shownVolume = volume;
            _shownCapacity = capacity;
            _value.text = WaterReservoirInfoText.FormatVolume(volume, capacity);
        }
    }
}
