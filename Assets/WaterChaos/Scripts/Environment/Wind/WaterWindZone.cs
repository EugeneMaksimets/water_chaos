using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>
    /// Trigger volume that pushes the water stream along its forward axis.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WaterWindZone : MonoBehaviour, IFlowInfluence
    {
        [Header("Wind")]
        [Tooltip("Sideways drift per meter of stream length (m/m). 0.3 → a 10 m stream drifts 3 m.")]
        [SerializeField, Min(0f)] private float _strength = 0.3f;

        public Vector3 GetEndOffset(float range)
        {
            return CalculateEndOffset(transform.forward, _strength, range);
        }

        /// <param name="direction">Wind direction; normalized internally.</param>
        /// <param name="strength">Drift per meter of stream length (m/m); negative → 0.</param>
        /// <param name="range">Stream length, in meters (m); negative → 0.</param>
        /// <returns>Offset of the stream end, in meters (m).</returns>
        public static Vector3 CalculateEndOffset(Vector3 direction, float strength, float range)
        {
            return direction.normalized * (Mathf.Max(0f, strength) * Mathf.Max(0f, range));
        }

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);
        }
    }
}