using NUnit.Framework;
using UnityEngine;
using WaterChaos.UI;

namespace WaterChaos.Tests
{
    public class RadialMenuLayoutTests
    {
        private const float DeadZone = 40f;
        private const float Tolerance = 0.001f;

        [Test]
        public void ThreeOptions_LeftTopRight()
        {
            Assert.AreEqual(0, RadialMenuLayout.GetIndex(new Vector2(-100f, 20f), 3, DeadZone));
            Assert.AreEqual(1, RadialMenuLayout.GetIndex(new Vector2(0f, 100f), 3, DeadZone));
            Assert.AreEqual(2, RadialMenuLayout.GetIndex(new Vector2(100f, 20f), 3, DeadZone));
        }

        [Test]
        public void TwoOptions_SplitAtTop()
        {
            Assert.AreEqual(0, RadialMenuLayout.GetIndex(new Vector2(-10f, 100f), 2, DeadZone));
            Assert.AreEqual(1, RadialMenuLayout.GetIndex(new Vector2(10f, 100f), 2, DeadZone));
        }

        [Test]
        public void InsideDeadZone_PicksNothing()
        {
            Assert.AreEqual(-1, RadialMenuLayout.GetIndex(new Vector2(10f, 10f), 3, DeadZone));
        }

        [Test]
        public void BelowCenter_PicksNearestEnd()
        {
            Assert.AreEqual(0, RadialMenuLayout.GetIndex(new Vector2(-100f, -50f), 3, DeadZone));
            Assert.AreEqual(2, RadialMenuLayout.GetIndex(new Vector2(100f, -50f), 3, DeadZone));
        }

        [Test]
        public void NoOptionsOrNaN_PicksNothing()
        {
            Assert.AreEqual(-1, RadialMenuLayout.GetIndex(new Vector2(0f, 100f), 0, DeadZone));
            Assert.AreEqual(-1, RadialMenuLayout.GetIndex(new Vector2(float.NaN, 100f), 3, DeadZone));
        }

        [Test]
        public void OneOption_CoversWholeHalfCircle()
        {
            Assert.AreEqual(0, RadialMenuLayout.GetIndex(new Vector2(-100f, 1f), 1, DeadZone));
            Assert.AreEqual(0, RadialMenuLayout.GetIndex(new Vector2(100f, 1f), 1, DeadZone));
            Assert.AreEqual(90f, RadialMenuLayout.CenterAngle(0, 1), Tolerance);
        }

        [Test]
        public void CenterDirection_OfMiddleOptionPointsUp()
        {
            Vector2 direction = RadialMenuLayout.CenterDirection(1, 3);

            Assert.AreEqual(0f, direction.x, Tolerance);
            Assert.AreEqual(1f, direction.y, Tolerance);
        }
    }
}
