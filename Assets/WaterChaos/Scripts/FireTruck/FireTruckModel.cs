using System;
using WaterChaos.Hydrant;
using WaterChaos.Water;

namespace WaterChaos.FireTruck
{
    /// <summary>
    /// Pure model of a fire truck's water outlet: its pump always runs at full pressure behind a valve
    /// that opens smoothly, and every litre comes out of the truck's own tank.
    /// When the tank cannot give the full stream, flow and pressure drop in proportion; an empty tank gives nothing.
    /// </summary>
    public sealed class FireTruckModel
    {
        // The valve and the full-pressure stream behave like a hydrant set to its maximum.
        private readonly HydrantModel _valve;
        private readonly WaterReservoir _tank;

        /// <param name="maxPressure">Outlet pressure, in pascals (Pa).</param>
        /// <param name="maxFlowRate">Flow at that pressure with the valve fully open, in liters per second (L/s).</param>
        /// <param name="valveSpeed">How fast the valve moves, in open-ratio per second (1/s).</param>
        /// <param name="tank">The truck's water tank; null = no water.</param>
        public FireTruckModel(float maxPressure, float maxFlowRate, float valveSpeed, WaterReservoir tank)
        {
            _valve = new HydrantModel(maxPressure, maxFlowRate, valveSpeed);
            _tank = tank;
        }

        public bool IsOpen => _valve.IsOpen;

        /// <summary>Water delivered by the last <see cref="Tick"/>: flow in L/s, pressure in Pa.</summary>
        public WaterFlowData Output { get; private set; }

        public void Toggle() => _valve.Toggle();
        public void Close() => _valve.Close();

        /// <summary>Moves the valve and takes this step's water from the tank.</summary>
        /// <param name="deltaTime">Elapsed time in seconds (s); negative and NaN values are ignored.</param>
        public void Tick(float deltaTime)
        {
            if (!(deltaTime > 0f))
                return;

            _valve.Tick(deltaTime);
            WaterFlowData wanted = _valve.Output;
            float requested = wanted.FlowRate * deltaTime;
            if (_tank == null || requested <= 0f)
            {
                Output = new WaterFlowData(0f, 0f, _tank != null ? _tank.Liquid : LiquidType.Water);
                return;
            }

            float share = Math.Min(_tank.Draw(requested) / requested, 1f);
            Output = new WaterFlowData(wanted.FlowRate * share, wanted.Pressure * share, _tank.Liquid);
        }
    }
}
