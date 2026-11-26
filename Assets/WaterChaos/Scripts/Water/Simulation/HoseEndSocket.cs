using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// A socket the free end of a laid hose (the transfer hose) plugs into, e.g. a splitter inlet.
    /// Holds one hose end at a time and hands what that hose carries to the <see cref="IWaterFlowReceiver"/>
    /// of its object. Taken out of the world (its object picked up), it lets the hose go and feeds nothing.
    /// Needs a collider on this object or a child so the player can aim at it.
    /// </summary>
    public sealed class HoseEndSocket : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Where the plugged hose end sits. Empty = this transform.")]
        [SerializeField] private Transform _anchor;

        private IWaterFlowReceiver _receiver;

        /// <summary>Where the plugged hose end sits.</summary>
        public Transform Anchor => _anchor != null ? _anchor : transform;

        /// <summary>The hose plugged in; null if none.</summary>
        public Object Plug { get; private set; }

        public bool IsAttached => Plug != null;

        // Found on first use, so it also works before Awake.
        private IWaterFlowReceiver Receiver => _receiver ??= GetComponentInParent<IWaterFlowReceiver>(true);

        /// <summary>Plugs <paramref name="plug"/> in. False if another hose is in, or the socket is out of the world.</summary>
        public bool TryAttach(Object plug)
        {
            if (plug == null || IsAttached || !enabled || !gameObject.activeInHierarchy)
                return false;

            Plug = plug;
            return true;
        }

        /// <summary>Unplugs <paramref name="plug"/>; the receiver gets nothing any more.</summary>
        public void Detach(Object plug)
        {
            if (plug == null || Plug != plug)
                return;

            Plug = null;
            Feed(null);
        }

        /// <summary>Called by the plugged hose: what it carries now (its source), or null.</summary>
        public void Feed(IWaterFlowSource source)
        {
            Receiver?.SetSource(source);
        }

        private void OnDisable()
        {
            if (Plug != null)
                Detach(Plug);
        }
    }
}
