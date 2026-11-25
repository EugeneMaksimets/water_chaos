using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class WaterReservoirDetectorTests
    {
        private const float MaxDistance = 4f;

        /// <summary>Far from anything in the open scene, so only test objects are hit.</summary>
        private static readonly Vector3 Origin = new Vector3(0f, 10000f, 0f);

        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly RaycastHit[] _buffer = new RaycastHit[16];

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void ReservoirAt3Meters_IsFound()
        {
            WaterReservoirComponent reservoir = CreateReservoirVolume(3f);

            Assert.AreSame(reservoir, Find(Vector3.forward));
        }

        [Test]
        public void ReservoirBeyond4Meters_IsNotFound()
        {
            CreateReservoirVolume(5f);

            Assert.IsNull(Find(Vector3.forward));
        }

        [Test]
        public void LookingAway_FindsNothing()
        {
            CreateReservoirVolume(3f);

            Assert.IsNull(Find(Vector3.back));
        }

        [Test]
        public void OrdinaryObject_IsNotAReservoir()
        {
            CreateBox("Crate", 3f, false);

            Assert.IsNull(Find(Vector3.forward));
        }

        [Test]
        public void WallInFront_BlocksReservoir()
        {
            CreateReservoirVolume(3f);
            CreateBox("Wall", 2f, false);

            Assert.IsNull(Find(Vector3.forward));
        }

        [Test]
        public void OtherTriggerInFront_DoesNotBlockReservoir()
        {
            WaterReservoirComponent reservoir = CreateReservoirVolume(3f);
            CreateBox("Wind Zone", 1.5f, true);

            Assert.AreSame(reservoir, Find(Vector3.forward));
        }

        [Test]
        public void TankWallUnderTarget_ResolvesToItsReservoir()
        {
            WaterReservoirComponent reservoir = CreateReservoirVolume(3.5f);
            GameObject wall = CreateBox("Tank Wall", 2.5f, false);
            GameObject tank = Track(new GameObject("Tank"));
            wall.transform.SetParent(tank.transform, true);

            WaterReservoirTarget target = tank.AddComponent<WaterReservoirTarget>();
            SerializedObject data = new SerializedObject(target);
            data.FindProperty("_reservoir").objectReferenceValue = reservoir;
            data.ApplyModifiedProperties();

            Assert.AreSame(reservoir, Find(Vector3.forward));
        }

        [Test]
        public void DifferentReservoirs_AreFoundSeparately()
        {
            WaterReservoirComponent ahead = CreateReservoirVolume(3f);
            WaterReservoirComponent behind = CreateReservoirVolume(-3f);

            Assert.AreSame(ahead, Find(Vector3.forward));
            Assert.AreSame(behind, Find(Vector3.back));
        }

        private WaterReservoirComponent Find(Vector3 direction)
        {
            Physics.SyncTransforms();
            return WaterReservoirDetector.Find(new Ray(Origin, direction), MaxDistance, ~0, _buffer);
        }

        /// <summary>1 m reservoir trigger whose near face is <paramref name="distance"/> meters along Z from the origin.</summary>
        private WaterReservoirComponent CreateReservoirVolume(float distance)
        {
            GameObject volume = CreateBox("Reservoir", distance, true);
            return volume.AddComponent<WaterReservoirComponent>();
        }

        /// <summary>1 m box whose near face is <paramref name="distance"/> meters along Z from the origin.</summary>
        private GameObject CreateBox(string name, float distance, bool trigger)
        {
            GameObject box = Track(new GameObject(name));
            float center = distance + Mathf.Sign(distance) * 0.5f;
            box.transform.position = Origin + Vector3.forward * center;
            box.AddComponent<BoxCollider>().isTrigger = trigger;
            return box;
        }

        private GameObject Track(GameObject go)
        {
            _created.Add(go);
            return go;
        }
    }
}
