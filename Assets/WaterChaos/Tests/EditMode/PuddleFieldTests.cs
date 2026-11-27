using NUnit.Framework;
using UnityEngine;
using WaterChaos.Surfaces;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>Puddles form where liquid lands, grow up to a limit and never stack on top of each other.</summary>
    public class PuddleFieldTests
    {
        private static readonly PuddleConfig Config = PuddleConfig.Default;

        private static PuddleField NewField(int max = 50) => new PuddleField(Config, max);

        [Test]
        public void WaterImpact_CreatesPuddle()
        {
            PuddleField field = NewField();
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 2f);

            Assert.IsNotNull(puddle);
            Assert.AreEqual(1, field.Count);
            Assert.AreEqual(LiquidType.Water, puddle.Liquid);
            Assert.AreEqual(2f, puddle.Volume, 1e-4f);
            Assert.IsFalse(puddle.IsFrozen);
        }

        [Test]
        public void RepeatedImpacts_SameLocation_UpdateExistingPuddle()
        {
            PuddleField field = NewField();
            PuddleModel first = field.AddLiquid(Vector3.zero, LiquidType.Water, 5f);
            float radius = first.Radius;

            for (int i = 0; i < 50; i++)
                Assert.AreSame(first, field.AddLiquid(Vector3.zero, LiquidType.Water, 1f));

            Assert.AreEqual(1, field.Count);
            Assert.AreEqual(55f, first.Volume, 1e-3f);
            Assert.Greater(first.Radius, radius);
        }

        [Test]
        public void ImpactsWithinMergeRadius_DoNotCreateDuplicates()
        {
            PuddleField field = NewField();
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 10f);

            // Just outside the edge but within the merge distance, in several directions.
            float reach = puddle.Radius + Config.MergeDistance * 0.9f;
            field.AddLiquid(new Vector3(reach, 0f, 0f), LiquidType.Water, 1f);
            field.AddLiquid(new Vector3(0f, 0.1f, -reach), LiquidType.Water, 1f);
            field.AddLiquid(new Vector3(-reach * 0.7f, 0f, reach * 0.7f), LiquidType.Water, 1f);

            Assert.AreEqual(1, field.Count);
        }

        [Test]
        public void SeparateLocations_CreateSeparatePuddles()
        {
            PuddleField field = NewField();
            PuddleModel a = field.AddLiquid(new Vector3(0f, 0f, 0f), LiquidType.Water, 5f);
            PuddleModel b = field.AddLiquid(new Vector3(10f, 0f, 0f), LiquidType.Water, 5f);
            PuddleModel c = field.AddLiquid(new Vector3(-10f, 0f, -30f), LiquidType.Water, 5f);

            Assert.AreEqual(3, field.Count);
            Assert.AreNotSame(a, b);
            Assert.AreNotSame(b, c);
        }

        [Test]
        public void PuddlesOnDifferentFloors_AreSeparate()
        {
            PuddleField field = NewField();
            field.AddLiquid(Vector3.zero, LiquidType.Water, 5f);
            field.AddLiquid(new Vector3(0f, 3f, 0f), LiquidType.Water, 5f);

            Assert.AreEqual(2, field.Count);
        }

        [Test]
        public void PuddleGrowth_RespectsMaximumSize()
        {
            PuddleField field = NewField();
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 100000f);
            float taken = puddle.AddLiquid(LiquidType.Water, 50f);

            Assert.AreEqual(Config.MaxRadius, puddle.Radius, 1e-4f);
            Assert.AreEqual(Config.MaxVolume, puddle.Volume, 1e-2f);
            Assert.AreEqual(0f, taken);
            Assert.IsTrue(puddle.IsFull);
        }

        [Test]
        public void FullPuddle_StillTakesNearbyLiquid_WithoutNewPuddle()
        {
            PuddleField field = NewField();
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 100000f);

            field.AddLiquid(new Vector3(puddle.Radius * 0.5f, 0f, 0f), LiquidType.Water, 10f);

            Assert.AreEqual(1, field.Count);
        }

        [Test]
        public void PuddleLimit_RemovesOldestPuddle_SoNewOnesAppearAnywhere()
        {
            PuddleField field = NewField(2);
            PuddleModel oldest = field.AddLiquid(new Vector3(0f, 0f, 0f), LiquidType.Water, 1f);
            PuddleModel second = field.AddLiquid(new Vector3(20f, 0f, 0f), LiquidType.Water, 1f);
            PuddleModel removed = null;
            field.Removed += p => removed = p;

            PuddleModel newest = field.AddLiquid(new Vector3(40f, 0f, 0f), LiquidType.Water, 1f);

            Assert.IsNotNull(newest);
            Assert.AreSame(oldest, removed);
            Assert.AreEqual(2, field.Count);
            Assert.IsFalse(field.TryGetAt(Vector3.zero, out _), "the oldest puddle is gone");
            Assert.IsTrue(field.TryGetAt(new Vector3(20f, 0f, 0f), out PuddleModel kept));
            Assert.AreSame(second, kept);
        }

        [Test]
        public void Puddle_DriesUp_AfterItsLifetime()
        {
            var config = new PuddleConfig(0.1f, 0.4f, 3f, 0.6f, 0f, 0.5f, 1f, 3f, 0.05f, 60f);
            var field = new PuddleField(config, 10);
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 5f);
            PuddleModel removed = null;
            field.Removed += p => removed = p;

            field.Tick(59f, null);
            Assert.AreEqual(1, field.Count, "still there before a minute");

            field.Tick(2f, null);
            Assert.AreEqual(0, field.Count, "dried up after a minute");
            Assert.AreSame(puddle, removed, "the view gets told, so it can sink away");
        }

        [Test]
        public void Puddle_DriesUp_EvenWhileStillBeingFilled_AndANewOneForms()
        {
            var config = new PuddleConfig(0.1f, 0.4f, 3f, 0.6f, 0f, 0.5f, 1f, 3f, 0.05f, 60f);
            var field = new PuddleField(config, 10);
            PuddleModel first = field.AddLiquid(Vector3.zero, LiquidType.Toxic, 5f);

            // A teammate keeps spraying the same spot.
            for (int i = 0; i < 61; i++)
            {
                field.Tick(1f, null);
                field.AddLiquid(Vector3.zero, LiquidType.Toxic, 1f);
            }

            Assert.IsFalse(field.TryGetAt(Vector3.zero, out PuddleModel now) && now == first, "the first puddle is gone after a minute");
            Assert.AreEqual(1, field.Count, "the spray starts a new one");
        }

        [Test]
        public void Puddle_NeverGrowsPastTheSpaceAroundIt()
        {
            PuddleField field = NewField();
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 100000f, 0.9f);

            Assert.AreEqual(0.9f, puddle.Radius, 1e-4f, "stops at the edge of the surface or a wall");
            Assert.IsTrue(puddle.IsFull);
        }

        [Test]
        public void NoRoomForAPuddle_LeavesNone()
        {
            PuddleField field = NewField();

            Assert.IsNull(field.AddLiquid(Vector3.zero, LiquidType.Water, 5f, Config.MinRadius * 0.5f));
            Assert.AreEqual(0, field.Count);
        }

        [Test]
        public void ExistingPuddle_FoundNearby_BeforeMeasuringSpace()
        {
            PuddleField field = NewField();
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 5f);

            Assert.AreSame(puddle, field.FindNearby(new Vector3(0.3f, 0f, 0f)));
            Assert.IsNull(field.FindNearby(new Vector3(20f, 0f, 0f)));
        }

        [Test]
        public void NoLiquid_CreatesNothing()
        {
            PuddleField field = NewField();

            Assert.IsNull(field.AddLiquid(Vector3.zero, LiquidType.Water, 0f));
            Assert.IsNull(field.AddLiquid(Vector3.zero, LiquidType.Water, float.NaN));
            Assert.AreEqual(0, field.Count);
        }

        [Test]
        public void SpatialLookup_WorksAtNegativeCoordinatesAndCellBorders()
        {
            var registry = new SurfacePatchRegistry<PuddleModel>(1f, 1f);
            var puddle = new PuddleModel(new Vector3(-0.01f, 0f, -0.01f), LiquidType.Water, Config);
            registry.Add(puddle);

            Assert.AreSame(puddle, registry.FindContaining(new Vector3(0.05f, 0f, 0.05f), 0.5f));
            Assert.IsNull(registry.FindContaining(new Vector3(5f, 0f, 5f), 0.5f));
            Assert.IsTrue(registry.Remove(puddle));
            Assert.AreEqual(0, registry.Count);
        }

        [Test]
        public void DifferentLiquid_MixesIntoExistingPuddle_ByLiquidRules()
        {
            PuddleField field = NewField();
            PuddleModel puddle = field.AddLiquid(Vector3.zero, LiquidType.Water, 5f);

            field.AddLiquid(Vector3.zero, LiquidType.Toxic, 1f);
            Assert.AreEqual(LiquidType.Toxic, puddle.Liquid);

            // Toxic wins over gasoline, as in a reservoir.
            field.AddLiquid(Vector3.zero, LiquidType.Gasoline, 1f);
            Assert.AreEqual(LiquidType.Toxic, puddle.Liquid);
            Assert.AreEqual(1, field.Count);
        }
    }
}
