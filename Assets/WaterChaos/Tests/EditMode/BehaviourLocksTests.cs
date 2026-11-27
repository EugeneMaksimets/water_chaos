using NUnit.Framework;
using UnityEngine;
using WaterChaos.Common;

namespace WaterChaos.Tests
{
    /// <summary>
    /// A knockdown and a soap bubble both switch the player's movement off: in whatever order they end,
    /// movement comes back only after the last one, never stays off for good.
    /// </summary>
    public class BehaviourLocksTests
    {
        private GameObject _go;
        private Behaviour _movement;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Player");
            _movement = _go.AddComponent<Light>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void OverlappingLocks_EndingInAnyOrder_RestoreMovement()
        {
            object knockdown = new object();
            object bubble = new object();

            BehaviourLocks.Lock(_movement, knockdown);
            BehaviourLocks.Lock(_movement, bubble);
            BehaviourLocks.Unlock(_movement, knockdown);
            Assert.IsFalse(_movement.enabled, "still in the bubble");

            BehaviourLocks.Unlock(_movement, bubble);
            Assert.IsTrue(_movement.enabled, "both are over: the player moves again");
        }

        [Test]
        public void ComponentOffBeforeTheFirstLock_StaysOff()
        {
            _movement.enabled = false;
            object pause = new object();

            BehaviourLocks.Lock(_movement, pause);
            BehaviourLocks.Unlock(_movement, pause);

            Assert.IsFalse(_movement.enabled);
        }

        [Test]
        public void SameOwnerLockingTwice_NeedsOneUnlock()
        {
            object menu = new object();
            BehaviourLocks.Lock(_movement, menu);
            BehaviourLocks.Lock(_movement, menu);

            BehaviourLocks.Unlock(_movement, menu);

            Assert.IsTrue(_movement.enabled);
            Assert.IsFalse(BehaviourLocks.IsLocked(_movement));
        }

        [Test]
        public void UnlockWithoutLock_ChangesNothing()
        {
            BehaviourLocks.Unlock(_movement, new object());
            Assert.IsTrue(_movement.enabled);
        }
    }
}
