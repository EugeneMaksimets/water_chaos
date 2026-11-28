using NUnit.Framework;
using WaterChaos.Surfaces;

namespace WaterChaos.Tests
{
    public class ToxicStunModelTests
    {
        private const float Duration = 5f;
        private const float Immunity = 3f;

        [Test]
        public void Hit_Stuns_ForTheDuration()
        {
            var stun = new ToxicStunModel(Duration, Immunity);

            Assert.IsTrue(stun.TryStun(10f));
            Assert.IsTrue(stun.IsStunned(14.9f));
            Assert.IsFalse(stun.IsStunned(15f));
        }

        [Test]
        public void HitsWhileStunned_DoNotExtendIt()
        {
            var stun = new ToxicStunModel(Duration, Immunity);
            stun.TryStun(10f);

            Assert.IsFalse(stun.TryStun(12f));
            Assert.IsFalse(stun.IsStunned(15f));
        }

        [Test]
        public void AfterStun_ImmuneForAWhile_ThenCanBeStunnedAgain()
        {
            var stun = new ToxicStunModel(Duration, Immunity);
            stun.TryStun(10f);

            Assert.IsFalse(stun.TryStun(17.9f));
            Assert.IsTrue(stun.TryStun(18f));
        }

        [Test]
        public void BadValues_AreTreatedAsZero()
        {
            var stun = new ToxicStunModel(float.NaN, -1f);

            Assert.IsTrue(stun.TryStun(1f));
            Assert.IsFalse(stun.IsStunned(1f));
            Assert.IsTrue(stun.TryStun(1f));
        }
    }
}
