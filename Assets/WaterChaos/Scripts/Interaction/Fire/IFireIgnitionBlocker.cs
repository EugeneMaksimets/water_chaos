using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Something covering a surface so no new fire can start there (e.g. a snowdrift).
    /// Registered in <see cref="FireIgnitionRules"/>; fire code never needs to know what it is.
    /// </summary>
    public interface IFireIgnitionBlocker
    {
        /// <summary>True if a new fire at <paramref name="firePosition"/> must not start.</summary>
        bool BlocksIgnitionAt(Vector3 firePosition);
    }
}
