using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Splitter
{
    /// <summary>
    /// One outlet of a <see cref="HoseSplitter"/>: the source a nozzle connects to through the
    /// <see cref="WaterConnectionPoint"/> on the same object. It has its own valve, worked by that nozzle:
    /// open with a nozzle connected, it gives its share of the splitter's flow. Opening it also starts the splitter's
    /// source if that is off; closing or stopping it (the nozzle leaves or is put down) closes only this outlet,
    /// so the other nozzles keep their supply. Pressure steps go to the source.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SplitterOutlet : MonoBehaviour, IWaterFlowSource, IWaterSourceControl
    {
        // Splitters chained into splitters are followed this deep at most.
        private const int MaxChain = 8;

        private HoseSplitter _splitter;
        private WaterConnectionPoint _point;

        /// <summary>This outlet's valve is open; closed at start and whenever its nozzle stops it.</summary>
        public bool IsOpen { get; private set; }

        /// <summary>A nozzle is connected here, the valve is open and the outlet is in the world: it takes a share.</summary>
        public bool IsFlowing => IsOpen && IsConnected;

        /// <summary>A nozzle is connected here and the outlet is in the world.</summary>
        public bool IsConnected
        {
            get
            {
                WaterConnectionPoint point = Point;
                return enabled && point != null && point.enabled && point.gameObject.activeInHierarchy && point.IsAttached;
            }
        }

        /// <summary>Source feeding the splitter; null when its input is disconnected.</summary>
        public IWaterFlowSource Upstream => Splitter != null ? Splitter.Source : null;

        /// <summary>The share of the splitter's flow while flowing; no flow (of the same liquid) otherwise.</summary>
        public WaterFlowData Output
        {
            get
            {
                if (Splitter == null)
                    return default;

                return IsFlowing ? Splitter.GetOutletFlow() : WaterFlowSplit.Split(Splitter.Input, 0);
            }
        }

        /// <summary>The valve is open and the splitter's source delivers.</summary>
        public bool IsActive => IsOpen && (!(Upstream is IWaterSourceControl control) || control.IsActive);

        /// <summary>Opens or closes this outlet; opening also starts the splitter's source if it is off.</summary>
        public void Toggle()
        {
            if (IsOpen)
            {
                IsOpen = false;
                return;
            }

            IsOpen = true;
            if (Upstream is IWaterSourceControl control && !control.IsActive)
                control.Toggle();
        }

        public void AdjustPressure(int direction)
        {
            if (Upstream is IWaterSourceControl control)
                control.AdjustPressure(direction);
        }

        /// <summary>Closes only this outlet: one nozzle leaving never stops the shared supply of the others.</summary>
        public void Stop()
        {
            IsOpen = false;
        }

        /// <summary>The real source behind any splitters (pump, hydrant, truck); the source itself if it is not an outlet.</summary>
        public static IWaterFlowSource ResolveOrigin(IWaterFlowSource source)
        {
            for (int i = 0; i < MaxChain && source is SplitterOutlet outlet; i++)
                source = outlet.Upstream;

            return source;
        }

        private HoseSplitter Splitter => _splitter != null ? _splitter : (_splitter = GetComponentInParent<HoseSplitter>(true));

        private WaterConnectionPoint Point => _point != null ? _point : (_point = GetComponent<WaterConnectionPoint>());
    }
}
