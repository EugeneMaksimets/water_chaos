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

        /// <summary>True while a hose is attached here.</summary>
        public bool IsAttached { get; private set; }

        /// <summary>Called by whoever attaches the hose.</summary>
        public void Attach() => IsAttached = true;

        /// <summary>Called by whoever detaches the hose.</summary>
        public void Detach() => IsAttached = false;

        private void Awake()
        {
            Source = GetComponentInParent<IWaterFlowSource>();
            Control = GetComponentInParent<IWaterSourceControl>();
            if (Source == null)
                Debug.LogError($"{name}: no IWaterFlowSource on this object or its parents.", this);
        }
    }
}