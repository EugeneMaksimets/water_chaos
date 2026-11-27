using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class HoseGroundDrapeTests
    {
        private const float Spacing = 0.15f;
        private const float MaxSag = 2.5f;
        private const float HalfWidth = 0.05f;
        private const float CornerRadius = 0.4f;
        private const float BendCurvature = 1.5f;
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
        public void Build_KeepsAttachmentEndsInPlace()
        {
            CreateFloor(Origin);
            HoseModel path = CreatePath(Origin + new Vector3(0f, 1f, 0f), Origin + new Vector3(0f, 1.2f, 4f));

            int count = Build(path, out Vector3[] points);

            Assert.Greater(count, 2);
            AssertNear(path.Anchor, points[0]);
            AssertNear(path.End, points[count - 1]);
        }

        [Test]
        public void Build_GivenAttachments_DrawsFromThemInsteadOfPathEnds()
        {
            CreateFloor(Origin);
            HoseModel path = CreatePath(Origin + new Vector3(0f, 0.4f, 0f), Origin + new Vector3(0f, 0.4f, 4f));
            Vector3 start = Origin + new Vector3(0f, 1f, 0f);
            Vector3 end = Origin + new Vector3(0f, 1.3f, 4f);

            var drape = new HoseGroundDrape();
            int count = drape.Build(path, start, end, Ground, Spacing, MaxSag, HalfWidth, CornerRadius, BendCurvature);

            AssertNear(start, drape.Points[0]);
            AssertNear(end, drape.Points[count - 1]);
        }

        [Test]
        public void Build_PathAboveFloor_NeverGoesIntoFloor()
        {
            CreateFloor(Origin);
            HoseModel path = CreatePath(Origin + new Vector3(0f, 1f, 0f), Origin + new Vector3(0f, 1.2f, 4f));

            int count = Build(path, out Vector3[] points);

            for (int i = 0; i < count; i++)
                Assert.GreaterOrEqual(points[i].y, Origin.y + HalfWidth - Tolerance, $"point {i}");
        }

        [Test]
        public void Build_PathAboveFloor_MiddleLiesOnFloor()
        {
            CreateFloor(Origin);
            HoseModel path = CreatePath(Origin + new Vector3(0f, 1f, 0f), Origin + new Vector3(0f, 1.2f, 4f));

            int count = Build(path, out Vector3[] points);

            int checkedPoints = 0;
            for (int i = 0; i < count; i++)
            {
                float along = points[i].z - Origin.z;
                if (along < 1.8f || along > 2.2f)
                    continue;

                Assert.AreEqual(Origin.y + HalfWidth, points[i].y, Tolerance, $"point {i}");
                checkedPoints++;
            }

            Assert.Greater(checkedPoints, 0);
        }

        [Test]
        public void Build_PathAboveFloor_BendsDownWithoutVerticalDrop()
        {
            CreateFloor(Origin);
            HoseModel path = CreatePath(Origin + new Vector3(0f, 1f, 0f), Origin + new Vector3(0f, 1.2f, 4f));

            int count = Build(path, out Vector3[] points);

            // A hanging hose curves down: never steeper than about 63 degrees between neighbours.
            for (int i = 1; i < count; i++)
            {
                Vector3 step = points[i] - points[i - 1];
                float flat = new Vector2(step.x, step.z).magnitude;
                Assert.LessOrEqual(Mathf.Abs(step.y), flat * 2f + Tolerance, $"segment {i - 1}-{i}");
            }
        }

        [Test]
        public void Build_NoGroundWithinSag_HangsNoLowerThanMaxSag()
        {
            CreateFloor(Origin + Vector3.down * 10f);
            HoseModel path = CreatePath(Origin + Vector3.up, Origin + new Vector3(0f, 1f, 4f));

            int count = Build(path, out Vector3[] points);

            for (int i = 0; i < count; i++)
            {
                Assert.GreaterOrEqual(points[i].y, Origin.y + 1f - MaxSag - Tolerance, $"point {i}");
                Assert.LessOrEqual(points[i].y, Origin.y + 1f + Tolerance, $"point {i}");
            }

            Assert.Less(points[count / 2].y, Origin.y + 1f - 0.1f);
        }

        [Test]
        public void Build_WrapPointInTheAir_HoseGoesAroundItOnTheFloor()
        {
            CreateFloor(Origin);
            Vector3 wrap = Origin + new Vector3(2f, 1f, 2f);
            HoseModel path = CreatePath(Origin + Vector3.up, Origin + new Vector3(4f, 1f, 0f));
            path.AddWrap(wrap, Vector3.up);

            int count = Build(path, out Vector3[] points);

            Vector3 onFloor = new Vector3(wrap.x, Origin.y + HalfWidth, wrap.z);
            float closest = float.MaxValue;
            for (int i = 0; i < count; i++)
                closest = Mathf.Min(closest, Vector3.Distance(points[i], onFloor));

            Assert.Less(closest, 0.1f);
        }

        [Test]
        public void Build_WrapCorner_HasNoSharpTurn()
        {
            CreateFloor(Origin);
            HoseModel path = CreatePath(Origin + Vector3.up, Origin + new Vector3(4f, 1f, 0f));
            path.AddWrap(Origin + new Vector3(2f, 1f, 2f), Vector3.up);

            int count = Build(path, out Vector3[] points);

            for (int i = 2; i < count; i++)
            {
                Vector3 before = Flat(points[i - 1] - points[i - 2]);
                Vector3 after = Flat(points[i] - points[i - 1]);
                if (before.sqrMagnitude < 1e-6f || after.sqrMagnitude < 1e-6f)
                    continue;

                Assert.LessOrEqual(Vector3.Angle(before, after), 40f, $"turn at point {i - 1}");
            }
        }

        [Test]
        public void Build_VeryLongPath_StaysWithinBuffer()
        {
            HoseModel path = CreatePath(Origin, Origin + new Vector3(0f, 0f, 1000f), 2000f);

            int count = Build(path, out Vector3[] points);

            Assert.LessOrEqual(count, HoseGroundDrape.MaxPoints);
            AssertNear(path.End, points[count - 1]);
        }

        private static int Build(HoseModel path, out Vector3[] points)
        {
            var drape = new HoseGroundDrape();
            int count = drape.Build(path, path.Anchor, path.End, Ground, Spacing, MaxSag, HalfWidth, CornerRadius, BendCurvature);
            points = drape.Points;
            return count;
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

        private static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.Less((expected - actual).magnitude, Tolerance, $"expected {expected}, got {actual}");
        }
    }
}
