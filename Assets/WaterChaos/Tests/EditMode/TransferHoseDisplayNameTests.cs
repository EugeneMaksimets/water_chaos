using NUnit.Framework;
using WaterChaos.WaterTransfer;

namespace WaterChaos.Tests
{
    public class TransferHoseDisplayNameTests
    {
        [TestCase(25f, "Hose 25m")]
        [TestCase(10f, "Hose 10m")]
        [TestCase(12.5f, "Hose 12.5m")]
        public void DisplayName_IsHoseAndLengthInMeters(float meters, string expected)
        {
            Assert.AreEqual(expected, TransferHose.FormatDisplayName(meters));
        }
    }
}
