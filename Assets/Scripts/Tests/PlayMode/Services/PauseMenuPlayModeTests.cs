using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections;
using System.Linq;
using System.Reflection;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The real <c>Example</c> scene's Pause Menu (issue #257): Escape with nothing to peel opens it, in Town and
    /// in the Field; the Run holds while it is open and the player's own Pause comes back as found; the panel
    /// hotkeys and the pause key do nothing; Escape or Resume closes it. Boots again over the Input System
    /// fixture's isolation, like <see cref="CancelPlayModeTests"/>.
    /// </summary>
    public sealed class PauseMenuPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;

        private static IPauseMenu Menu => ServiceLocator.Get<IPauseMenu>();
        private static ISimulationService Simulation => ServiceLocator.Get<ISimulationService>();
        private static InventoryContext Context => ServiceLocator.Get<IInventoryService>().ActiveContext;

        private IEnumerator OpenExample()
        {
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());
            keyboard = InputSystem.AddDevice<Keyboard>();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void LeaveTheGameAsItWasFound()
        {
            if (!ServiceLocator.IsArmed)
                return;

            Menu.Close();
            ServiceLocator.Get<IInventoryService>().CloseContext();

            // The services outlive the test with domain reload off, and the next fixture sends its own Run.
            _ = Simulation.LeaveField();
        }

        // Input time, which the fixture holds still: a press within the repeat guard of the last is one press.
        private IEnumerator Tap(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            currentTime += 1.0;
            PressAndRelease(key);
            yield return null;
        }

        private static MonoBehaviour Popup() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(component => component.GetType().Name == "PauseMenuPopup");

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);

        private static GameObject ScreenObject() => Member<GameObject>(Popup(), "screen");

        private static void SendToTheField() => Simulation.Send(GameBoot.Load().Locations.First());

        [UnityTest]
        public IEnumerator Escape_WithNothingToPeel_OpensTheMenuInTown_AndEscapeAgainClosesIt()
        {
            yield return OpenExample();
            Assert.That(ScreenObject().activeSelf, Is.False, "premise: the screen starts closed");

            yield return Tap(keyboard.escapeKey);

            Assert.That(Menu.IsOpen, Is.True);
            Assert.That(ScreenObject().activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Member<Component>(Popup(), "resume").gameObject),
                "Resume is selected, so Enter closes the menu");

            yield return Tap(keyboard.escapeKey);

            Assert.That(Menu.IsOpen, Is.False);
            Assert.That(ScreenObject().activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Escape_OpensTheMenuInTheField_HoldingTheRun_AndResumeReleasesIt()
        {
            yield return OpenExample();
            SendToTheField();
            yield return null;

            yield return Tap(keyboard.escapeKey);

            Assert.That(Menu.IsOpen, Is.True);
            Assert.That(Simulation.IsHeld, Is.True);
            Assert.That(Simulation.IsPaused, Is.False, "the menu's hold is not the player's Pause");

            var resume = Member<Component>(Popup(), "resume");
            _ = ExecuteEvents.Execute(resume.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

            Assert.That(Menu.IsOpen, Is.False);
            Assert.That(Simulation.IsHeld, Is.False);
        }

        [UnityTest]
        public IEnumerator ASpacePausedRun_IsStillPausedWhenTheMenuCloses()
        {
            yield return OpenExample();
            SendToTheField();
            yield return Tap(keyboard.spaceKey);
            Assert.That(Simulation.IsPaused, Is.True, "premise: Space paused the Run");

            yield return Tap(keyboard.escapeKey);
            yield return Tap(keyboard.escapeKey);

            Assert.That(Menu.IsOpen, Is.False);
            Assert.That(Simulation.IsHeld, Is.False);
            Assert.That(Simulation.IsPaused, Is.True);
        }

        [UnityTest]
        public IEnumerator WhileTheMenuIsOpen_ThePanelHotkeysAndThePauseKeyDoNothing()
        {
            yield return OpenExample();
            SendToTheField();
            yield return Tap(keyboard.escapeKey);

            yield return Tap(keyboard.sKey);
            yield return Tap(keyboard.iKey);
            yield return Tap(keyboard.hKey);
            yield return Tap(keyboard.spaceKey);

            Assert.That(Context, Is.EqualTo(InventoryContext.None));
            Assert.That(Simulation.IsPaused, Is.False);

            yield return Tap(keyboard.escapeKey);
            yield return Tap(keyboard.sKey);

            Assert.That(Context, Is.EqualTo(InventoryContext.Stash), "the hotkeys work again once it is closed");
        }

        [UnityTest]
        public IEnumerator AnOpenPanel_IsPeeledBeforeTheMenuOpens()
        {
            yield return OpenExample();
            yield return Tap(keyboard.iKey);
            Assert.That(Context, Is.EqualTo(InventoryContext.Hero), "premise");

            yield return Tap(keyboard.escapeKey);

            Assert.That(Context, Is.EqualTo(InventoryContext.None));
            Assert.That(Menu.IsOpen, Is.False, "the press closed the panel and went no further");

            yield return Tap(keyboard.escapeKey);

            Assert.That(Menu.IsOpen, Is.True);
        }

        [UnityTest]
        public IEnumerator LeavingTheScene_ClosesTheMenu_SoTheRunIsNeverLeftHeld()
        {
            yield return OpenExample();
            SendToTheField();
            yield return Tap(keyboard.escapeKey);
            Assert.That(Simulation.IsHeld, Is.True, "premise");

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;

            Assert.That(Menu.IsOpen, Is.False);
            Assert.That(Simulation.IsHeld, Is.False);
        }
    }
}
