using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    public class PlayerInteractionTests
    {
        private static readonly Action Nothing = () => { };

        [Test]
        public void Action_WithoutLabel_Throws()
        {
            Assert.Throws<ArgumentException>(() => new InteractionAction("", null, Nothing));
            Assert.Throws<ArgumentException>(() => new InteractionAction(null, null, Nothing));
        }

        [Test]
        public void Action_WithoutExecute_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new InteractionAction("Turn On", null, null));
        }

        [Test]
        public void Execute_RunsTheGivenAction()
        {
            int runs = 0;
            InteractionAction action = new InteractionAction("Turn On", null, () => runs++);

            action.Execute();

            Assert.AreEqual(1, runs);
        }

        [Test]
        public void AddUnique_SkipsSameLabel()
        {
            List<InteractionAction> actions = new List<InteractionAction>();

            Assert.IsTrue(PlayerInteraction.AddUnique(actions, new InteractionAction("Pick Up", null, Nothing)));
            Assert.IsTrue(PlayerInteraction.AddUnique(actions, new InteractionAction("Turn On", null, Nothing)));
            Assert.IsFalse(PlayerInteraction.AddUnique(actions, new InteractionAction("Pick Up", null, Nothing)));

            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual("Pick Up", actions[0].Label);
            Assert.AreEqual("Turn On", actions[1].Label);
        }

        [Test]
        public void Action_ByDefault_IsInMenu()
        {
            Assert.IsTrue(new InteractionAction("Open", null, Nothing).InMenu);
        }

        [Test]
        public void SelectMenuActions_SkipsButtonOnlyActions()
        {
            List<InteractionAction> all = new List<InteractionAction>
            {
                new InteractionAction("Water On/Off", null, Nothing, true, InteractionButton.Primary, false),
                new InteractionAction("Open", null, Nothing),
            };
            List<InteractionAction> menu = new List<InteractionAction>();

            PlayerInteraction.SelectMenuActions(all, menu);

            Assert.AreEqual(1, menu.Count);
            Assert.AreEqual("Open", menu[0].Label);
        }

        [Test]
        public void PrimaryIndex_PrefersMenuActionOverButtonOnly()
        {
            List<InteractionAction> actions = new List<InteractionAction>
            {
                new InteractionAction("Water On/Off", null, Nothing, true, InteractionButton.Primary, false),
                new InteractionAction("Open", null, Nothing),
            };

            Assert.AreEqual(1, PlayerInteraction.PrimaryIndex(actions));
        }

        [Test]
        public void PrimaryIndex_FallsBackToButtonOnly()
        {
            List<InteractionAction> actions = new List<InteractionAction>
            {
                new InteractionAction("Pick Up", null, Nothing, true, InteractionButton.Interact),
                new InteractionAction("Water On/Off", null, Nothing, true, InteractionButton.Primary, false),
            };

            Assert.AreEqual(1, PlayerInteraction.PrimaryIndex(actions));
        }

        [Test]
        public void PrimaryIndex_NoPrimaryAction_IsMinusOne()
        {
            List<InteractionAction> actions = new List<InteractionAction>
            {
                new InteractionAction("Pick Up", null, Nothing, true, InteractionButton.Interact),
            };

            Assert.AreEqual(-1, PlayerInteraction.PrimaryIndex(actions));
        }

        [Test]
        public void UsesMenu_OnlyWithMoreThanOneMenuAction()
        {
            List<InteractionAction> one = new List<InteractionAction> { new InteractionAction("Pick Up", null, Nothing) };
            List<InteractionAction> two = new List<InteractionAction>
            {
                new InteractionAction("Connect Hose", null, Nothing),
                new InteractionAction("Pick Up", null, Nothing, true, InteractionButton.Interact),
            };

            Assert.IsFalse(PlayerInteraction.UsesMenu(new List<InteractionAction>()));
            Assert.IsFalse(PlayerInteraction.UsesMenu(one));
            Assert.IsTrue(PlayerInteraction.UsesMenu(two));
        }

        [Test]
        public void PrimaryIndex_WhenTheMenuIsUsed_OnlyButtonOnlyActions()
        {
            List<InteractionAction> actions = new List<InteractionAction>
            {
                new InteractionAction("Place Hose", null, Nothing),
                new InteractionAction("Water On/Off", null, Nothing, true, InteractionButton.Primary, false),
            };

            Assert.AreEqual(1, PlayerInteraction.PrimaryIndex(actions, true));
            Assert.AreEqual(0, PlayerInteraction.PrimaryIndex(actions, false));
        }

        [Test]
        public void PrimaryIndex_WhenTheMenuIsUsedAndNoButtonOnlyAction_IsMinusOne()
        {
            List<InteractionAction> actions = new List<InteractionAction>
            {
                new InteractionAction("Place Hose", null, Nothing),
                new InteractionAction("Connect Hose", null, Nothing),
            };

            Assert.AreEqual(-1, PlayerInteraction.PrimaryIndex(actions, true));
        }

        [Test]
        public void SelectMenuActions_ClearsPreviousResult()
        {
            List<InteractionAction> menu = new List<InteractionAction> { new InteractionAction("Old", null, Nothing) };

            PlayerInteraction.SelectMenuActions(new List<InteractionAction>(), menu);

            Assert.AreEqual(0, menu.Count);
        }

        [TestCase("Pump (1)", "Pump")]
        [TestCase("Reservoir B (12)", "Reservoir B")]
        [TestCase("Pump", "Pump")]
        [TestCase("TransferHose_20m", "TransferHose_20m")]
        [TestCase("Valve (open)", "Valve (open)")]
        [TestCase("", "Дія")]
        public void CleanName_DropsDuplicateSuffixOnly(string objectName, string expected)
        {
            Assert.AreEqual(expected, PlayerInteraction.CleanName(objectName));
        }

        [Test]
        public void FocusOf_StopsBelowEmptyGroupObjects()
        {
            // Gameplay (empty group) / Pumps (empty group) / Pump (has a collider) / HoseAttachPoint (target)
            GameObject gameplay = new GameObject("Gameplay");
            GameObject pumps = new GameObject("Pumps");
            GameObject pump = new GameObject("Pump", typeof(BoxCollider));
            GameObject point = new GameObject("HoseAttachPoint");
            pumps.transform.SetParent(gameplay.transform);
            pump.transform.SetParent(pumps.transform);
            point.transform.SetParent(pump.transform);

            try
            {
                Assert.AreSame(pump.transform, PlayerInteraction.FocusOf(point.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameplay);
            }
        }

        [Test]
        public void FocusOf_ObjectWithoutGroup_IsItsRoot()
        {
            GameObject hydrant = new GameObject("FireHydrant_Red", typeof(CapsuleCollider));
            GameObject anchor = new GameObject("HoseAnchor");
            anchor.transform.SetParent(hydrant.transform);

            try
            {
                Assert.AreSame(hydrant.transform, PlayerInteraction.FocusOf(anchor.transform));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(hydrant);
            }
        }

        [Test]
        public void Action_UsesPrimaryButtonByDefault()
        {
            var action = new InteractionAction("Turn On", null, Nothing);

            Assert.AreEqual(InteractionButton.Primary, action.Button);
        }

        [Test]
        public void IndexOf_FindsFirstActionOfButton()
        {
            var actions = new List<InteractionAction>
            {
                new InteractionAction("Pick Up", null, Nothing, true, InteractionButton.Interact),
                new InteractionAction("Connect", null, Nothing),
                new InteractionAction("Place", null, Nothing),
            };

            Assert.AreEqual(1, PlayerInteraction.IndexOf(actions, InteractionButton.Primary));
            Assert.AreEqual(0, PlayerInteraction.IndexOf(actions, InteractionButton.Interact));
        }

        [Test]
        public void IndexOf_NoActionForButton_ReturnsMinusOne()
        {
            var actions = new List<InteractionAction> { new InteractionAction("Connect", null, Nothing) };

            Assert.AreEqual(-1, PlayerInteraction.IndexOf(actions, InteractionButton.Interact));
        }
    }
}
