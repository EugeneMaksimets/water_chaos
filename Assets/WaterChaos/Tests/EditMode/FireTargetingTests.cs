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
    }
}
