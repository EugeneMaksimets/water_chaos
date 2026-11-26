using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Transfer side of a source's single outlet (pump or hydrant). It has no connector of its own: the transfer hose
    /// starts at the same anchor as the nozzle hose (<see cref="WaterConnectionPoint.HoseAnchor"/>), and only one of
    /// them is attached at a time. Put it on the source object that has the collider, so the player can aim at it.
    /// Holds at most one transfer hose; reads the source through its public API only.
    /// A pump draws its water from a reservoir and must be told the hose takes its output; a hydrant just flows.
    /// </summary>
    public sealed class TransferConnectionPoint : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Pump this outlet belongs to. Empty = the PumpController on this object or its parents; none on a hydrant.")]
        [SerializeField] private PumpController _pump;

        [Header("Transfer")]
        [Tooltip("Flow the pump is set to when started for a transfer, in liters per second (L/s). Limited by the pump's max flow.")]
        [SerializeField, Min(0.1f)] private float _transferFlowRate = 10f;

        private IWaterFlowSource _source;
        private IWaterSourceControl _control;
        private Transform _sourceTransform;
        private WaterConnectionPoint _regularOutlet;
        private PumpDeployment _deployment;

        /// <summary>Pump of this outlet; null on a hydrant.</summary>
        public PumpController Pump => _pump;

        /// <summary>What the hose carries from here (pump, hydrant).</summary>
        public IWaterFlowSource Source => _source;

        /// <summary>The source object; it never blocks its own hose.</summary>
        public Transform SourceTransform => _sourceTransform != null ? _sourceTransform : transform;

        /// <summary>Where the transfer hose starts: the outlet the nozzle hose uses; this transform if the source has none.</summary>
        public Transform Anchor => _regularOutlet != null ? _regularOutlet.HoseAnchor : transform;

        /// <summary>Transfer hose attached here; null if none.</summary>
        public TransferHose AttachedHose { get; private set; }

        public bool IsAttached => AttachedHose != null;

        /// <summary>The source's regular outlet has the nozzle hose attached; the output belongs to it.</summary>
        public bool IsRegularOutletBusy => _regularOutlet != null && _regularOutlet.IsAttached;

        /// <summary>The pump is on, or the hydrant valve is open.</summary>
        public bool IsTurnedOn => _pump != null ? _pump.isTurnOn : _control != null && _control.IsActive;

        /// <summary>The pump intake sits in a source that has run dry; never for a hydrant.</summary>
        public bool IsSourceEmpty => _pump != null && (_deployment != null
            ? _deployment.Status == IntakeStatus.SourceEmpty
            : _pump.Reservoir != null && _pump.Reservoir.IsEmpty);

        /// <summary>The pump intake has usable water; a hydrant always has.</summary>
        public bool HasWater => _pump == null || (_pump.Reservoir != null && !_pump.Reservoir.IsEmpty);

        /// <summary>Volume that left the source this physics step, in liters (L).</summary>
        public float DrawnThisStep => _pump != null
            ? _pump.DrawnThisStep
            : _source != null ? _source.Output.FlowRate * Time.fixedDeltaTime : 0f;

        /// <summary>Reservoir this step's water came from; null for a hydrant (the water network).</summary>
        public WaterReservoir DrawnSourceThisStep => _pump != null ? _pump.DrawnSourceThisStep : null;

        private void Awake()
        {
            if (_pump == null)
                _pump = GetComponentInParent<PumpController>();

            _source = _pump != null ? _pump : GetComponentInParent<IWaterFlowSource>();
            if (_source == null)
            {
                Debug.LogError($"{name}: TransferConnectionPoint needs a pump or another IWaterFlowSource.", this);
                enabled = false;
                return;
            }

            _control = _source as IWaterSourceControl;
            _sourceTransform = ((Component)_source).transform;
            _regularOutlet = _sourceTransform.GetComponentInChildren<WaterConnectionPoint>(true);
            _deployment = _pump != null ? _pump.GetComponent<PumpDeployment>() : null;
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
        /// Takes the hose off: the source no longer delivers through this outlet and, unless the nozzle hose
        /// still uses it, stops, so no water flows with nowhere to go.
        /// </summary>
        public void Detach(TransferHose hose)
        {
            if (hose == null || AttachedHose != hose)
                return;

            AttachedHose = null;
            if (_pump != null)
                _pump.SetExternalOutletConnected(false);
            if (!IsRegularOutletBusy && _control != null)
                _control.Stop();
        }

        /// <summary>
        /// Starts the source: a pump at the pressure that gives <see cref="_transferFlowRate"/>
        /// (it refuses without water or a delivering outlet), a hydrant by opening its valve.
        /// </summary>
        public void TurnOn()
        {
            if (_pump != null)
            {
                _pump.SetTargetPressure(TransferRules.TargetPressureFor(_transferFlowRate, _pump.MaxFlowRate, _pump.MaxPressure));
                if (!_pump.isTurnOn)
                    _pump.Toggle();
                return;
            }

            if (_control != null && !_control.IsActive)
                _control.Toggle();
        }

        public void TurnOff()
        {
            if (IsTurnedOn && _control != null)
                _control.Stop();
        }

        /// <summary>Tells a pump whether the transfer hose takes its output now; a hydrant needs no telling.</summary>
        public void SetDelivering(bool delivering)
        {
            if (_pump != null)
                _pump.SetExternalOutletConnected(delivering);
        }
    }
}
