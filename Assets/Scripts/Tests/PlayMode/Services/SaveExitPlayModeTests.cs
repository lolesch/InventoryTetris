using NUnit.Framework;
using Submodules.Utility.Persistence;
using Submodules.Utility.Services;
using Submodules.Utility.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The real scenes' Save &amp; Exit (issue #258): in Town it writes and loads the Main Menu with no popup; in
    /// the Field it asks first, and on confirm Returns with the loot kept; Cancel leaves the Run untouched; a
    /// failed write stays in the game with the cause, Retry works and Exit without saving has its own confirm.
    /// Quit Game (issue #259) follows the same rule and then quits, through <see cref="GameExit.Quitter"/> so the
    /// test run survives it, and is hidden in a WebGL build.
    /// The saves are real files in a scratch folder. Boots again over the Input System fixture's isolation,
    /// like <see cref="PauseMenuPlayModeTests"/>.
    /// </summary>
    public sealed class SaveExitPlayModeTests : InputTestFixture
    {
        private string folder;
        private FailableStore store;
        private Action previousQuitter;
        private int quits;
        private DateTime savedAtTheQuit;

        private static IHeroSaveService Saves => ServiceLocator.Get<IHeroSaveService>();
        private static IPauseMenu Menu => ServiceLocator.Get<IPauseMenu>();
        private static ISimulationService Simulation => ServiceLocator.Get<ISimulationService>();
        private static ISession Session => ServiceLocator.Get<ISession>();

        // The save store with a switch that makes every write fail, over the real files.
        private sealed class FailableStore : ISaveStore
        {
            private readonly ISaveStore real;

            public FailableStore(ISaveStore real) => this.real = real;

            public bool FailWrites { get; set; }

            public bool TryRead(string key, out string content) => real.TryRead(key, out content);
            public bool TryReadBackup(string key, out string content) => real.TryReadBackup(key, out content);
            public bool Delete(string key) => real.Delete(key);
            public IReadOnlyCollection<string> Keys() => real.Keys();
            public bool SetAside(string key) => real.SetAside(key);
            public void AppendSideFile(string fileName, string text) => real.AppendSideFile(fileName, text);

            public void Write(string key, string content)
            {
                if (FailWrites)
                    throw new IOException("the disk is full");

                real.Write(key, content);
            }
        }

        [TearDown]
        public void LeaveTheGameAsItWasFound()
        {
            if (ServiceLocator.IsArmed)
            {
                Menu.Close();

                // The services outlive the test with domain reload off, and the next fixture sends its own Run.
                _ = Simulation.LeaveField();
            }

            if (previousQuitter != null)
                GameExit.Quitter = previousQuitter;

            previousQuitter = null;

            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        // Aria on disk and selected, the Main Menu open; or straight into the game scene.
        private IEnumerator Open(string scene)
        {
            folder = Path.Combine(Path.GetTempPath(), "InventoryTetris-saveexit-" + Guid.NewGuid().ToString("N"));
            store = new FailableStore(new FileSaveStore(folder));

            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load(), store);
            _ = InputSystem.AddDevice<Keyboard>();
            _ = Saves.Load(Saves.Create("Aria").Id);

            quits = 0;
            previousQuitter ??= GameExit.Quitter;
            GameExit.Quitter = () =>
            {
                quits++;
                savedAtTheQuit = Saves.List()[0].SavedAtUtc;
            };

            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
        }

        private static MonoBehaviour Find(string typeName) =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(component => component.GetType().Name == typeName);

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);

        private static void Click(Component target)
        {
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            _ = ExecuteEvents.Execute(target.gameObject, click, ExecuteEvents.pointerClickHandler);
        }

        private static MonoBehaviour Popup() => Member<MonoBehaviour>(Find("PauseMenuPopup"), "saveExitPopup");

        private static void ClickQuitGame() => Click(Member<Component>(Find("PauseMenuPopup"), "quitGame"));

        private static void ClickSaveExit() => Click(Member<Component>(Find("PauseMenuPopup"), "saveExit"));

        private static string PopupText() => Member<TextMeshProUGUI>(Popup(), "message").text;

        private static void ClickPopup(string button) => Click(Member<Component>(Popup(), button));

        private static IEnumerator WaitForScene(string name)
        {
            var deadline = Time.realtimeSinceStartup + 30f;

            while (SceneManager.GetActiveScene().name != name && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name));

            // The loader keeps refusing a second load until its loading screen has faded out again.
            while (LoaderIsBusy() && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.That(LoaderIsBusy(), Is.False);
        }

        private static bool LoaderIsBusy() => SceneProvider.Instance.IsLoading;

        // Loot the hero picked up on this Run: a helm in the bag.
        private static ItemInstance PutHelmInBag()
        {
            var helm = ServiceLocator.Get<IItemService>().RollEquipment(EquipmentType.Helm, 0f);
            var package = new Package(null, helm, 1u);
            Assert.That(Session.Hero.Inventory.TryAddToContainer(ref package), Is.True, "premise: the helm fits");
            return helm;
        }

        private static bool BagHolds(ItemInstance item) =>
            Session.Hero.Inventory.StoredPackages.Values.Any(stored => stored.Item.Equals(item));

        private static void SendToTheField() => Simulation.Send(GameBoot.Load().Locations.First());

        [UnityTest]
        public IEnumerator InTown_SaveAndExit_WritesTheHeroAndLoadsTheMenu_WithNoPopup()
        {
            yield return Open("Example");
            var savedBefore = Saves.List()[0].SavedAtUtc;
            Menu.Open();
            yield return null;

            ClickSaveExit();

            Assert.That(Popup().gameObject.activeSelf, Is.False, "Town asks nothing");
            yield return WaitForScene("MainMenu");
            Assert.That(Saves.List()[0].SavedAtUtc, Is.GreaterThan(savedBefore), "the hero was written");
            Assert.That(Menu.IsOpen, Is.False);
        }

        [UnityTest]
        public IEnumerator InTheField_AConfirmAppears_AndCancelLeavesTheRunUntouched()
        {
            yield return Open("Example");
            SendToTheField();
            var helm = PutHelmInBag();
            Menu.Open();
            yield return null;

            ClickSaveExit();

            Assert.That(Popup().gameObject.activeSelf, Is.True);
            Assert.That(PopupText(), Is.EqualTo(SaveAndExit.FieldQuestion));
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InField), "asking ends nothing");

            ClickPopup("back");
            yield return null;

            Assert.That(Popup().gameObject.activeSelf, Is.False);
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InField), "Cancel leaves the Run untouched");
            Assert.That(Menu.IsOpen, Is.True, "and the Pause Menu stays where it was");
            Assert.That(BagHolds(helm), Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"));

            ClickSaveExit();
            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.True, "the Cancel key closes the confirm too");
            Assert.That(Popup().gameObject.activeSelf, Is.False);
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InField));
        }

        [UnityTest]
        public IEnumerator MenuPlayRunSaveAndExitMenu_TheBankedLootIsPresentAfterPlay()
        {
            yield return Open("MainMenu");
            Click(Find("PlayButton"));
            yield return WaitForScene("Example");

            SendToTheField();
            var helm = PutHelmInBag();
            Menu.Open();
            yield return null;

            ClickSaveExit();
            ClickPopup("primary");

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown), "the implicit Return");
            Assert.That(Session.Hero.Corpse.Exists, Is.False, "no Death penalty");
            yield return WaitForScene("MainMenu");

            Click(Find("PlayButton"));
            yield return WaitForScene("Example");

            Assert.That(BagHolds(helm), Is.True, "the Run's loot came back from the file");
        }

        [UnityTest]
        public IEnumerator AFailedWrite_StaysInTheGame_NamesTheCause_AndRetryLeavesOnceItWrites()
        {
            yield return Open("Example");
            SendToTheField();
            var helm = PutHelmInBag();
            Menu.Open();
            yield return null;
            store.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero"));
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero"));

            ClickSaveExit();
            ClickPopup("primary");
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"), "a failed write keeps the player in the game");
            Assert.That(Popup().gameObject.activeSelf, Is.True);
            Assert.That(PopupText(), Does.Contain(SaveResult.Failed(SaveFailure.StoreFailed).Reason));
            Assert.That(BagHolds(helm), Is.True, "the banked loot is still held");

            store.FailWrites = false;
            ClickPopup("primary");

            yield return WaitForScene("MainMenu");
            Assert.That(Saves.List()[0].SavedAtUtc, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-5)));
        }

        [UnityTest]
        public IEnumerator ExitWithoutSaving_NeedsItsOwnConfirm_AndBackReturnsToTheFailure()
        {
            yield return Open("Example");
            Menu.Open();
            yield return null;
            store.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero"));

            ClickSaveExit();
            yield return null;

            Assert.That(Popup().gameObject.activeSelf, Is.True, "Town went straight through, and the failure opened the popup");

            ClickPopup("secondary");
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"), "asking is not leaving");
            Assert.That(PopupText(), Is.EqualTo(SaveAndExit.DiscardQuestion));

            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.True);
            Assert.That(PopupText(), Does.StartWith("The hero was not saved."), "Cancel steps back to the failure");

            ClickPopup("secondary");
            ClickPopup("secondary");

            yield return WaitForScene("MainMenu");
        }

        [UnityTest]
        public IEnumerator InTown_QuitGame_WritesTheHeroThenQuits_WithNoPopup()
        {
            yield return Open("Example");
            var savedBefore = Saves.List()[0].SavedAtUtc;
            Menu.Open();
            yield return null;

            ClickQuitGame();

            Assert.That(Popup().gameObject.activeSelf, Is.False, "Town asks nothing");
            Assert.That(quits, Is.EqualTo(1));
            Assert.That(savedAtTheQuit, Is.GreaterThan(savedBefore), "the hero was written before the quit");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"), "a quit loads no scene");
        }

        [UnityTest]
        public IEnumerator InTheField_QuitGame_AsksFirst_AndCancelQuitsNothing()
        {
            yield return Open("Example");
            SendToTheField();
            var helm = PutHelmInBag();
            Menu.Open();
            yield return null;

            ClickQuitGame();

            Assert.That(Popup().gameObject.activeSelf, Is.True);
            Assert.That(PopupText(), Is.EqualTo(SaveAndExit.FieldQuestion));
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InField), "asking ends nothing");
            Assert.That(quits, Is.Zero);

            ClickPopup("back");
            yield return null;

            Assert.That(Popup().gameObject.activeSelf, Is.False);
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InField));
            Assert.That(BagHolds(helm), Is.True);
            Assert.That(quits, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InTheField_QuitGame_ReturnsAndSavesTheLoot_BeforeItQuits()
        {
            yield return Open("Example");
            SendToTheField();
            var helm = PutHelmInBag();
            Menu.Open();
            yield return null;
            var savedBefore = Saves.List()[0].SavedAtUtc;

            ClickQuitGame();
            ClickPopup("primary");

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown), "the implicit Return");
            Assert.That(Session.Hero.Corpse.Exists, Is.False, "no Death penalty");
            Assert.That(quits, Is.EqualTo(1));
            Assert.That(savedAtTheQuit, Is.GreaterThan(savedBefore), "written before the quit");

            _ = Saves.Load(Saves.List()[0].Id);
            Assert.That(BagHolds(helm), Is.True, "the Run's loot came back from the file");
        }

        [UnityTest]
        public IEnumerator AFailedWriteOnQuitGame_StaysInTheGame_RetryQuitsOnceItWrites()
        {
            yield return Open("Example");
            SendToTheField();
            var helm = PutHelmInBag();
            Menu.Open();
            yield return null;
            store.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero"));
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero"));

            ClickQuitGame();
            ClickPopup("primary");
            yield return null;

            Assert.That(quits, Is.Zero, "a failed write quits nothing");
            Assert.That(Popup().gameObject.activeSelf, Is.True);
            Assert.That(PopupText(), Does.Contain(SaveResult.Failed(SaveFailure.StoreFailed).Reason));
            Assert.That(BagHolds(helm), Is.True);

            store.FailWrites = false;
            ClickPopup("primary");

            Assert.That(quits, Is.EqualTo(1));
            Assert.That(Popup().gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator AFailedWriteOnQuitGame_CanQuitWithoutSaving_BehindItsOwnConfirm()
        {
            yield return Open("Example");
            Menu.Open();
            yield return null;
            store.FailWrites = true;
            LogAssert.Expect(LogType.Error, new Regex("Could not save hero"));

            ClickQuitGame();
            ClickPopup("secondary");

            Assert.That(PopupText(), Is.EqualTo(SaveAndExit.DiscardQuestion));
            Assert.That(quits, Is.Zero, "asking is not quitting");

            ClickPopup("secondary");

            Assert.That(quits, Is.EqualTo(1));

            // The real quit raises Application.quitting, whose save would write the hero the player just chose not to save.
            store.FailWrites = false;
            var savedBefore = Saves.List()[0].SavedAtUtc;
            GameExit.SaveHero(Simulation, Saves);
            Assert.That(Saves.List()[0].SavedAtUtc, Is.EqualTo(savedBefore), "the quit that follows Exit without saving writes nothing");
        }

        [UnityTest]
        public IEnumerator OnceTheGameIsLeaving_NeitherExitButtonTakesAClick()
        {
            yield return Open("Example");
            Menu.Open();
            yield return null;

            ClickQuitGame();

            Assert.That(ExitButton("quitGame").interactable, Is.False);
            Assert.That(ExitButton("saveExit").interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator AnExitTheSceneLoaderRefuses_LeavesBothButtonsLive_SoTheNextClickCanLeave()
        {
            yield return Open("Example");
            Menu.Open();
            yield return null;
            Assert.That(RequestScene("MainMenu"), Is.True, "premise: a load is already under way, as in the loader's fade");
            LogAssert.Expect(LogType.Error, new Regex("while another is still loading"));

            ClickSaveExit();

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"));
            Assert.That(ExitButton("saveExit").interactable, Is.True, "the refused leave locked nothing");
            Assert.That(ExitButton("quitGame").interactable, Is.True);

            yield return WaitForScene("MainMenu");
        }

        [UnityTest]
        public IEnumerator ASceneLoad_IsRefusedWhileOneIsUnderway_AndAcceptedAgainOnceItEnds()
        {
            yield return Open("Example");
            Assert.That(LoaderIsBusy(), Is.False);

            Assert.That(RequestScene("MainMenu"), Is.True);
            Assert.That(LoaderIsBusy(), Is.True, "the loader is busy at once, before its first frame");
            LogAssert.Expect(LogType.Error, new Regex("while another is still loading"));
            Assert.That(RequestScene("MainMenu"), Is.False, "one load at a time, even in the same frame");

            yield return WaitForScene("MainMenu");

            Assert.That(RequestScene("Example"), Is.True, "the loader is free again");
            yield return WaitForScene("Example");
        }

        [UnityTest]
        public IEnumerator ASceneLoad_OfAnUnknownScene_IsRefusedBeforeTheScreenFades()
        {
            yield return Open("Example");
            LogAssert.Expect(LogType.Error, new Regex("not in the build"));

            Assert.That(RequestScene("NoSuchScene"), Is.False);
            Assert.That(LoaderIsBusy(), Is.False, "nothing was started");
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Example"));
        }

        private static UnityEngine.UI.Selectable ExitButton(string field) =>
            Member<Component>(Find("PauseMenuPopup"), field).GetComponent<UnityEngine.UI.Selectable>();

        private static bool RequestScene(string scene) => SceneProvider.Instance.LoadScene(scene);

        [UnityTest]
        public IEnumerator QuitGame_IsHiddenInAWebGLBuild_AndOfferedOnTheOthers()
        {
            yield return Open("Example");
            var panel = Find("PauseMenuPopup");
            var quitGame = Member<Component>(panel, "quitGame").gameObject;
            var apply = panel.GetType().GetMethod("Apply")!;

            _ = apply.Invoke(panel, new object[] { RuntimePlatform.WebGLPlayer });
            Assert.That(quitGame.activeSelf, Is.False);

            _ = apply.Invoke(panel, new object[] { RuntimePlatform.WindowsPlayer });
            Assert.That(quitGame.activeSelf, Is.True);
        }
    }
}
