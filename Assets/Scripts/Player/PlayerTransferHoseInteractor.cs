using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Items;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Player
{
    /// <summary>
    /// Player side of the transfer hose, separate from the nozzle hose.
    /// Pump under the crosshair: offers the valid pump menu actions (<see cref="MenuActions"/>) and performs the chosen one.
    /// F lays the carried end down; holding interact on a placed end takes it again.
    /// While an end is carried, the player is held within the hose length.
    /// F is left to the nozzle system while the nozzle hose is connected somewhere.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(-20)]
    public sealed class PlayerTransferHoseInteractor : MonoBehaviour
    {
        private const int HitBufferSize = 16;

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerInventory _inventory;
        [SerializeField] private PlayerEquipment _equipment;
        [SerializeField] private Transform _camera;

        [Tooltip("Where a carried hose end is held; a child of the camera. Empty = the camera.")]
        [SerializeField] private Transform _carryPoint;

        [Tooltip("The nozzle hose connector, read only: while it has a connection, F belongs to it. Optional.")]
        [SerializeField] private PlayerHoseConnector _nozzleConnector;

        [Header("Settings")]
        [Tooltip("How far the player can reach a pump or a hose end, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 3f;

        [Tooltip("Layers the interact ray can hit. Exclude the Player layer.")]
        [SerializeField] private LayerMask _interactMask = ~0;

        [Tooltip("How long interact must be held to take a placed hose end, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _holdDuration = 0.8f;

        [Tooltip("Free hose needed before the 'length limit' state clears, in meters (m).")]
        [SerializeField, Min(0f)] private float _releaseMargin = 0.3f;

        [Tooltip("Where a coil is dropped when the inventory is full, in meters (m) in front of the player.")]
        [SerializeField, Min(0f)] private float _dropDistance = 1f;

        private readonly RaycastHit[] _hits = new RaycastHit[HitBufferSize];
        private readonly List<PumpMenuAction> _menuActions = new List<PumpMenuAction>();

        private CharacterController _body;
        private TransferHose _heldHose;
        private TransferHose _carried;
        private TransferHose _lastHose;
        private TransferHose _awaitingStart;
        private UnityEngine.Object _targetObject;
        private float _holdTime;
        private bool _waitForRelease;
        private bool _menuOpen;

        /// <summary>The carried end was laid down successfully.</summary>
        public event Action HosePlaced;

        /// <summary>The carried end could not be laid down (no ground under it).</summary>
        public event Action PlaceFailed;

        /// <summary>Pump transfer outlet under the crosshair; null if none.</summary>
        public TransferConnectionPoint TargetPoint { get; private set; }

        /// <summary>Transfer hose whose placed end is under the crosshair; null if none.</summary>
        public TransferHose TargetEnd { get; private set; }

        /// <summary>What finishing the hold on the placed end under the crosshair would do.</summary>
        public TransferHoseAction TargetAction { get; private set; }

        /// <summary>How far the current hold is, 0..1.</summary>
        public float HoldProgress => Mathf.Clamp01(_holdTime / _holdDuration);

        /// <summary>Valid actions for the pump under the crosshair, in menu order; empty without one. Do not modify.</summary>
        public IReadOnlyList<PumpMenuAction> MenuActions => _menuActions;

        /// <summary>The pump menu can open: a pump is under the crosshair, the hands allow it and something can be done.</summary>
        public bool CanOpenMenu => TargetPoint != null && HandsFreeForTransfer && _menuActions.Count > 0;

        /// <summary>Transfer hose whose free end is in the hands; null if none.</summary>
        public TransferHose Carried => _carried;

        /// <summary>F would lay the carried end down.</summary>
        public bool CanPlace => _carried != null && !_menuOpen && !NozzleOwnsF;

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

        private bool NozzleOwnsF => _nozzleConnector != null && _nozzleConnector.Connected != null;

        private Transform CarryPoint => _carryPoint != null ? _carryPoint : _camera;

        /// <summary>Hands are empty or hold a transfer hose, so nothing else (e.g. the nozzle) reacts at the pump.</summary>
        private bool HandsFreeForTransfer => _heldHose != null || _equipment == null || _equipment.Current == null;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            if (_input == null || _inventory == null || _camera == null)
            {
                Debug.LogError($"{name}: PlayerTransferHoseInteractor needs Input, Inventory and Camera references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_input != null)
                _input.TogglePressed += OnTogglePressed;
        }

        private void OnDisable()
        {
            if (_input != null)
                _input.TogglePressed -= OnTogglePressed;

            ClearTarget();
            _holdTime = 0f;
            _waitForRelease = false;
        }

        /// <summary>A transfer hose item came into the hands.</summary>
        public void HoldHose(TransferHose hose) => _heldHose = hose;

        /// <summary>That transfer hose item left the hands.</summary>
        public void ReleaseHose(TransferHose hose)
        {
            if (_heldHose == hose)
                _heldHose = null;
        }

        /// <summary>The pump menu is open: F and the hose-end hold are ignored meanwhile.</summary>
        public void SetMenuOpen(bool open) => _menuOpen = open;

        /// <summary>Performs a pump menu action on the pump under the crosshair. False if it is not valid now.</summary>
        public bool Execute(PumpMenuAction action)
        {
            if (TargetPoint == null || !_menuActions.Contains(action))
                return false;

            switch (action)
            {
                case PumpMenuAction.TurnOn:
                    TargetPoint.TurnOn();
                    return TargetPoint.Pump.isTurnOn;
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
            // The hose dropped its end itself (the pump left the world).
            if (_carried != null && _carried.State != TransferHoseState.Carried)
                SetCarried(null);

            UpdateAwaitingStart();
            FindTarget();
            RefreshMenuActions();
            UpdateEndHold();
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
            if (_awaitingStart.State != TransferHoseState.Placed || point == null || point.Pump.isTurnOn)
                _awaitingStart = null;
        }

        private void UpdateEndHold()
        {
            if (!_input.InteractHeld || _menuOpen)
            {
                _waitForRelease = false;
                _holdTime = 0f;
                return;
            }

            if (_waitForRelease || TargetAction != TransferHoseAction.PullHose)
                return;

            _holdTime += Time.deltaTime;
            if (_holdTime < _holdDuration)
                return;

            _holdTime = 0f;
            _waitForRelease = true;
            if (TargetEnd.TryPickUpEnd(CarryPoint))
                SetCarried(TargetEnd);
        }

        private void OnTogglePressed()
        {
            if (!CanPlace)
                return;

            TransferHose hose = _carried;
            if (hose.TryPlace(_camera.forward))
            {
                SetCarried(null);
                _awaitingStart = hose.ConnectedPoint != null && !hose.ConnectedPoint.Pump.isTurnOn ? hose : null;
                HosePlaced?.Invoke();
            }
            else
            {
                PlaceFailed?.Invoke();
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
                point.Pump.isTurnOn,
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

                if (point != null && point.isActiveAndEnabled)
                {
                    best = hit.distance;
                    TargetPoint = point;
                    TargetEnd = null;
                }
                else if (end != null && end.State == TransferHoseState.Placed && hit.collider.transform.IsChildOf(end.End))
                {
                    best = hit.distance;
                    TargetPoint = null;
                    TargetEnd = end;
                }
                else if (!hit.collider.isTrigger)
                {
                    best = hit.distance;
                    TargetPoint = null;
                    TargetEnd = null;
                }
            }

            if (TargetEnd != null)
                TargetAction = _carried != null ? TransferHoseAction.PlaceCarriedFirst : TransferHoseAction.PullHose;

            if (TargetPoint != _targetObject && TargetEnd != _targetObject)
            {
                _targetObject = TargetPoint != null ? TargetPoint : TargetEnd;
                _holdTime = 0f;
            }
        }

        private void ClearTarget()
        {
            TargetPoint = null;
            TargetEnd = null;
            TargetAction = TransferHoseAction.None;
        }

        private void SetCarried(TransferHose hose)
        {
            _carried = hose;
            if (hose != null)
                _lastHose = hose;
        }
    }
}
