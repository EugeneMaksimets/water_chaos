using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// An area where no fire may start outdoors (ground, yard, street): fires inside rooms still burn and spread there.
    /// Checked by <see cref="FireSpreader.CanBurnAt"/>, so it stops spreading and flamethrowers alike.
    /// The collider is a trigger on the Ignore Raycast layer, so streams and interaction rays pass through it.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class NoOutdoorFireZone : MonoBehaviour
    {
        private const int IgnoreRaycastLayer = 2;

        private static readonly List<NoOutdoorFireZone> s_zones = new List<NoOutdoorFireZone>();

        private Collider _area;

        /// <summary>True if the point lies in any enabled zone.</summary>
        public static bool Covers(Vector3 position)
        {
            foreach (NoOutdoorFireZone zone in s_zones)
            {
                if (zone._area.ClosestPoint(position) == position)
                    return true;
            }

            return false;
        }

        // Domain reload on entering Play Mode is off in this project: drop zones left from the last run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_zones.Clear();
        }

        private void Awake()
        {
            _area = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            s_zones.Add(this);
        }

        private void OnDisable()
        {
            s_zones.Remove(this);
        }

        private void OnValidate()
        {
            GetComponent<Collider>().isTrigger = true;
            gameObject.layer = IgnoreRaycastLayer;
        }

        private void OnDrawGizmos()
        {
            if (!(GetComponent<Collider>() is BoxCollider box))
                return;

            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
