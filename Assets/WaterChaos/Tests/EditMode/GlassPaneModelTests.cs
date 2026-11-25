using NUnit.Framework;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    public class GlassPaneModelTests
    {
        private const float MinForce = 60f;
        private const float CrackImpulse = 15f;
        private const float BreakImpulse = 40f;
        private const float Step = 0.02f;
        private const float Tolerance = 0.0001f;

        private static GlassPaneModel CreatePane()
        {
            return new GlassPaneModel(MinForce, CrackImpulse, BreakImpulse);
        }

        [Test]
        public void NewPane_IsIntact()
        {
            GlassPaneModel pane = CreatePane();

            Assert.AreEqual(GlassPaneState.Intact, pane.State);
            Assert.AreEqual(0f, pane.Damage, Tolerance);
        }

        [Test]
        public void WeakWater_NeverDamagesGlass()
        {
            GlassPaneModel pane = CreatePane();

            for (int i = 0; i < 10000; i++)
                pane.ApplyImpact(59f, Step);

            Assert.AreEqual(GlassPaneState.Intact, pane.State);
            Assert.AreEqual(0f, pane.Damage, Tolerance);
        }

        [Test]
        public void StrongWater_AddsForceTimesTime()
        {
            GlassPaneModel pane = CreatePane();

            pane.ApplyImpact(100f, 0.1f);

            Assert.AreEqual(10f, pane.Damage, Tolerance);
            Assert.AreEqual(GlassPaneState.Intact, pane.State);
        }

        [Test]
        public void Glass_CracksAtCrackImpulse()
        {
            GlassPaneModel pane = CreatePane();

            bool changed = pane.ApplyImpact(100f, 0.15f); // 15 N·s

            Assert.IsTrue(changed);
            Assert.AreEqual(GlassPaneState.Cracked, pane.State);
        }

        [Test]
        public void Glass_BreaksAtBreakImpulse()
        {
            GlassPaneModel pane = CreatePane();

            pane.ApplyImpact(100f, 0.2f);                 // 20: cracked
            bool changed = pane.ApplyImpact(100f, 0.2f);  // 40: broken

            Assert.IsTrue(changed);
            Assert.AreEqual(GlassPaneState.Broken, pane.State);
        }

        [Test]
        public void OneHugeHit_GoesStraightToBroken()
        {
            GlassPaneModel pane = CreatePane();

            pane.ApplyImpact(1000f, 1f);

            Assert.AreEqual(GlassPaneState.Broken, pane.State);
        }

        [Test]
        public void ApplyImpact_ReportsChangeOnlyOnce()
        {
            GlassPaneModel pane = CreatePane();
            pane.ApplyImpact(100f, 0.16f); // cracked

            bool changed = pane.ApplyImpact(100f, 0.01f);

            Assert.IsFalse(changed);
            Assert.AreEqual(GlassPaneState.Cracked, pane.State);
        }

        [Test]
        public void BrokenGlass_IgnoresFurtherWater()
        {
            GlassPaneModel pane = CreatePane();
            pane.ApplyImpact(1000f, 1f);
            float damage = pane.Damage;

            bool changed = pane.ApplyImpact(1000f, 1f);

            Assert.IsFalse(changed);
            Assert.AreEqual(damage, pane.Damage, Tolerance);
        }

        [Test]
        public void InvalidTime_IsIgnored()
        {
            GlassPaneModel pane = CreatePane();

            pane.ApplyImpact(100f, -1f);
            pane.ApplyImpact(100f, float.NaN);
            pane.ApplyImpact(float.NaN, 1f);

            Assert.AreEqual(0f, pane.Damage, Tolerance);
        }

        [Test]
        public void BreakImpulseBelowCrack_IsRaisedToCrack()
        {
            GlassPaneModel pane = new GlassPaneModel(0f, 20f, 5f);

            pane.ApplyImpact(100f, 0.1f); // 10 N·s

            Assert.AreEqual(GlassPaneState.Intact, pane.State);

            pane.ApplyImpact(100f, 0.1f); // 20 N·s
            Assert.AreEqual(GlassPaneState.Broken, pane.State);
        }

        [Test]
        public void NegativeSettings_AreClampedToZero()
        {
            GlassPaneModel pane = new GlassPaneModel(-5f, -1f, -1f);

            pane.ApplyImpact(0.001f, Step);

            Assert.AreEqual(GlassPaneState.Broken, pane.State);
        }
    }
}
