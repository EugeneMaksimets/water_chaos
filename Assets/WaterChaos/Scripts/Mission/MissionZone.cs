using UnityEngine;

namespace WaterChaos.Mission
{
    /// <summary>
    /// An invisible area used by the mission: "the player entered the house", "fires in this yard",
    /// "show the hint near the tank". Only answers whether a point is inside; never reacts by itself.
    /// The collider is a trigger on the Ignore Raycast layer, so water streams and interaction rays pass through it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class MissionZone : MonoBehaviour
    {
        private const int IgnoreRaycastLayer = 2;

        private Collider _area;

        private Collider Area => _area != null ? _area : (_area = GetComponent<Collider>());

        /// <summary>True if the world point lies inside the zone.</summary>
        public bool Contains(Vector3 point)
        {
            Collider area = Area;
            return area.enabled && area.gameObject.activeInHierarchy && area.ClosestPoint(point) == point;
        }

        private void OnValidate()
        {
            Area.isTrigger = true;
            gameObject.layer = IgnoreRaycastLayer;
        }

        private void OnDrawGizmos()
        {
            if (!(Area is BoxCollider box))
                return;

            Gizmos.color = new Color(1f, 0.72f, 0.15f, 0.12f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(1f, 0.72f, 0.15f, 0.8f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
