using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class PhysicsHoseObstacleQueryTests
    {
        // Far from the scene origin so nothing else in an open scene is hit.
        private static readonly Vector3 Origin = new Vector3(-5000f, 0f, 5000f);
        private static readonly LayerMask Mask = 1;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void Linecast_ThroughOwnItem_IsNotBlocked()
        {
            GameObject item = CreateBox(Origin);
            var query = new PhysicsHoseObstacleQuery(Mask);
            query.SetIgnored(null, null, item.transform);

            bool hit = query.Linecast(Origin + Vector3.back * 3f, Origin + Vector3.forward * 3f, out _, out _);

            Assert.IsFalse(hit);
        }

        [Test]
        public void Linecast_ThroughOtherObject_IsBlocked()
        {
            CreateBox(Origin);
            GameObject item = CreateBox(Origin + Vector3.up * 10f);
            var query = new PhysicsHoseObstacleQuery(Mask);
            query.SetIgnored(null, null, item.transform);

            bool hit = query.Linecast(Origin + Vector3.back * 3f, Origin + Vector3.forward * 3f, out _, out _);

            Assert.IsTrue(hit);
        }

        private GameObject CreateBox(Vector3 position)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.layer = 0;
            box.transform.position = position;
            _created.Add(box);
            Physics.SyncTransforms();
            return box;
        }
    }
}
