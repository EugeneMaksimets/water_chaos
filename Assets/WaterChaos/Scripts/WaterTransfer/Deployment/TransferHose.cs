using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Items;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Transfer hose item in the world: attaches its start to a pump transfer outlet,
    /// lets the player carry its free end, lays that end on the ground and keeps the path around obstacles.
    /// Independent of the nozzle hose; reuses only the pure path classes (<see cref="HoseModel"/>, <see cref="HosePathSolver"/>).
    /// </summary>
    [RequireComponent(typeof(PickableItem))]
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class TransferHose : MonoBehaviour
    {
        /// <summary>After the player's length check (-20), before the stream (0) and the hose look (10).</summary>
        public const int ExecutionOrder = -10;

        private const int GroundBufferSize = 8;

        [Header("References")]
        [Tooltip("Free (discharge) end: a child with a trigger collider the player aims at. Its forward is the discharge direction.")]
        [SerializeField] private Transform _end;

        [Tooltip("Coil look and collider, shown while the hose is an item; hidden while it is laid out.")]
        [SerializeField] private GameObject _coil;

        [Header("Length")]
        [Tooltip("Physical hose length, in meters (m).")]
        [SerializeField, Min(1f)] private float _maxLength = 10f;

        [Header("Wrapping")]
        [Tooltip("Layers the hose wraps around. Leave out Water and Player.")]
        [SerializeField] private LayerMask _obstacleMask = 1;

        [Tooltip("Gap between a wrap point and the obstacle surface, in meters (m).")]
        [SerializeField, Min(0.01f)] private float _surfaceOffset = 0.08f;

        [Tooltip("Wrap points the hose may hold at once.")]
        [SerializeField, Range(1, 64)] private int _maxWrapPoints = 32;

        [Header("Placement")]
        [Tooltip("Layers the free end can rest on. Leave out Water and Player.")]
        [SerializeField] private LayerMask _groundMask = 1;

        [Tooltip("How far below the hands the end looks for ground, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDropHeight = 3f;

        [Tooltip("Height of the end above the ground it rests on, in meters (m).")]
        [SerializeField, Min(0f)] private float _restHeight = 0.05f;

        [Header("Drop")]
        [Tooltip("Smallest horizontal distance from the player a dropped coil lands at, in meters (m).")]
        [SerializeField, Min(0.3f)] private float _dropClearance = 0.8f;

        [Tooltip("Radius kept free of other objects (pump, walls) around a dropped coil, in meters (m).")]
        [SerializeField, Min(0.05f)] private float _coilRadius = 0.25f;

        private readonly TransferHoseModel _model = new TransferHoseModel();
        private readonly RaycastHit[] _groundHits = new RaycastHit[GroundBufferSize];
        private readonly Collider[] _overlaps = new Collider[GroundBufferSize];

        private HoseModel _path;
        private HosePathSolver _solver;
        private PhysicsHoseObstacleQuery _query;
        private PickableItem _item;
        private Rigidbody _body;
        private Transform _carrier;

        public TransferHoseModel Model => _model;
        public TransferHoseState State => _model.State;
        public Transform End => _end;
        public PickableItem Item => _item;

        /// <summary>Current path and length; points from the pump connector to the free end.</summary>
        public HoseModel Path => _path;

        /// <summary>Pump outlet the hose start is attached to; null while stored.</summary>
        public TransferConnectionPoint ConnectedPoint { get; private set; }

        private void Awake()
        {
            _item = GetComponent<PickableItem>();
            _body = GetComponent<Rigidbody>();

            if (_end == null || _coil == null)
            {
                Debug.LogError($"{name}: TransferHose needs End and Coil references.", this);
                enabled = false;
                return;
            }

            _path = new HoseModel(_maxLength);
            _query = new PhysicsHoseObstacleQuery(_obstacleMask);
            _solver = new HosePathSolver(_query, _surfaceOffset, _maxWrapPoints);
            _end.gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (_model.IsConnected)
                Disconnect();
        }

        private void LateUpdate()
        {
            if (!_model.IsConnected)
                return;

            // The pump left the world (picked up): the hose cannot stay attached to nothing.
            if (ConnectedPoint == null || !ConnectedPoint.isActiveAndEnabled)
            {
                Disconnect();
                return;
            }

            if (_model.State == TransferHoseState.Carried && _carrier != null)
                _end.SetPositionAndRotation(_carrier.position, _carrier.rotation);

            _path.SetAnchor(ConnectedPoint.Anchor.position);
            _solver.Step(_path, _end.position);
        }

        /// <summary>
        /// Takes the hose out of the hands and attaches its start to the pump transfer outlet;
        /// the free end follows <paramref name="carrier"/> until it is placed.
        /// </summary>
        /// <param name="playerRoot">The carrying player; its colliders never block the hose.</param>
        public bool TryConnect(TransferConnectionPoint point, Transform carrier, Transform playerRoot)
        {
            if (!enabled || !_model.CanConnect || point == null || carrier == null || !point.TryAttach(this))
                return false;

            _item.Release(point.Anchor.position, Vector3.zero);
            SetLaidOut(true);

            ConnectedPoint = point;
            _carrier = carrier;
            _end.SetPositionAndRotation(carrier.position, carrier.rotation);

            // The pump never blocks its own hose.
            _query.SetIgnored(point.Pump.transform, playerRoot);
            _path.Reset(point.Anchor.position, _end.position);

            return _model.TryConnect();
        }

        /// <summary>
        /// Lays the carried end on the ground below it, facing <paramref name="forward"/>, never farther than the hose allows.
        /// Rejected when there is no ground under the end.
        /// </summary>
        public bool TryPlace(Vector3 forward)
        {
            if (!_model.CanPlace)
                return false;

            bool hasGround = FindGround(_end.position, out Vector3 ground);
            if (!TransferHosePlacement.TryResolve(_path.LastFixedPoint, _path.FreeSegmentLength, hasGround, ground, out Vector3 point))
                return false;

            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f)
                flat = Vector3.forward;

            _end.SetPositionAndRotation(point, Quaternion.LookRotation(flat.normalized, Vector3.up));
            _carrier = null;
            return _model.TryPlace();
        }

        /// <summary>The player takes the placed end again; it follows <paramref name="carrier"/>.</summary>
        public bool TryPickUpEnd(Transform carrier)
        {
            if (carrier == null || !_model.TryPickUpEnd())
                return false;

            _carrier = carrier;
            return true;
        }

        /// <summary>
        /// Detaches the hose from the pump (which stops delivering) and turns it back into a coil at its current place.
        /// The caller decides where the coil goes (inventory or world).
        /// </summary>
        public bool TryDisconnect()
        {
            if (!_model.CanDisconnect)
                return false;

            Disconnect();
            return true;
        }

        /// <summary>
        /// Detaches the hose from the pump (which stops delivering) and lays it on the ground as a coil item
        /// at <paramref name="preferred"/>, or beside the player if that spot is unsafe. It can be picked up again.
        /// </summary>
        /// <param name="viewPoint">The player's eyes: the coil is kept clear of them and never dropped behind a wall.</param>
        /// <param name="forward">The player's facing; it orders the fallback spots and turns the coil.</param>
        public bool TryDropCoil(Vector3 preferred, Vector3 viewPoint, Vector3 forward)
        {
            if (!_model.CanDisconnect)
                return false;

            Vector3 spot = FindDropSpot(preferred, viewPoint, forward);

            Disconnect();
            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            _item.Release(spot, Vector3.zero);
            return true;
        }

        private void Disconnect()
        {
            if (ConnectedPoint != null)
                ConnectedPoint.Detach(this);

            ConnectedPoint = null;
            _carrier = null;
            SetLaidOut(false);
            _model.TryDisconnect();
        }

        /// <summary>Laid out: a fixed line with a free end. Not laid out: a coil item with normal physics.</summary>
        private void SetLaidOut(bool laidOut)
        {
            if (laidOut)
                _body.isKinematic = true;

            _coil.SetActive(!laidOut);
            _end.gameObject.SetActive(laidOut);
        }

        /// <summary>
        /// First spot (see <see cref="TransferHoseDrop"/>) that is visible from the player, has ground under it and room
        /// for the coil; the preferred spot kept clear of the player if none has.
        /// </summary>
        private Vector3 FindDropSpot(Vector3 preferred, Vector3 viewPoint, Vector3 forward)
        {
            for (int i = 0; i < TransferHoseDrop.CandidateCount; i++)
            {
                Vector3 candidate = TransferHoseDrop.GetCandidate(i, viewPoint, preferred, forward, _dropClearance);
                if (Physics.Linecast(viewPoint, candidate, _groundMask, QueryTriggerInteraction.Ignore))
                    continue;

                if (FindGround(candidate, out Vector3 ground) && HasRoom(ground))
                    return ground;
            }

            return TransferHoseDrop.GetCandidate(0, viewPoint, preferred, forward, _dropClearance);
        }

        /// <summary>No solid object (pump, wall, another item) overlaps a coil resting at <paramref name="ground"/>.</summary>
        private bool HasRoom(Vector3 ground)
        {
            Vector3 center = ground + Vector3.up * (_coilRadius + _restHeight);
            int count = Physics.OverlapSphereNonAlloc(center, _coilRadius, _overlaps, _groundMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!_overlaps[i].transform.IsChildOf(transform))
                    return false;
            }

            return true;
        }

        /// <summary>First solid surface under the point; triggers such as water volumes are skipped.</summary>
        private bool FindGround(Vector3 from, out Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(from, Vector3.down, _groundHits, _maxDropHeight, _groundMask, QueryTriggerInteraction.Ignore);

            point = from;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _groundHits[i];
                if (hit.distance >= best || hit.collider.transform.IsChildOf(transform))
                    continue;

                best = hit.distance;
                point = hit.point + Vector3.up * _restHeight;
            }

            return best < float.MaxValue;
        }
    }
}
