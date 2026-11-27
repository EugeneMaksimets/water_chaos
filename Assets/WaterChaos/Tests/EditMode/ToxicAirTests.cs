using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// The toxic house: without a gas mask the player is pushed out after a few seconds; with it nothing toxic
    /// affects them. The toxic volume is made of the house's boxes.
    /// </summary>
    public class ToxicAirTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void Ejection_AfterTolerance_WhenInsideUnprotected()
        {
            var ejection = new ToxicEjectionModel(3f);

            Assert.IsFalse(ejection.Tick(true, false, 1f));
            Assert.IsFalse(ejection.Tick(true, false, 1.5f));
            Assert.AreEqual(2.5f / 3f, ejection.Ratio, 1e-4f);
            Assert.IsTrue(ejection.Tick(true, false, 0.6f), "pushed out after 3 s");
            Assert.AreEqual(0f, ejection.Exposure, "the timer restarts after a push");
        }

        [Test]
        public void Ejection_ResetsWhenOutside()
        {
            var ejection = new ToxicEjectionModel(3f);
            ejection.Tick(true, false, 2.5f);

            Assert.IsFalse(ejection.Tick(false, false, 0.1f));
            Assert.IsFalse(ejection.Tick(true, false, 2.5f), "stepping out resets the time");
        }

        [Test]
        public void Ejection_NeverWithGasMask()
        {
            var ejection = new ToxicEjectionModel(3f);
            for (int i = 0; i < 100; i++)
                Assert.IsFalse(ejection.Tick(true, true, 1f));
        }

        [Test]
        public void GasMask_ProtectsFromEveryToxicSource()
        {
            _go = new GameObject("Player");
            var exposure = _go.AddComponent<ToxicExposure>();
            typeof(ToxicExposure).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(exposure, null);

            exposure.ReceiveImpact(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Toxic));
            Assert.IsTrue(exposure.IsExposed, "a toxic hit without a mask");

            exposure.SetProtected(true);
            Assert.IsFalse(exposure.IsExposed, "the mask protects");
            exposure.ReceiveImpact(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Toxic));
            Assert.IsFalse(exposure.IsExposed);
        }

        [Test]
        public void ToxicAirZone_ContainsPointsInItsBoxesOnly()
        {
            _go = new GameObject("House");
            var room = _go.AddComponent<BoxCollider>();
            room.size = new Vector3(10f, 3f, 8f);
            room.center = new Vector3(0f, 1.5f, 0f);
            var zone = _go.AddComponent<ToxicAirZone>();
            typeof(ToxicAirZone).GetField("_areas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(zone, new[] { room });

            Assert.IsTrue(zone.Contains(new Vector3(2f, 1f, 1f)));
            Assert.IsFalse(zone.Contains(new Vector3(20f, 1f, 1f)));
            Assert.IsFalse(zone.Contains(new Vector3(0f, 5f, 0f)), "above the house");
        }

        [Test]
        public void ToxicAirZone_IsToxicOnlyWhileActive()
        {
            _go = new GameObject("House");
            var room = _go.AddComponent<BoxCollider>();
            room.size = Vector3.one * 4f;
            var zone = _go.AddComponent<ToxicAirZone>();
            typeof(ToxicAirZone).GetField("_areas", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(zone, new[] { room });

            Assert.IsFalse(ToxicHazards.AnyContains(Vector3.zero));
            zone.SetActive(true);
            Assert.IsTrue(ToxicHazards.AnyContains(Vector3.zero), "the house fills with toxic air");
            zone.SetActive(false);
            Assert.IsFalse(ToxicHazards.AnyContains(Vector3.zero), "the air clears");
        }
    }
}
