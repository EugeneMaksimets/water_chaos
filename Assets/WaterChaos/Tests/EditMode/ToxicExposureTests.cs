using NUnit.Framework;
using UnityEngine;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// One exposure per player, fed by toxic puddles and direct toxic hits alike: no flicker, no duplicates,
    /// fades only when every source is gone.
    /// </summary>
    public class ToxicExposureTests
    {
        private const float Hold = 0.8f;

        [Test]
        public void ToxicPuddle_IsFoundUnderFeet_AndExposes()
        {
            var field = new PuddleField(PuddleConfig.Default, 10);
            field.AddLiquid(Vector3.zero, LiquidType.Toxic, 20f);
            field.AddLiquid(new Vector3(10f, 0f, 0f), LiquidType.Water, 20f);

            Assert.IsTrue(field.IsInLiquid(new Vector3(0.1f, 0.05f, 0f), l => l.Corrodes()));
            Assert.IsFalse(field.IsInLiquid(new Vector3(10f, 0.05f, 0f), l => l.Corrodes()), "water is harmless");

            var exposure = new ToxicExposureModel(Hold);
            exposure.SetInPuddle(true);
            Assert.IsTrue(exposure.IsExposed(0f));
        }

        [Test]
        public void LeavingPuddle_EndsExposure()
        {
            var exposure = new ToxicExposureModel(Hold);
            exposure.SetInPuddle(true);
            exposure.SetInPuddle(false);

            Assert.IsFalse(exposure.IsExposed(10f));
        }

        [Test]
        public void DirectHit_Exposes_ThenEndsAfterHoldTime()
        {
            var exposure = new ToxicExposureModel(Hold);
            exposure.Hit(1f);

            Assert.IsTrue(exposure.IsExposed(1f));
            Assert.IsTrue(exposure.IsExposed(1f + Hold * 0.9f));
            Assert.IsFalse(exposure.IsExposed(1f + Hold * 1.1f));
        }

        [Test]
        public void ContinuousHits_RefreshTimer()
        {
            var exposure = new ToxicExposureModel(Hold);
            for (float t = 0f; t <= 5f; t += 0.02f)
            {
                exposure.Hit(t);
                Assert.IsTrue(exposure.IsExposed(t + 0.01f));
            }

            Assert.IsTrue(exposure.IsExposed(5f + Hold * 0.5f));
            Assert.IsFalse(exposure.IsExposed(5f + Hold * 2f));
        }

        [Test]
        public void MultipleSources_ShareOneExposure_WithoutFlicker()
        {
            var exposure = new ToxicExposureModel(Hold);
            exposure.SetInPuddle(true);
            exposure.Hit(0f);

            // Stepping out of the puddle while still sprayed: stays on.
            exposure.SetInPuddle(false);
            Assert.IsTrue(exposure.IsExposed(0.1f));

            // Spray stops while back in the puddle: still on.
            exposure.SetInPuddle(true);
            Assert.IsTrue(exposure.IsExposed(Hold * 3f));

            // Every source gone: off.
            exposure.SetInPuddle(false);
            Assert.IsFalse(exposure.IsExposed(Hold * 3f));
        }

        [Test]
        public void ToxicExposure_IsSinglePerCharacter()
        {
            Assert.IsTrue(System.Attribute.IsDefined(typeof(ToxicExposure), typeof(DisallowMultipleComponent)));

            var go = new GameObject("Player");
            try
            {
                Assert.IsNotNull(go.AddComponent<ToxicExposure>());
                Assert.AreEqual(1, go.GetComponents<ToxicExposure>().Length);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void FrozenWaterPuddle_IsNotToxic()
        {
            var field = new PuddleField(PuddleConfig.Default, 10);
            field.AddLiquid(Vector3.zero, LiquidType.Water, 20f);

            Assert.IsFalse(field.IsInLiquid(Vector3.zero, l => l.Corrodes()));
        }
    }
}
