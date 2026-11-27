using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;
using WaterChaos.Surfaces;
using WaterChaos.ToxicReaction;
using WaterChaos.Water;

namespace WaterChaos.Tests
{
    /// <summary>
    /// Toxic liquid does not put fire out: the fire reacts for a while (cloud, splashes), then cools down,
    /// so one hit never makes endless clouds. Clouds set off other fires in a chain that shrinks and stops.
    /// </summary>
    public class ToxicReactionTests
    {
        private const float Duration = 2.5f;
        private const float Cooldown = 6f;

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null)
                    Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        // ───────────── Reaction timer ─────────────

        [Test]
        public void Reaction_LastsItsDuration_ThenCoolsDown_ThenCanStartAgain()
        {
            var reaction = new ToxicReactionModel(Duration, Cooldown);

            Assert.IsTrue(reaction.TryStart(10f));
            Assert.IsTrue(reaction.IsReacting(10f + Duration * 0.9f));
            Assert.IsFalse(reaction.IsReacting(10f + Duration));
            Assert.IsTrue(reaction.IsCoolingDown(10f + Duration + 1f));
            Assert.IsFalse(reaction.TryStart(10f + Duration + Cooldown * 0.9f), "no new reaction during the cooldown");
            Assert.IsTrue(reaction.TryStart(10f + Duration + Cooldown));
        }

        [Test]
        public void Reaction_RepeatedHits_DoNotRestartOrExtendIt()
        {
            var reaction = new ToxicReactionModel(Duration, Cooldown);
            int starts = 0;
            for (float t = 0f; t < Duration; t += 0.02f)
            {
                if (reaction.TryStart(t))
                    starts++;
            }

            Assert.AreEqual(1, starts);
            Assert.IsFalse(reaction.IsReacting(Duration + 0.01f), "constant spraying does not keep it going");
        }

        [Test]
        public void Reaction_StoppedEarly_StillCoolsDown()
        {
            var reaction = new ToxicReactionModel(Duration, Cooldown);
            reaction.TryStart(0f);

            reaction.Stop(1f);

            Assert.IsFalse(reaction.IsReacting(1f));
            Assert.IsFalse(reaction.CanStart(1f + Cooldown * 0.5f));
            Assert.IsTrue(reaction.CanStart(1f + Cooldown));
        }

        // ───────────── Cloud ─────────────

        [Test]
        public void Cloud_SpreadsToFullRadius_HoldsAndDisappears()
        {
            var cloud = new ToxicCloudModel(Vector3.zero, 0.5f, 3f, 2f, 6f, 0);

            Assert.AreEqual(0.5f, cloud.Radius(0f), 1e-4f);
            Assert.Greater(cloud.Radius(1f), 0.5f);
            Assert.Less(cloud.Radius(1f), 3f);
            Assert.AreEqual(3f, cloud.Radius(2f), 1e-4f);
            Assert.AreEqual(3f, cloud.Radius(5f), 1e-4f);
            Assert.AreEqual(0f, cloud.Radius(6f));
            Assert.IsFalse(cloud.IsAlive(6f));
        }

        [Test]
        public void Cloud_ContainsPointsInsideItsCurrentRadius()
        {
            var cloud = new ToxicCloudModel(Vector3.zero, 0.5f, 3f, 2f, 6f, 0);
            var point = new Vector3(2f, 0f, 0f);

            Assert.IsFalse(cloud.Contains(point, 0f), "not spread that far yet");
            Assert.IsTrue(cloud.Contains(point, 3f));
            Assert.IsFalse(cloud.Contains(point, 7f), "gone");
        }

        // ───────────── Chain ─────────────

        [Test]
        public void Chain_StopsAtTheLimit_AndEachStepIsSmaller()
        {
            Assert.IsTrue(ToxicChainRules.CanTriggerChain(0, 1));
            Assert.IsFalse(ToxicChainRules.CanTriggerChain(1, 1), "a chain cloud sets off nothing more");
            Assert.IsFalse(ToxicChainRules.CanTriggerChain(0, 0), "chains can be switched off");

            Assert.AreEqual(1f, ToxicChainRules.SizeScale(0, 0.5f));
            Assert.AreEqual(0.5f, ToxicChainRules.SizeScale(1, 0.5f), 1e-5f);
            Assert.AreEqual(0.25f, ToxicChainRules.SizeScale(2, 0.5f), 1e-5f);
        }

