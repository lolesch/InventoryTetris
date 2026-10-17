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
        private IEnumerator OpenMenu(bool withHero, Action<IHeroSaveService> seed = null)
        {
            folder = Path.Combine(Path.GetTempPath(), "InventoryTetris-menu-" + Guid.NewGuid().ToString("N"));

            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load(), new FileSaveStore(folder));
            keyboard = InputSystem.AddDevice<Keyboard>();

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
        public IEnumerator PlayAndDelete_AreNotShown_WithNoHero_AndAClickEntersNothing()
        {
            yield return OpenMenu(withHero: false);
            var play = Find<Selectable>("PlayButton");

            Assert.That(play.gameObject.activeSelf, Is.False, "no hero, no Play");
            Assert.That(Find<Selectable>("HeroDeleteButton").gameObject.activeSelf, Is.False, "and nothing to delete");

            Click(play);
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        }

        [UnityTest]
        public IEnumerator PlayAndDelete_AppearWhenAHeroIsSelected_AsContinue()
        {
            yield return OpenMenu(withHero: false);
            var play = Find<Selectable>("PlayButton");
            Assert.That(play.gameObject.activeSelf, Is.False, "premise");

            _ = Saves.Load(Saves.Create("Aria").Id);

            Assert.That(play.gameObject.activeSelf, Is.True);
            Assert.That(play.interactable, Is.True);
            Assert.That(play.GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("Continue"), "a hero with a save continues");
            Assert.That(Find<Selectable>("HeroDeleteButton").gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator AFreshLaunch_CreatesNoHero_AndOffersOnlyTheCreateSlot()
        {
            yield return OpenMenu(withHero: false);

            Assert.That(Saves.List(), Is.Empty);
            Assert.That(Saves.ActiveHeroId, Is.Null);
            Assert.That(Directory.Exists(folder) ? Directory.GetFiles(folder) : Array.Empty<string>(), Is.Empty, "no file written");
            Assert.That(CreateSlot().interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator Exit_IsShownOnNative_AndHiddenInWebGLWhereTheSavedLineTakesItsPlace()
        {
            yield return OpenMenu(withHero: true);
            var status = Find<MonoBehaviour>("MainMenuLayout");
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
        public IEnumerator ADraft_IsFirstSavedWhenTheGameStarts_AndIsNamedOnTheMenu()
        {
            yield return OpenMenu(withHero: false);
            var popup = Find<MonoBehaviour>("HeroNamePopup");

            Click(CreateSlot());
            yield return null;
            Member<TMP_InputField>(popup, "input").text = "Bran";
            Click(Member<Component>(popup, "confirm"));
            yield return null;

            Assert.That(Directory.Exists(folder) ? Directory.GetFiles(folder) : Array.Empty<string>(), Is.Empty, "naming a hero writes nothing");
            Assert.That(Texts("HeroNameDisplay"), Is.EqualTo("Bran"));
            Assert.That(Texts("HeroLevelDisplay"), Does.Match("^Level \\d+$"));
            Assert.That(Texts("HeroClassDisplay"), Is.EqualTo(GameBoot.Load().DefaultHero.DisplayName));

            Click(Find<Selectable>("PlayButton"));
            var deadline = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().name != "Example" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"));
            Assert.That(Saves.List().Single().Name, Is.EqualTo("Bran"), "written when the game started");
            Assert.That(Saves.TryGetDraft(out _), Is.False);
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(Saves.List().Single().Id));
        }

        // The display's label sits on its own object.
        private static string Texts(string typeName) => Find<MonoBehaviour>(typeName).GetComponent<TextMeshProUGUI>().text;

        [UnityTest]
        public IEnumerator NameEntry_ClosesOnCancel_AndCreatesNothing()
        {
            yield return OpenMenu(withHero: true);
            var popup = Find<MonoBehaviour>("HeroNamePopup");
            var input = Member<TMP_InputField>(popup, "input");
            var cancel = Member<Component>(popup, "cancel");
            var heroes = Saves.List().Count;
            var active = Saves.ActiveHeroId;

            Click(CreateSlot());
            yield return null;
            input.text = "Bran";

            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.True, "the open entry consumes the Cancel press");
            Assert.That(popup.gameObject.activeSelf, Is.False);
            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.False, "and a second press finds nothing at the root");

            Click(CreateSlot());
            yield return null;
            Assert.That(input.text, Is.Empty, "reopened clean");
            Click(cancel);
            yield return null;

            Assert.That(popup.gameObject.activeSelf, Is.False, "the Cancel button closes it too");
            Assert.That(Saves.List().Count, Is.EqualTo(heroes));
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(active));
        }

        // ── delete, and the slots that cannot be read ───────────────────────

        [Serializable]
        private sealed class AnyPayload
        {
            public int value;
        }

        private static Selectable SlotOf(string heroId) =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(component => component.GetType().Name == "HeroSlotToggle")
                .First(slot => Member<string>(slot, "heroId") == heroId)
                .GetComponent<Selectable>();

        private static bool IsOn(Selectable slot) => (bool)slot.GetType().GetProperty("IsOn")!.GetValue(slot);

        private string SavePath(string name, string id) => Path.Combine(folder, HeroFileKey.Compose(name, id) + ".sav");

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

        [UnityTest]
        public IEnumerator Delete_OfTheOnlyHero_LeavesNoneSelected_AndNothingToPlayOrDelete()
        {
            yield return OpenMenu(withHero: true);
            var popup = Find<MonoBehaviour>("HeroNamePopup");
            var deletePopup = Find<MonoBehaviour>("HeroDeletePopup");
            var aria = Saves.ActiveHeroId;

            Click(Find<Selectable>("HeroDeleteButton"));
            yield return null;
            Click(Member<Component>(deletePopup, "confirm"));
            yield return null;

            Assert.That(deletePopup.gameObject.activeSelf, Is.False);
            Assert.That(Saves.List(), Is.Empty);
            Assert.That(Saves.ActiveHeroId, Is.Null);
            Assert.That(Saves.LastSelectedHeroId, Is.Null, "a later quit cannot bring it back");
            Assert.That(File.Exists(SavePath("Aria", aria)), Is.False);
            Assert.That(Find<Selectable>("PlayButton").gameObject.activeSelf, Is.False);
            Assert.That(Find<Selectable>("HeroDeleteButton").gameObject.activeSelf, Is.False);
            Assert.That(Find<MonoBehaviour>("HeroShowcaseText").GetComponent<TextMeshProUGUI>().text, Is.Empty);
            Assert.That(popup.gameObject.activeSelf, Is.False, "and the slots did not re-select anything, or open the name entry");
            Assert.That(UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(component => component.GetType().Name == "HeroSlotToggle")
                .Any(slot => IsOn(slot.GetComponent<Selectable>())), Is.False, "no slot is on");
        }

        [UnityTest]
        public IEnumerator Delete_SelectsTheNextHeroThatReads()
        {
            string bran = null;
            yield return OpenMenu(withHero: true, seed: saves => bran = saves.Create("Bran").Id);
            var aria = Saves.ActiveHeroId;
            var deletePopup = Find<MonoBehaviour>("HeroDeletePopup");

            Click(Find<Selectable>("HeroDeleteButton"));
            yield return null;
            Click(Member<Component>(deletePopup, "confirm"));
            yield return null;

            Assert.That(Saves.List().Single().Id, Is.EqualTo(bran));
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(bran), "the remaining hero is selected and loaded");
            Assert.That(IsOn(SlotOf(bran)), Is.True);
            Assert.That(Find<Selectable>("PlayButton").interactable, Is.True);
            Assert.That(Find<MonoBehaviour>("HeroShowcaseText").GetComponent<TextMeshProUGUI>().text, Does.Contain("Kills"));
            Assert.That(File.Exists(SavePath("Aria", aria)), Is.False);
        }

        [UnityTest]
        public IEnumerator AnUnreadableSlot_IsSelectable_ShowsItsReason_AndOffersOnlyDelete()
        {
            string bran = null, damaged = null, future = null;
            yield return OpenMenu(withHero: true, seed: saves =>
            {
                bran = saves.Create("Bran").Id;
                damaged = saves.Create("Cass").Id;
                future = saves.Create("Dana").Id;

                File.WriteAllText(SavePath("Cass", damaged), "this is not a save");
                File.WriteAllText(SavePath("Cass", damaged) + ".bak", "nor is this");

                // Written twice: the second leaves the first as the file's backup.
                var newer = new SaveSlot<AnyPayload>(new FileSaveStore(folder), new JsonUtilitySerializer(),
                    HeroFileKey.Compose("Dana", future), 2);
                newer.Save(new AnyPayload());
                newer.Save(new AnyPayload());
            });
            var aria = Saves.ActiveHeroId;
            var showcase = Find<MonoBehaviour>("HeroShowcaseText").GetComponent<TextMeshProUGUI>();
            var play = Find<Selectable>("PlayButton");
            var delete = Find<Selectable>("HeroDeleteButton");
            var deletePopup = Find<MonoBehaviour>("HeroDeletePopup");
            Assert.That(SlotOf(damaged).interactable, Is.True, "an unreadable slot can be chosen");
            Assert.That(SlotOf(future).interactable, Is.True);

            Click(SlotOf(damaged));
            yield return null;

            Assert.That(showcase.text, Is.EqualTo("File damaged and no backup could be read"));
            Assert.That(IsOn(SlotOf(damaged)), Is.True);
            Assert.That(IsOn(SlotOf(aria)), Is.False);
            Assert.That(Saves.LastSelectedHeroId, Is.EqualTo(damaged));
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(aria), "it never entered the Session");
            Assert.That(play.gameObject.activeSelf, Is.False, "only Delete is available");
            Assert.That(delete.gameObject.activeSelf, Is.True);
            Assert.That(delete.interactable, Is.True);
            Assert.That(File.Exists(SavePath("Cass", damaged)), Is.True, "choosing it leaves the file alone");

            Click(play);
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));

            Click(SlotOf(future));
            yield return null;

            Assert.That(showcase.text, Is.EqualTo("Saved by a newer version of the game"));
            Assert.That(play.gameObject.activeSelf, Is.False);
            Assert.That(delete.interactable, Is.True);

            Click(delete);
            yield return null;
            Assert.That(Member<TextMeshProUGUI>(deletePopup, "question").text, Does.Contain("unreadable"));
            Click(Member<Component>(deletePopup, "confirm"));
            yield return null;

            Assert.That(Directory.GetFiles(folder, "*" + future + "*"), Is.Empty, "the file and its backup are both gone");
            Assert.That(File.Exists(SavePath("Cass", damaged)), Is.True, "the other one is untouched");
            Assert.That(Saves.ActiveHeroId, Is.EqualTo(bran), "the newest hero that reads is selected");
            Assert.That(play.interactable, Is.True);
            Assert.That(IsOn(SlotOf(bran)), Is.True);

            Click(SlotOf(damaged));
            yield return null;
            Click(delete);
            yield return null;
            Click(Member<Component>(deletePopup, "confirm"));
            yield return null;

            Assert.That(Directory.GetFiles(folder, "*" + damaged + "*"), Is.Empty, "a damaged file and its backup go too");
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
