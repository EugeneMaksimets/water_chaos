using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Water;

namespace WaterChaos.Splitter
{
    /// <summary>
    /// A hose laid before the game starts: attached to a source's connection point (e.g. the hydrant outlet)
    /// and plugged into a <see cref="HoseEndSocket"/> (e.g. the splitter inlet), so the splitter is fed from that source.
    /// The point counts as taken, so the player cannot attach a nozzle there meanwhile.
    /// An optional <see cref="HoseController"/> draws the hose between the two (its End Point is the socket).
    /// <see cref="Disconnect"/> takes it all off, so the player can plug their own hose into the socket.
    /// </summary>
    public sealed class FixedHoseFeed : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Connection point of the source the hose starts at (e.g. the hydrant's WaterConnectionPoint).")]
        [SerializeField] private WaterConnectionPoint _from;

        [Tooltip("Socket the hose end is plugged into (e.g. the HoseSplitter inlet).")]
        [SerializeField] private HoseEndSocket _socket;

        [Tooltip("Optional: the visible hose; its End Point should be the socket.")]
        [SerializeField] private HoseController _hose;

        /// <summary>The hose is still plugged into the socket.</summary>
        public bool IsConnected => _socket != null && _socket.Plug == this;

        // Start: connection points find their source in Awake, the splitter is ready by then.
        private void Start()
        {
            if (_from == null || _from.Source == null || _socket == null)
            {
                Debug.LogError($"{name}: FixedHoseFeed needs From (a WaterConnectionPoint with a source) and a Socket.", this);
                enabled = false;
                return;
            }

            if (!_from.IsFreeFor(null) || !_socket.TryAttach(this))
            {
                Debug.LogWarning($"{name}: {_from.name} or {_socket.name} is already taken; the feed is not connected.", this);
                return;
            }

            _from.Attach();
            _socket.Feed(_from.Source);
            if (_hose != null)
                _hose.Connect(_from);
        }

        /// <summary>Takes the hose off both ends; the socket gets nothing and is free, the source point is free.</summary>
        public void Disconnect()
        {
            if (!IsConnected)
                return;

            _socket.Detach(this);
            _from.Detach();
            if (_hose != null)
            {
                _hose.Disconnect();
                _hose.gameObject.SetActive(false);
            }
        }
    }
}
