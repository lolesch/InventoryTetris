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
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The real <c>Example</c> scene's entry notice (issue #256): a hero loaded from its backup, or with packages
    /// left out, is told once on entering the game; a clean load is told nothing, and a later scene change shows
    /// nothing again. The saves are real files in a scratch folder. Boots again over the Input System fixture's
    /// isolation, like <see cref="MainMenuPlayModeTests"/>.
    /// </summary>
    public sealed class EntryNoticePlayModeTests : InputTestFixture
    {
        private string folder;
        private FileSaveStore store;

        private static IHeroSaveService Saves => ServiceLocator.Get<IHeroSaveService>();

        [TearDown]
        public void RemoveTheScratchSaves()
        {
            if (folder != null && Directory.Exists(folder))
                Directory.Delete(folder, true);
        }

        private string KeyOf(string id) => store.Keys().Single(key => HeroFileKey.IdOf(key) == id);

        // A hero on disk, made to go wrong by `damage`, loaded as Play would load it, then the game scene.
        private IEnumerator EnterGame(Action<HeroSummary> damage)
        {
            folder = Path.Combine(Path.GetTempPath(), "InventoryTetris-notice-" + Guid.NewGuid().ToString("N"));
            store = new FileSaveStore(folder);

            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load(), store);
            _ = InputSystem.AddDevice<Keyboard>();

            var hero = Saves.Create("Aria");
            damage?.Invoke(hero);
            Assert.That(Saves.Load(hero.Id).Entered, Is.True, "premise: the hero loads");

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        private void DamageThePrimary(HeroSummary hero) => store.Write(KeyOf(hero.Id), "{ truncated");

        private void LeaveAnItemWhoseDefinitionIsGone(HeroSummary hero)
        {
            var slot = new SaveSlot<HeroDto>(store, new JsonUtilitySerializer(), KeyOf(hero.Id), 1);
            var saved = slot.Load().Payload;
            saved.stash.packages = saved.stash.packages.Append(new PackageDto
            {
                x = 7,
                y = 5,
                instance = new ItemInstance("deleted-in-a-patch", ItemRarity.Common, 1, null).ToDto(),
                amount = 1u,
            }).ToArray();
            slot.Save(saved);
        }

        private static MonoBehaviour Notice() =>
            UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(component => component.GetType().Name == "EntryNoticePopup");

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);

        private static void Click(Component target)
        {
            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            _ = ExecuteEvents.Execute(target.gameObject, click, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator ABackupRestore_ShowsTheNoticeOnEnteringTheGame_ThenOKClosesIt()
        {
            yield return EnterGame(DamageThePrimary);
            var notice = Notice();
            var screen = Member<GameObject>(notice, "screen");

            Assert.That(screen.activeSelf, Is.True);
            Assert.That(Member<TextMeshProUGUI>(notice, "message").text, Is.EqualTo(EntryNotice.BackupText));
            Assert.That(Member<Component>(notice, "show").gameObject.activeSelf, Is.False, "no names to show");

            Click(Member<Component>(notice, "dismiss"));

            Assert.That(screen.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TheNotice_DoesNotShowAgainOnALaterSceneChange()
        {
            yield return EnterGame(DamageThePrimary);
            Assert.That(Member<GameObject>(Notice(), "screen").activeSelf, Is.True, "premise: shown on entry");

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;

            Assert.That(Member<GameObject>(Notice(), "screen").activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator APartialLoad_ShowsTheCount_AndTheNamesOnlyOnShow()
        {
            yield return EnterGame(LeaveAnItemWhoseDefinitionIsGone);
            var notice = Notice();
            var names = Member<TextMeshProUGUI>(notice, "names");

            Assert.That(Member<GameObject>(notice, "screen").activeSelf, Is.True);
            Assert.That(Member<TextMeshProUGUI>(notice, "message").text, Is.EqualTo("1 item couldn't be restored"));
            Assert.That(names.gameObject.activeSelf, Is.False, "the names wait behind Show");

            var show = Member<Component>(notice, "show");
            Click(show);

            Assert.That(names.gameObject.activeSelf, Is.True);
            Assert.That(names.text, Is.EqualTo("deleted-in-a-patch (Stash)"));
            Assert.That(show.gameObject.activeSelf, Is.False, "Show has been answered");
        }

        [UnityTest]
        public IEnumerator ACleanLoad_ShowsNothing()
        {
            yield return EnterGame(null);

            Assert.That(Member<GameObject>(Notice(), "screen").activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator TheCancelKey_DismissesTheNotice_AndASecondPressOpensThePauseMenu()
        {
            yield return EnterGame(DamageThePrimary);
            var screen = Member<GameObject>(Notice(), "screen");
            Assert.That(screen.activeSelf, Is.True, "premise");

            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.True, "the open notice consumes the press");
            Assert.That(screen.activeSelf, Is.False);
            Assert.That(ServiceLocator.Get<ICancelChain>().Cancel(), Is.True, "nothing left above it, the press reaches the Pause Menu");
            Assert.That(ServiceLocator.Get<IPauseMenu>().IsOpen, Is.True);
        }
    }
}
