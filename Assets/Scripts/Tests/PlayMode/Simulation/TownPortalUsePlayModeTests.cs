using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// A click on the Town Portal on the real <c>Example</c> scene (issue #368, ADR-0022): with the Run held it takes the
    /// hero back to the same fight and selects its Location; with no Run it Sends to the portal's own Location with the
    /// figure arriving through the portal. The views live in the predefined assembly, so they are read by type name.
    /// </summary>
    public sealed class TownPortalUsePlayModeTests
    {
        private const float WaitSeconds = 12f;

        private LocationConfig _thornwood;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _thornwood = GameBoot.Load().Locations.First(location => location.Id == "location.thornwood");

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            InventoryService.Instance.CloseContext();

            if (Simulation.Run.IsRecalling)
                Simulation.Tick(1f);
            if (Simulation.Run.Phase == RunPhase.Held)
                Simulation.Resume();
            if (Simulation.Run.Phase == RunPhase.Live)
                _ = Simulation.Return();

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static ISimulationService Simulation => SimulationService.Instance;

        private static Component[] Named(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(c => c.GetType().Name == typeName).Cast<Component>().ToArray();

        private static bool Flag(object target, string name) => (bool)target.GetType().GetProperty(name).GetValue(target);

        private static Component FieldFace() => Named("FieldFacePanel").Single();

        private static Component TownFace() => Named("RunPhasePanel").Single();

        private static Component ToggleOf(LocationConfig location) =>
            Named("LocationToggle").First(c => Equals(c.GetType().GetProperty("Location").GetValue(c), location));

        private static void ClickThePortal() =>
            ((IPointerClickHandler)Named("TownPortalMarker").Single())
                .OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });

        private static IEnumerator Until(System.Func<bool> condition, string because)
        {
            var until = Time.realtimeSinceStartup + WaitSeconds;
            while (!condition() && Time.realtimeSinceStartup < until)
                yield return null;

            Assert.That(condition(), Is.True, because);
        }

        // The Field map up, the hero Sent to Thornwood and walked in, a few ticks fought, then Recalled and held.
        private IEnumerator HeldAtThornwood()
        {
            _ = FieldFace().GetType().GetMethod("ToggleState").Invoke(FieldFace(), new object[] { true });
            yield return Until(() => Flag(FieldFace(), "IsExtended"), "premise: the Field map is up");

            var toggle = ToggleOf(_thornwood);
            _ = toggle.GetType().GetMethod("SetToggle").Invoke(toggle, new object[] { true });

            Time.timeScale = 20f;
            yield return null;
            var figure = Named("HeroFigure").Single();
            for (var frame = 0; Flag(figure, "IsWalking") && frame < 3000; frame++)
                yield return null;
            Time.timeScale = 1f;

            for (var tick = 0; tick < 12; tick++)
                Simulation.Tick(0.5f);

            var package = new Package(null, ItemService.Instance.RollConsumable(ConsumableType.PortalScroll, 0f), 1u);
            Assert.That(Session.Instance.Hero.Inventory.TryAddToContainer(ref package), Is.True, "premise: the scroll fits the bag");
            Simulation.Recall();
            Simulation.Tick(1f);
            yield return null;
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "premise: the portal is open");

            yield return Until(() => Flag(TownFace(), "IsExtended"), "premise: the Town map is up");
            yield return Until(() => Flag(FieldFace(), "IsCollapsed"), "premise: the Field map has gone");
            yield return Until(() => !Flag(toggle, "IsOn"), "premise: the Location's highlight went with the Field map");
        }

        [UnityTest]
        public IEnumerator TheTownPortal_WithTheRunHeld_ResumesTheSameFight_AndSelectsItsLocation()
        {
            yield return HeldAtThornwood();
            var encounter = Simulation.Run.Encounter;
            var progress = encounter.ProgressText;

            ClickThePortal();
            yield return null;

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Live), "the Run is live again");
            Assert.That(Simulation.Run.Encounter, Is.SameAs(encounter), "the same fight: no Send or Relocate was issued");
            Assert.That(Simulation.Run.Encounter.ProgressText, Is.EqualTo(progress), "at the same \"Encounter N of M\"");
            Assert.That(Session.Instance.Hero.TownPortal, Is.Null, "the portal is gone");
            Assert.That(Flag(FieldFace(), "IsExtended") || !Flag(FieldFace(), "IsCollapsed"), Is.True, "the Field map is coming back");
            Assert.That(Flag(ToggleOf(_thornwood), "IsOn"), Is.True, "the Location's toggle is selected");
            Assert.That(Flag(TownFace(), "IsExtended"), Is.False, "the Town map gives way");
        }

        [UnityTest]
        public IEnumerator TheTownPortal_WithNoHeldRun_SendsToItsOwnLocation_WithTheFigureArrivingNotWalking()
        {
            Session.Instance.Hero.MarkCompleted(_thornwood.Id);
            var ashfall = GameBoot.Load().Locations.First(location => location.Id == "location.ashfall");
            Session.Instance.Hero.TownPortal = ashfall;
            yield return null;
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.None), "premise: no Run is held");

            ClickThePortal();
            for (var frame = 0; frame < 5; frame++)
            {
                yield return null;
                Assert.That(Flag(Named("HeroFigure").Single(), "IsWalking"), Is.False, "the figure arrives through the portal, he does not walk in");
            }

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Live), "a Run is Sent");
            Assert.That(Session.Instance.Hero.SelectedLocation, Is.EqualTo(ashfall), "to the portal's Location, not the selected one");
            Assert.That(Flag(ToggleOf(ashfall), "IsOn"), Is.True, "whose toggle is selected");
            Assert.That(Flag(FieldFace(), "IsExtended") || !Flag(FieldFace(), "IsCollapsed"), Is.True, "and the Field map is up");
        }
    }
}
