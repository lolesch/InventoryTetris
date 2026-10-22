using NUnit.Framework;
using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using Submodules.Utility.UI;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The Changelog panel on the real <c>MainMenu</c> scene (issues #228, #233-#237): the button opens it on
    /// Patch Notes with exactly one tab showing, every tab body scrolls inside a mask, and a re-enabled panel
    /// rebuilds its rows. The tab content is <c>ChangelogTabsTests</c>'. Boots over the Input System fixture like
    /// <see cref="MainMenuPlayModeTests"/>. Run with the PlayMode filter.
    /// </summary>
    public sealed class ChangelogPanelPlayModeTests : InputTestFixture
    {
        private const float FadeTimeout = 5f;

        private string folder;

        private static Component FindView() =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(component => component.GetType().Name == "ChangelogPanelView");

        private static Component FindButton() =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(component => component.name == "ChangelogButton" && component is Selectable);

        private static Transform Part(Component view, string name) =>
            view.GetComponentsInChildren<Transform>(true).First(part => part.name == name);

        private static float Alpha(Component part) => part.GetComponent<CanvasGroup>().alpha;

        private static void Click(Component target)
        {
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            _ = ExecuteEvents.Execute(target.gameObject, click, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + FadeTimeout;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        [TearDown]
        public void RemoveTheScratchSaves()
        {
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        private IEnumerator OpenMenu()
        {
            folder = Path.Combine(Path.GetTempPath(), "InventoryTetris-changelog-" + Guid.NewGuid().ToString("N"));

            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load(), new FileSaveStore(folder));
            _ = InputSystem.AddDevice<Keyboard>();

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
        }

        private static IEnumerator OpenPanel(Component view)
        {
            Click(FindButton());
            yield return Until(() => Alpha(view) >= 1f);
        }

        private static void AssertOnlyTabShows(Component view, string tab)
        {
            foreach (var name in new[] { "PatchNotes", "KnownIssues", "Roadmap" })
            {
                var shown = name == tab;
                Assert.That(Part(view, name + "Tab").GetComponent<PanelToggle>().IsOn, Is.EqualTo(shown), name + " toggle");
                Assert.That(Alpha(Part(view, name + "Panel")), shown ? Is.EqualTo(1f) : Is.EqualTo(0f), name + " panel");
            }
        }

        [UnityTest]
        public IEnumerator TheButtonOpensThePanelOnPatchNotes_WithExactlyOneTabShowing()
        {
            yield return OpenMenu();
            var view = FindView();
            Assert.That(Alpha(view), Is.EqualTo(0f), "premise: the panel starts closed");

            yield return OpenPanel(view);

            Assert.That(Alpha(view), Is.EqualTo(1f));
            AssertOnlyTabShows(view, "PatchNotes");
        }

        [UnityTest]
        public IEnumerator EveryTabBody_SitsInAMaskedScrollView_SoLongTextCannotOverflow()
        {
            yield return OpenMenu();
            var view = FindView();

            foreach (var name in new[] { "PatchNotes", "KnownIssues", "Roadmap" })
            {
                var panel = Part(view, name + "Panel");
                var scroll = panel.GetComponent<ScrollRect>();
                var body = (RectTransform)Part(view, name + "Body");

                Assert.That(scroll, Is.Not.Null, name + " scrolls");
                Assert.That(scroll.content, Is.SameAs(body));
                Assert.That(scroll.viewport.GetComponent<RectMask2D>(), Is.Not.Null, name + " is clipped to its viewport");
                Assert.That(body.parent, Is.SameAs(scroll.viewport));
                Assert.That(scroll.horizontal, Is.False);
            }

            yield return OpenPanel(view);
            var patchNotes = Part(view, "PatchNotesPanel").GetComponent<ScrollRect>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(patchNotes.content);

            // The seeded history is longer than the panel; the scroll position moves the body, not the panel.
            Assert.That(patchNotes.content.rect.height, Is.GreaterThan(0f), "the entries are laid out in the body");
            patchNotes.verticalNormalizedPosition = 0f;
            Assert.That(patchNotes.content.rect.height > patchNotes.viewport.rect.height
                ? patchNotes.content.anchoredPosition.y > 0f
                : patchNotes.content.anchoredPosition.y == 0f, Is.True, "scrolled to the end, or nothing to scroll");
        }

        [UnityTest]
        public IEnumerator AnEnabledAgainPanel_RebuildsItsRowsInsteadOfKeepingOrDoublingThem()
        {
            yield return OpenMenu();
            var view = FindView();
            int Entries() => Part(view, "PatchNotesBody").Cast<Transform>().Count(child => child.name == "Entry");
            int Lines() => Part(view, "KnownIssuesBody").Cast<Transform>().Count(child => child.name == "Line");
            var entries = Entries();
            var lines = Lines();
            Assert.That(entries, Is.GreaterThan(0), "premise: the panel built its rows");

            view.gameObject.SetActive(false);
            yield return null;
            Assert.That(Entries() + Lines(), Is.Zero, "disabling removes the rows, so none outlive Play in the scene");

            view.gameObject.SetActive(true);
            yield return null;

            Assert.That(Entries(), Is.EqualTo(entries));
            Assert.That(Lines(), Is.EqualTo(lines));
        }
    }
}
