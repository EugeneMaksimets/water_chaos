using NUnit.Framework;
using UnityEngine;
using WaterChaos.Hose;

namespace WaterChaos.Tests
{
    public class HosePathEndTests
    {
        // A 1 m high wall across x = 0.
        private sealed class WallQuery : IHoseObstacleQuery
        {
            public bool Linecast(Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
            {
                point = default;
                normal = Vector3.left;
                if (Mathf.Sign(from.x) == Mathf.Sign(to.x))
                    return false;

                float t = from.x / (from.x - to.x);
                point = Vector3.Lerp(from, to, t);
                return point.y < 1f;
            }

            public bool IsClear(Vector3 from, Vector3 to, float radius) => !Linecast(from, to, out _, out _);
        }

        private static readonly Vector3 Fixed = new Vector3(-3f, 0.4f, 0f);

        [Test]
        public void NothingInTheWay_ThePathStaysLow()
        {
            Vector3 low = new Vector3(-1f, 0.4f, 0f);

            Assert.AreEqual(low, HosePathEnd.Choose(new WallQuery(), Fixed, new Vector3(-1f, 1.2f, 0f), low));
        }

        [Test]
        public void OverAWall_ThePathFollowsTheHands_SoItGoesOverTheTop()
        {
            Vector3 hand = new Vector3(1f, 2f, 0f);

            Assert.AreEqual(hand, HosePathEnd.Choose(new WallQuery(), new Vector3(-3f, 1.5f, 0f), hand, new Vector3(1f, 0.4f, 0f)));
        }

        [Test]
        public void BehindAWallEvenAtHandHeight_ThePathStaysLow_SoItWrapsAroundTheEnd()
        {
            Vector3 low = new Vector3(1f, 0.4f, 0f);

            Assert.AreEqual(low, HosePathEnd.Choose(new WallQuery(), Fixed, new Vector3(1f, 0.9f, 0f), low));
        }
    }
}
