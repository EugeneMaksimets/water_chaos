using UnityEngine;

namespace WaterChaos.Water
{
    /// <summary>
    /// Marks where a hose can be attached; exposes the water source of this object or its parents.
    /// Needs a collider on this object or a child so the player can aim at it.
    /// </summary>
    public sealed class WaterConnectionPoint : MonoBehaviour
    {
        public IWaterFlowSource Source { get; private set; }

        /// <summary>Controls of the same source; null if it has none.</summary>
        public IWaterSourceControl Control { get; private set; }

        private void Awake()
        {
            Source = GetComponentInParent<IWaterFlowSource>();
            Control = GetComponentInParent<IWaterSourceControl>();
            if (Source == null)
                Debug.LogError($"{name}: no IWaterFlowSource on this object or its parents.", this);
        }
    }
}