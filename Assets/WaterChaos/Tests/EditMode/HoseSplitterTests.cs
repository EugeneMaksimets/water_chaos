using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Splitter;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// The splitter only divides the flow: the liquid from the original source reaches every nozzle unchanged.
    /// Includes the whole data path: reservoir → pump → splitter → outlet → nozzle stream.
    /// </summary>
    public class HoseSplitterTests
    {
        private const float Tolerance = 1e-4f;

        // A liquid the splitter has never heard of: it must still pass through unchanged.
        private const LiquidType FutureLiquid = (LiquidType)42;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
            {
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            _created.Clear();
        }

        // ───────────── Pure split ─────────────

        [TestCase(1, 1f)]
        [TestCase(2, 0.5f)]
        [TestCase(3, 1f / 3f)]
        public void Split_GivesEachOutletAnEqualShare(int outlets, float share)
        {
            WaterFlowData input = new WaterFlowData(9f, 200_000f, LiquidType.Water);

            WaterFlowData output = WaterFlowSplit.Split(input, outlets);

            Assert.AreEqual(9f * share, output.FlowRate, Tolerance);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Split_TotalNeverExceedsInput(int outlets)
        {
            WaterFlowData input = new WaterFlowData(10f, 200_000f);

            WaterFlowData output = WaterFlowSplit.Split(input, outlets);

            Assert.LessOrEqual(output.FlowRate * outlets, input.FlowRate + Tolerance);
        }

        [Test]
        public void Split_KeepsPressure()
        {
            WaterFlowData output = WaterFlowSplit.Split(new WaterFlowData(9f, 150_000f), 3);

            Assert.AreEqual(150_000f, output.Pressure, Tolerance);
        }

        [TestCase(LiquidType.Water)]
        [TestCase(LiquidType.Gasoline)]
        [TestCase(FutureLiquid)]
        public void Split_PassesTheLiquidThroughUnchanged(LiquidType liquid)
        {
            Assert.AreEqual(liquid, WaterFlowSplit.Split(new WaterFlowData(6f, 1f, liquid), 2).Liquid);
            Assert.AreEqual(liquid, WaterFlowSplit.Split(new WaterFlowData(0f, 0f, liquid), 0).Liquid);
        }

        [Test]
        public void Split_NoOutlets_GivesNoFlow()
        {
            WaterFlowData output = WaterFlowSplit.Split(new WaterFlowData(6f, 1f), 0);

            Assert.AreEqual(0f, output.FlowRate);
            Assert.AreEqual(0f, output.Pressure);
        }

        // ───────────── Splitter components ─────────────

        [TestCase(LiquidType.Water)]
        [TestCase(LiquidType.Gasoline)]
        [TestCase(FutureLiquid)]
        public void EveryConnectedOutlet_GetsTheSourceLiquid(LiquidType liquid)
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f, liquid)));
            Attach(outlets[0], outlets[1], outlets[2]);

            foreach (SplitterOutlet outlet in outlets)
            {
                Assert.AreEqual(liquid, outlet.Output.Liquid);
                Assert.AreEqual(3f, outlet.Output.FlowRate, Tolerance);
            }
        }

        [Test]
        public void ConnectionOrder_DoesNotChangeTheLiquid()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f, LiquidType.Gasoline)));

            Attach(outlets[2]);
            Assert.AreEqual(LiquidType.Gasoline, outlets[2].Output.Liquid);

            Attach(outlets[0]);
            Attach(outlets[1]);
            foreach (SplitterOutlet outlet in outlets)
                Assert.AreEqual(LiquidType.Gasoline, outlet.Output.Liquid);
        }

        [Test]
        public void DisconnectingOneNozzle_RedistributesTheFlow()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f)));
            Attach(outlets[0], outlets[1], outlets[2]);

            outlets[1].GetComponent<WaterConnectionPoint>().Detach();

            Assert.AreEqual(4.5f, outlets[0].Output.FlowRate, Tolerance);
            Assert.AreEqual(4.5f, outlets[2].Output.FlowRate, Tolerance);
            Assert.AreEqual(0f, outlets[1].Output.FlowRate);
        }

        [Test]
        public void UnconnectedOutlet_GetsNothing()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f)));
            Attach(outlets[0]);

            Assert.AreEqual(9f, outlets[0].Output.FlowRate, Tolerance);
            Assert.AreEqual(0f, outlets[1].Output.FlowRate);
        }

        [Test]
        public void DisconnectingTheInput_StopsEveryOutlet()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f, LiquidType.Gasoline)));
            Attach(outlets[0], outlets[1], outlets[2]);

            splitter.SetSource(null);

            foreach (SplitterOutlet outlet in outlets)
                Assert.AreEqual(0f, outlet.Output.FlowRate);
        }

        [Test]
        public void DisabledOutlet_IsLeftOutOfTheSplit()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f)));
            Attach(outlets[0], outlets[1], outlets[2]);

            outlets[2].gameObject.SetActive(false);

            Assert.AreEqual(4.5f, outlets[0].Output.FlowRate, Tolerance);
        }

        [Test]
        public void DestroyedOutlet_IsLeftOutOfTheSplit()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f)));
            Attach(outlets[0], outlets[1], outlets[2]);

            Object.DestroyImmediate(outlets[2].gameObject);

            Assert.AreEqual(4.5f, outlets[0].Output.FlowRate, Tolerance);
        }

        [Test]
        public void TakenOutlet_IsNotFreeForAnotherNozzle()
        {
            SplitterOutlet[] outlets = CreateSplitter(out _);
            WaterConnectionPoint taken = outlets[0].GetComponent<WaterConnectionPoint>();
            WaterConnectionPoint other = outlets[1].GetComponent<WaterConnectionPoint>();
            taken.Attach();

            Assert.IsFalse(taken.IsFreeFor(null), "another nozzle");
            Assert.IsFalse(taken.IsFreeFor(other), "a nozzle connected elsewhere");
            Assert.IsTrue(taken.IsFreeFor(taken), "the nozzle already connected here");
            Assert.IsTrue(other.IsFreeFor(null));
        }

        [Test]
        public void Outlet_IsClosedUntilItsNozzleOpensIt()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new FixedSource(new WaterFlowData(9f, 100_000f)));
            outlets[0].GetComponent<WaterConnectionPoint>().Attach();

            Assert.AreEqual(0f, outlets[0].Output.FlowRate);

            outlets[0].Toggle();
            Assert.AreEqual(9f, outlets[0].Output.FlowRate, Tolerance);
        }

        [Test]
        public void StoppingOneOutlet_ClosesOnlyItAndNeverStopsTheSource()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            FixedSource source = new FixedSource(new WaterFlowData(9f, 100_000f));
            splitter.SetSource(source);
            Attach(outlets[0], outlets[1], outlets[2]);

            outlets[1].Stop();

            Assert.AreEqual(0, source.Stops);
            Assert.AreEqual(0f, outlets[1].Output.FlowRate);
            Assert.AreEqual(4.5f, outlets[0].Output.FlowRate, Tolerance);
            Assert.IsFalse(outlets[1].IsActive);
        }

        [Test]
        public void OpeningAnOutlet_StartsTheSourceOnlyIfItIsOff()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            FixedSource source = new FixedSource(new WaterFlowData(9f, 100_000f)) { IsActive = false };
            splitter.SetSource(source);

            outlets[0].Toggle();
            Assert.AreEqual(1, source.Toggles);

            source.IsActive = true;
            outlets[1].Toggle();
            Assert.AreEqual(1, source.Toggles, "a running source is left alone");
        }

        [Test]
        public void PressureSteps_GoToTheSource()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            FixedSource source = new FixedSource(new WaterFlowData(9f, 100_000f));
            splitter.SetSource(source);

            outlets[2].AdjustPressure(1);

            Assert.AreEqual(1, source.PressureSteps);
        }

        // ───────────── Inlet socket (transfer hose end) ─────────────

        [Test]
        public void Socket_TakesOneHoseAndFeedsTheSplitter()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            HoseEndSocket socket = CreateSocket(splitter);
            GameObject hoseA = CreateObject("Hose A");
            GameObject hoseB = CreateObject("Hose B");
            Attach(outlets[0]);

            Assert.IsTrue(socket.TryAttach(hoseA));
            Assert.IsFalse(socket.TryAttach(hoseB), "a second hose is rejected");

            socket.Feed(new FixedSource(new WaterFlowData(9f, 100_000f, LiquidType.Gasoline)));
            Assert.AreEqual(LiquidType.Gasoline, outlets[0].Output.Liquid);
            Assert.AreEqual(9f, outlets[0].Output.FlowRate, Tolerance);
        }

        [Test]
        public void Socket_UnpluggingTheHose_StopsEveryOutlet()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            HoseEndSocket socket = CreateSocket(splitter);
            GameObject hose = CreateObject("Hose");
            socket.TryAttach(hose);
            socket.Feed(new FixedSource(new WaterFlowData(9f, 100_000f)));
            Attach(outlets[0], outlets[1]);

            socket.Detach(hose);

            Assert.IsFalse(socket.IsAttached);
            Assert.AreEqual(0f, outlets[0].Output.FlowRate);
            Assert.AreEqual(0f, outlets[1].Output.FlowRate);
        }

        [Test]
        public void Socket_OnlyThePluggedHoseCanUnplug()
        {
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            HoseEndSocket socket = CreateSocket(splitter);
            GameObject hose = CreateObject("Hose");
            socket.TryAttach(hose);
            socket.Feed(new FixedSource(new WaterFlowData(9f, 100_000f)));
            Attach(outlets[0]);

            socket.Detach(CreateObject("Other Hose"));

            Assert.IsTrue(socket.IsAttached);
            Assert.AreEqual(9f, outlets[0].Output.FlowRate, Tolerance);
        }

        // ───────────── Whole path ─────────────

        [TestCase(LiquidType.Water)]
        [TestCase(LiquidType.Gasoline)]
        public void ReservoirLiquid_ReachesEveryNozzleStream(LiquidType liquid)
        {
            PumpModel pump = new PumpModel(100_000f, 9f, 1_000_000f);
            pump.SetSource(new WaterReservoir(1000f, 1000f, 0f, liquid));
            pump.SetOutletConnected(true);
            pump.SetTargetPressure(100_000f);
            pump.Start();
            pump.Tick(0.1f);

            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new PumpSource(pump));
            Attach(outlets[0], outlets[1], outlets[2]);

            foreach (SplitterOutlet outlet in outlets)
            {
                WaterStream stream = CreateStream();
                stream.SetSource(outlet);

                Assert.AreEqual(liquid, stream.Flow.Liquid);
                Assert.AreEqual(3f, stream.Flow.FlowRate, Tolerance);
            }
        }

        [Test]
        public void SourceLiquidChange_ReachesTheNozzlesWithoutTouchingTheSplitter()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 10f);
            PumpModel pump = new PumpModel(100_000f, 9f, 1_000_000f);
            pump.SetSource(reservoir);
            pump.SetOutletConnected(true);
            pump.SetTargetPressure(100_000f);
            pump.Start();
            pump.Tick(0.1f);

            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new PumpSource(pump));
            Attach(outlets[0], outlets[1]);
            Assert.AreEqual(LiquidType.Water, outlets[0].Output.Liquid);

            reservoir.Add(1f, LiquidType.Gasoline);

            Assert.AreEqual(LiquidType.Gasoline, outlets[0].Output.Liquid);
            Assert.AreEqual(LiquidType.Gasoline, outlets[1].Output.Liquid);
        }

        // ───────────── Helpers ─────────────

        private SplitterOutlet[] CreateSplitter(out HoseSplitter splitter)
        {
            GameObject root = new GameObject("Splitter");
            _created.Add(root);
            splitter = root.AddComponent<HoseSplitter>();

            SplitterOutlet[] outlets = new SplitterOutlet[3];
            for (int i = 0; i < outlets.Length; i++)
            {
                GameObject port = new GameObject("Outlet " + i);
                port.transform.SetParent(root.transform, false);
                // Source first: the point looks for it when it wakes up.
                outlets[i] = port.AddComponent<SplitterOutlet>();
                port.AddComponent<WaterConnectionPoint>();
            }

            return outlets;
        }

        private HoseEndSocket CreateSocket(HoseSplitter splitter)
        {
            GameObject inlet = new GameObject("Inlet");
            inlet.transform.SetParent(splitter.transform, false);
            return inlet.AddComponent<HoseEndSocket>();
        }

        private GameObject CreateObject(string name)
        {
            GameObject go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private WaterStream CreateStream()
        {
            GameObject go = new GameObject("Stream");
            _created.Add(go);
            return go.AddComponent<WaterStream>();
        }

        /// <summary>Connects a nozzle to each outlet and opens it.</summary>
        private static void Attach(params SplitterOutlet[] outlets)
        {
            foreach (SplitterOutlet outlet in outlets)
            {
                outlet.GetComponent<WaterConnectionPoint>().Attach();
                if (!outlet.IsOpen)
                    outlet.Toggle();
            }
        }

        private sealed class FixedSource : IWaterFlowSource, IWaterSourceControl
        {
            public FixedSource(WaterFlowData output) => Output = output;

            public WaterFlowData Output { get; }
            public int Toggles { get; private set; }
            public int PressureSteps { get; private set; }
            public int Stops { get; private set; }
            public bool IsActive { get; set; } = true;

            public void Toggle() => Toggles++;
            public void AdjustPressure(int direction) => PressureSteps += direction;
            public void Stop() => Stops++;
        }

        private sealed class PumpSource : IWaterFlowSource
        {
            private readonly PumpModel _pump;

            public PumpSource(PumpModel pump) => _pump = pump;

            public WaterFlowData Output => _pump.Output;
        }
    }
}