        [Test]
        public void Settings_ChainCloud_IsSmallerThanFirst()
        {
            var settings = Track(ScriptableObject.CreateInstance<ToxicReactionSettings>());

            ToxicCloudModel first = settings.CreateCloud(Vector3.zero, 0);
            ToxicCloudModel chain = settings.CreateCloud(Vector3.zero, 1);

            Assert.Less(chain.MaxRadius, first.MaxRadius);
            Assert.AreEqual(1, chain.Generation);
        }

        // ───────────── Fire component ─────────────

        [Test]
        public void ToxicHit_StartsReaction_FireKeepsBurning()
        {
            FireToxicReaction reaction = CreateReactingFire(out FireExtinguishHandler fire);
            int starts = 0;
            int generation = -1;
            reaction.ReactionStarted += g => { starts++; generation = g; };

            fire.ReceiveImpact(Toxic());
            reaction.ReceiveImpact(Toxic());

            Assert.IsTrue(reaction.IsReacting);
            Assert.IsTrue(fire.IsBurning);
            Assert.AreEqual(1f, fire.Intensity, 1e-4f, "toxic liquid does not cool the fire");
            Assert.AreEqual(1, starts);
            Assert.AreEqual(0, generation);

            for (int i = 0; i < 20; i++)
                reaction.ReceiveImpact(Toxic());
            Assert.AreEqual(1, starts, "more toxic liquid during the reaction makes no new cloud");
        }

        [Test]
        public void OtherLiquids_DoNotStartReaction()
        {
            FireToxicReaction reaction = CreateReactingFire(out _);

            reaction.ReceiveImpact(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Water));
            reaction.ReceiveImpact(new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Gasoline));

            Assert.IsFalse(reaction.IsReacting);
        }

        [Test]
        public void ExtinguishedFire_DoesNotReact()
        {
            FireToxicReaction reaction = CreateReactingFire(out FireExtinguishHandler fire);
            fire.Extinguish();

            Assert.IsFalse(reaction.TryReact(0));
            Assert.IsFalse(reaction.IsReacting);
        }

        [Test]
        public void ChainReaction_ReportsItsGeneration()
        {
            FireToxicReaction reaction = CreateReactingFire(out _);
            int generation = -1;
            reaction.ReactionStarted += g => generation = g;

            Assert.IsTrue(reaction.TryReact(1));
            Assert.AreEqual(1, generation);
        }

        // ───────────── Exposure ─────────────

        private sealed class FakeArea : IToxicHazardArea
        {
            public bool Contains(Vector3 position) => position.sqrMagnitude < 1f;
        }

        [Test]
        public void Cloud_ExposesCharacter_UntilItLeaves()
        {
            var exposure = new ToxicExposureModel(0.8f);
            exposure.SetInCloud(true);
            Assert.IsTrue(exposure.IsExposed(0f));

            exposure.SetInCloud(false);
            Assert.IsFalse(exposure.IsExposed(0f));
        }

        [Test]
        public void HazardRegistry_FindsRegisteredAreasOnly()
        {
            var area = new FakeArea();
            ToxicHazards.Register(area);
            try
            {
                Assert.IsTrue(ToxicHazards.AnyContains(Vector3.zero));
                Assert.IsFalse(ToxicHazards.AnyContains(new Vector3(5f, 0f, 0f)));
            }
            finally
            {
                ToxicHazards.Unregister(area);
            }

            Assert.IsFalse(ToxicHazards.AnyContains(Vector3.zero));
        }

        // ───────────── Helpers ─────────────

        private static WaterImpact Toxic()
        {
            return new WaterImpact(Vector3.zero, Vector3.up, Vector3.down, 1f, 1f, LiquidType.Toxic);
        }

        private FireToxicReaction CreateReactingFire(out FireExtinguishHandler fire)
        {
            var go = Track(new GameObject("Fire"));
            fire = go.AddComponent<FireExtinguishHandler>();
            CallAwake(fire);

            var settings = Track(ScriptableObject.CreateInstance<ToxicReactionSettings>());
            var reaction = go.AddComponent<FireToxicReaction>();
            typeof(FireToxicReaction).GetField("_settings", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(reaction, settings);
            CallAwake(reaction);
            return reaction;
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        // Edit mode does not run Awake, where these components build their models.
        private static void CallAwake(MonoBehaviour component)
        {
            component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
        }
    }
}
