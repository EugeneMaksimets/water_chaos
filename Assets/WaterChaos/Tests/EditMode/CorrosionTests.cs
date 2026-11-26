using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// Toxic liquid eats corrodible objects away: hits and wet time add corrosion, resistance slows it,
    /// at the threshold the object is destroyed once. Water and immune surfaces are left alone.
    /// </summary>
    public class CorrosionTests
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

        // ───────────── Model ─────────────

        [Test]
        public void Model_ToxicLiquid_AddsCorrosion()
        {
            CorrosionModel model = new CorrosionModel(0f, 1f, 10f, 0f, 0f);

            model.ApplyLiquid(2f, LiquidType.Toxic.CorrosionPower());

            Assert.AreEqual(2f, model.Corrosion, Tolerance);
            Assert.AreEqual(0.2f, model.Progress, Tolerance);
        }

        [Test]
        public void Model_RepeatedHits_Accumulate()
        {
            CorrosionModel model = new CorrosionModel(0f, 1f, 100f, 0f, 0f);

            for (int i = 0; i < 5; i++)
                model.ApplyLiquid(2f, 1f);

            Assert.AreEqual(10f, model.Corrosion, Tolerance);
        }

        [Test]
        public void Model_HigherResistance_CorrodesSlower()
        {
            CorrosionModel weak = new CorrosionModel(0f, 1f, 100f, 0f, 0f);
            CorrosionModel reinforced = new CorrosionModel(0.8f, 1f, 100f, 0f, 0f);

            weak.ApplyLiquid(10f, 1f);
            reinforced.ApplyLiquid(10f, 1f);

            Assert.AreEqual(10f, weak.Corrosion, Tolerance);
            Assert.AreEqual(2f, reinforced.Corrosion, Tolerance);
        }

        [Test]
        public void Model_FullResistance_IsImmune()
        {
            CorrosionModel immune = new CorrosionModel(1f, 1f, 1f, 5f, 5f);

            immune.ApplyLiquid(1000f, 1f);
            immune.Tick(10f);

            Assert.AreEqual(0f, immune.Corrosion);
            Assert.IsFalse(immune.IsDestroyed);
        }

        [TestCase(LiquidType.Water)]
        [TestCase(LiquidType.Gasoline)]
        public void Model_NonCorrosiveLiquid_DoesNothing(LiquidType liquid)
        {
            CorrosionModel model = new CorrosionModel(0f, 1f, 1f, 5f, 5f);

            model.ApplyLiquid(1000f, liquid.CorrosionPower());
            model.Tick(10f);

            Assert.AreEqual(0f, model.Corrosion);
            Assert.IsFalse(model.IsDestroyed);
        }

        [Test]
        public void Model_WetSurface_KeepsCorrodingUntilItDries()
        {
            CorrosionModel model = new CorrosionModel(0f, 1f, 100f, 2f, 3f);
            model.ApplyLiquid(1f, 1f);

            model.Tick(1f);
            Assert.AreEqual(3f, model.Corrosion, Tolerance, "1 from the hit + 2/s for 1 s");
            Assert.IsTrue(model.IsWet);

            model.Tick(10f);
            Assert.AreEqual(7f, model.Corrosion, Tolerance, "only the 2 s still wet count");
            Assert.IsFalse(model.IsWet);
        }

        [Test]
        public void Model_Water_WashesToxicOff()
        {
            CorrosionModel model = new CorrosionModel(0f, 1f, 100f, 2f, 3f);
            model.ApplyLiquid(1f, 1f);

            model.ApplyLiquid(1f, LiquidType.Water.CorrosionPower());
            model.Tick(5f);

            Assert.IsFalse(model.IsWet);
            Assert.AreEqual(1f, model.Corrosion, Tolerance, "the damage stays, but it stops growing");
        }

        [Test]
        public void Model_ReachingThreshold_DestroysOnce()
        {
            CorrosionModel model = new CorrosionModel(0f, 1f, 5f, 1f, 5f);

            Assert.IsFalse(model.ApplyLiquid(4f, 1f));
            Assert.IsTrue(model.ApplyLiquid(4f, 1f), "this hit crosses the threshold");
            Assert.IsFalse(model.ApplyLiquid(4f, 1f));
            Assert.IsFalse(model.Tick(5f));

            Assert.IsTrue(model.IsDestroyed);
            Assert.AreEqual(1f, model.Progress, Tolerance);
            Assert.IsFalse(model.IsWet);
        }

        [Test]
        public void Model_InvalidInput_IsIgnored()
        {
            CorrosionModel model = new CorrosionModel(float.NaN, -1f, 10f, float.NaN, -5f);

            model.ApplyLiquid(float.NaN, 1f);
            model.ApplyLiquid(-3f, 1f);
            model.Tick(float.NaN);

            Assert.AreEqual(0f, model.Corrosion);
        }

        // ───────────── Component ─────────────

        [Test]
        public void Wall_ToxicHit_StartsCorrosion()
        {
            CorrodibleObject wall = CreateWall(0f, 10f);

            wall.ReceiveImpact(Impact(LiquidType.Toxic, 2f));

            Assert.AreEqual(0.2f, wall.Progress, Tolerance);
            Assert.IsTrue(wall.IsWet);
        }

        [Test]
        public void Wall_RepeatedToxicHits_Accumulate()
        {
            CorrodibleObject wall = CreateWall(0f, 100f);
            int changes = 0;
            wall.CorrosionChanged += _ => changes++;

            for (int i = 0; i < 4; i++)
                wall.ReceiveImpact(Impact(LiquidType.Toxic, 5f));

            Assert.AreEqual(0.2f, wall.Progress, Tolerance);
            Assert.AreEqual(4, changes);
        }

        [Test]
        public void Wall_HigherResistance_CorrodesSlower()
        {
            CorrodibleObject weak = CreateWall(0f, 100f);
            CorrodibleObject reinforced = CreateWall(0.85f, 100f);

            weak.ReceiveImpact(Impact(LiquidType.Toxic, 20f));
            reinforced.ReceiveImpact(Impact(LiquidType.Toxic, 20f));

            Assert.Greater(weak.Progress, reinforced.Progress * 5f);
        }

        [Test]
        public void Wall_WaterHit_LeavesItIntact()
        {
            CorrodibleObject wall = CreateWall(0f, 1f);

            for (int i = 0; i < 100; i++)
                wall.ReceiveImpact(Impact(LiquidType.Water, 5f));

            Assert.AreEqual(0f, wall.Progress);
            Assert.IsFalse(wall.IsDestroyed);
        }

        [Test]
        public void ImmuneWall_ToxicHits_LeaveItIntact()
        {
            CorrodibleObject loadBearing = CreateWall(1f, 1f);

            for (int i = 0; i < 100; i++)
                loadBearing.ReceiveImpact(Impact(LiquidType.Toxic, 5f));
            loadBearing.Advance(30f);

            Assert.AreEqual(0f, loadBearing.Progress);
            Assert.IsTrue(loadBearing.GetComponent<Collider>().enabled);
        }

        [Test]
        public void Wall_AtThreshold_IsDestroyed_CollidersAndLookGone()
        {
            CorrodibleObject wall = CreateWall(0f, 10f);

            wall.ReceiveImpact(Impact(LiquidType.Toxic, 10f));

            Assert.IsTrue(wall.IsDestroyed);
            Assert.IsFalse(wall.GetComponent<Collider>().enabled, "water, player and hose pass through");
            Assert.IsFalse(wall.GetComponent<Renderer>().enabled);
        }

        [Test]
        public void Wall_WetTime_AloneCanFinishIt()
        {
            CorrodibleObject wall = CreateWall(0f, 10f, soakRate: 4f, wetDuration: 3f);
            wall.ReceiveImpact(Impact(LiquidType.Toxic, 1f));

            wall.Advance(3f);

            Assert.IsTrue(wall.IsDestroyed, "1 from the hit + 4/s for 3 s reaches 10");
        }

        [Test]
        public void Wall_IsDestroyedOnlyOnce()
        {
            CorrodibleObject wall = CreateWall(0f, 10f, soakRate: 5f, wetDuration: 5f);
            int destroyed = 0;
            wall.Destroyed += _ => destroyed++;

            for (int i = 0; i < 10; i++)
            {
                wall.ReceiveImpact(Impact(LiquidType.Toxic, 5f));
                wall.Advance(1f);
            }

            Assert.AreEqual(1, destroyed);
        }

        [Test]
        public void DisabledWall_IgnoresHits()
        {
            CorrodibleObject wall = CreateWall(0f, 10f);
            wall.enabled = false;

            wall.ReceiveImpact(Impact(LiquidType.Toxic, 100f));
            wall.Advance(10f);

            Assert.AreEqual(0f, wall.Progress);
            Assert.IsFalse(wall.IsDestroyed);
        }

        [Test]
        public void InactiveWall_IgnoresHits()
        {
            CorrodibleObject wall = CreateWall(0f, 10f);
            wall.gameObject.SetActive(false);

            wall.ReceiveImpact(Impact(LiquidType.Toxic, 100f));

            Assert.AreEqual(0f, wall.Progress);
        }

        [Test]
        public void DestroyedGameObject_IsHandledSafely()
        {
            CorrodibleObject wall = CreateWall(0f, 10f);
            Object.DestroyImmediate(wall.gameObject);

            Assert.DoesNotThrow(() =>
            {
                wall.ReceiveImpact(Impact(LiquidType.Toxic, 100f));
                wall.Advance(1f);
            });
        }

        [Test]
        public void Wall_WhosePartsWereRemoved_BreaksWithoutErrors()
        {
            CorrodibleObject wall = CreateWall(0f, 10f);
            _ = wall.Renderers; // the parts are found before they go
            Object.DestroyImmediate(wall.GetComponent<MeshRenderer>());

            Assert.DoesNotThrow(() => wall.ReceiveImpact(Impact(LiquidType.Toxic, 100f)));
            Assert.IsTrue(wall.IsDestroyed);
        }

        [Test]
        public void ToxicImpact_ReachesBothFireAndWallHandlersWithoutTheStreamKnowingWhich()
        {
            // The stream hands every receiver the same impact; each target decides how to react.
            CorrodibleObject wall = CreateWall(0f, 100f);
            IWaterImpactReceiver receiver = wall;

            receiver.ReceiveImpact(Impact(LiquidType.Toxic, 10f));

            Assert.AreEqual(0.1f, wall.Progress, Tolerance);
        }

        // ───────────── Helpers ─────────────

        private static WaterImpact Impact(LiquidType liquid, float volume)
        {
            return new WaterImpact(Vector3.zero, Vector3.back, Vector3.forward, volume, 10f, liquid);
        }

        private CorrodibleObject CreateWall(float resistance, float threshold, float soakRate = 0f, float wetDuration = 1f)
        {
            CorrosionProfile profile = ScriptableObject.CreateInstance<CorrosionProfile>();
            _created.Add(profile);
            SerializedObject profileData = new SerializedObject(profile);
            profileData.FindProperty("_resistance").floatValue = resistance;
            profileData.FindProperty("_threshold").floatValue = threshold;
            profileData.FindProperty("_corrosionPerLiter").floatValue = 1f;
            profileData.FindProperty("_soakRate").floatValue = soakRate;
            profileData.FindProperty("_wetDuration").floatValue = wetDuration;
            profileData.ApplyModifiedPropertiesWithoutUndo();

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            _created.Add(go);
            CorrodibleObject wall = go.AddComponent<CorrodibleObject>();
            SerializedObject wallData = new SerializedObject(wall);
            wallData.FindProperty("_profile").objectReferenceValue = profile;
            wallData.ApplyModifiedPropertiesWithoutUndo();
            return wall;
        }
    }
}
