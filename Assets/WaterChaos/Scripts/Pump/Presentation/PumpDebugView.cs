using UnityEngine;

namespace WaterChaos.Pump
{
    /// <summary>
    /// Temporary on-screen readout of pump state. IMGUI keeps it scene-setup free;
    /// replaced by the real minimal UI in slice 6.
    /// </summary>
    public sealed class PumpDebugView : MonoBehaviour
    {
        private const float PascalsPerKilopascal = 1000f;

        [SerializeField] private PumpController _pump;

        private void OnGUI()
        {
            if (_pump == null)
                return;

            string state = _pump.IsRunning ? "ON" : "OFF";
            float current = _pump.CurrentPressure / PascalsPerKilopascal;
            float target = _pump.TargetPressure / PascalsPerKilopascal;
            float max = _pump.MaxPressure / PascalsPerKilopascal;

            GUI.Label(new Rect(10f, 10f, 400f, 100f),
                $"Pump: {state}\n" +
                $"Current pressure: {current:F0} kPa\n" +
                $"Target pressure: {target:F0} kPa (limit {max:F0})\n" +
                $"Flow: {_pump.Output.FlowRate:F2} L/s");
        }
    }
}
