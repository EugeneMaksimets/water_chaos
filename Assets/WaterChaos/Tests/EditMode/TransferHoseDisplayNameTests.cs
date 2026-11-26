using NUnit.Framework;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    public class TransferHoseDisplayNameTests
    {
        [TestCase(25f, "Шланг 25 м")]
        [TestCase(10f, "Шланг 10 м")]
        [TestCase(12.5f, "Шланг 12.5 м")]
        public void DisplayName_IsHoseAndLengthInMeters(float meters, string expected)
        {
            Assert.AreEqual(expected, TransferHose.FormatDisplayName(meters));
        }
    }
}
