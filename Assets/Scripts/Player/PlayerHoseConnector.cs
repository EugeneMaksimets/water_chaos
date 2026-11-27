using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Interaction;
using WaterChaos.Water;
using WaterChaos.Sounds;

namespace WaterChaos.Player
{
    /// <summary>
    /// Offers "Connect Nozzle" in the interaction menu for a free connection point under the crosshair
    /// ("Disconnect Nozzle" on the attached one; connecting to another point switches to it).
    /// One hose per point: a point another hose holds is not offered.
    /// Works only while a hose end is in the hands (see <see cref="AttachNozzle"/>): a nozzle, or a splitter input
    /// ("Connect Splitter"); disabled otherwise.
    /// </summary>
    public class PlayerHoseConnector : MonoBehaviour, IInteractionProvider
    {
        private const string DefaultItemName = "ствол";

        [Header("References")]
        [SerializeField] private Transform _camera;

        [Tooltip("Right mouse button takes the hose off its source. Empty = taken from this object.")]
        [SerializeField] private PlayerInputHandler _input;

        [Header("Settings")]
        [Tooltip("How far the player can reach a connection point, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 3f;
        [SerializeField] private LayerMask _interactMask = ~0;

        private readonly List<WaterConnectionPoint> _itemPoints = new List<WaterConnectionPoint>();
        private IWaterFlowReceiver _stream;
        private string _connectText = "Під'єднати " + DefaultItemName;
        private string _disconnectText = "Від'єднати " + DefaultItemName;
        private HoseController _hose;
        private WaterConnectionPoint _connected;
        private WaterConnectionPoint _target;
        private Action _interactTarget;
        private Action<WaterConnectionPoint> _onAttachmentLost;
        private Action _onSecondary;
        private bool _disconnectLearned;

        /// <summary>The held hose is at full length and holds the player back; false without a hose.</summary>
        public event Action<bool> LimitReachedChanged;

        /// <summary>
        /// Raised right before the nozzle hose attaches to a point, so whatever else uses that pump outlet
        /// (the transfer hose) can be taken off first.
        /// </summary>
        public event Action<WaterConnectionPoint> Connecting;

        public WaterConnectionPoint Connected => _connected;

        /// <summary>Connection point under the crosshair that the menu action would act on; null if none.</summary>
        public WaterConnectionPoint Target => _target;

        /// <summary>True when the menu action on the target would detach the hose.</summary>
        public bool TargetIsConnected => _target != null && _target == _connected;

        /// <summary>Menu text of taking the hose off, e.g. "Від'єднати ствол".</summary>
        public string DisconnectText => _disconnectText;

        /// <summary>The hose is on a source and the player has never taken it off with the right mouse button yet.</summary>
        public bool ShowDisconnectHint => isActiveAndEnabled && _connected != null && !_disconnectLearned;

        /// <summary>The held hose is at full length; false without a hose.</summary>
        public bool IsLimitReached => _hose != null && _hose.IsLimitReached;

        private void Awake()
        {
            _interactTarget = InteractTarget;
            _onAttachmentLost = OnAttachmentLost;
            _onSecondary = OnSecondaryPressed;
            if (_input == null)
                _input = GetComponent<PlayerInputHandler>();

            // No nozzle in the hands yet.
            enabled = false;
        }

        private void OnEnable()
        {
            if (_input != null)
                _input.SecondaryPressed += _onSecondary;
        }

        private void OnDisable()
        {
            _target = null;
            if (_input != null)
                _input.SecondaryPressed -= _onSecondary;
        }

        /// <summary>Right mouse button: the hose comes off its source wherever the player looks.</summary>
        private void OnSecondaryPressed()
        {
            if (_connected == null)
                return;

            Disconnect();
            _disconnectLearned = true;
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            if (_target != null)
                actions.Add(new InteractionAction(TargetIsConnected ? _disconnectText : _connectText, _target, _interactTarget));
        }

        /// <summary>A hose end came into the hands: interact now connects what it feeds (a nozzle stream, a splitter) and its optional hose.</summary>
        /// <param name="kept">Point the item stayed connected to while it lay in the world (see <see cref="ReleaseNozzle"/>); null if none.</param>
        /// <param name="itemName">Name in the menu, e.g. "Nozzle" → "Connect Nozzle".</param>
        public void AttachNozzle(IWaterFlowReceiver stream, HoseController hose, WaterConnectionPoint kept = null,
            string itemName = DefaultItemName)
        {
            DetachNozzle();
            if (stream == null)
                return;

            _stream = stream;
            _hose = hose;
            string label = string.IsNullOrEmpty(itemName) ? DefaultItemName : itemName;
            _connectText = "Під'єднати " + label;
            _disconnectText = "Від'єднати " + label;
            if (_hose != null)
                _hose.LimitReachedChanged += OnLimitReachedChanged;

            enabled = true;

            if (kept == null)
                return;

            // The stream and hose are still attached to the kept point; only the player's side is restored.
            _connected = kept;
            _connected.AttachmentLost += _onAttachmentLost;

            // The source left the world while the nozzle lay there (the hose may already have let go): it comes off fully now.
            if (!kept.isActiveAndEnabled || !kept.IsAttached || (_hose != null && !_hose.IsConnected))
                Disconnect();
        }

        /// <summary>
        /// The nozzle was put down in the world: the source stops, but the hose stays attached to it.
        /// Returns the point to hand back to <see cref="AttachNozzle"/> when the nozzle is picked up again.
        /// </summary>
        public WaterConnectionPoint ReleaseNozzle()
        {
            WaterConnectionPoint kept = _connected;
            if (kept != null)
            {
                kept.AttachmentLost -= _onAttachmentLost;
                if (kept.Control != null)
                    kept.Control.Stop();
            }

            _connected = null;
            DetachNozzle();
            return kept;
        }

        /// <summary>The nozzle left the hands: the hose comes off, the source stops and interact does nothing.</summary>
        public void DetachNozzle()
        {
            if (_connected != null)
                Disconnect();

            if (_hose != null)
            {
                _hose.LimitReachedChanged -= OnLimitReachedChanged;
                _hose = null;
                LimitReachedChanged?.Invoke(false);
            }

            _stream = null;
            enabled = false;
        }

        /// <summary>
        /// This frame's player move, in meters (m), limited by the held hose; unchanged without a hose.
        /// </summary>
        public Vector3 ConstrainMove(Vector3 delta)
        {
            return _hose != null ? _hose.ConstrainMove(delta) : delta;
        }

        private void Update()
        {
            _target = FindTarget();
        }

        /// <summary>Menu action: acts on the point under the crosshair now, if there still is one.</summary>
        private void InteractTarget()
        {
            if (!isActiveAndEnabled)
                return;

            WaterConnectionPoint target = FindTarget();
            if (target != null)
                Interact(target);
        }

        private WaterConnectionPoint FindTarget()
        {
            Ray ray = new Ray(_camera.position, _camera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _interactMask, QueryTriggerInteraction.Collide))
                return null;

            WaterConnectionPoint point = hit.collider.GetComponentInParent<WaterConnectionPoint>();
            if (point == null)
                point = FindOnItem(hit);

            return IsUsable(point) ? point : null;
        }

        private bool IsUsable(WaterConnectionPoint point)
        {
            return point != null && point.Source != null && point.isActiveAndEnabled && point.IsFreeFor(_connected);
        }

        /// <summary>
        /// Aiming at the body of an item with several outlets (a splitter): the outlet this nozzle is already on,
        /// else the free outlet nearest to where the player aims.
        /// </summary>
        private WaterConnectionPoint FindOnItem(RaycastHit hit)
        {
            Rigidbody item = hit.collider.attachedRigidbody;
            if (item == null)
                return null;

            item.GetComponentsInChildren(true, _itemPoints);
            WaterConnectionPoint best = null;
            float bestDistance = float.MaxValue;
            foreach (WaterConnectionPoint candidate in _itemPoints)
            {
                if (candidate == _connected)
                {
                    best = candidate;
                    break;
                }

                float distance = (candidate.HoseAnchor.position - hit.point).sqrMagnitude;
                if (IsUsable(candidate) && distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            _itemPoints.Clear();
            return best;
        }

        private void Interact(WaterConnectionPoint point)
        {
            bool detachOnly = point == _connected;

            if (_connected != null)
                Disconnect();

            if (!detachOnly)
                Connect(point);
        }

        private void Connect(WaterConnectionPoint point)
        {
            Connecting?.Invoke(point);

            _connected = point;
            _connected.Attach();
            Sound.PlayUI(SoundId.HoseConnect);
            _connected.AttachmentLost += _onAttachmentLost;
            _stream.SetSource(point.Source);

            if (_hose != null)
                _hose.Connect(point);
        }

        private void Disconnect()
        {
            _connected.AttachmentLost -= _onAttachmentLost;
            if (_connected.Control != null)
                _connected.Control.Stop();

            _connected.Detach();
            _connected = null;
            _stream.SetSource(null);

            if (_hose != null)
                _hose.Disconnect();
        }

        /// <summary>The attached source left the world (e.g. the pump was picked up): the hose comes off at once.</summary>
        private void OnAttachmentLost(WaterConnectionPoint point)
        {
            if (point == _connected)
                Disconnect();
        }

        private void OnLimitReachedChanged(bool reached)
        {
            LimitReachedChanged?.Invoke(reached);
        }
    }
}
