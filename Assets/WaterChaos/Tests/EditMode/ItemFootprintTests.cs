using NUnit.Framework;
using UnityEngine;
using WaterChaos.Items;

namespace WaterChaos.Tests
{
    public class ItemFootprintTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void GetRadius_CenteredBox_IsHalfDiagonalOfTopView()
        {
            var bounds = new Bounds(Vector3.zero, new Vector3(0.6f, 2f, 0.8f));

            float radius = ItemFootprint.GetRadius(Vector3.zero, bounds);

            Assert.AreEqual(0.5f, radius, Tolerance);
        }

        [Test]
        public void GetRadius_IgnoresHeight()
        {
            var low = new Bounds(Vector3.zero, new Vector3(0.6f, 0.1f, 0.8f));
            var tall = new Bounds(new Vector3(0f, 3f, 0f), new Vector3(0.6f, 5f, 0.8f));

            Assert.AreEqual(ItemFootprint.GetRadius(Vector3.zero, low), ItemFootprint.GetRadius(Vector3.zero, tall), Tolerance);
        }

        [Test]
        public void GetRadius_BoxAwayFromPivot_ReachesFarthestCorner()
        {
            var bounds = new Bounds(new Vector3(1f, 0f, 0f), new Vector3(2f, 1f, 0f));

            float radius = ItemFootprint.GetRadius(Vector3.zero, bounds);

            Assert.AreEqual(2f, radius, Tolerance);
        }

        [Test]
        public void GetRadius_NeverLessThanZero()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);

            Assert.AreEqual(0f, ItemFootprint.GetRadius(Vector3.zero, bounds), Tolerance);
        }
    }
}
