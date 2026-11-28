using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Teammate;

namespace WaterChaos.Tests
{
    /// <summary>A teammate aims at the roof fires when lifted to the roof and at the fires around them on the ground.</summary>
    public class FireTargetingTests
    {
        private static readonly List<Vector3> Fires = new List<Vector3>
        {
            new Vector3(4f, 0.5f, 6f),   // ground fire in front of the house
            new Vector3(0f, 7.5f, 10f),  // roof fire
        };

        [Test]
        public void OnTheGround_PicksTheGroundFire()
        {
            Assert.AreEqual(0, FireTargeting.Pick(new Vector3(0f, 1.2f, 0f), Fires, 30f, 2f));
        }

        [Test]
        public void LiftedToTheRoof_PicksTheRoofFire()
        {
            Assert.AreEqual(1, FireTargeting.Pick(new Vector3(0f, 8f, 2f), Fires, 30f, 2f));
        }

        [Test]
        public void NothingInRange_PicksNothing()
        {
            Assert.AreEqual(-1, FireTargeting.Pick(new Vector3(100f, 0f, 100f), Fires, 18f, 2f));
            Assert.AreEqual(-1, FireTargeting.Pick(Vector3.zero, new List<Vector3>(), 18f, 2f));
        }

        [Test]
        public void HeightCountsMoreThanSideways()
        {
            Assert.Greater(FireTargeting.Score(Vector3.zero, new Vector3(0f, 3f, 0f), 2f),
                FireTargeting.Score(Vector3.zero, new Vector3(3f, 0f, 0f), 2f));
        }

        [Test]
        public void PickRandom_ChoosesAmongFiresInRange_ByTheRoll()
        {
            var fires = new List<Vector3> { new Vector3(2f, 0f, 0f), new Vector3(50f, 0f, 0f), new Vector3(0f, 0f, 3f) };

            Assert.AreEqual(0, FireTargeting.PickRandom(Vector3.zero, fires, 10f, 2f, 0f));
            Assert.AreEqual(2, FireTargeting.PickRandom(Vector3.zero, fires, 10f, 2f, 0.6f));
            Assert.AreEqual(2, FireTargeting.PickRandom(Vector3.zero, fires, 10f, 2f, 1f));
        }

        [Test]
        public void PickRandom_NothingInRange_PicksNothing()
        {
            var fires = new List<Vector3> { new Vector3(50f, 0f, 0f) };

            Assert.AreEqual(-1, FireTargeting.PickRandom(Vector3.zero, fires, 10f, 2f, 0.5f));
        }

        [Test]
        public void IsInRange_UsesTheWeightedDistance()
        {
            Assert.IsTrue(FireTargeting.IsInRange(Vector3.zero, new Vector3(5f, 0f, 0f), 10f, 2f));
            Assert.IsFalse(FireTargeting.IsInRange(Vector3.zero, new Vector3(0f, 6f, 0f), 10f, 2f));
        }
    }
}
