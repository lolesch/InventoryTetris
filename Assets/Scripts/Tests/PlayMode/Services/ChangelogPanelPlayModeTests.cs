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
    /// Patch Notes, exactly one tab shows at a time, closing and reopening starts on Patch Notes again, and only
    /// the newest Patch Notes entry starts expanded. Boots over the Input System fixture like
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

        private static IEnumerator ClosePanel(Component view)
        {
            Part(view, "CloseButton").GetComponent<Button>().onClick.Invoke();
            yield return Until(() => Alpha(view) <= 0f);
            yield return null;
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
        public IEnumerator SwitchingTabs_ShowsOneTabAtATime_AndAReopenedPanelStartsOnPatchNotes()
        {
            yield return OpenMenu();
            var view = FindView();
            yield return OpenPanel(view);

            Click(Part(view, "RoadmapTab"));
            yield return Until(() => Alpha(Part(view, "RoadmapPanel")) >= 1f && Alpha(Part(view, "PatchNotesPanel")) <= 0f);
            AssertOnlyTabShows(view, "Roadmap");

            Click(Part(view, "KnownIssuesTab"));
            yield return Until(() => Alpha(Part(view, "KnownIssuesPanel")) >= 1f && Alpha(Part(view, "RoadmapPanel")) <= 0f);
            AssertOnlyTabShows(view, "KnownIssues");

            yield return ClosePanel(view);
            yield return OpenPanel(view);
            yield return Until(() => Alpha(Part(view, "PatchNotesPanel")) >= 1f && Alpha(Part(view, "KnownIssuesPanel")) <= 0f);

            AssertOnlyTabShows(view, "PatchNotes");
        }

        [UnityTest]
        public IEnumerator OnlyTheNewestEntryStartsExpanded_AHeadingToggles_AndAReopenResetsThem()
        {
            yield return OpenMenu();
            var view = FindView();
            var entries = Part(view, "PatchNotesBody").Cast<Transform>().Where(child => child.name == "Entry").ToArray();
            Assert.That(entries.Length, Is.GreaterThan(1), "premise: the shipped file has several entries");

            bool Open(int index) => entries[index].Find("Bullets").gameObject.activeSelf;

            yield return OpenPanel(view);
            Assert.That(Open(0), Is.True, "the newest entry starts expanded");
            Assert.That(Enumerable.Range(1, entries.Length - 1).Any(Open), Is.False, "older entries start collapsed");

            Click(entries[1].Find("Heading"));
            Click(entries[0].Find("Heading"));
            Assert.That(Open(1), Is.True, "a click expands a collapsed entry");
            Assert.That(Open(0), Is.False, "a click collapses an expanded entry");

            yield return ClosePanel(view);

            Assert.That(Open(0), Is.True, "closing resets the newest entry to expanded");
            Assert.That(Open(1), Is.False, "closing resets the others to collapsed");
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
