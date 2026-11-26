using NUnit.Framework;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class LiquidLabelColorsTests
    {
        [Test]
        public void Water_IsWhite()
        {
            Assert.AreEqual(Color.white, LiquidLabelColors.Get(LiquidType.Water));
        }

        [Test]
        public void Gasoline_IsYellow()
        {
            Color color = LiquidLabelColors.Get(LiquidType.Gasoline);

            Assert.AreEqual(1f, color.r, 1e-4f);
            Assert.Greater(color.g, 0.8f);
            Assert.Less(color.b, 0.3f);
            Assert.AreEqual(1f, color.a, 1e-4f);
        }
    }
}
