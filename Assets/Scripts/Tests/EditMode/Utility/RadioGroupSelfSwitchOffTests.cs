using NUnit.Framework;
using Submodules.Utility.UI;
using UnityEngine;
using UnityEngine.UI;

namespace ToolSmiths.Tests.EditMode.Utility
{
    /// <summary>
    /// The "the active toggle switched itself off" behaviour (issue #30). A menu that lets
    /// a toggle turn itself off produces a real "no panel open" state: the group must clear
    /// <see cref="RadioGroup.ActivatedToggle"/> and raise <see cref="RadioGroup.OnGroupChanged"/>,
    /// and a sibling taking over must not be mistaken for it.
    /// </summary>
    [TestFixture]
    public sealed class RadioGroupSelfSwitchOffTests
    {
        [Test]
        public void Deactivate_TheActiveToggle_ClearsItAndFiresOnGroupChanged()
        {
            var group = MakeGroup();
            var toggle = MakeToggle(group);

            group.Activate(toggle);
            Assert.That(group.ActivatedToggle, Is.SameAs(toggle));

            var changed = 0;
            group.OnGroupChanged += () => changed++;

            group.Deactivate(toggle);

            Assert.That(group.ActivatedToggle, Is.Null, "nothing is active after the active toggle turns itself off");
            Assert.That(changed, Is.EqualTo(1), "the group state changed, so OnGroupChanged fires once");
        }

        [Test]
        public void Deactivate_ANonActiveToggle_IsIgnored()
        {
            var group = MakeGroup();
            var active = MakeToggle(group);
            var other = MakeToggle(group);

            group.Activate(active);

            var changed = 0;
            group.OnGroupChanged += () => changed++;

            group.Deactivate(other);

            Assert.That(group.ActivatedToggle, Is.SameAs(active), "only the active toggle can switch the group off");
            Assert.That(changed, Is.Zero, "nothing changed, so OnGroupChanged does not fire");
        }

        [Test]
        public void Deactivate_Null_IsIgnored_EvenWhenNothingIsActive()
        {
            var group = MakeGroup();

            var changed = 0;
            group.OnGroupChanged += () => changed++;

            group.Deactivate(null);

            Assert.That(changed, Is.Zero, "a null toggle must not be read as 'the (null) active toggle switched off'");
        }

        [Test]
        public void ASiblingTakingOver_DoesNotClearTheGroup()
        {
            var group = MakeGroup();
            var current = MakeToggle(group);
            var sibling = MakeToggle(group);

            group.Activate(current);
            group.Activate(sibling);

            Assert.That(group.ActivatedToggle, Is.SameAs(sibling), "a sibling taking over is selection, not a switch-off");
        }

        [Test]
        public void SettingTheActiveToggleOff_RoutesThroughSetToggle_AndClearsTheGroup()
        {
            var group = MakeGroup();
            var toggle = MakeToggle(group);

            toggle.SetToggle(true);
            Assert.That(group.ActivatedToggle, Is.SameAs(toggle), "turning a grouped toggle on activates it");

            var changed = 0;
            group.OnGroupChanged += () => changed++;

            toggle.SetToggle(false);

            Assert.That(group.ActivatedToggle, Is.Null,
                "SetToggle(false) on the active toggle - the path every click, submit and script share - clears the group");
            Assert.That(changed, Is.EqualTo(1), "and raises OnGroupChanged once");
        }

        /// <summary>A group with the <see cref="LayoutGroup"/> it validates for, built
        /// inactive so that requirement is met before any callback runs.</summary>
        private static RadioGroup MakeGroup()
        {
            var go = new GameObject("group", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.SetActive(false);
            var group = go.AddComponent<RadioGroup>();
            go.SetActive(true);
            return group;
        }

        /// <summary>
        /// A toggle wired the way the scene wires one: parented under the group so
        /// <see cref="AbstractToggle.RadioGroup"/> resolves, with a target graphic so the
        /// shared <see cref="InteractiveElement"/> enable path has nothing to complain
        /// about. Built inactive so <c>targetGraphic</c> is set before OnEnable runs.
        /// </summary>
        private static TestToggle MakeToggle(RadioGroup group)
        {
            var go = new GameObject("toggle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.SetActive(false);
            go.transform.SetParent(group.transform);

            var toggle = go.AddComponent<TestToggle>();
            toggle.targetGraphic = go.GetComponent<Image>();

            go.SetActive(true);
            return toggle;
        }

        /// <summary>AbstractToggle with no panel behaviour - just the group contract.</summary>
        private sealed class TestToggle : AbstractToggle { }
    }
}
