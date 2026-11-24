using UnityEngine;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Marks ground or another large surface fire can creep across, like grass.
    /// Unlike FlammableObject it holds many fires, spaced by the exclusion radius.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlammableSurface : MonoBehaviour
    {
    }
}
