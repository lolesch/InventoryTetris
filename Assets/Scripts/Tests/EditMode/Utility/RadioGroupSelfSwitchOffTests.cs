using NUnit.Framework;
using Submodules.Utility.UI;
using UnityEngine;

namespace ToolSmiths.Tests.EditMode.Utility
{
    /// <summary>
    /// The "the active toggle switched itself off" signal (issue #30). A menu that lets a
    /// toggle turn itself off produces a real "no panel open" state; the group must clear
    /// and tell its listeners, and a sibling taking over must not be mistaken for a self
    /// switch-off.
    /// </summary>
    [TestFixture]
    public sealed class RadioGroupSelfSwitchOffTests
    {
        [Test]
        public void NotifySelfSwitchOff_OnTheActiveToggle_ClearsItAndFiresBothSignals()
        {
            var group = new GameObject().AddComponent<RadioGroup>();
            var toggle = new GameObject().AddComponent<TestToggle>();

            group.Activate(toggle);
            Assert.That(group.ActivatedToggle, Is.SameAs(toggle));

            var switchedOff = false;
            var groupChanged = false;
            group.OnActiveSwitchedOff += _ => switchedOff = true;
            group.OnGroupChanged += () => groupChanged = true;

            group.NotifySelfSwitchOff(toggle);

            Assert.That(group.ActivatedToggle, Is.Null, "nothing is active after the active toggle turns itself off");
            Assert.That(switchedOff, Is.True, "the explicit self-switch-off signal fires");
            Assert.That(groupChanged, Is.True, "the group state changed, so OnGroupChanged fires too");
        }

        [Test]
        public void NotifySelfSwitchOff_OnANonActiveToggle_IsIgnored()
        {
            var group = new GameObject().AddComponent<RadioGroup>();
            var active = new GameObject().AddComponent<TestToggle>();
            var other = new GameObject().AddComponent<TestToggle>();

            group.Activate(active);

            var fired = false;
            group.OnActiveSwitchedOff += _ => fired = true;

            group.NotifySelfSwitchOff(other);

            Assert.That(group.ActivatedToggle, Is.SameAs(active), "only the active toggle can switch the group off");
            Assert.That(fired, Is.False);
        }

        [Test]
        public void Activate_ASibling_DoesNotFireTheSelfSwitchOffSignal()
        {
            var group = new GameObject().AddComponent<RadioGroup>();
            var current = new GameObject().AddComponent<TestToggle>();
            var sibling = new GameObject().AddComponent<TestToggle>();

            group.Activate(current);

            var fired = false;
            group.OnActiveSwitchedOff += _ => fired = true;

            group.Activate(sibling);

            Assert.That(group.ActivatedToggle, Is.SameAs(sibling));
            Assert.That(fired, Is.False, "a sibling taking over is selection, not a self switch-off");
        }

        [Test]
        public void SwitchingOff_ThenSwitchingAResistorBackOn_EndsWithTheOldToggleActiveAgain()
        {
            var group = new GameObject().AddComponent<RadioGroup>();
            var toggle = new GameObject().AddComponent<TestToggle>();
            var sibling = new GameObject().AddComponent<TestToggle>();

            group.Activate(toggle);

            var switchedOffCount = 0;
            group.OnActiveSwitchedOff += _ => switchedOffCount++;

            group.NotifySelfSwitchOff(toggle);
            group.Activate(sibling);

            Assert.That(switchedOffCount, Is.EqualTo(1), "only the genuine self switch-off counted; the sibling's take-over did not");
            Assert.That(group.ActivatedToggle, Is.SameAs(sibling));
        }

        /// <summary>AbstractToggle with no panel behaviour - just the group contract.</summary>
        private sealed class TestToggle : AbstractToggle { }
    }
}