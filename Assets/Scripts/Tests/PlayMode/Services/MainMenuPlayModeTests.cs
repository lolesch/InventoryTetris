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
    /// The real <c>MainMenu</c> scene (issue #252) wired to its flow: Play enters the game scene, and the
    /// showcase, name entry and delete confirmation answer their controls. The rules are
    /// <c>MainMenuFlowTests</c>'. The saves are real files in a scratch folder. Boots again over the Input
    /// System fixture's isolation, like <see cref="HotkeyPlayModeTests"/>. Run with the PlayMode filter.
    /// </summary>
    public sealed class MainMenuPlayModeTests : InputTestFixture
    {
        private string folder;

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
        private IEnumerator OpenMenu(bool withHero, Action<IHeroSaveService> seed = null)
        {
            folder = Path.Combine(Path.GetTempPath(), "InventoryTetris-menu-" + Guid.NewGuid().ToString("N"));

            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load(), new FileSaveStore(folder));

            if (withHero)
                _ = Saves.Load(Saves.Create("Aria").Id);

            seed?.Invoke(Saves);

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

        // The create slot is the one whose private flag says it creates; the popup is reached by type name.
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
            var popup = Find<MonoBehaviour>("HeroNamePopup");
            var input = Member<TMP_InputField>(popup, "input");
            var reason = Member<TextMeshProUGUI>(popup, "reason");
            var confirm = Member<Component>(popup, "confirm");
            var heroes = Saves.List().Count;
            var active = Saves.ActiveHeroId;
            Assert.That(popup.gameObject.activeSelf, Is.False, "premise: the entry starts closed");

            Click(CreateSlot());
            yield return null;

            Assert.That(popup.gameObject.activeSelf, Is.True, "a click on the create slot opens the name entry");
            Assert.That(Saves.List().Count, Is.EqualTo(heroes), "nothing is created by opening it");
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(active));

            input.text = "a/b";
            Click(confirm);
            yield return null;

            Assert.That(popup.gameObject.activeSelf, Is.True, "a refused name keeps the entry open");
            Assert.That(reason.text, Does.Contain("cannot contain /"));
            Assert.That(Saves.List().Count, Is.EqualTo(heroes));

            input.text = "  Bran ";
            Click(confirm);
            yield return null;

            Assert.That(popup.gameObject.activeSelf, Is.False);
            Assert.That(Saves.List().Count, Is.EqualTo(heroes), "a named hero is a draft: nothing is written yet");
            Assert.That(Saves.TryGetDraft(out var bran), Is.True);
            Assert.That(bran.Name, Is.EqualTo("Bran"));
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(bran.Id), "the new hero is created, loaded and selected");
            Assert.That(Find<Selectable>("PlayButton").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("Start Game"));
        }

        [UnityTest]
        public IEnumerator Delete_AsksFirstNamingTheHero_AndCancelDeletesNothing()
        {
            yield return OpenMenu(withHero: true);
            var popup = Find<MonoBehaviour>("HeroDeletePopup");
            var question = Member<TextMeshProUGUI>(popup, "question");
            var delete = Find<Selectable>("HeroDeleteButton");
            var aria = Saves.ActiveHeroId;
            Assert.That(delete.interactable, Is.True, "premise: a hero is selected");
            Assert.That(popup.gameObject.activeSelf, Is.False, "premise: the confirmation starts closed");

            Click(delete);
            yield return null;

            Assert.That(popup.gameObject.activeSelf, Is.True);
            Assert.That(question.text, Does.Contain("Aria"), "it names the hero");
            Assert.That(Saves.List().Count, Is.EqualTo(1), "asking deletes nothing");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Member<Component>(popup, "cancel").gameObject),
                "Cancel is selected, so Enter deletes nothing");

            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.True, "the Cancel key dismisses it");
            Assert.That(popup.gameObject.activeSelf, Is.False);
            Assert.That(Saves.List().Single().Id, Is.EqualTo(aria));

            Click(delete);
            yield return null;
            Click(Member<Component>(popup, "cancel"));
            yield return null;

            Assert.That(popup.gameObject.activeSelf, Is.False, "the Cancel button dismisses it too");
            Assert.That(Saves.List().Single().Id, Is.EqualTo(aria));
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(aria));
            Assert.That(Find<Selectable>("PlayButton").interactable, Is.True);
        }
    }
}
