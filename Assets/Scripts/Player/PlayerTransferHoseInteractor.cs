using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Interaction;
using WaterChaos.Items;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Player
{
    /// <summary>
    /// Player side of the transfer hose, separate from the nozzle hose.
    /// Offers its actions in the interaction menu: the valid source actions (<see cref="PumpMenuRules"/>) for the pump
    /// or hydrant under the crosshair, "Pull Hose" on a placed end, "Place Hose" while an end is carried and
    /// "Connect Hose" while the carried end points at a free socket (a splitter inlet).
    /// While an end is carried, the player is held within the hose length.
    /// The pump has one outlet: the nozzle hose connecting to it takes the transfer hose off and drops it beside the player.
    /// Taking another item into the hands while carrying the end drops the hose as a coil where the end was.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-20)]
    public sealed class PlayerTransferHoseInteractor : MonoBehaviour, IInteractionProvider
    {
        private const int HitBufferSize = 16;

        private const string TurnOnText = "Turn On";
        private const string TurnOffText = "Turn Off";
        private const string ConnectText = "Connect Hose";
        private const string PickUpText = "Pick Up Hose";
        private const string PullText = "Pull Hose";
        private const string PlaceText = "Place Hose";
        private const string PlugText = "Connect Hose";

        [Header("References")]
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private PlayerEquipment _equipment;
        [SerializeField] private Transform _camera;

        [Tooltip("Where a carried hose end is held; a child of the camera. Empty = the camera.")]
        [SerializeField] private Transform _carryPoint;

        [Tooltip("The nozzle hose connector: its connecting to a pump takes the transfer hose off. Optional.")]
        [SerializeField] private PlayerHoseConnector _nozzleConnector;

        [Header("Settings")]
        [Tooltip("How far the player can reach a pump or a hose end, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 3f;

        [Tooltip("Layers the interact ray can hit. Exclude the Player layer.")]
        [SerializeField] private LayerMask _interactMask = ~0;

        [Tooltip("Free hose needed before the 'length limit' state clears, in meters (m).")]
        [SerializeField, Min(0f)] private float _releaseMargin = 0.3f;

        [Tooltip("How far from the player a coil is dropped (inventory full: in front; nozzle took the pump: to the right), in meters (m).")]
        [SerializeField, Min(0f)] private float _dropDistance = 1f;

        private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];
        private readonly List<PumpMenuAction> _menuActions = new List<PumpMenuAction>();

        private CharacterController _body;
        private TransferHose _heldHose;
        private TransferHose _carried;
        private TransferHose _lastHose;
        private TransferHose _awaitingStart;
        private Action[] _pumpActions;
        private Action _pullHose;
        private Action _placeHose;
        private Action _plugHose;

        /// <summary>The carried end was laid down successfully.</summary>
        public event Action HosePlaced;

        /// <summary>The carried end could not be laid down (no ground under it).</summary>
        public event Action PlaceFailed;

        /// <summary>Pump transfer outlet under the crosshair; null if none.</summary>
        public TransferConnectionPoint TargetPoint { get; private set; }

        /// <summary>Free socket (e.g. a splitter inlet) under the crosshair while an end is carried; null if none.</summary>
        public HoseEndSocket TargetSocket { get; private set; }

        /// <summary>Transfer hose whose placed end is under the crosshair; null if none.</summary>
        public TransferHose TargetEnd { get; private set; }

        /// <summary>Transfer hose whose free end is in the hands; null if none.</summary>
        public TransferHose Carried => _carried;

        /// <summary>The carried hose is at full length and holds the player back.</summary>
        public bool IsLimitReached { get; private set; }

        /// <summary>A hose was placed and its pump has not been started yet: the player should go back and start it.</summary>
        public bool IsAwaitingPumpStart => _awaitingStart != null;

        /// <summary>
        /// Hose the HUD reports on: the one on the outlet under the crosshair, the carried one,
        /// or the one last used; null if none is connected.
        /// </summary>
        public TransferHose ActiveHose
        {
            get
            {
                if (TargetPoint != null && TargetPoint.IsAttached)
                    return TargetPoint.AttachedHose;
                if (_carried != null)
                    return _carried;
                return _lastHose != null && _lastHose.Model.IsConnected ? _lastHose : null;
            }
        }

        private Transform CarryPoint => _carryPoint != null ? _carryPoint : _camera;

        /// <summary>Hands are empty or hold a transfer hose, so nothing else (e.g. the nozzle) reacts at the pump.</summary>
        private bool HandsFreeForTransfer => _heldHose != null || _equipment == null || _equipment.Current == null;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();

            // Indexed by PumpMenuAction; cached so collecting menu actions does not allocate.
            _pumpActions = new Action[]
            {
                () => Execute(PumpMenuAction.TurnOn),
                () => Execute(PumpMenuAction.TurnOff),
                () => Execute(PumpMenuAction.Connect),
                () => Execute(PumpMenuAction.PickUp)
            };
            _pullHose = PullHose;
            _placeHose = PlaceCarried;
            _plugHose = PlugCarried;

            if (_inventory == null || _camera == null)
            {
                Debug.LogError($"{name}: PlayerTransferHoseInteractor needs Inventory and Camera references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_nozzleConnector != null)
                _nozzleConnector.Connecting += OnNozzleConnecting;
        }

        private void OnDisable()
        {
            if (_nozzleConnector != null)
                _nozzleConnector.Connecting -= OnNozzleConnecting;

            ClearTarget();
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            // Hands empty or holding a transfer hose, so the pump menu does not compete with e.g. the nozzle.
            if (TargetPoint != null && HandsFreeForTransfer)
            {
                foreach (PumpMenuAction action in _menuActions)
                    actions.Add(new InteractionAction(GetLabel(action), TargetPoint, _pumpActions[(int)action]));
            }

            if (TargetEnd != null && _carried == null)
                actions.Add(new InteractionAction(PullText, TargetEnd, _pullHose));

            if (_carried != null && TargetSocket != null)
                actions.Add(new InteractionAction(PlugText, TargetSocket, _plugHose));

            if (_carried != null)
                actions.Add(new InteractionAction(PlaceText, null, _placeHose));
        }

        /// <summary>A transfer hose item came into the hands.</summary>
        public void HoldHose(TransferHose hose) => _heldHose = hose;

        /// <summary>That transfer hose item left the hands.</summary>
        public void ReleaseHose(TransferHose hose)
        {
            if (_heldHose == hose)
                _heldHose = null;
        }

        /// <summary>Performs a pump menu action on the pump under the crosshair. False if it is not valid now.</summary>
        private bool Execute(PumpMenuAction action)
        {
            if (TargetPoint == null || !_menuActions.Contains(action))
                return false;

            switch (action)
            {
                case PumpMenuAction.TurnOn:
                    TargetPoint.TurnOn();
                    return TargetPoint.IsTurnedOn;
                case PumpMenuAction.TurnOff:
                    TargetPoint.TurnOff();
                    return true;
                case PumpMenuAction.Connect:
                    return Connect(TargetPoint);
                case PumpMenuAction.PickUp:
                    return Disconnect(TargetPoint.AttachedHose);
                default:
                    return false;
            }
        }

        private void Update()
        {
            // Another item came into the hands (PlayerEquipment, last LateUpdate): the end stays where it was held.
            if (_carried != null && HoldsOtherItem(_carried))
                DropCoil(_carried, _carried.End.position);

            // The hose dropped its end itself (the pump left the world).
            if (_carried != null && _carried.State != TransferHoseState.Carried)
                SetCarried(null);

            UpdateAwaitingStart();
            FindTarget();
            RefreshMenuActions();
        }

        // After PlayerMovement (Update) and before the hose end follows the hands (TransferHose, -10).
        private void LateUpdate()
        {
            if (_carried == null)
            {
                IsLimitReached = false;
                return;
            }

            HoseModel path = _carried.Path;
            Vector3 offset = CarryPoint.position - path.LastFixedPoint;
            float free = path.FreeSegmentLength;

            // Only the horizontal part is held back, so gravity and jumps are never blocked.
            Vector2 horizontal = new Vector2(offset.x, offset.z);
            float limit = Mathf.Sqrt(Mathf.Max(0f, free * free - offset.y * offset.y));
            float excess = horizontal.magnitude - limit;

            if (excess > 0f)
            {
                Vector2 back = horizontal.normalized * excess;
                _body.Move(new Vector3(-back.x, 0f, -back.y));
                IsLimitReached = true;
            }
            else if (path.RemainingLength > _releaseMargin)
            {
                IsLimitReached = false;
            }
        }

        /// <summary>The instruction to start the pump ends once that pump runs or the hose is no longer laid out.</summary>
        private void UpdateAwaitingStart()
        {
            if (_awaitingStart == null)
                return;

            TransferConnectionPoint point = _awaitingStart.ConnectedPoint;
            if (_awaitingStart.State != TransferHoseState.Placed || point == null || point.IsTurnedOn)
                _awaitingStart = null;
        }

        /// <summary>Menu action: takes the placed end under the crosshair again.</summary>
        private void PullHose()
        {
            if (TargetEnd != null && _carried == null && TargetEnd.TryPickUpEnd(CarryPoint))
                SetCarried(TargetEnd);
        }

        /// <summary>Menu action: lays the carried end down where the player looks.</summary>
        private void PlaceCarried()
        {
            if (_carried == null)
                return;

            TransferHose hose = _carried;
            if (hose.TryPlace(_camera.forward))
            {
                SetCarried(null);
                // Only a pump needs to be started for the transfer; a hydrant just flows when opened.
                TransferConnectionPoint point = hose.ConnectedPoint;
                _awaitingStart = point != null && point.Pump != null && !point.IsTurnedOn ? hose : null;
                HosePlaced?.Invoke();
            }
            else
            {
                PlaceFailed?.Invoke();
            }
        }

        /// <summary>Menu action: plugs the carried end into the socket under the crosshair (a splitter inlet).</summary>
        private void PlugCarried()
        {
            if (_carried == null || TargetSocket == null)
                return;

            TransferHose hose = _carried;
            if (hose.TryConnectEnd(TargetSocket))
            {
                SetCarried(null);
                HosePlaced?.Invoke();
            }
        }

        private bool Connect(TransferConnectionPoint point)
        {
            TransferHose hose = _heldHose;
            if (hose == null || !hose.TryConnect(point, CarryPoint, transform))
                return false;

            // The hose leaves the inventory; it now lives in the world, attached to the pump.
            _inventory.Model.RemoveSelected();
            SetCarried(hose);
            return true;
        }

        private bool Disconnect(TransferHose hose)
        {
            if (hose == null || !hose.TryDisconnect())
                return false;

            if (_carried == hose)
                SetCarried(null);
            if (_awaitingStart == hose)
                _awaitingStart = null;

            PickableItem item = hose.Item;
            if (_inventory.Model.TryAdd(item))
                item.PickUp();
            else
                item.Release(transform.position + transform.forward * _dropDistance + Vector3.up * 0.5f, Vector3.zero);

            return true;
        }

        /// <summary>The nozzle hose is about to take the shared pump outlet: the transfer hose comes off first.</summary>
        private void OnNozzleConnecting(WaterConnectionPoint point)
        {
            TransferConnectionPoint transfer = point != null ? point.GetComponentInParent<TransferConnectionPoint>() : null;
            if (transfer == null || transfer.AttachedHose == null)
                return;

            Vector3 beside = _camera.position + transform.right * _dropDistance;
            DropCoil(transfer.AttachedHose, beside);
        }

        /// <summary>Disconnects the hose (the pump stops) and leaves it in the world as a coil near <paramref name="preferred"/>.</summary>
        private void DropCoil(TransferHose hose, Vector3 preferred)
        {
            if (!hose.TryDropCoil(preferred, _camera.position, _camera.forward))
                return;

            if (_carried == hose)
                SetCarried(null);
            if (_awaitingStart == hose)
                _awaitingStart = null;
        }

        /// <summary>The hands hold an item other than this hose (its own item is still in them right after connecting).</summary>
        private bool HoldsOtherItem(TransferHose hose)
        {
            return _equipment != null && _equipment.Current != null && _equipment.Current != (IPickableItem)hose.Item;
        }

        private void RefreshMenuActions()
        {
            TransferConnectionPoint point = TargetPoint;
            if (point == null)
            {
                _menuActions.Clear();
                return;
            }

            TransferHose hose = point.AttachedHose;
            bool destinationFull = false;
            if (hose != null && hose.TryGetComponent(out WaterTransferController transfer))
                destinationFull = transfer.Destination != null && transfer.Destination.IsFull;

            PumpMenuContext context = new PumpMenuContext(
                _heldHose != null,
                _carried != null,
                point.IsAttached,
                hose != null ? hose.State : TransferHoseState.Stored,
                point.IsTurnedOn,
                point.IsRegularOutletBusy,
                point.HasWater,
                destinationFull);

            PumpMenuRules.Collect(context, _menuActions);
        }

        /// <summary>
        /// Closest pump transfer outlet or placed hose end along the view ray.
        /// Triggers (water volumes) do not block it, so an end lying in a reservoir can be reached; solid objects do.
        /// </summary>
        private void FindTarget()
        {
            ClearTarget();

            Ray ray = new Ray(_camera.position, _camera.forward);
            int count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, _interactMask, QueryTriggerInteraction.Collide);

            float best = _maxDistance;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.distance > best)
                    continue;

                TransferConnectionPoint point = hit.collider.GetComponentInParent<TransferConnectionPoint>();
                TransferHose end = hit.collider.GetComponentInParent<TransferHose>();
                HoseEndSocket socket = FindSocket(hit.collider);

                if (point != null && point.isActiveAndEnabled)
                {
                    best = hit.distance;
                    TargetPoint = point;
                    TargetEnd = null;
                    TargetSocket = null;
                }
                else if (socket != null && _carried != null && socket.isActiveAndEnabled && !socket.IsAttached)
                {
                    best = hit.distance;
                    TargetPoint = null;
                    TargetEnd = null;
                    TargetSocket = socket;
                }
                else if (socket != null && socket.Plug is TransferHose plugged && plugged.State == TransferHoseState.Placed)
                {
                    // Aiming at a socket with a hose in it: that hose end can be pulled out.
                    best = hit.distance;
                    TargetPoint = null;
                    TargetEnd = plugged;
                    TargetSocket = null;
                }
                else if (end != null && end.State == TransferHoseState.Placed && hit.collider.transform.IsChildOf(end.End))
                {
                    best = hit.distance;
                    TargetPoint = null;
                    TargetEnd = end;
                    TargetSocket = null;
                }
                else if (!hit.collider.isTrigger)
                {
                    best = hit.distance;
                    TargetPoint = null;
                    TargetEnd = null;
                    TargetSocket = null;
                }
            }

        }

        /// <summary>The socket of the aimed part, or of the item it belongs to (aiming anywhere at a splitter finds its inlet).</summary>
        private static HoseEndSocket FindSocket(Collider collider)
        {
            HoseEndSocket socket = collider.GetComponentInParent<HoseEndSocket>();
            if (socket != null)
                return socket;

            Rigidbody item = collider.attachedRigidbody;
            return item != null ? item.GetComponentInChildren<HoseEndSocket>() : null;
        }

        private void ClearTarget()
        {
            TargetPoint = null;
            TargetEnd = null;
            TargetSocket = null;
        }

        private static string GetLabel(PumpMenuAction action)
        {
            switch (action)
            {
                case PumpMenuAction.TurnOn: return TurnOnText;
                case PumpMenuAction.TurnOff: return TurnOffText;
                case PumpMenuAction.Connect: return ConnectText;
                case PumpMenuAction.PickUp: return PickUpText;
                default: return action.ToString();
            }
        }

        private void SetCarried(TransferHose hose)
        {
            _carried = hose;
            if (hose != null)
                _lastHose = hose;
        }
    }
}
