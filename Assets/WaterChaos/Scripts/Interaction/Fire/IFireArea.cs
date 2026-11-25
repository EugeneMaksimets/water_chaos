using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Area a fire belongs to (a room). Fire code asks it where it may spread,
    /// without knowing about temperatures or coolers.
    /// </summary>
    public interface IFireArea
    {
        /// <summary>False while the area protects itself (e.g. a working cooler): no spreading.</summary>
        bool AllowsSpread { get; }

        /// <summary>True if a fire placed at <paramref name="firePosition"/> would belong to this area.</summary>
        bool Contains(Vector3 firePosition);
    }
}
