using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    /// <summary>The physical hose lies on the ground and never passes through a thin wall it goes around.</summary>
    public class HoseVerletSimulationTests
    {
        private const float Step = 0.02f;
        private const float Radius = 0.05f;

        private readonly List<GameObject> _created = new List<GameObject>();
        private BoxCollider _wall;

        [SetUp]
        public void SetUp()
        {
            CreateBox("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f));
            // A thin fence across the straight line, 4 m long (z -2..2), 1 m high.
            _wall = CreateBox("Fence", new Vector3(0f, 0.5f, 0f), new Vector3(0.14f, 1f, 4f));
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _created)
                Object.DestroyImmediate(go);
            _created.Clear();
        }

        [Test]
        public void HoseAroundAFence_NeverCrossesIt_AndLiesOnTheGround()
        {
            Vector3 start = new Vector3(-2f, 0.05f, 0f);
            Vector3 end = new Vector3(2f, 0.05f, 0f);
            var path = new HoseModel(20f);
            path.Reset(start, end);
            // The gameplay path goes around the fence's end.
            path.AddWrap(new Vector3(0f, 0.4f, 2.2f), Vector3.up);

            HoseVerletSimulation rope = CreateRope(path.MaxLength);
            rope.Reset(path, start, end);
            for (int i = 0; i < 300; i++)
                rope.Step(start, end, path.Length * 1.15f, Physics.gravity, Step);

            Vector3[] points = new Vector3[HoseTubeMesh.MaxRings];
            int count = rope.BuildSmoothPoints(points, 1);
            for (int i = 0; i < count - 1; i++)
            {
                Assert.IsFalse(_wall.Raycast(new Ray(points[i], points[i + 1] - points[i]), out _, Vector3.Distance(points[i], points[i + 1])),
                    $"Piece {i} passes through the fence.");
            }

            Assert.Less(points[count / 4].y, 0.3f, "The hose should lie on the ground away from the fence.");
        }

        [Test]
        public void PulledTautToTheOtherSide_IsStoppedByTheFence()
        {
            // Laid on one side, then both ends move past the fence with no wrap: the rope must not be dragged through.
            Vector3 start = new Vector3(-2f, 0.05f, 0f);
            Vector3 end = new Vector3(-1f, 0.05f, 0f);
            var path = new HoseModel(20f);
            path.Reset(start, end);

            HoseVerletSimulation rope = CreateRope(path.MaxLength);
            rope.Reset(path, start, end);
            for (int i = 0; i < 50; i++)
                rope.Step(start, end, 1.15f, Physics.gravity, Step);

            Vector3[] before = new Vector3[HoseTubeMesh.MaxRings];
            int count = rope.BuildSmoothPoints(before, 1);
            Vector3 middle = before[count / 2];

            Vector3 farEnd = new Vector3(2f, 0.05f, 0f);
            path.SetEnd(farEnd);
            for (int i = 0; i < 50; i++)
                rope.Step(start, farEnd, 1.15f, Physics.gravity, Step);

            Vector3[] after = new Vector3[HoseTubeMesh.MaxRings];
            rope.BuildSmoothPoints(after, 1);
            Assert.Less(after[count / 2].x, 0f, $"The middle of the hose ({middle} -> {after[count / 2]}) was pulled through the fence.");
        }

        [Test]
        public void FromAPumpToTheHands_TheMiddleLiesOnTheGround()
        {
            // Like the transfer hose: the pump end low, the carried end at hand height 10 m away, no obstacles between.
            Object.DestroyImmediate(_wall.gameObject);
            Physics.SyncTransforms();
            Vector3 start = new Vector3(-8f, 0.6f, 5f);
            Vector3 end = new Vector3(2f, 1.4f, 5f);
            var path = new HoseModel(30f);
            path.Reset(start, end);

            HoseVerletSimulation rope = CreateRope(path.MaxLength);
            rope.Reset(path, start, end);
            for (int i = 0; i < 300; i++)
                rope.Step(start, end, path.Length * 1.15f, Physics.gravity, Step);

            Vector3[] points = new Vector3[HoseTubeMesh.MaxRings];
            int count = rope.BuildSmoothPoints(points, 1);
            Assert.Less(points[count / 2].y, 0.3f, $"The middle of the hose hangs in the air at {points[count / 2]}.");
        }

        [Test]
        public void CarriedAroundTheEndOfAFence_NeverCrossesIt()
        {
            // The real path solver and physics: the player carries the hose from one side of a fence, around its end, to the other.
            var owner = new GameObject("Rope Owner");
            _created.Add(owner);
            var solver = new HosePathSolver(new PhysicsHoseObstacleQuery(~0), 0.08f, 32);
            var rope = new HoseRope();
            Vector3 start = new Vector3(-3f, 0.05f, 0f);
            Vector3[] route = { new Vector3(-1f, 1.2f, 0f), new Vector3(-1f, 1.2f, 3f), new Vector3(1f, 1.2f, 3f), new Vector3(1.5f, 1.2f, 0f) };
            var path = new HoseModel(30f);
            path.Reset(start, Low(route[0]));
            Vector3 hand = route[0];

            for (int leg = 1; leg < route.Length; leg++)
            {
                while ((route[leg] - hand).sqrMagnitude > 1e-4f)
                {
                    hand = Vector3.MoveTowards(hand, route[leg], 0.1f);
                    solver.Step(path, Low(hand));
                    rope.Step(owner.transform, path, start, hand, ~0, c => false, Radius);
                    AssertDoesNotCross(rope, _wall);
                }
            }

            for (int i = 0; i < 150; i++)
            {
                rope.Step(owner.transform, path, start, hand, ~0, c => false, Radius);
                AssertDoesNotCross(rope, _wall);
            }
        }

        [Test]
        public void WalkingAroundATreeWithTheHose_NeverCrossesIt_NeverOverStretches()
        {
            // The game in small: the real path solver and physics, the player walks around a tree, 10 cm per physics step.
            Object.DestroyImmediate(_wall.gameObject);
            BoxCollider tree = CreateBox("Tree", new Vector3(0f, 1.5f, 0f), new Vector3(0.4f, 3f, 0.4f));
            Physics.SyncTransforms();

            var owner = new GameObject("Rope Owner");
            _created.Add(owner);
            var query = new PhysicsHoseObstacleQuery(~0);
            var solver = new HosePathSolver(query, 0.08f, 32);
            var rope = new HoseRope();
            Vector3 start = new Vector3(-5f, 0.05f, 0f);
            Vector3[] route = { new Vector3(-2f, 1.2f, 1f), new Vector3(2f, 1.2f, 1f), new Vector3(2f, 1.2f, -1f), new Vector3(-1f, 1.2f, -1f) };
            var path = new HoseModel(30f);
            path.Reset(start, Low(route[0]));
            Vector3 hand = route[0];

            for (int leg = 1; leg < route.Length; leg++)
            {
                while ((route[leg] - hand).sqrMagnitude > 1e-4f)
                {
                    hand = Vector3.MoveTowards(hand, route[leg], 0.1f);
                    solver.Step(path, Low(hand));
                    rope.Step(owner.transform, path, start, hand, ~0, c => false, Radius);
                    AssertDoesNotCross(rope, tree);
                }
            }

            for (int i = 0; i < 150; i++)
                rope.Step(owner.transform, path, start, hand, ~0, c => false, Radius);
            AssertDoesNotCross(rope, tree);

            int count = rope.BuildPoints(out Vector3[] points);
            Assert.Greater(path.WrapCount, 0, "The path should wrap around the tree.");
            Assert.Less(points[count / 3].y, 0.3f, $"The hose should lie on the ground, not hang at {points[count / 3]}.");
        }

        // The path finds obstacles low, like the hoses in the game.
        private static Vector3 Low(Vector3 hand) => new Vector3(hand.x, 0.4f, hand.z);

        private static void AssertDoesNotCross(HoseRope rope, Collider obstacle)
        {
            int count = rope.BuildPoints(out Vector3[] points);
            for (int i = 0; i < count - 1; i++)
            {
                float distance = Vector3.Distance(points[i], points[i + 1]);
                Assert.IsFalse(distance > 1e-4f && obstacle.Raycast(new Ray(points[i], points[i + 1] - points[i]), out _, distance),
                    $"Piece {i} ({points[i]} -> {points[i + 1]}) passes through {obstacle.name}.");
            }
        }

        private HoseVerletSimulation CreateRope(float maxLength)
        {
            var owner = new GameObject("Rope Owner");
            _created.Add(owner);
            int segments = Mathf.CeilToInt(maxLength / 0.3f);
            return new HoseVerletSimulation(segments, owner.transform, ~0, c => false, Radius, 0.98f, 0.3f, 0.9f, 12);
        }

        private BoxCollider CreateBox(string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.transform.position = center;
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }
    }
}
