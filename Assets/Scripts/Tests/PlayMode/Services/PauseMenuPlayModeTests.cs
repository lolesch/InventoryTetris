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
    /// The real <c>Example</c> scene's Pause Menu (issue #257) wired to the Escape key and its Resume button, and
    /// closed by a scene change. The rules are <c>PauseMenuTests</c>'. Boots again over the Input System
    /// fixture's isolation, like <see cref="CancelPlayModeTests"/>.
    /// </summary>
    public sealed class PauseMenuPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;

        private static IPauseMenu Menu => ServiceLocator.Get<IPauseMenu>();
        private static ISimulationService Simulation => ServiceLocator.Get<ISimulationService>();

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

        private static void SendToTheField() => Simulation.Send(GameBoot.Load().Locations.First(location => location.Id == "location.thornwood"));

        [UnityTest]
        public IEnumerator Escape_OpensTheMenuInTheField_HoldingTheRun_AndResumeReleasesIt()
        {
            yield return OpenExample();
            SendToTheField();
            yield return null;

            yield return Tap(keyboard.escapeKey);

            Assert.That(Menu.IsOpen, Is.True);
            Assert.That(Simulation.IsMenuFrozen, Is.True);
            Assert.That(Simulation.IsPaused, Is.False, "the menu's hold is not the player's Pause");

            var resume = Member<Component>(Popup(), "resume");
            _ = ExecuteEvents.Execute(resume.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);

            Assert.That(Menu.IsOpen, Is.False);
            Assert.That(Simulation.IsMenuFrozen, Is.False);
        }

        [UnityTest]
        public IEnumerator LeavingTheScene_ClosesTheMenu_SoTheRunIsNeverLeftHeld()
        {
            yield return OpenExample();
            SendToTheField();
            yield return Tap(keyboard.escapeKey);
            Assert.That(Simulation.IsMenuFrozen, Is.True, "premise");

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;

            Assert.That(Menu.IsOpen, Is.False);
            Assert.That(Simulation.IsMenuFrozen, Is.False);
        }
    }
}
