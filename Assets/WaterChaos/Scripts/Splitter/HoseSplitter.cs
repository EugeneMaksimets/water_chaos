using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Splitter
{
    /// <summary>
    /// Three-way hose splitter: its input is fed by a hose from a source (like a nozzle),
    /// and the flow is shared equally between the <see cref="SplitterOutlet"/>s that have a nozzle connected and are open.
    /// Only routes and divides: never looks at or changes the liquid.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoseSplitter : MonoBehaviour, IWaterFlowReceiver
    {
        private IWaterFlowSource _source;
        private SplitterOutlet[] _outlets;

        // Liquid of the last source, so a disconnected splitter still reports it (with no flow) instead of a default.
        private LiquidType _lastLiquid;

        /// <summary>Source the input hose is connected to; null when disconnected.</summary>
        public IWaterFlowSource Source => _source;

        /// <summary>Flow arriving at the input; no flow when disconnected.</summary>
        public WaterFlowData Input => _source != null ? _source.Output : new WaterFlowData(0f, 0f, _lastLiquid);

        /// <summary>Open outlets with a nozzle connected that are still in the world.</summary>
        public int FlowingOutletCount
        {
            get
            {
                int count = 0;
                foreach (SplitterOutlet outlet in Outlets)
                {
                    if (outlet != null && outlet.IsFlowing)
                        count++;
                }

                return count;
            }
        }

        // Found on first use, so it also works before Awake.
        private SplitterOutlet[] Outlets => _outlets ??= GetComponentsInChildren<SplitterOutlet>(true);

        /// <summary>Called when the input hose is connected to a source or taken off (null).</summary>
        public void SetSource(IWaterFlowSource source)
        {
            if (_source != null)
                _lastLiquid = _source.Output.Liquid;

            _source = source;
        }

        /// <summary>What each connected outlet gets now.</summary>
        public WaterFlowData GetOutletFlow()
        {
            return WaterFlowSplit.Split(Input, FlowingOutletCount);
        }
    }
}
