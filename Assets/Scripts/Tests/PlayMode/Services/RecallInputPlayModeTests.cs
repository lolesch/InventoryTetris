using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections;
using System.Linq;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The three Recall inputs on the real <c>Example</c> scene (issue #371, ADR-0022): the <c>B</c> key through a
    /// virtual keyboard, the HUD Recall button with its scroll count, and a right-click on a Portal Scroll in the bag.
    /// The views live in the predefined assembly, so they are read by type name.
    /// </summary>
    public sealed class RecallInputPlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private LocationConfig location;

        private static ISimulationService Simulation => ServiceLocator.Get<ISimulationService>();

        private IEnumerator OpenExample()
        {
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());

            keyboard = InputSystem.AddDevice<Keyboard>();
            _ = InputSystem.AddDevice<Mouse>();
            location = GameBoot.Load().Locations.First(l => l.Id == "location.thornwood");

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void LeaveTheFieldAndTheBag()
        {
            if (!ServiceLocator.IsArmed)
                return;

            Time.timeScale = 1f;
            ServiceLocator.Get<IInventoryService>().CloseContext();

            // A hero cannot be replaced while a Run is in the Field, held or live.
            if (Simulation.Run.IsRecalling)
                Simulation.Tick(1f);
            if (Simulation.Run.Phase == RunPhase.Held)
                Simulation.Resume();
            if (Simulation.Run.Phase == RunPhase.Live)
                _ = Simulation.Return();

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static void GiveScrolls(uint amount)
        {
            var package = new Package(null, ItemService.Instance.RollConsumable(ConsumableType.PortalScroll, 0f), amount);
            Assert.That(Session.Instance.Hero.Inventory.TryAddToContainer(ref package), Is.True, "premise: the scrolls fit the bag");
        }

        private static int ScrollsInTheBag => Simulation.PortalScrollCount;

        private void SendToTheField()
        {
            Session.Instance.Hero.IsInvincible = true;
            Simulation.Send(location);
        }

        private IEnumerator PressRecallKey()
        {
            // Input time, which the fixture holds still: a press within the repeat guard of the last is one press.
            currentTime += 1.0;
            PressAndRelease(keyboard.bKey);
            yield return null;
        }

        private static Component TheScene(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Single(c => c.GetType().Name == typeName);

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(owner);

        private static Selectable RecallButton => (Selectable)TheScene("RecallButton");

        private static string ButtonCount => Member<TMP_Text>(TheScene("RecallButton"), "countLabel").text;

        [UnityTest]
        public IEnumerator TheKey_RecallsALiveRunOnce_AndSpendsOneScroll()
        {
            yield return OpenExample();
            GiveScrolls(3u);
            SendToTheField();

            yield return PressRecallKey();
            yield return PressRecallKey();

            Assert.That(Simulation.Run.IsRecalling, Is.True);
            Assert.That(ScrollsInTheBag, Is.EqualTo(2), "the second press found the portal opening");
        }

        [UnityTest]
        public IEnumerator TheKey_DoesNothingInTown()
        {
            yield return OpenExample();
            GiveScrolls(2u);

            yield return PressRecallKey();

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.None));
            Assert.That(ScrollsInTheBag, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator TheButton_ShowsTheBagsScrolls_AndIsLiveOnlyWhileARecallWouldWork()
        {
            yield return OpenExample();
            yield return null;
            Assert.That(RecallButton.interactable, Is.False, "no scroll, no Run");
            Assert.That(ButtonCount, Is.EqualTo("0"));

            GiveScrolls(2u);
            yield return null;
            Assert.That(ButtonCount, Is.EqualTo("2"), "the count follows the bag");
            Assert.That(RecallButton.interactable, Is.False, "Town has no Run to Recall");

            SendToTheField();
            yield return null;
            Assert.That(RecallButton.interactable, Is.True);

            ExecuteEvents.Execute(RecallButton.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);
            yield return null;

            Assert.That(Simulation.Run.IsRecalling, Is.True);
            Assert.That(ButtonCount, Is.EqualTo("1"));
            Assert.That(RecallButton.interactable, Is.False, "the portal is opening");
        }

        [UnityTest]
        public IEnumerator AfterAComplete_TheButtonIsStillEnabled()
        {
            yield return OpenExample();
            GiveScrolls(1u);
            SendToTheField();
            for (var i = 0; i < 20000 && Simulation.Run.Encounter.Phase != SimulationPhase.Complete; i++)
                Simulation.Tick(0.1f);
            Assert.That(Simulation.Run.Encounter.Phase, Is.EqualTo(SimulationPhase.Complete), "premise: the Population cleared");

            yield return null;

            Assert.That(RecallButton.interactable, Is.True);
        }

        private static void RightClickTheScroll()
        {
            var bag = Session.Instance.Hero.Inventory;
            var cell = bag.StoredPackages.Single().Key;

            var slot = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(c => c.GetType().Name == "InventorySlotDisplay")
                .Single(c => ReferenceEquals(Property(c, "Container"), bag) && (Vector2Int)Property(c, "Position") == cell);

            ExecuteEvents.Execute(slot.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right },
                ExecuteEvents.pointerClickHandler);
        }

        [UnityTest]
        public IEnumerator ARightClickOnAScrollInTheBag_Recalls()
        {
            yield return OpenExample();
            GiveScrolls(2u);
            SendToTheField();

            RightClickTheScroll();
            yield return null;

            Assert.That(Simulation.Run.IsRecalling, Is.True);
            Assert.That(ScrollsInTheBag, Is.EqualTo(1), "one scroll spent");
        }

        [UnityTest]
        public IEnumerator ARightClickOnAScroll_WhenTheRecallIsRefused_LeavesItInTheBag()
        {
            yield return OpenExample();
            GiveScrolls(2u);

            RightClickTheScroll();
            yield return null;

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.None));
            Assert.That(ScrollsInTheBag, Is.EqualTo(2), "a refused Recall does not consume the scroll");
        }

        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    }
}
