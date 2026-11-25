using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Runs the transfer while the hose is attached, every physics step after the pump has ticked:
    /// finds the reservoir at the hose end, settles the water the pump drew this step
    /// (into that reservoir, or out into the environment), and tells the pump whether to deliver next step.
    /// The pump is the only one that draws water; reservoirs own their volume.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class WaterTransferController : MonoBehaviour
    {
        private const int BufferSize = 8;

        [Header("References")]
        [SerializeField] private TransferHose _hose;

        [Tooltip("Open-end stream at the hose end (existing WaterStream). Gets the pump as source while discharging into the environment.")]
        [SerializeField] private WaterStream _discharge;

        [Header("Destination")]
        [Tooltip("Layers of WaterBody trigger volumes.")]
        [SerializeField] private LayerMask _waterMask = 1 << 4;

        [Tooltip("How close the hose end must be to a water volume to fill it, in meters (m).")]
        [SerializeField, Min(0.01f)] private float _detectRadius = 0.25f;

        private readonly Collider[] _overlaps = new Collider[BufferSize];

        private TransferConnectionPoint _point;
        private bool _delivering;

        /// <summary>Water body the hose end lies in; null if none.</summary>
        public WaterBody DestinationBody { get; private set; }

        public WaterReservoir Destination => DestinationBody != null ? DestinationBody.Reservoir : null;

        /// <summary>Pump the hose is attached to; null while stored.</summary>
        public PumpController Pump => _point != null ? _point.Pump : null;

        /// <summary>The hose takes the pump output this step.</summary>
        public bool IsDelivering => _delivering;

        /// <summary>Water moving through the hose, in liters per second (L/s); 0 when it does not deliver.</summary>
        public float FlowRate => _delivering && _point != null ? _point.Pump.Output.FlowRate : 0f;

        /// <summary>Where last step's water ended up.</summary>
        public TransferResult LastResult { get; private set; }

        public TransferStatus Status
        {
            get
            {
                TransferConnectionPoint point = _hose.ConnectedPoint;
                if (point == null)
                    return TransferStatus.NotConnected;

                WaterReservoir destination = Destination;
                return TransferRules.Evaluate(
                    _hose.State,
                    point.IsRegularOutletBusy,
                    point.IsSourceEmpty,
                    point.HasWater,
                    destination != null,
                    destination != null && destination.IsFull,
                    point.Pump.isTurnOn,
                    FlowRate);
            }
        }

        private void Awake()
        {
            if (_hose == null)
            {
                Debug.LogError($"{name}: WaterTransferController needs a Hose reference.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void FixedUpdate()
        {
            TransferConnectionPoint point = _hose.ConnectedPoint;
            if (point == null || point != _point)
            {
                Clear();
                _point = point;
                if (point == null)
                    return;
            }

            PumpController pump = point.Pump;
            DestinationBody = _hose.State == TransferHoseState.Placed ? FindBody(_hose.End.position) : null;
            WaterReservoir destination = Destination;
            bool outletBusy = point.IsRegularOutletBusy;

            // The pump ticked earlier this step and reports exactly what it drew and from where.
            // Not measured from the source volume: another transfer may fill the same reservoir in the same step.
            if (_delivering && !outletBusy)
                LastResult = WaterTransferSettlement.Settle(pump.DrawnSourceThisStep, destination, pump.DrawnThisStep);
            else
                LastResult = default;

            _delivering = TransferRules.CanDeliver(_hose.State, outletBusy, destination != null && destination.IsFull);
            point.SetDelivering(_delivering);

            if (_discharge != null)
                _discharge.SetSource(_delivering && destination == null ? pump : null);

        }

        private void Clear()
        {
            _delivering = false;
            _point = null;
            DestinationBody = null;
            LastResult = default;

            if (_discharge != null)
                _discharge.SetSource(null);
        }

        /// <summary>Of the water bodies near the point, the one whose surface is highest above it.</summary>
        private WaterBody FindBody(Vector3 point)
        {
            int count = Physics.OverlapSphereNonAlloc(point, _detectRadius, _overlaps, _waterMask, QueryTriggerInteraction.Collide);

            WaterBody best = null;
            float bestDepth = float.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                if (!_overlaps[i].TryGetComponent(out WaterBody body) || body.Reservoir == null)
                    continue;

                float depth = body.GetDepth(point);
                if (depth > bestDepth)
                {
                    best = body;
                    bestDepth = depth;
                }
            }

            return best;
        }
    }
}
