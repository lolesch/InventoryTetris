using NUnit.Framework;
using Submodules.Utility.UI;
using System.Reflection;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// A Popup opens with one button selected so Enter presses it, and a Notice (one button, nothing to
    /// choose) routes Cancel to that button (issue #321). Engine-light: a real <see cref="EventSystem"/> and
    /// real buttons, the Cancel chain over a <see cref="FakeInput"/>.
    /// </summary>
    [TestFixture]
    public sealed class PopupFocusTests
    {
        private sealed class CountingButton : AbstractButton
        {
            public int Clicks { get; private set; }

            protected override void OnClick() => Clicks++;
        }

        private EventSystem events;
        private CancelChain chain;
        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            events = new GameObject("events").AddComponent<EventSystem>();
            // Edit Mode never runs OnEnable, which is what makes an EventSystem the current one.
            Switch("OnEnable");
            chain = new CancelChain(new FakeInput());
            root = new GameObject("popup");
        }

        [TearDown]
        public void TearDown()
        {
            Switch("OnDisable");
            Object.DestroyImmediate(events.gameObject);
            Object.DestroyImmediate(root);
        }

        private void Switch(string message) =>
            typeof(EventSystem).GetMethod(message, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(events, null);

        // Built inactive so Awake (which wants a target graphic) runs once it has one.
        private CountingButton Button(string name)
        {
            var host = new GameObject(name);
            host.SetActive(false);
            host.transform.SetParent(root.transform);

            var button = host.AddComponent<CountingButton>();
            button.targetGraphic = host.AddComponent<Image>();
            host.SetActive(true);
            return button;
        }

        private void Submit() =>
            _ = ExecuteEvents.Execute(events.currentSelectedGameObject, new BaseEventData(events), ExecuteEvents.submitHandler);

        [Test]
        public void ThePopup_SelectsItsDefaultButton_AndEnterPressesOnlyThat()
        {
            var yes = Button("yes");
            var cancel = Button("cancel");

            Assert.That(PopupFocus.SelectDefault(cancel), Is.True);
            Submit();

            Assert.That(events.currentSelectedGameObject, Is.SameAs(cancel.gameObject));
            Assert.That(cancel.Clicks, Is.EqualTo(1), "Enter pressed the safe button");
            Assert.That(yes.Clicks, Is.Zero, "and nothing that would delete");
        }

        [Test]
        public void ADisabledDefault_IsNotSelected_AndTheSelectionStaysWhereItWas()
        {
            var other = Button("other");
            var disabled = Button("disabled");
            disabled.interactable = false;
            events.SetSelectedGameObject(other.gameObject);

            Assert.That(PopupFocus.SelectDefault(disabled), Is.False);

            Assert.That(events.currentSelectedGameObject, Is.SameAs(other.gameObject));
        }

        [Test]
        public void WithNoEventSystem_SelectingChangesNothing()
        {
            var button = Button("button");
            Switch("OnDisable");

            Assert.That(PopupFocus.SelectDefault(button), Is.False);
        }

        [Test]
        public void ANotice_SelectsItsButton_AndCancelPressesIt()
        {
            var ok = Button("ok");
            using var layer = PopupFocus.OpenNotice(ok, chain);

            Assert.That(events.currentSelectedGameObject, Is.SameAs(ok.gameObject));
            Assert.That(chain.Cancel(), Is.True, "the notice consumes the press");
            Assert.That(ok.Clicks, Is.EqualTo(1), "as the button's own click");
        }

        [Test]
        public void ANoticeThatClosed_NoLongerHearsCancel()
        {
            var ok = Button("ok");
            var layer = PopupFocus.OpenNotice(ok, chain);

            layer.Dispose();

            Assert.That(chain.Cancel(), Is.False);
            Assert.That(ok.Clicks, Is.Zero);
        }

        [Test]
        public void ANoticeWhoseButtonCannotBePressed_StillConsumesCancel_SoTheMenuBehindStaysShut()
        {
            var ok = Button("ok");
            ok.interactable = false;
            using var layer = PopupFocus.OpenNotice(ok, chain);

            Assert.That(chain.Cancel(), Is.True);
            Assert.That(ok.Clicks, Is.Zero);
        }
    }
}
