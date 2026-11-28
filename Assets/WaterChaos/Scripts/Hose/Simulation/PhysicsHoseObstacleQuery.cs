using UnityEngine;

namespace WaterChaos.Hose
{
    /// <summary>
    /// Obstacle checks with Unity physics. Ignores triggers and the colliders of the
    /// things the hose is attached to (the source, the player, and the item that carries the hose end).
    /// </summary>
    public sealed class PhysicsHoseObstacleQuery : IHoseObstacleQuery
    {
        private const int BufferSize = 16;

        private readonly RaycastHit[] _hits = new RaycastHit[BufferSize];
        private readonly LayerMask _mask;

        private Transform _ignoredA;
        private Transform _ignoredB;
        private Transform _ignoredC;

        public PhysicsHoseObstacleQuery(LayerMask mask)
        {
            _mask = mask;
        }

        /// <summary>Colliders on these objects or their children never block the hose; null = none.</summary>
        public void SetIgnored(Transform first, Transform second, Transform third = null)
        {
            _ignoredA = first;
            _ignoredB = second;
            _ignoredC = third;
        }

        public bool Linecast(Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
        {
            point = default;
            normal = default;

            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-4f)
                return false;

            int count = Physics.RaycastNonAlloc(from, delta / distance, _hits, distance, _mask, QueryTriggerInteraction.Ignore);
            if (!TryGetClosest(count, out RaycastHit hit))
                return false;

            point = hit.point;
            normal = hit.normal;
            return true;
        }

        public bool IsClear(Vector3 from, Vector3 to, float radius)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-4f)
                return true;

            int count = Physics.SphereCastNonAlloc(from, radius, delta / distance, _hits, distance, _mask, QueryTriggerInteraction.Ignore);
            return !TryGetClosest(count, out _);
        }

        // NonAlloc results are not sorted by distance.
        private bool TryGetClosest(int count, out RaycastHit closest)
        {
            closest = default;
            float best = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];

                // distance == 0: the cast started inside this collider, so it is not between the points.
                if (hit.distance <= 0f || hit.distance >= best || IsIgnored(hit.collider.transform))
                    continue;

                best = hit.distance;
                closest = hit;
            }

            return best < float.MaxValue;
        }

        /// <summary>True if this collider belongs to something the hose is attached to (it never blocks the hose).</summary>
        public bool IsIgnored(Collider collider)
        {
            return collider != null && IsIgnored(collider.transform);
        }

        private bool IsIgnored(Transform target)
        {
            return (_ignoredA != null && target.IsChildOf(_ignoredA))
                || (_ignoredB != null && target.IsChildOf(_ignoredB))
                || (_ignoredC != null && target.IsChildOf(_ignoredC));
        }
    }
}
