using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WaterChaos.Pump;
using WaterChaos.Soap;
using WaterChaos.Splitter;
using WaterChaos.Teammate;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    /// <summary>
    /// Soap is a modifier of water, owned by the reservoir and passed on unchanged by pump, hose, splitter and nozzle.
    /// Soapy water only adds the bubble that lifts a teammate; the bubble is a separate object around the
    /// original character, which stays visible and gets its movement and physics back afterwards.
    /// </summary>
    public class SoapTests
    {
        private const float Tolerance = 1e-4f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }

            _created.Clear();
        }

        // ───────────── 1–3. Soap activates only in water ─────────────

        [Test]
        public void Soap_InWater_MakesItSoapy()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f);

            Assert.IsTrue(reservoir.AddSoap());
            Assert.IsTrue(reservoir.IsSoapy);
            Assert.AreEqual(LiquidType.Water, reservoir.Liquid, "soap is not a liquid of its own");
            Assert.AreEqual(500f, reservoir.Volume, Tolerance, "soap adds no volume");
        }

        [Test]
        public void Reservoir_ByDefault_IsNotSoapy()
        {
            Assert.IsFalse(new WaterReservoir(1000f, 500f).IsSoapy);
        }

        [Test]
        public void Soap_InEmptyReservoir_DoesNothing()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 0f);

            Assert.IsFalse(reservoir.AddSoap());
            Assert.IsFalse(reservoir.IsSoapy);
        }

        [Test]
        public void Soap_InGasoline_DoesNothing()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f, 0f, LiquidType.Gasoline);

            Assert.IsFalse(reservoir.AddSoap());
            Assert.IsFalse(reservoir.IsSoapy);
            Assert.AreEqual(LiquidType.Gasoline, reservoir.Liquid);
        }

        [Test]
        public void Soap_InToxicLiquid_DoesNothing()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f, 0f, LiquidType.Toxic);

            Assert.IsFalse(reservoir.AddSoap());
            Assert.IsFalse(reservoir.IsSoapy);
            Assert.AreEqual(LiquidType.Toxic, reservoir.Liquid);
        }

        [TestCase(LiquidType.Water, true)]
        [TestCase(LiquidType.Gasoline, false)]
        [TestCase(LiquidType.Toxic, false)]
        public void OnlyWater_CanBeSoapy(LiquidType liquid, bool expected)
        {
            Assert.AreEqual(expected, liquid.CanBeSoapy());
            Assert.AreEqual(expected, new WaterFlowData(5f, 1f, liquid, true).IsSoapy);
            Assert.AreEqual(expected, new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, liquid, true).IsSoapy);
        }

        // ───────────── 4. Soapy water is still water ─────────────

        [Test]
        public void SoapyFlow_KeepsEveryWaterValue()
        {
            WaterFlowData plain = new WaterFlowData(7f, 180_000f);
            WaterFlowData soapy = new WaterFlowData(7f, 180_000f, LiquidType.Water, true);

            Assert.IsTrue(soapy.IsSoapy);
            Assert.AreEqual(plain.FlowRate, soapy.FlowRate);
            Assert.AreEqual(plain.Pressure, soapy.Pressure);
            Assert.AreEqual(plain.Liquid, soapy.Liquid);
        }

        [Test]
        public void SoapyImpact_KeepsEveryWaterValue()
        {
            WaterImpact plain = new WaterImpact(Vector3.one, Vector3.up, Vector3.down, 0.3f, 120f);
            WaterImpact soapy = new WaterImpact(Vector3.one, Vector3.up, Vector3.down, 0.3f, 120f, LiquidType.Water, true);

            Assert.AreEqual(plain.Volume, soapy.Volume);
            Assert.AreEqual(plain.Force, soapy.Force);
            Assert.AreEqual(plain.Liquid, soapy.Liquid);
            Assert.AreEqual(plain.Liquid.ExtinguishingPower(), soapy.Liquid.ExtinguishingPower(), "puts fire out like water");
            Assert.IsFalse(soapy.Liquid.IsFlammable());
            Assert.IsFalse(soapy.Liquid.Corrodes());
        }

        [Test]
        public void SoapyReservoir_PumpsExactlyLikePlainWater()
        {
            WaterReservoir soapyTank = new WaterReservoir(1000f, 500f);
            soapyTank.AddSoap();
            PumpModel soapy = CreateRunningPump(soapyTank);
            PumpModel plain = CreateRunningPump(new WaterReservoir(1000f, 500f));

            Assert.AreEqual(plain.Output.FlowRate, soapy.Output.FlowRate, Tolerance);
            Assert.AreEqual(plain.Output.Pressure, soapy.Output.Pressure, Tolerance);
            Assert.AreEqual(plain.LastDrawnVolume, soapy.LastDrawnVolume, Tolerance);
            Assert.AreEqual(LiquidType.Water, soapy.Output.Liquid);
        }

        // ───────────── 5–6. Propagation: pump → hose → splitter → nozzle ─────────────

        [Test]
        public void Pump_FromSoapyReservoir_GivesSoapyWater()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f);
            reservoir.AddSoap();

            PumpModel pump = CreateRunningPump(reservoir);

            Assert.IsTrue(pump.Output.IsSoapy);
        }

        [Test]
        public void NozzleHoseOnSoapyPump_SpraysSoapyWater()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f);
            reservoir.AddSoap();
            WaterStream stream = CreateStream();

            stream.SetSource(new PumpSource(CreateRunningPump(reservoir)));

            Assert.IsTrue(stream.Flow.IsSoapy);
            Assert.Greater(stream.Flow.FlowRate, 0f);
        }

        [Test]
        public void Split_KeepsTheSoap()
        {
            WaterFlowData input = new WaterFlowData(9f, 100_000f, LiquidType.Water, true);

            Assert.IsTrue(WaterFlowSplit.Split(input, 3).IsSoapy);
            Assert.IsTrue(WaterFlowSplit.Split(input, 0).IsSoapy);
        }

        [Test]
        public void EveryNozzleBehindTheSplitter_GetsSoapyWaterFromTheSameSource()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f);
            reservoir.AddSoap();
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            splitter.SetSource(new PumpSource(CreateRunningPump(reservoir)));
            Attach(outlets);

            foreach (SplitterOutlet outlet in outlets)
            {
                WaterStream stream = CreateStream();
                stream.SetSource(outlet);
                Assert.IsTrue(stream.Flow.IsSoapy);
                Assert.AreEqual(3f, stream.Flow.FlowRate, Tolerance);
            }
        }

        [Test]
        public void ReconnectingTheSplitterHose_NeitherLosesNorKeepsStaleSoap()
        {
            WaterReservoir soapyTank = new WaterReservoir(1000f, 500f);
            soapyTank.AddSoap();
            PumpSource soapy = new PumpSource(CreateRunningPump(soapyTank));
            PumpSource plain = new PumpSource(CreateRunningPump(new WaterReservoir(1000f, 500f)));
            SplitterOutlet[] outlets = CreateSplitter(out HoseSplitter splitter);
            Attach(outlets[0]);

            splitter.SetSource(soapy);
            splitter.SetSource(null);
            splitter.SetSource(soapy);
            Assert.IsTrue(outlets[0].Output.IsSoapy, "soap is back after reconnecting");

            splitter.SetSource(plain);
            Assert.IsFalse(outlets[0].Output.IsSoapy, "a plain source gives plain water");
        }

        [Test]
        public void Truck_FromSoapyTank_GivesSoapyWater()
        {
            WaterReservoir tank = new WaterReservoir(1000f, 500f);
            tank.AddSoap();
            var truck = new FireTruck.FireTruckModel(100_000f, 9f, 100f, tank);
            truck.Toggle();

            truck.Tick(0.1f);

            Assert.IsTrue(truck.Output.IsSoapy);
        }

        [Test]
        public void Transfer_SoapyWaterIntoWater_MakesTheDestinationSoapy()
        {
            WaterReservoir source = new WaterReservoir(1000f, 500f);
            source.AddSoap();
            WaterReservoir destination = new WaterReservoir(1000f, 100f);

            WaterTransferSettlement.Settle(source, destination, 50f);

            Assert.IsTrue(destination.IsSoapy);
        }

        // ───────────── 7. Other liquids remove the soap ─────────────

        [TestCase(LiquidType.Gasoline)]
        [TestCase(LiquidType.Toxic)]
        public void AnotherLiquidTakingOver_WashesTheSoapOut(LiquidType other)
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f);
            reservoir.AddSoap();
            PumpModel pump = CreateRunningPump(reservoir);

            reservoir.Add(1f, other);
            pump.Tick(0.1f);

            Assert.AreEqual(other, reservoir.Liquid);
            Assert.IsFalse(reservoir.IsSoapy);
            Assert.IsFalse(pump.Output.IsSoapy);
        }

        [Test]
        public void BackToFreshWater_IsPlainAgain()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f);
            reservoir.AddSoap();
            reservoir.Add(1f, LiquidType.Gasoline);

            reservoir.Draw(reservoir.Volume);
            reservoir.Add(500f, LiquidType.Water);

            Assert.AreEqual(LiquidType.Water, reservoir.Liquid);
            Assert.IsFalse(reservoir.IsSoapy);
        }

        [Test]
        public void GasolineMeetingToxic_NeutralizesIntoPlainWater()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f, 0f, LiquidType.Gasoline);

            reservoir.Add(10f, LiquidType.Toxic, true);

            Assert.AreEqual(LiquidType.Water, reservoir.Liquid);
            Assert.IsFalse(reservoir.IsSoapy);
        }

        [Test]
        public void PlainWaterAddedToSoapyWater_StaysSoapy()
        {
            WaterReservoir reservoir = new WaterReservoir(1000f, 500f);
            reservoir.AddSoap();

            reservoir.Add(100f, LiquidType.Water);

            Assert.IsTrue(reservoir.IsSoapy);
        }

        [Test]
        public void HydrantWater_IsNeverSoapy()
        {
            var hydrant = new Hydrant.HydrantModel(100_000f, 9f, 100f);
            hydrant.Toggle();
            hydrant.Tick(10f);

            Assert.IsFalse(hydrant.Output.IsSoapy);
        }

        // ───────────── Bubble timeline (pure model) ─────────────

        [Test]
        public void Bubble_GoesThroughEveryPhaseInOrder()
        {
            SoapBubbleModel model = CreateModel(riseHeight: 2f);
            var phases = new List<SoapBubblePhase>();
            model.PhaseChanged += phases.Add;

            Assert.IsTrue(model.TryCapture());
            for (int i = 0; i < 400 && model.Phase != SoapBubblePhase.Falling; i++)
                model.Tick(0.05f);
            model.Land();

            CollectionAssert.AreEqual(new[]
            {
                SoapBubblePhase.BubbleForming, SoapBubblePhase.Encapsulated, SoapBubblePhase.Rising,
                SoapBubblePhase.Bursting, SoapBubblePhase.Falling, SoapBubblePhase.Normal,
            }, phases);
        }

        [Test]
        public void Bubble_RisesToThePlannedHeightAndNoHigher()
        {
            SoapBubbleModel model = CreateModel(riseHeight: 6.5f);
            model.TryCapture();

            float lifted = 0f;
            for (int i = 0; i < 1000 && model.Phase != SoapBubblePhase.Bursting; i++)
                lifted += model.Tick(0.02f);

            Assert.AreEqual(6.5f, lifted, 1e-3f);
            Assert.AreEqual(6.5f, model.RisenHeight, 1e-3f);
        }

        [Test]
        public void Bubble_StopsUnderACeilingAndPops()
        {
            SoapBubbleModel model = CreateModel(riseHeight: 6f);
            model.TryCapture();
            TickUntil(model, SoapBubblePhase.Rising);

            float lifted = 0f;
            for (int i = 0; i < 1000 && model.Phase == SoapBubblePhase.Rising; i++)
                lifted += model.Tick(0.02f, Mathf.Max(0f, 1.2f - lifted));

            Assert.AreEqual(1.2f, lifted, 1e-3f, "never goes into the ceiling");
            Assert.AreEqual(SoapBubblePhase.Bursting, model.Phase);
        }

        [Test]
        public void Bubble_WithNoRoomAbove_PopsWithoutLifting()
        {
            SoapBubbleModel model = CreateModel(riseHeight: 6f);
            model.TryCapture();
            TickUntil(model, SoapBubblePhase.Rising);

            float lifted = 0f;
            for (int i = 0; i < 100 && model.Phase == SoapBubblePhase.Rising; i++)
                lifted += model.Tick(0.02f, 0f);

            Assert.AreEqual(0f, lifted);
            Assert.AreEqual(SoapBubblePhase.Bursting, model.Phase);
        }

        [Test]
        public void Bubble_SecondCaptureWhileActive_IsIgnored()
        {
            SoapBubbleModel model = CreateModel();

            Assert.IsTrue(model.TryCapture());
            Assert.IsFalse(model.TryCapture());
            model.Tick(0.05f);
            Assert.IsFalse(model.TryCapture());
        }

        [Test]
        public void Bubble_AfterLanding_WaitsForTheCooldown()
        {
            SoapBubbleModel model = CreateModel(cooldown: 1f);
            model.TryCapture();
            TickUntil(model, SoapBubblePhase.Falling);
            model.Land();

            Assert.IsFalse(model.TryCapture(), "still being sprayed right after landing");
            model.Tick(1.1f);
            Assert.IsTrue(model.TryCapture());
        }

        [Test]
        public void Bubble_LiftsOnlyWhileRising()
        {
            SoapBubbleModel model = CreateModel(riseHeight: 3f);
            model.TryCapture();

            Assert.AreEqual(0f, model.Tick(0.05f), "forming");
            TickUntil(model, SoapBubblePhase.Bursting);
            Assert.AreEqual(0f, model.Tick(0.01f), "bursting");
        }

        // ───────────── 8–12. Teammate, bubble object and restoring ─────────────

        [Test]
        public void SoapyHit_OnTeammate_FormsABubble()
        {
            Teammate teammate = CreateTeammate();

            teammate.Receiver.ReceiveImpact(SoapyImpact());

            Assert.AreEqual(SoapBubblePhase.BubbleForming, teammate.Controller.Phase);
            Assert.IsNotNull(teammate.Controller.ActiveBubble);
        }

        [TestCase(LiquidType.Water)]
        [TestCase(LiquidType.Gasoline)]
        [TestCase(LiquidType.Toxic)]
        public void NonSoapyHit_OnTeammate_DoesNothing(LiquidType liquid)
        {
            Teammate teammate = CreateTeammate();

            // Gasoline and toxic liquid cannot be soapy even if asked to.
            teammate.Receiver.ReceiveImpact(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 0.2f, 50f, liquid,
                liquid != LiquidType.Water));

            Assert.AreEqual(SoapBubblePhase.Normal, teammate.Controller.Phase);
            Assert.IsNull(teammate.Controller.ActiveBubble);
        }

        [Test]
        public void SoapyHit_WithNoWater_DoesNothing()
        {
            Teammate teammate = CreateTeammate();

            teammate.Receiver.ReceiveImpact(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 0f, 0f, LiquidType.Water, true));

            Assert.AreEqual(SoapBubblePhase.Normal, teammate.Controller.Phase);
        }

        [Test]
        public void ObjectsWithoutAReceiver_AreNeverEncapsulated()
        {
            // The stream hands impacts only to receivers on the hit collider; a plain object (or the player) has none.
            GameObject barrel = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            Teammate teammate = CreateTeammate();

            Assert.AreEqual(0, barrel.GetComponents<Interaction.IWaterImpactReceiver>().Length);
            Assert.AreEqual(1, teammate.Root.GetComponents<Interaction.IWaterImpactReceiver>().Length);
        }

        [Test]
        public void RepeatedSoapyHits_MakeOnlyOneBubble()
        {
            Teammate teammate = CreateTeammate();

            for (int i = 0; i < 10; i++)
                teammate.Receiver.ReceiveImpact(SoapyImpact());

            Assert.AreEqual(1, teammate.Root.GetComponentsInChildren<SoapBubbleView>(true).Length);
            Assert.AreEqual(1, teammate.Root.GetComponents<SoapBubbleController>().Length);
        }

        [Test]
        public void EncapsulatedFirefighter_StaysFullyVisibleAndUnchanged()
        {
            Teammate teammate = CreateTeammate();
            Material material = teammate.Body.sharedMaterial;
            Vector3 scale = teammate.Model.transform.localScale;

            teammate.Receiver.ReceiveImpact(SoapyImpact());

            Assert.IsTrue(teammate.Model.activeInHierarchy);
            Assert.IsTrue(teammate.Body.enabled);
            Assert.AreSame(material, teammate.Body.sharedMaterial, "the character keeps its own material");
            Assert.AreEqual(scale, teammate.Model.transform.localScale, "the character is not scaled down");
            Assert.AreEqual(Vector3.one, teammate.Root.transform.localScale);
        }

        [Test]
        public void Bubble_IsASeparateObjectAroundTheCharacter()
        {
            Teammate teammate = CreateTeammate();

            teammate.Receiver.ReceiveImpact(SoapyImpact());
            SoapBubbleView bubble = teammate.Controller.ActiveBubble;
            bubble.SetFormProgress(1f);

            Assert.AreNotSame(teammate.Model, bubble.gameObject);
            Assert.AreSame(teammate.Anchor, bubble.transform.parent, "attached to the effect anchor");
            Assert.IsFalse(teammate.Model.transform.IsChildOf(bubble.transform), "the character is not inside the bubble's hierarchy");
            Assert.IsFalse(bubble.transform.IsChildOf(teammate.Model.transform), "the bubble is not part of the character model");

            Bounds character = teammate.Body.bounds;
            float clearance = bubble.Radius - (character.center - bubble.Shell.position).magnitude - character.extents.magnitude;
            Assert.Greater(clearance, 0f, "the whole character fits inside with room to spare");
        }

        [Test]
        public void Release_RestoresMovementAndPhysics()
        {
            Teammate teammate = CreateTeammate();
            Rigidbody body = teammate.Root.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;

            teammate.Receiver.ReceiveImpact(SoapyImpact());
            Assert.IsFalse(teammate.Motor.enabled, "own movement paused while carried");
            Assert.IsTrue(body.isKinematic);
            Assert.IsFalse(body.useGravity);

            teammate.Controller.Cancel();

            Assert.IsTrue(teammate.Motor.enabled);
            Assert.IsFalse(body.isKinematic);
            Assert.IsTrue(body.useGravity);
            Assert.IsNull(teammate.Controller.ActiveBubble);
            Assert.AreEqual(SoapBubblePhase.Normal, teammate.Controller.Phase);
        }

        [Test]
        public void Release_KeepsAMovementComponentThatWasAlreadyOff()
        {
            Teammate teammate = CreateTeammate();
            teammate.Motor.enabled = false;

            teammate.Receiver.ReceiveImpact(SoapyImpact());
            teammate.Controller.Cancel();

            Assert.IsFalse(teammate.Motor.enabled);
        }

        [Test]
        public void DisabledController_IgnoresSoapyHits()
        {
            Teammate teammate = CreateTeammate();
            teammate.Controller.enabled = false;

            teammate.Receiver.ReceiveImpact(SoapyImpact());

            Assert.AreEqual(SoapBubblePhase.Normal, teammate.Controller.Phase);
        }

        [Test]
        public void ThirdFloor_IsTwoFloorsUpPlusTheExtra()
        {
            Teammate teammate = CreateTeammate();

            Assert.AreEqual(3f * 2f + 0.5f, teammate.Controller.RiseHeight, Tolerance);
        }

        // ───────────── Helpers ─────────────

        private sealed class Teammate
        {
            public GameObject Root;
            public GameObject Model;
            public MeshRenderer Body;
            public Transform Anchor;
            public TeammateMotor Motor;
            public SoapBubbleController Controller;
            public SoapBubbleReceiver Receiver;
        }

        private static SoapBubbleModel CreateModel(float riseHeight = 6.5f, float cooldown = 0.5f)
        {
            return new SoapBubbleModel(0.3f, 0.2f, 2f, riseHeight, 0.4f, 0.1f, cooldown);
        }

        private static void TickUntil(SoapBubbleModel model, SoapBubblePhase phase)
        {
            for (int i = 0; i < 2000 && model.Phase != phase; i++)
                model.Tick(0.02f);
            Assert.AreEqual(phase, model.Phase);
        }

        private static WaterImpact SoapyImpact()
        {
            return new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 0.2f, 50f, LiquidType.Water, true);
        }

        private Teammate CreateTeammate()
        {
            var teammate = new Teammate { Root = Track(new GameObject("Teammate")) };
            teammate.Root.AddComponent<CharacterController>();

            // Stand-in for the firefighter model: a 1.8 m tall mesh with its own material.
            teammate.Model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            teammate.Model.name = "Firefighter";
            Object.DestroyImmediate(teammate.Model.GetComponent<Collider>());
            teammate.Model.transform.SetParent(teammate.Root.transform, false);
            teammate.Model.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            teammate.Model.transform.localScale = new Vector3(0.6f, 1.8f, 0.4f);
            teammate.Body = teammate.Model.GetComponent<MeshRenderer>();
            teammate.Body.sharedMaterial = Track(new Material(Shader.Find("Hidden/InternalErrorShader")));

            teammate.Anchor = new GameObject("BubbleEffectAnchor").transform;
            teammate.Anchor.SetParent(teammate.Root.transform, false);

            teammate.Motor = teammate.Root.AddComponent<TeammateMotor>();
            teammate.Controller = teammate.Root.AddComponent<SoapBubbleController>();
            teammate.Receiver = teammate.Root.AddComponent<SoapBubbleReceiver>();

            var so = new SerializedObject(teammate.Controller);
            so.FindProperty("_bubblePrefab").objectReferenceValue = CreateBubbleTemplate();
            so.FindProperty("_anchor").objectReferenceValue = teammate.Anchor;
            SerializedProperty suspend = so.FindProperty("_suspendWhileCarried");
            suspend.arraySize = 1;
            suspend.GetArrayElementAtIndex(0).objectReferenceValue = teammate.Motor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return teammate;
        }

        private SoapBubbleView CreateBubbleTemplate()
        {
            GameObject root = Track(new GameObject("Bubble Template"));
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(shell.GetComponent<Collider>());
            shell.transform.SetParent(root.transform, false);
            var view = root.AddComponent<SoapBubbleView>();

            var so = new SerializedObject(view);
            so.FindProperty("_shell").objectReferenceValue = shell.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static PumpModel CreateRunningPump(WaterReservoir reservoir)
        {
            PumpModel pump = new PumpModel(100_000f, 9f, 1_000_000f);
            pump.SetSource(reservoir);
            pump.SetOutletConnected(true);
            pump.SetTargetPressure(100_000f);
            pump.Start();
            pump.Tick(0.1f);
            return pump;
        }

        private SplitterOutlet[] CreateSplitter(out HoseSplitter splitter)
        {
            GameObject root = Track(new GameObject("Splitter"));
            splitter = root.AddComponent<HoseSplitter>();

            SplitterOutlet[] outlets = new SplitterOutlet[3];
            for (int i = 0; i < outlets.Length; i++)
            {
                GameObject port = new GameObject("Outlet " + i);
                port.transform.SetParent(root.transform, false);
                outlets[i] = port.AddComponent<SplitterOutlet>();
                port.AddComponent<WaterConnectionPoint>();
            }

            return outlets;
        }

        private static void Attach(params SplitterOutlet[] outlets)
        {
            foreach (SplitterOutlet outlet in outlets)
            {
                outlet.GetComponent<WaterConnectionPoint>().Attach();
                if (!outlet.IsOpen)
                    outlet.Toggle();
            }
        }

        private WaterStream CreateStream()
        {
            return Track(new GameObject("Stream")).AddComponent<WaterStream>();
        }

        private T Track<T>(T item) where T : Object
        {
            _created.Add(item);
            return item;
        }

        private sealed class PumpSource : IWaterFlowSource
        {
            private readonly PumpModel _pump;

            public PumpSource(PumpModel pump) => _pump = pump;

            public WaterFlowData Output => _pump.Output;
        }
    }
}
