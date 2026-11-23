using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Player
{
    /// <summary>
    /// Interact attaches the hose to the connection point the player looks at; interact again detaches it.
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

        private WaterConnectionPoint _connected;

        public WaterConnectionPoint Connected => _connected;

        private void OnEnable() => _input.InteractPressed += OnInteractPressed;
        private void OnDisable() => _input.InteractPressed -= OnInteractPressed;

        private void OnInteractPressed()
        {
            if (_connected != null)
                Disconnect();
            else
                TryConnect();
        }

        private void TryConnect()
        {
            Ray ray = new Ray(_camera.position, _camera.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, _maxDistance, _interactMask, QueryTriggerInteraction.Collide))
                return;

            WaterConnectionPoint point = hit.collider.GetComponentInParent<WaterConnectionPoint>();
            if (point == null || point.Source == null)
                return;

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
