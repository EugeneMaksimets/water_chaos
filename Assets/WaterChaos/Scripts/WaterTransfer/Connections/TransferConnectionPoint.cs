using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Transfer side of the pump's single outlet. It has no connector of its own: the transfer hose starts at the
    /// same anchor as the nozzle hose (<see cref="WaterConnectionPoint.HoseAnchor"/>), and only one of them is attached at a time.
    /// Put it on the pump object that has the collider, so the player can aim at it.
    /// Holds at most one transfer hose; reads the pump through its public API only.
    /// </summary>
    public sealed class TransferConnectionPoint : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Pump this outlet belongs to. Empty = the PumpController on this object or its parents.")]
        [SerializeField] private PumpController _pump;

        [Header("Transfer")]
        [Tooltip("Flow the pump is set to when started for a transfer, in liters per second (L/s). Limited by the pump's max flow.")]
        [SerializeField, Min(0.1f)] private float _transferFlowRate = 10f;

        private WaterConnectionPoint _regularOutlet;
        private PumpDeployment _deployment;

        public PumpController Pump => _pump;

        /// <summary>Where the transfer hose starts: the pump outlet the nozzle hose uses; this transform if the pump has none.</summary>
        public Transform Anchor => _regularOutlet != null ? _regularOutlet.HoseAnchor : transform;

        /// <summary>Transfer hose attached here; null if none.</summary>
        public TransferHose AttachedHose { get; private set; }

        public bool IsAttached => AttachedHose != null;

        /// <summary>The pump's regular outlet has the nozzle hose attached; the pump output belongs to it.</summary>
        public bool IsRegularOutletBusy => _regularOutlet != null && _regularOutlet.IsAttached;

        /// <summary>The pump intake sits in a source that has run dry.</summary>
        public bool IsSourceEmpty => _deployment != null
            ? _deployment.Status == IntakeStatus.SourceEmpty
            : _pump.Reservoir != null && _pump.Reservoir.IsEmpty;

        /// <summary>The pump intake has usable water.</summary>
        public bool HasWater => _pump.Reservoir != null && !_pump.Reservoir.IsEmpty;

        private void Awake()
        {
            if (_pump == null)
                _pump = GetComponentInParent<PumpController>();

            if (_pump == null)
            {
                Debug.LogError($"{name}: TransferConnectionPoint needs a PumpController.", this);
                enabled = false;
                return;
            }

            _regularOutlet = _pump.GetComponentInChildren<WaterConnectionPoint>(true);
            _deployment = _pump.GetComponent<PumpDeployment>();
        }

        /// <summary>Attaches the hose here. False if the outlet is taken (transfer or nozzle hose) or off.</summary>
        public bool TryAttach(TransferHose hose)
        {
            if (hose == null || IsAttached || IsRegularOutletBusy || !isActiveAndEnabled)
                return false;

            AttachedHose = hose;
            return true;
        }

        /// <summary>
        /// Takes the hose off: the pump no longer delivers through this outlet and, unless the nozzle hose
        /// still uses it, stops, so no water is drawn with nowhere to go.
        /// </summary>
        public void Detach(TransferHose hose)
        {
            if (hose == null || AttachedHose != hose)
                return;

            AttachedHose = null;
            _pump.SetExternalOutletConnected(false);
            if (!IsRegularOutletBusy)
                _pump.Stop();
        }

        /// <summary>
        /// Starts the pump at the pressure that gives <see cref="_transferFlowRate"/>.
        /// The pump itself refuses to start without water at the intake or a delivering outlet.
        /// </summary>
        public void TurnOn()
        {
            _pump.SetTargetPressure(TransferRules.TargetPressureFor(_transferFlowRate, _pump.MaxFlowRate, _pump.MaxPressure));
            if (!_pump.isTurnOn)
                _pump.Toggle();
        }

        public void TurnOff()
        {
            if (_pump.isTurnOn)
                _pump.Stop();
        }

        /// <summary>Tells the pump whether the transfer hose takes its output now.</summary>
        public void SetDelivering(bool delivering) => _pump.SetExternalOutletConnected(delivering);
    }
}
