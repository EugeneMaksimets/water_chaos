using System;
using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Player
{
    /// <summary>
    /// Offers "Connect Nozzle" in the interaction menu for a connection point under the crosshair
    /// ("Disconnect Nozzle" on the attached one; connecting to another point switches to it).
    /// Works only while a nozzle is in the hands (see <see cref="AttachNozzle"/>); disabled otherwise.
    /// </summary>
    public class PlayerHoseConnector : MonoBehaviour, IInteractionProvider
    {
        private const string ConnectText = "Connect Nozzle";
        private const string DisconnectText = "Disconnect Nozzle";

        [Header("References")]
        [SerializeField] private Transform _camera;

        [Header("Settings")]
        [Tooltip("How far the player can reach a connection point, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 3f;
        [SerializeField] private LayerMask _interactMask = ~0;

        private WaterStream _stream;
        private HoseController _hose;
        private WaterConnectionPoint _connected;
        private WaterConnectionPoint _target;
        private Action _interactTarget;

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

        /// <summary>The held hose is at full length; false without a hose.</summary>
        public bool IsLimitReached => _hose != null && _hose.IsLimitReached;

        private void Awake()
        {
            _interactTarget = InteractTarget;

            // No nozzle in the hands yet.
            enabled = false;
        }

        private void OnDisable()
        {
            _target = null;
        }

        public void CollectActions(PlayerInteraction player, List<InteractionAction> actions)
        {
            if (_target != null)
                actions.Add(new InteractionAction(TargetIsConnected ? DisconnectText : ConnectText, _target, _interactTarget));
        }

        /// <summary>A nozzle came into the hands: interact now connects its stream and optional hose.</summary>
        /// <param name="kept">Point the nozzle stayed connected to while it lay in the world (see <see cref="ReleaseNozzle"/>); null if none.</param>
        public void AttachNozzle(WaterStream stream, HoseController hose, WaterConnectionPoint kept = null)
        {
            DetachNozzle();
            if (stream == null)
                return;

            _stream = stream;
            _hose = hose;
            if (_hose != null)
                _hose.LimitReachedChanged += OnLimitReachedChanged;

            enabled = true;

            if (kept == null)
                return;

            // The stream and hose are still attached to the kept point; only the player's side is restored.
            _connected = kept;

            // The source left the world while the nozzle lay there (the hose may already have let go): it comes off fully now.
            if (!kept.isActiveAndEnabled || (_hose != null && !_hose.IsConnected))
                Disconnect();
        }

        /// <summary>
        /// The nozzle was put down in the world: the source stops, but the hose stays attached to it.
        /// Returns the point to hand back to <see cref="AttachNozzle"/> when the nozzle is picked up again.
        /// </summary>
        public WaterConnectionPoint ReleaseNozzle()
        {
            WaterConnectionPoint kept = _connected;
            if (kept != null && kept.Control != null)
                kept.Control.Stop();

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
            // The attached source left the world (e.g. the pump was picked up): the hose comes off.
            if (_connected != null && !_connected.isActiveAndEnabled)
                Disconnect();

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
            return point != null && point.Source != null ? point : null;
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
            _stream.SetSource(point.Source);

            if (_hose != null)
                _hose.Connect(point);
        }

        private void Disconnect()
        {
            if (_connected.Control != null)
                _connected.Control.Stop();

            _connected.Detach();
            _connected = null;
            _stream.SetSource(null);

            if (_hose != null)
                _hose.Disconnect();
        }

        private void OnLimitReachedChanged(bool reached)
        {
            LimitReachedChanged?.Invoke(reached);
        }
    }
}
