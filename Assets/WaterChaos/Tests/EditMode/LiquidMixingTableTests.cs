using NUnit.Framework;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// The agreed mixing table (order does not matter, "soap" = a bar thrown into the tank):
    /// water + gasoline = gasoline; water + toxic = toxic; gasoline + toxic = toxic;
    /// water + soap = soapy; toxic + soap = soapy; gasoline + soap = nothing (the bar stays);
    /// soapy + toxic = soapy; soapy + gasoline = soapy; soapy + water = soapy; soapy + soap = nothing (the bar stays).
    /// </summary>
    public class LiquidMixingTableTests
    {
        private const float Liters = 100f;

        private static WaterReservoir Tank(LiquidType liquid, bool soapy = false)
        {
            var tank = new WaterReservoir(1000f, Liters, 0f, liquid);
            if (soapy)
                Assert.IsTrue(tank.AddSoap(), "setup: the tank becomes soapy");
            return tank;
        }

        private static void AssertMix(LiquidType a, bool aSoapy, LiquidType b, bool bSoapy, LiquidType liquid, bool soapy)
        {
            // Both orders: pouring a into b and b into a.
            WaterReservoir first = Tank(a, aSoapy);
            first.Add(Liters, b, bSoapy);
            Assert.AreEqual(liquid, first.Liquid, $"{b}{(bSoapy ? " (soapy)" : "")} into {a}{(aSoapy ? " (soapy)" : "")}");
            Assert.AreEqual(soapy, first.IsSoapy, $"soap: {b} into {a}");

            WaterReservoir second = Tank(b, bSoapy);
            second.Add(Liters, a, aSoapy);
            Assert.AreEqual(liquid, second.Liquid, $"{a}{(aSoapy ? " (soapy)" : "")} into {b}{(bSoapy ? " (soapy)" : "")}");
            Assert.AreEqual(soapy, second.IsSoapy, $"soap: {a} into {b}");
        }

        [Test]
        public void WaterAndGasoline_IsGasoline()
        {
            AssertMix(LiquidType.Water, false, LiquidType.Gasoline, false, LiquidType.Gasoline, false);
        }

        [Test]
        public void WaterAndToxic_IsToxic()
        {
            AssertMix(LiquidType.Water, false, LiquidType.Toxic, false, LiquidType.Toxic, false);
        }

        [Test]
        public void GasolineAndToxic_IsToxic()
        {
            AssertMix(LiquidType.Gasoline, false, LiquidType.Toxic, false, LiquidType.Toxic, false);
            Assert.AreEqual(LiquidType.Toxic, LiquidType.Gasoline.MixWith(LiquidType.Toxic));
            Assert.AreEqual(LiquidType.Toxic, LiquidType.Toxic.MixWith(LiquidType.Gasoline));
        }

        [Test]
        public void SoapyAndToxic_IsSoapy()
        {
            AssertMix(LiquidType.Water, true, LiquidType.Toxic, false, LiquidType.Water, true);
        }

        [Test]
        public void SoapyAndGasoline_IsSoapy()
        {
            AssertMix(LiquidType.Water, true, LiquidType.Gasoline, false, LiquidType.Water, true);
        }

        [Test]
        public void SoapyAndWater_IsSoapy()
        {
            AssertMix(LiquidType.Water, true, LiquidType.Water, false, LiquidType.Water, true);
        }

        [Test]
        public void WaterAndSoap_IsSoapy_BarDissolves()
        {
            WaterReservoir tank = Tank(LiquidType.Water);

            Assert.IsTrue(tank.AddSoap(), "the bar dissolves");
            Assert.AreEqual(LiquidType.Water, tank.Liquid);
            Assert.IsTrue(tank.IsSoapy);
        }

        [Test]
        public void ToxicAndSoap_IsSoapyWater_BarDissolves()
        {
            WaterReservoir tank = Tank(LiquidType.Toxic);

            Assert.IsTrue(tank.AddSoap(), "the bar dissolves");
            Assert.AreEqual(LiquidType.Water, tank.Liquid);
            Assert.IsTrue(tank.IsSoapy);
        }

        [Test]
        public void GasolineAndSoap_NothingHappens_BarStays()
        {
            WaterReservoir tank = Tank(LiquidType.Gasoline);

            Assert.IsFalse(tank.AddSoap(), "the bar just lies there");
            Assert.AreEqual(LiquidType.Gasoline, tank.Liquid);
            Assert.IsFalse(tank.IsSoapy);
        }

        [Test]
        public void SoapyAndSoap_NothingHappens_BarStays()
        {
            WaterReservoir tank = Tank(LiquidType.Water, true);

            Assert.IsFalse(tank.AddSoap(), "already soapy: the bar is not used up");
            Assert.IsTrue(tank.IsSoapy);
        }

        [Test]
        public void SameLiquid_StaysTheSame()
        {
            foreach (LiquidType liquid in new[] { LiquidType.Water, LiquidType.Gasoline, LiquidType.Toxic })
                AssertMix(liquid, false, liquid, false, liquid, false);
        }
    }
}
