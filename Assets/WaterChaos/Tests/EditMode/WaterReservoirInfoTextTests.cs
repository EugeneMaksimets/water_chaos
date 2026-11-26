using NUnit.Framework;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    public class WaterReservoirInfoTextTests
    {
        [Test]
        public void FormatVolume_ShowsCurrentAndMaxInLiters()
        {
            Assert.AreEqual("750 л / 2000 л", WaterReservoirInfoText.FormatVolume(750, 2000));
        }

        [Test]
        public void PanelColor_IsYellow_RedWhenEmpty()
        {
            Assert.AreEqual(WaterReservoirInfoText.TextColor, WaterReservoirInfoText.GetColor(750));
            Assert.AreEqual(WaterReservoirInfoText.EmptyColor, WaterReservoirInfoText.GetColor(0));

            Color yellow = WaterReservoirInfoText.TextColor;
            Assert.AreEqual(1f, yellow.r, 1e-4f);
            Assert.Greater(yellow.g, 0.8f);
            Assert.Less(yellow.b, 0.3f);

            Color red = WaterReservoirInfoText.EmptyColor;
            Assert.Greater(red.r, 0.8f);
            Assert.Less(red.g, 0.4f);
        }

        [Test]
        public void BarLabel_NeverNamesTheLiquid()
        {
            Assert.AreEqual("Бак", WaterReservoirInfoText.BarLabel(false));
            Assert.AreEqual("Бак (ПОРОЖНІЙ)", WaterReservoirInfoText.BarLabel(true));
        }

        [TestCase(749.6f, 750)]
        [TestCase(750.4f, 750)]
        [TestCase(0f, 0)]
        public void ToLiters_RoundsToWholeLiters(float liters, int expected)
        {
            Assert.AreEqual(expected, WaterReservoirInfoText.ToLiters(liters));
        }

        [Test]
        public void ReservoirValues_ReflectVolumeChanges_WithoutChangingTheReservoir()
        {
            WaterReservoir reservoir = new WaterReservoir(2000f, 1000f);
            reservoir.Draw(250f);

            string text = Format(reservoir);

            Assert.AreEqual("750 л / 2000 л", text);
            Assert.AreEqual(750f, reservoir.Volume);
            Assert.AreEqual(2000f, reservoir.Capacity);
        }

        private static string Format(WaterReservoir reservoir)
        {
            return WaterReservoirInfoText.FormatVolume(
                WaterReservoirInfoText.ToLiters(reservoir.Volume),
                WaterReservoirInfoText.ToLiters(reservoir.Capacity));
        }
    }
}
