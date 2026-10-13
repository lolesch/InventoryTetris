using NUnit.Framework;
using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
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
    /// The real <c>MainMenu</c> scene (issue #252): Play enters the game with the selected hero read from disk
    /// again, is disabled with no hero, Exit and the saved line follow the platform, and Cancel at the menu root
    /// does nothing. The saves are real files in a scratch folder. Boots again over the Input System fixture's
    /// isolation, like <see cref="HotkeyPlayModeTests"/>. Run with the PlayMode filter.
    /// </summary>
    public sealed class MainMenuPlayModeTests : InputTestFixture
    {
        private string folder;
        private Keyboard keyboard;

        private static T Find<T>(string typeName) where T : Component =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(component => component.GetType().Name == typeName) as T;

        private static IHeroSaveService Saves => ServiceLocator.Get<IHeroSaveService>();

        [TearDown]
        public void RemoveTheScratchSaves()
        {
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        // The menu over a saves folder holding Aria, who is the selected hero when the menu opens (a launch
        // continues the last-selected one), or over an empty folder.
        private IEnumerator OpenMenu(bool withHero)
        {
            folder = Path.Combine(Path.GetTempPath(), "InventoryTetris-menu-" + Guid.NewGuid().ToString("N"));

            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load(), new FileSaveStore(folder));
            keyboard = InputSystem.AddDevice<Keyboard>();

            if (withHero)
                _ = Saves.Load(Saves.Create("Aria").Id);

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
        }

        private static void Click(Component target)
        {
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            _ = ExecuteEvents.Execute(target.gameObject, click, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator Play_ReadsTheHeroFromDiskAgain_AndEntersTheGameScene()
        {
            yield return OpenMenu(withHero: true);
            var session = ServiceLocator.Get<ISession>();
            var play = Find<Selectable>("PlayButton");
            Assert.That(play.interactable, Is.True, "premise: a hero is selected");

            // What a previous trip leaves in the Session and never saved.
            var leftover = ServiceLocator.Get<IItemService>().RollEquipment(EquipmentType.Helm, 0f);
            var package = new Package(null, leftover, 1u);
            Assert.That(session.Hero.Inventory.TryAddToContainer(ref package), Is.True, "premise: the helm fits");
            var worldBefore = session.World;

            Click(play);

            var deadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().name != "Example" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"));
            Assert.That(session.World, Is.Not.SameAs(worldBefore), "a fresh World");
            Assert.That(session.Hero.Inventory.StoredPackages.Values.Any(stored => stored.Item == leftover), Is.False,
                "what the previous trip held is not in the hero read from disk");
            Assert.That(Saves.ActiveHeroId, Is.Not.Null, "the hero on disk is the Session's");
        }

        [UnityTest]
        public IEnumerator Play_IsDisabled_WithNoHero_AndAClickEntersNothing()
        {
            yield return OpenMenu(withHero: false);
            var play = Find<Selectable>("PlayButton");

            Assert.That(play.interactable, Is.False);

            Click(play);
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator Play_BecomesAvailable_WhenAHeroIsCreated()
        {
            yield return OpenMenu(withHero: false);
            var play = Find<Selectable>("PlayButton");
            Assert.That(play.interactable, Is.False, "premise");

            _ = Saves.Load(Saves.Create("Aria").Id);

            Assert.That(play.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator Exit_IsShownOnNative_AndHiddenInWebGLWhereTheSavedLineTakesItsPlace()
        {
            yield return OpenMenu(withHero: true);
            var status = Find<MonoBehaviour>("MainMenuStatus");
            var exit = Find<Selectable>("QuitGameButton").gameObject;
            var line = status.transform.Find("SavedLine").gameObject;

            Assert.That(exit.activeSelf, Is.True, "native keeps Exit");
            Assert.That(line.activeSelf, Is.False, "and shows no saved line");

            status.GetType().GetMethod("Apply")!.Invoke(status, new object[] { RuntimePlatform.WebGLPlayer });

            var saved = Saves.List()[0].SavedAtUtc.ToLocalTime().ToString("HH:mm");
            Assert.That(exit.activeSelf, Is.False, "WebGL hides Exit");
            Assert.That(line.activeSelf, Is.True);
            Assert.That(line.GetComponent<TextMeshProUGUI>().text, Does.Match($"^Saved: Aria, Level \\d+, {saved}$"));
        }

        [UnityTest]
        public IEnumerator Showcase_ShowsTheSelectedHerosFigures_AndIsBlankWithNoHero()
        {
            yield return OpenMenu(withHero: false);
            var text = Find<MonoBehaviour>("HeroShowcaseText").GetComponent<TextMeshProUGUI>();
            Assert.That(text.text, Is.Empty, "no hero is selected");

            var aria = Saves.Create("Aria");
            _ = Saves.Load(aria.Id);
            ServiceLocator.Get<ISession>().Hero.AddKills(7);
            Assert.That(Saves.Save().Failure, Is.EqualTo(SaveFailure.None), "premise");
            _ = Saves.Load(aria.Id);

            Assert.That(text.text, Does.Contain("Kills: 7"), "read from the hero the Session now holds");

            var bran = Saves.Create("Bran");
            _ = Saves.Load(bran.Id);

            Assert.That(text.text, Is.EqualTo("Location:\nCorpse: none\nKills: 0\nGear level:"));
        }

        // The create slot is the one whose private flag says it creates; the prompt is reached by type name.
        private static Selectable CreateSlot() =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(component => component.GetType().Name == "HeroSlotToggle")
                .First(slot => (bool)slot.GetType().GetField("creates", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(slot))
                .GetComponent<Selectable>();

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);

        [UnityTest]
        public IEnumerator CreateSlot_AsksForAName_AndCreatesNothingUntilItIsConfirmed()
        {
            yield return OpenMenu(withHero: true);
            var prompt = Find<MonoBehaviour>("HeroNamePrompt");
            var input = Member<TMP_InputField>(prompt, "input");
            var reason = Member<TextMeshProUGUI>(prompt, "reason");
            var confirm = Member<Component>(prompt, "confirm");
            var heroes = Saves.List().Count;
            var active = Saves.ActiveHeroId;
            Assert.That(prompt.gameObject.activeSelf, Is.False, "premise: the entry starts closed");

            Click(CreateSlot());
            yield return null;

            Assert.That(prompt.gameObject.activeSelf, Is.True, "a click on the create slot opens the name entry");
            Assert.That(Saves.List().Count, Is.EqualTo(heroes), "nothing is created by opening it");
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(active));

            input.text = "a/b";
            Click(confirm);
            yield return null;

            Assert.That(prompt.gameObject.activeSelf, Is.True, "a refused name keeps the entry open");
            Assert.That(reason.text, Does.Contain("cannot contain /"));
            Assert.That(Saves.List().Count, Is.EqualTo(heroes));

            input.text = "  Bran ";
            Click(confirm);
            yield return null;

            Assert.That(prompt.gameObject.activeSelf, Is.False);
            Assert.That(Saves.List().Count, Is.EqualTo(heroes + 1));
            var bran = Saves.List().Single(hero => hero.Name == "Bran");
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(bran.Id), "the new hero is created, loaded and selected");
        }

        [UnityTest]
        public IEnumerator NameEntry_ClosesOnCancel_AndCreatesNothing()
        {
            yield return OpenMenu(withHero: true);
            var prompt = Find<MonoBehaviour>("HeroNamePrompt");
            var input = Member<TMP_InputField>(prompt, "input");
            var cancel = Member<Component>(prompt, "cancel");
            var heroes = Saves.List().Count;
            var active = Saves.ActiveHeroId;

            Click(CreateSlot());
            yield return null;
            input.text = "Bran";

            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.True, "the open entry consumes the Cancel press");
            Assert.That(prompt.gameObject.activeSelf, Is.False);
            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.False, "and a second press finds nothing at the root");

            Click(CreateSlot());
            yield return null;
            Assert.That(input.text, Is.Empty, "reopened clean");
            Click(cancel);
            yield return null;

            Assert.That(prompt.gameObject.activeSelf, Is.False, "the Cancel button closes it too");
            Assert.That(Saves.List().Count, Is.EqualTo(heroes));
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(active));
        }

        [UnityTest]
        public IEnumerator Cancel_AtTheMenuRoot_DoesNothing()
        {
            yield return OpenMenu(withHero: true);
            var hero = Saves.ActiveHeroId;

            currentTime += 1.0;
            PressAndRelease(keyboard.escapeKey);
            yield return null;

            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.False, "no layer consumes Cancel at the root");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(hero));
            Assert.That(Find<Selectable>("PlayButton").interactable, Is.True);
        }
    }
}
