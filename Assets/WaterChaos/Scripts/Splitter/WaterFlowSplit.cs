using WaterChaos.Water;

namespace WaterChaos.Splitter
{
    /// <summary>
    /// Pure split of one flow between several outlets: equal shares of the flow rate, the same pressure
    /// (one shared manifold), and always the incoming liquid, whatever it is (soap included).
    /// </summary>
    public static class WaterFlowSplit
    {
        /// <param name="input">Flow arriving at the splitter.</param>
        /// <param name="outlets">Outlets that take a share; none or no flow gives zero flow (of the same liquid).</param>
        /// <returns>What each outlet gets; all shares together never exceed the input.</returns>
        public static WaterFlowData Split(WaterFlowData input, int outlets)
        {
            if (outlets <= 0 || !(input.FlowRate > 0f))
                return new WaterFlowData(0f, 0f, input.Liquid, input.IsSoapy);

            return new WaterFlowData(input.FlowRate / outlets, input.Pressure, input.Liquid, input.IsSoapy);
        }
    }
}
