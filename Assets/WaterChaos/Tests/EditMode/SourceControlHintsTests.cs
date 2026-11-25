using NUnit.Framework;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    public class SourceControlHintsTests
    {
        [Test]
        public void New_ShowsToggleHintOnly()
        {
            var hints = new SourceControlHints();

            Assert.IsTrue(hints.ShowToggleHint);
            Assert.IsFalse(hints.ShowPressureHint);
        }

        [Test]
        public void AfterFirstToggle_ShowsPressureHintInstead()
        {
            var hints = new SourceControlHints();

            hints.NotifyToggled();

            Assert.IsFalse(hints.ShowToggleHint);
            Assert.IsTrue(hints.ShowPressureHint);
        }

        [Test]
        public void AfterToggleAndPressureChange_ShowsNothing()
        {
            var hints = new SourceControlHints();

            hints.NotifyToggled();
            hints.NotifyPressureChanged();

            Assert.IsFalse(hints.ShowToggleHint);
            Assert.IsFalse(hints.ShowPressureHint);
        }

        [Test]
        public void PressureChangedBeforeToggle_PressureHintNeverShows()
        {
            var hints = new SourceControlHints();

            hints.NotifyPressureChanged();
            hints.NotifyToggled();

            Assert.IsFalse(hints.ShowPressureHint);
        }

        [Test]
        public void ToggleAgain_DoesNotBringHintsBack()
        {
            var hints = new SourceControlHints();

            hints.NotifyToggled();
            hints.NotifyPressureChanged();
            hints.NotifyToggled();

            Assert.IsFalse(hints.ShowToggleHint);
            Assert.IsFalse(hints.ShowPressureHint);
        }

        [Test]
        public void InteractionAction_ShowsPromptByDefault()
        {
            var action = new InteractionAction("Water On/Off", null, () => { });

            Assert.IsTrue(action.ShowPrompt);
        }

        [Test]
        public void InteractionAction_CanHidePrompt()
        {
            var action = new InteractionAction("Water On/Off", null, () => { }, false);

            Assert.IsFalse(action.ShowPrompt);
        }
    }
}
