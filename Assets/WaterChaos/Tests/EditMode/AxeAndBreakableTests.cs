using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Items;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>The axe swings on a cooldown; its blows break doors after a few hits and shatter glass at once.</summary>
    public class AxeAndBreakableTests
    {
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

        // ───────────── Swing ─────────────

        [Test]
        public void Swing_WaitsForTheCooldown()
        {
            AxeSwingModel axe = new AxeSwingModel(0.5f);

            Assert.IsTrue(axe.TrySwing(0f));
            Assert.IsFalse(axe.TrySwing(0.3f));
            Assert.IsTrue(axe.TrySwing(0.5f));
        }

        [Test]
        public void ToolHit_SanitizesItsValues()
        {
            ToolHit hit = new ToolHit(Vector3.zero, new Vector3(0f, 0f, 5f), float.NaN);

            Assert.AreEqual(0f, hit.Damage);
            Assert.AreEqual(Vector3.forward, hit.Direction);
            Assert.AreEqual(0f, new ToolHit(Vector3.zero, Vector3.up, -2f).Damage);
        }

        // ───────────── Durability ─────────────

        [Test]
        public void Durability_BreaksOnTheLastBlowOnly()
        {
            DurabilityModel door = new DurabilityModel(3f);

            Assert.IsFalse(door.ApplyHit(1f));
            Assert.IsFalse(door.ApplyHit(1f));
            Assert.IsTrue(door.ApplyHit(1f));
            Assert.IsFalse(door.ApplyHit(1f), "a broken door does not break again");
            Assert.IsTrue(door.IsBroken);
            Assert.AreEqual(1f, door.Progress, 1e-4f);
        }

        [Test]
        public void Durability_IgnoresEmptyBlows()
        {
            DurabilityModel door = new DurabilityModel(1f);

            door.ApplyHit(0f);
            door.ApplyHit(float.NaN);
            door.ApplyHit(-5f);

            Assert.IsFalse(door.IsBroken);
            Assert.AreEqual(0f, door.Damage);
        }

        // ───────────── Door ─────────────

        [Test]
        public void Door_BreaksAfterFourBlows_AndStopsBlocking()
        {
            BreakableDoor door = CreateDoor();
            int broken = 0;
            door.Broken += _ => broken++;

            for (int i = 0; i < 3; i++)
                door.ReceiveToolHit(Blow());
            Assert.IsFalse(door.IsBroken);
            Assert.IsTrue(door.GetComponent<Collider>().enabled);

            door.ReceiveToolHit(Blow());
            door.ReceiveToolHit(Blow());

            Assert.IsTrue(door.IsBroken);
            Assert.AreEqual(1, broken);
            Assert.IsFalse(door.GetComponent<Collider>().enabled);
            Assert.IsFalse(door.GetComponent<Renderer>().enabled);
        }

        [Test]
        public void Door_IgnoresWater()
        {
            // Water only pushes doors (WaterPushHandler); a door is never an impact receiver that breaks.
            BreakableDoor door = CreateDoor();

            Assert.IsFalse(typeof(IWaterImpactReceiver).IsAssignableFrom(typeof(BreakableDoor)));
            Assert.AreEqual(0f, door.Progress);
        }

        [Test]
        public void Durability_CrackStagesGrowWithDamage()
        {
            DurabilityModel door = new DurabilityModel(4f);
            Assert.AreEqual(0, door.VisibleCrackStages(2), "intact: no cracks");

            door.ApplyHit(1f);
            Assert.AreEqual(1, door.VisibleCrackStages(2), "the first blow already cracks it");
            door.ApplyHit(1f);
            Assert.AreEqual(1, door.VisibleCrackStages(2));
            door.ApplyHit(1f);
            Assert.AreEqual(2, door.VisibleCrackStages(2));

            door.ApplyHit(1f);
            Assert.AreEqual(0, door.VisibleCrackStages(2), "broken: the leaf and its cracks are gone");
        }

        [Test]
        public void Door_ShowsCracksStageByStage_ThenReleasesItsPlanks()
        {
            BreakableDoor door = CreateDoor(out GameObject[] cracks, out Rigidbody[] planks);

            door.ReceiveToolHit(Blow());
            Assert.IsTrue(cracks[0].activeSelf);
            Assert.IsFalse(cracks[1].activeSelf);

            door.ReceiveToolHit(Blow());
            door.ReceiveToolHit(Blow());
            Assert.IsTrue(cracks[1].activeSelf);
            Assert.IsFalse(planks[0].gameObject.activeSelf, "planks wait until the door breaks");

            door.ReceiveToolHit(Blow());

            Assert.IsTrue(door.IsBroken);
            Assert.IsFalse(cracks[0].activeSelf);
            Assert.IsFalse(cracks[1].activeSelf);
            foreach (Rigidbody plank in planks)
            {
                Assert.IsTrue(plank.gameObject.activeSelf);
                Assert.IsFalse(plank.isKinematic);
            }
        }

        [Test]
        public void Door_BlowsDoNotSwingIt()
        {
            BreakableDoor door = CreateDoor();
            Rigidbody leaf = door.gameObject.AddComponent<Rigidbody>();

            door.ReceiveToolHit(Blow());

            Assert.AreEqual(Vector3.zero, leaf.linearVelocity);
            Assert.AreEqual(Vector3.zero, leaf.GetAccumulatedForce());
        }

        [Test]
        public void BrokenDoor_LeavesItsHinge()
        {
            BreakableDoor door = CreateDoor();
            door.gameObject.AddComponent<HingeJoint>();

            for (int i = 0; i < 4; i++)
                door.ReceiveToolHit(Blow());

            Assert.IsNull(door.GetComponent<HingeJoint>());
            Assert.IsTrue(door.GetComponent<Rigidbody>().isKinematic);
        }

        [Test]
        public void DestroyedDoor_IsHandledSafely()
        {
            BreakableDoor door = CreateDoor();
            Object.DestroyImmediate(door.gameObject);

            Assert.DoesNotThrow(() => door.ReceiveToolHit(Blow()));
        }

        // ───────────── Glass ─────────────

        [Test]
        public void Glass_BreakAtOnce_FromAnyState()
        {
            GlassPaneModel glass = new GlassPaneModel(60f, 120f, 360f);
            glass.ApplyImpact(100f, 1.5f);
            Assert.AreEqual(GlassPaneState.Cracked, glass.State);

            Assert.IsTrue(glass.Break());

            Assert.AreEqual(GlassPaneState.Broken, glass.State);
            Assert.IsFalse(glass.Break(), "already broken");
            Assert.IsFalse(glass.ApplyImpact(1000f, 1f));
        }

        [Test]
        public void Glass_IsAToolTarget()
        {
            GameObject pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(pane);

            Assert.IsNotNull(pane.AddComponent<WaterBreakableGlass>() as IToolHitReceiver);
        }

        // ───────────── Helpers ─────────────

        private static ToolHit Blow()
        {
            return new ToolHit(Vector3.zero, Vector3.forward, 1f);
        }

        private BreakableDoor CreateDoor()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Door";
            _created.Add(go);
            return go.AddComponent<BreakableDoor>();
        }

        private BreakableDoor CreateDoor(out GameObject[] cracks, out Rigidbody[] planks)
        {
            BreakableDoor door = CreateDoor();
            cracks = new GameObject[2];
            for (int i = 0; i < cracks.Length; i++)
            {
                cracks[i] = new GameObject("Cracks " + i);
                cracks[i].transform.SetParent(door.transform, false);
                cracks[i].SetActive(false);
            }

            planks = new Rigidbody[3];
            for (int i = 0; i < planks.Length; i++)
            {
                GameObject plank = new GameObject("Plank " + i);
                plank.transform.SetParent(door.transform, false);
                planks[i] = plank.AddComponent<Rigidbody>();
                planks[i].isKinematic = true;
                plank.SetActive(false);
            }

            var data = new UnityEditor.SerializedObject(door);
            SerializedArray(data, "_crackStages", cracks);
            SerializedArray(data, "_fragments", planks);
            data.ApplyModifiedPropertiesWithoutUndo();
            return door;
        }

        private static void SerializedArray(UnityEditor.SerializedObject data, string field, Object[] values)
        {
            UnityEditor.SerializedProperty list = data.FindProperty(field);
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
