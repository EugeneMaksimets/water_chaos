using NUnit.Framework;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    public class ObjectMenuNamesTests
    {
        [TestCase("Axe", "Сокира")]
        [TestCase("Nozzle", "Пожежний ствол")]
        [TestCase("Pump", "Насос")]
        [TestCase("Soap", "Мило")]
        [TestCase("HoseSplitter", "Трійник")]
        [TestCase("Fridge", "Холодильник")]
        [TestCase("AirConditioner", "Кондиціонер")]
        [TestCase("FireTruck", "Пожежна машина")]
        [TestCase("FireHydrant_Red", "Гідрант")]
        public void Translate_KnownObject_GivesUkrainianName(string sceneName, string expected)
        {
            Assert.AreEqual(expected, ObjectMenuNames.Translate(sceneName));
        }

        [TestCase("axe")]
        [TestCase("AXE")]
        public void Translate_IgnoresCase(string sceneName)
        {
            Assert.AreEqual("Сокира", ObjectMenuNames.Translate(sceneName));
        }

        [Test]
        public void Translate_DropsMissionSuffix()
        {
            Assert.AreEqual("Насос", ObjectMenuNames.Translate("Pump · Underground Tank"));
        }

        [TestCase("Door_Red")]
        [TestCase("DoorFront")]
        public void Translate_DoorFamily_IsDoors(string sceneName)
        {
            Assert.AreEqual("Двері", ObjectMenuNames.Translate(sceneName));
        }

        [Test]
        public void Translate_UnknownObject_KeepsItsName()
        {
            Assert.AreEqual("Mystery Box", ObjectMenuNames.Translate("Mystery Box"));
        }

        [TestCase(null)]
        [TestCase("")]
        public void Translate_Empty_GivesEmpty(string sceneName)
        {
            Assert.AreEqual(string.Empty, ObjectMenuNames.Translate(sceneName));
        }
    }
}
