using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Pump
{
    // replaced by the real minimal UI

    public sealed class PumpDebugView : MonoBehaviour
    {
        private const float PascalsPerKilopascal = 1000f;

        [SerializeField] 
        private PumpController _pump;

        [SerializeField]
        private WaterReservoirComponent _reservoir;

        [Tooltip("Optional: shows the intake check of a deployable pump.")]
        [SerializeField]
        private PumpDeployment _deployment;

        private void OnGUI()
        {
            if (_pump == null)
                return;

            string state = _pump.isTurnOn ? "ON" : "OFF";
            float current = _pump.CurrentPressure / PascalsPerKilopascal;
            float target = _pump.TargetPressure / PascalsPerKilopascal;
            float max = _pump.MaxPressure / PascalsPerKilopascal;

            WaterReservoir source = _reservoir != null && _reservoir.Reservoir != null
                ? _reservoir.Reservoir
                : _pump.Reservoir;

            string intake = _deployment != null
                ? $"Intake: {_deployment.Status} ({_deployment.Submersion:F2} m)\n"
                : string.Empty;

            GUI.Label(new Rect(10f, 10f, 400f, 160f),
                $"Pump: {state} / {_pump.State}\n" +
                intake +
                $"Current pressure: {current:F0} kPa\n" +
                $"Target pressure: {target:F0} kPa (limit {max:F0})\n" +
                $"Flow: {_pump.Output.FlowRate:F2} L/s" +
                $"Flow: {_pump.Output.FlowRate:F2} L/s\n" +
                $"Supply: {_pump.SupplyRatio:P0}\n" +
                (source != null
                    ? $"Source: {source.Volume:F0} / {source.Capacity:F0} L ({source.FillRatio:P0})"
                    : "Source: none"));
        }
    }
}