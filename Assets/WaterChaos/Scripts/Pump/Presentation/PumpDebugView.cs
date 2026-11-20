using UnityEngine;

namespace WaterChaos.Pump
{
    // replaced by the real minimal UI

    public sealed class PumpDebugView : MonoBehaviour
    {
        private const float PascalsPerKilopascal = 1000f;

        [SerializeField] private PumpController _pump;

        private void OnGUI()
        {
            if (_pump == null)
                return;

            string state = _pump.isTurnOn ? "ON" : "OFF";
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