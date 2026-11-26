using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Splitter
{
    /// <summary>
    /// A hose laid before the game starts: from the start it is plugged into a <see cref="HoseEndSocket"/>
    /// (e.g. the splitter inlet) and feeds it from a fixed source (e.g. the hydrant).
    /// <see cref="Disconnect"/> takes it off, so the player can plug their own hose into that socket.
    /// </summary>
    public sealed class FixedHoseFeed : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Source the hose comes from; must implement IWaterFlowSource (e.g. HydrantController).")]
        [SerializeField] private MonoBehaviour _source;

        [Tooltip("Socket the hose end is plugged into (e.g. the HoseSplitter inlet).")]
        [SerializeField] private HoseEndSocket _socket;

        [Tooltip("Optional: the hose look; hidden when the feed is taken off.")]
        [SerializeField] private GameObject _visual;

        /// <summary>The hose is still plugged into the socket.</summary>
        public bool IsConnected => _socket != null && _socket.Plug == this;

        // Start: the socket's receiver (the splitter) is ready by then.
        private void Start()
        {
            IWaterFlowSource source = _source as IWaterFlowSource;
            if (source == null || _socket == null)
            {
                Debug.LogError($"{name}: FixedHoseFeed needs a Source (IWaterFlowSource) and a Socket.", this);
                enabled = false;
                return;
            }

            if (!_socket.TryAttach(this))
            {
                Debug.LogWarning($"{name}: socket {_socket.name} is taken or out of the world; the feed is not connected.", this);
                return;
            }

            _socket.Feed(source);
        }

        /// <summary>Takes the hose off the socket; it feeds nothing any more and the socket is free.</summary>
        public void Disconnect()
        {
            if (!IsConnected)
                return;

            _socket.Detach(this);
            if (_visual != null)
                _visual.SetActive(false);
        }

        private void OnValidate()
        {
            if (_source != null && !(_source is IWaterFlowSource))
            {
                Debug.LogError($"{name}: {_source.GetType().Name} is not an IWaterFlowSource.", this);
                _source = null;
            }
        }
    }
}
