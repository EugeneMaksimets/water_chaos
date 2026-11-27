using UnityEngine;

namespace WaterChaos.Surfaces
{
    /// <summary>A round patch lying on a surface (a puddle, a snowdrift).</summary>
    public interface ISurfacePatch
    {
        /// <summary>Center on the surface, in world space. Never moves.</summary>
        Vector3 Center { get; }

        /// <summary>Horizontal radius, in meters (m).</summary>
        float Radius { get; }
    }
}
