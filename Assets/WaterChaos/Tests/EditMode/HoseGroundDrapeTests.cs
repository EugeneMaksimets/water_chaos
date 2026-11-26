using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class HoseGroundDrapeTests
    {
        private const float Spacing = 0.4f;
        private const float MaxSag = 2.5f;
        private const float HalfWidth = 0.05f;
        private const float Tolerance = 1e-3f;

        // Far from the scene origin so nothing else in an open scene is hit.
        private static readonly Vector3 Origin = new Vector3(5000f, 0f, 5000f);
        private static readonly LayerMask Ground = 1;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void Build_PathAboveFloor_KeepsEndsAndLaysSamplesOnFloor()
        {
            CreateFloor(Origin);
            HoseModel path = CreatePath(Origin + new Vector3(0f, 1f, 0f), Origin + new Vector3(0f, 1.2f, 4f));
            var drape = new HoseGroundDrape();

            int count = drape.Build(path, Ground, Spacing, MaxSag, HalfWidth);

            Assert.Greater(count, 2);
            AssertNear(path.Anchor, drape.Points[0]);
            AssertNear(path.End, drape.Points[count - 1]);
            for (int i = 1; i < count - 1; i++)
                Assert.AreEqual(Origin.y + HalfWidth, drape.Points[i].y, Tolerance);
        }

        [Test]
        public void Build_NoGroundWithinSag_KeepsStraightLine()
        {
            CreateFloor(Origin + Vector3.down * 10f);
            HoseModel path = CreatePath(Origin + Vector3.up, Origin + new Vector3(0f, 1f, 4f));
            var drape = new HoseGroundDrape();

            int count = drape.Build(path, Ground, Spacing, MaxSag, HalfWidth);

            for (int i = 0; i < count; i++)
                Assert.AreEqual(Origin.y + 1f, drape.Points[i].y, Tolerance);
        }

        [Test]
        public void Build_KeepsWrapPointsInOrder()
        {
            CreateFloor(Origin);
            Vector3 wrap = Origin + new Vector3(2f, 0.5f, 2f);
            HoseModel path = CreatePath(Origin + Vector3.up, Origin + new Vector3(4f, 1f, 0f));
            path.AddWrap(wrap, Vector3.right);
            var drape = new HoseGroundDrape();

            int count = drape.Build(path, Ground, Spacing, MaxSag, HalfWidth);

            int found = -1;
            for (int i = 0; i < count; i++)
            {
                if ((drape.Points[i] - wrap).sqrMagnitude < Tolerance * Tolerance)
                    found = i;
            }

            Assert.Greater(found, 0);
            Assert.Less(found, count - 1);
        }

        [Test]
        public void Build_VeryLongPath_StaysWithinBuffer()
        {
            HoseModel path = CreatePath(Origin, Origin + new Vector3(0f, 0f, 1000f), 2000f);
            var drape = new HoseGroundDrape();

            int count = drape.Build(path, Ground, Spacing, MaxSag, HalfWidth);

            Assert.LessOrEqual(count, HoseGroundDrape.MaxPoints);
            AssertNear(path.End, drape.Points[count - 1]);
        }

        private void CreateFloor(Vector3 top)
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = 0;
            floor.transform.localScale = new Vector3(50f, 1f, 50f);
            floor.transform.position = top + Vector3.down * 0.5f;
            _created.Add(floor);
            Physics.SyncTransforms();
        }

        private static HoseModel CreatePath(Vector3 anchor, Vector3 end, float maxLength = 50f)
        {
            var path = new HoseModel(maxLength);
            path.Reset(anchor, end);
            return path;
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.Less((expected - actual).magnitude, Tolerance, $"expected {expected}, got {actual}");
        }
    }
}
