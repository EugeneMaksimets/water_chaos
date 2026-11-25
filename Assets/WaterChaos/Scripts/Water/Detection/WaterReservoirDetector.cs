using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Finds the water reservoir under the crosshair: a ray from the camera that sees reservoir triggers
    /// (lakes, tank volumes), passes through other triggers and stops at the first solid collider.
    /// Detection only: it never changes the reservoir.
    /// </summary>
    public sealed class WaterReservoirDetector : MonoBehaviour
    {
        private const int MaxHits = 16;

        [Header("References")]
        [Tooltip("The look ray starts here and goes along its forward (the player camera).")]
        [SerializeField] private Transform _camera;

        [Header("Settings")]
        [Tooltip("How far the player can read a reservoir, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDistance = 4f;

        [Tooltip("Layers the look ray can hit: reservoirs and everything that blocks the view. Exclude the Player layer.")]
        [SerializeField] private LayerMask _mask = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];

        /// <summary>Reservoir under the crosshair within reach; null if none.</summary>
        public WaterReservoirComponent Target { get; private set; }

        private void Awake()
        {
            if (_camera == null)
            {
                Debug.LogError($"{name}: WaterReservoirDetector needs a Camera reference.", this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            Target = null;
        }

        private void Update()
        {
            Target = Find(new Ray(_camera.position, _camera.forward), _maxDistance, _mask, _hits);
        }

        /// <summary>
        /// Nearest reservoir along the ray, unless a solid non-reservoir collider is in front of it.
        /// </summary>
        /// <param name="maxDistance">Reach in meters (m).</param>
        /// <param name="buffer">Reused hit buffer, so the query does not allocate.</param>
        public static WaterReservoirComponent Find(Ray ray, float maxDistance, int mask, RaycastHit[] buffer)
        {
            int count = Physics.RaycastNonAlloc(ray, buffer, maxDistance, mask, QueryTriggerInteraction.Collide);
            SortByDistance(buffer, count);

            for (int i = 0; i < count; i++)
            {
                Collider collider = buffer[i].collider;
                WaterReservoirComponent reservoir = Resolve(collider);
                if (reservoir != null)
                    return reservoir;

                // Other triggers (wind zones, fire areas) do not block the view; walls do.
                if (!collider.isTrigger)
                    return null;
            }

            return null;
        }

        private static WaterReservoirComponent Resolve(Collider collider)
        {
            WaterReservoirTarget target = collider.GetComponentInParent<WaterReservoirTarget>();
            if (target != null && target.Reservoir != null)
                return target.Reservoir;

            return collider.GetComponentInParent<WaterReservoirComponent>();
        }

        /// <summary>Insertion sort: the hit count is tiny and it does not allocate.</summary>
        private static void SortByDistance(RaycastHit[] hits, int count)
        {
            for (int i = 1; i < count; i++)
            {
                RaycastHit hit = hits[i];
                int j = i - 1;
                while (j >= 0 && hits[j].distance > hit.distance)
                {
                    hits[j + 1] = hits[j];
                    j--;
                }
                hits[j + 1] = hit;
            }
        }
    }
}
