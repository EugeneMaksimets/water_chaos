using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// A body of water in the scene (lake, pond, pool). The trigger box is the water volume:
    /// its top is the surface. The volume of water itself is owned by <see cref="WaterReservoirComponent"/>.
    /// Keep the box unrotated around X and Z so its top stays flat. Put it on the "Water" layer.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(WaterReservoirComponent))]
    public sealed class WaterBody : MonoBehaviour
    {
        private BoxCollider _volume;
        private WaterReservoirComponent _reservoir;

        /// <summary>Water left in this body; null before Awake.</summary>
        public WaterReservoir Reservoir => _reservoir.Reservoir;

        /// <summary>World height of the water surface, in meters (m).</summary>
        public float SurfaceHeight => _volume.bounds.max.y;

        /// <summary>World height of the deepest point of the volume, in meters (m).</summary>
        public float BottomHeight => _volume.bounds.min.y;

        private void Awake()
        {
            _volume = GetComponent<BoxCollider>();
            _reservoir = GetComponent<WaterReservoirComponent>();
            _volume.isTrigger = true;
        }

        /// <summary>True if the world point lies inside the water volume.</summary>
        public bool Contains(Vector3 point)
        {
            return _volume.ClosestPoint(point) == point;
        }

        /// <summary>Depth of a world point below the surface, in meters (m); negative above it.</summary>
        public float GetDepth(Vector3 point)
        {
            return SurfaceHeight - point.y;
        }

        private void OnValidate()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }
    }
}
