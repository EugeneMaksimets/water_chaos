using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// One blow of a hand tool (an axe) at an object.
    /// </summary>
    public readonly struct ToolHit
    {
        /// <summary>Where the blow landed, in world space.</summary>
        public Vector3 Point { get; }

        /// <summary>Direction of the blow (normalized; zero if unknown).</summary>
        public Vector3 Direction { get; }

        /// <summary>Damage of the blow in hits (1 = one normal axe blow); negative and NaN become 0.</summary>
        public float Damage { get; }

        public ToolHit(Vector3 point, Vector3 direction, float damage)
        {
            Point = point;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
            Damage = float.IsNaN(damage) ? 0f : Mathf.Max(0f, damage);
        }
    }

    /// <summary>
    /// Implemented by objects that react to hand tool blows (doors break, glass shatters).
    /// The tool only delivers the blow; the target decides what happens.
    /// </summary>
    public interface IToolHitReceiver
    {
        void ReceiveToolHit(in ToolHit hit);
    }
}
