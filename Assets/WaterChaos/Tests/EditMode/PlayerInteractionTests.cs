using System;
using System.Collections.Generic;
using NUnit.Framework;
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

        [TestCase("Pump (1)", "Pump")]
        [TestCase("Reservoir B (12)", "Reservoir B")]
        [TestCase("Pump", "Pump")]
        [TestCase("TransferHose_20m", "TransferHose_20m")]
        [TestCase("Valve (open)", "Valve (open)")]
        [TestCase("", "Interact")]
        public void CleanName_DropsDuplicateSuffixOnly(string objectName, string expected)
        {
            Assert.AreEqual(expected, PlayerInteraction.CleanName(objectName));
        }
    }
}
