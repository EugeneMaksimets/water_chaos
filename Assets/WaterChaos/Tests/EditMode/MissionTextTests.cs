using NUnit.Framework;
using WaterChaos.Mission;

namespace WaterChaos.Tests
{
    public class MissionTextTests
    {
        [Test]
        public void HintThatRepeatsTheTitle_IsADuplicate()
        {
            Assert.IsTrue(MissionText.HintRepeatsTitle(
                "Кинь мило в підземний резервуар",
                "Кинь мило в підземний резервуар - вода в ньому стане мильною."));
        }

        [Test]
        public void SameTextWithOtherCaseAndPunctuation_IsADuplicate()
        {
            Assert.IsTrue(MissionText.HintRepeatsTitle("Підключи брандспойт до машини", "підключи брандспойт до машини!"));
        }

        [Test]
        public void HintWithOtherInformation_IsNotADuplicate()
        {
            Assert.IsFalse(MissionText.HintRepeatsTitle(
                "Увімкни холодильник і кондиціонер",
                "Холодильник і кондиціонер швидко знизять температуру в будинку."));
        }

        [TestCase(null, "Підказка")]
        [TestCase("Ціль", null)]
        [TestCase("", "")]
        [TestCase("  ", "Підказка")]
        public void EmptyTexts_AreNeverDuplicates(string title, string hint)
        {
            Assert.IsFalse(MissionText.HintRepeatsTitle(title, hint));
        }
    }
}
