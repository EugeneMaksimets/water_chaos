using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.FireTruck
{
    /// <summary>
    /// Fire truck as a water source: the nozzle hose attaches to its outlet (a <see cref="WaterConnectionPoint"/>
    /// in its children) and gets the truck's full pressure while its tank lasts.
    /// The tank size and the water in it are set on the <see cref="WaterReservoirComponent"/> next to it.
    /// </summary>
    [RequireComponent(typeof(WaterReservoirComponent))]
    public sealed class FireTruckController : MonoBehaviour, IWaterFlowSource, IWaterSourceControl
    {
        [Header("Pump")]
        [Tooltip("Outlet pressure, always the maximum, in pascals (Pa).")]
        [SerializeField, Min(0f)] private float _pressure = 500_000f;

        [Tooltip("Flow at that pressure with the valve fully open, in liters per second (L/s).")]
        [SerializeField, Min(0f)] private float _flowRate = 10f;

        [Header("Valve")]
        [Tooltip("Open-ratio per second (1/s): 2 = opens fully in 0.5 s.")]
        [SerializeField, Min(0.01f)] private float _valveSpeed = 2f;

        private WaterReservoirComponent _tank;
        private FireTruckModel _model;

        /// <summary>The truck's water tank; null before the tank is created.</summary>
        public WaterReservoir Reservoir => _tank != null ? _tank.Reservoir : null;
        public bool IsOpen => Model.IsOpen;
        public WaterFlowData Output => Model.Output;

        // Created on first use: the tank builds its reservoir in its own Awake.
        private FireTruckModel Model => _model ??= new FireTruckModel(_pressure, _flowRate, _valveSpeed, Reservoir);

        private void Awake()
        {
            _tank = GetComponent<WaterReservoirComponent>();
        }

        private void FixedUpdate()
        {
            Model.Tick(Time.fixedDeltaTime);
        }

        public void Toggle() => Model.Toggle();

        /// <summary>The truck always works at full pressure; the wheel does nothing.</summary>
        public void AdjustPressure(int direction)
        {
        }

        public void Stop() => Model.Close();
    }
}
