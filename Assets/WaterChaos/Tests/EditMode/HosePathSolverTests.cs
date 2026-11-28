using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class HosePathSolverTests
    {
        private const float SurfaceOffset = 0.08f;
        private const float StepSize = 0.05f;
        private const float Tolerance = 1e-3f;

        // A 2 x 2 x 2 m box at the origin; the anchor is south of it.
        private static readonly Bounds Box = new Bounds(Vector3.zero, new Vector3(2f, 2f, 2f));
        private static readonly Vector3 Anchor = new Vector3(0f, 0f, -5f);
        private static readonly Vector3 EastOfBox = new Vector3(3f, 0f, 3f);
        private static readonly Vector3 NorthOfBox = new Vector3(0f, 0f, 3f);

        /// <summary>Exact segment test against one box, standing in for physics. The box can be removed.</summary>
        private sealed class BoxQuery : IHoseObstacleQuery
        {
            private readonly Bounds _box;

            public BoxQuery() : this(Box) { }

            public BoxQuery(Bounds box)
            {
                _box = box;
            }

            public bool Exists { get; set; } = true;

            public bool Linecast(Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
            {
                point = default;
                normal = default;
                return Exists && Intersect(_box, from, to, out point, out normal);
            }

            public bool IsClear(Vector3 from, Vector3 to, float radius)
            {
                if (!Exists)
                    return true;

                Bounds grown = _box;
                grown.Expand(radius * 2f);
                return !Intersect(grown, from, to, out _, out _);
            }

            // Slab method. Like Physics.Raycast, a segment starting inside the box does not hit it.
            private static bool Intersect(Bounds box, Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
            {
                point = default;
                normal = default;

                Vector3 delta = to - from;
                float enter = 0f;
                float exit = 1f;
                int hitAxis = -1;
                float hitSign = 0f;

                for (int axis = 0; axis < 3; axis++)
                {
                    float origin = from[axis];
                    float direction = delta[axis];
                    float min = box.min[axis];
                    float max = box.max[axis];

                    if (Mathf.Abs(direction) < 1e-8f)
                    {
                        if (origin < min || origin > max)
                            return false;
                        continue;
                    }

                    float near = (min - origin) / direction;
                    float far = (max - origin) / direction;
                    float sign = -1f;
                    if (near > far)
                    {
                        (near, far) = (far, near);
                        sign = 1f;
                    }

                    if (near > enter)
                    {
                        enter = near;
                        hitAxis = axis;
                        hitSign = sign;
                    }

                    exit = Mathf.Min(exit, far);
                    if (enter > exit)
                        return false;
                }

                if (hitAxis < 0)
                    return false;

                point = from + delta * enter;
                normal[hitAxis] = hitSign;
                return true;
            }
        }

        private static HosePathSolver CreateSolver() => new HosePathSolver(new BoxQuery(), SurfaceOffset, 32);

        private static void Walk(HosePathSolver solver, HoseModel hose, Vector3 to)
        {
            Vector3 position = hose.End;
            while ((to - position).magnitude > StepSize)
            {
                position = Vector3.MoveTowards(position, to, StepSize);
                solver.Step(hose, position);
                AssertPathAvoidsBox(hose);
            }

            solver.Step(hose, to);
            AssertPathAvoidsBox(hose);
        }

        private static void AssertPathAvoidsBox(HoseModel hose)
        {
            BoxQuery query = new BoxQuery();
            for (int i = 0; i < hose.PointCount - 1; i++)
            {
                bool hit = query.Linecast(hose.GetPoint(i), hose.GetPoint(i + 1), out _, out _);
                Assert.IsFalse(hit, $"Segment {i} passes through the box.");
            }
        }

        [Test]
        public void NoObstacle_StaysStraight()
        {
            HoseModel hose = new HoseModel(15f);
            hose.Reset(Anchor, EastOfBox);

            Walk(CreateSolver(), hose, new Vector3(6f, 0f, 3f));

            Assert.AreEqual(0, hose.WrapCount);
            Assert.AreEqual(Vector3.Distance(Anchor, hose.End), hose.Length, Tolerance);
        }

        [Test]
        public void WalkingBehindObstacle_WrapsAroundIt()
        {
            HoseModel hose = new HoseModel(15f);
            hose.Reset(Anchor, EastOfBox);

            Walk(CreateSolver(), hose, NorthOfBox);

            Assert.GreaterOrEqual(hose.WrapCount, 1);
            Assert.Greater(hose.Length, Vector3.Distance(Anchor, NorthOfBox) + Tolerance);
        }

        [Test]
        public void WalkingBack_UnwrapsAndRestoresLength()
        {
            HosePathSolver solver = CreateSolver();
            HoseModel hose = new HoseModel(15f);
            hose.Reset(Anchor, EastOfBox);
            Walk(solver, hose, NorthOfBox);

            Walk(solver, hose, EastOfBox);

            // Back where it started: no leftover wrap points, the same length as before the trip.
            Assert.AreEqual(0, hose.WrapCount);
            Assert.AreEqual(Vector3.Distance(Anchor, EastOfBox), hose.Length, Tolerance);
            Assert.AreEqual(15f - Vector3.Distance(Anchor, EastOfBox), hose.RemainingLength, Tolerance);
        }

        [Test]
        public void StandingStill_DoesNotFlicker()
        {
            HosePathSolver solver = CreateSolver();
            HoseModel hose = new HoseModel(15f);
            hose.Reset(Anchor, EastOfBox);
            Walk(solver, hose, NorthOfBox);
            int wraps = hose.WrapCount;

            for (int i = 0; i < 10; i++)
                solver.Step(hose, NorthOfBox);

            Assert.AreEqual(wraps, hose.WrapCount);
        }

        [Test]
        public void ThinPost_SweptPastInOneBigStep_IsStillWrappedAround()
        {
            // A 10 cm post between the anchor and the end; the end jumps from its left to its right in one step,
            // so neither the old nor the new straight line touches it.
            Bounds post = new Bounds(Vector3.zero, new Vector3(0.1f, 2f, 0.1f));
            BoxQuery query = new BoxQuery(post);
            HosePathSolver solver = new HosePathSolver(query, SurfaceOffset, 32);
            HoseModel hose = new HoseModel(15f);
            hose.Reset(Anchor, new Vector3(-2f, 0f, 2f));

            solver.Step(hose, new Vector3(2f, 0f, 2f));

            Assert.Greater(hose.WrapCount, 0);
            for (int i = 0; i < hose.PointCount - 1; i++)
                Assert.IsFalse(query.Linecast(hose.GetPoint(i), hose.GetPoint(i + 1), out _, out _), $"Segment {i} passes through the post.");
        }

        [Test]
        public void ObstacleGone_WrapIsReleased_EvenIfTheHoseStillBendsTheSameWay()
        {
            BoxQuery query = new BoxQuery();
            HosePathSolver solver = new HosePathSolver(query, SurfaceOffset, 32);
            HoseModel hose = new HoseModel(15f);
            hose.Reset(Anchor, EastOfBox);
            Walk(solver, hose, NorthOfBox);
            Assert.Greater(hose.WrapCount, 0);

            query.Exists = false;
            solver.Step(hose, NorthOfBox);

            Assert.AreEqual(0, hose.WrapCount);
            Assert.AreEqual(Vector3.Distance(Anchor, NorthOfBox), hose.Length, Tolerance);
        }

        [Test]
        public void NaNEnd_IsIgnored()
        {
            HoseModel hose = new HoseModel(15f);
            hose.Reset(Anchor, EastOfBox);
            float length = hose.Length;

            CreateSolver().Step(hose, new Vector3(float.NaN, 0f, 0f));

            Assert.AreEqual(length, hose.Length, Tolerance);
            Assert.AreEqual(EastOfBox, hose.End);
        }
    }
}
