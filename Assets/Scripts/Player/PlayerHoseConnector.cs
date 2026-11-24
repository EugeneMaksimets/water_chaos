using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Player
{
    /// <summary>
    /// Holding interact on a connection point attaches the hose to it.
    /// Holding it on the attached point detaches the hose; on another point switches to it.
    /// </summary>
    public class PlayerHoseConnector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private Transform _camera;
        [SerializeField] private WaterStream _stream;

        [Header("Settings")]
        [Tooltip("How far the player can reach a connection point, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 3f;
        [SerializeField] private LayerMask _interactMask = ~0;

        [Tooltip("How long interact must be held to attach or detach, in seconds (s).")]
        [SerializeField, Min(0.01f)] private float _holdDuration = 0.8f;

        private WaterConnectionPoint _connected;
        private WaterConnectionPoint _target;
        private float _holdTime;
        private bool _waitForRelease;

        public WaterConnectionPoint Connected => _connected;

        /// <summary>Connection point under the crosshair that interact would act on; null if none.</summary>
        public WaterConnectionPoint Target => _target;

        /// <summary>True when finishing the hold on the target would detach the hose.</summary>
        public bool TargetIsConnected => _target != null && _target == _connected;

        /// <summary>How far the current hold is, 0..1.</summary>
        public float HoldProgress => Mathf.Clamp01(_holdTime / _holdDuration);

        private void Update()
        {
            // The attached source left the world (e.g. the pump was picked up): the hose comes off.
            if (_connected != null && !_connected.isActiveAndEnabled)
                Disconnect();

            WaterConnectionPoint target = FindTarget();
            if (target != _target)
            {
                _target = target;
                _holdTime = 0f;
            }

            if (!_input.InteractHeld)
            {
                _waitForRelease = false;
                _holdTime = 0f;
                return;
            }

            // After an action the key must be released before the next hold starts.
            if (_waitForRelease || _target == null)
                return;

            _holdTime += Time.deltaTime;
            if (_holdTime < _holdDuration)
                return;

            _holdTime = 0f;
            _waitForRelease = true;
            Interact(_target);
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
            _connected = point;
            _stream.SetSource(point.Source);
        }

        private void Disconnect()
        {
            if (_connected.Control != null)
                _connected.Control.Stop();

            _connected = null;
            _stream.SetSource(null);
        }
    }
}
