using System.Collections;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.UI;
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

        // The Field map up, the hero Sent to Thornwood and walked in, a few ticks fought.
        private IEnumerator FightingBeforeTheRecall()
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
        }

        // Recalled and held, the hero in Town.
        private IEnumerator HeldAtThornwood()
        {
            yield return FightingBeforeTheRecall();

            Simulation.Recall();
            Simulation.Tick(1f);
            yield return null;
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "premise: the portal is open");

            yield return Until(() => Flag(TownFace(), "IsExtended"), "premise: the Town map is up");
            yield return Until(() => Flag(FieldFace(), "IsCollapsed"), "premise: the Field map has gone");
            yield return Until(() => !Flag(ToggleOf(_thornwood), "IsOn"), "premise: the Location's highlight went with the Field map");
        }

        [UnityTest]
        public IEnumerator TheTownPortal_WithTheRunHeld_ResumesTheSameFight_AndSelectsItsLocation()
        {
            yield return HeldAtThornwood();
            var encounter = Simulation.Run.Encounter;
            var progress = encounter.ProgressText;

            ClickThePortal();
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "the Run waits while the hero walks into the portal");
            yield return Until(() => Simulation.Run.Phase == RunPhase.Live, "the hero has gone into the portal and the Run resumes");
            yield return null;

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Live), "the Run is live again");
            Assert.That(Simulation.Run.Encounter, Is.SameAs(encounter), "the same fight: no Send or Relocate was issued");
            Assert.That(Simulation.Run.Encounter.ProgressText, Is.EqualTo(progress), "at the same \"Encounter N of M\"");
            Assert.That(Session.Instance.Hero.TownPortal, Is.Null, "the portal is gone");
            Assert.That(Flag(FieldFace(), "IsExtended") || !Flag(FieldFace(), "IsCollapsed"), Is.True, "the Field map is coming back");
            Assert.That(Flag(ToggleOf(_thornwood), "IsOn"), Is.True, "the Location's toggle is selected");
            Assert.That(Flag(TownFace(), "IsExtended"), Is.False, "the Town map gives way");
        }

        private static Component FieldMarker() => Named("FieldPortalMarker").Single();

        private static void ClickTheFieldMarker() =>
            ((IPointerClickHandler)FieldMarker())
                .OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });

        // Held and in Town: out through the Town Exit, as a player walks to the Recall point.
        private IEnumerator HeldAndWalkedOutToTheField()
        {
            yield return HeldAtThornwood();

            GameObject.Find("ToFieldsButton").GetComponent<AbstractButton>()
                .OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            yield return Until(() => Flag(FieldFace(), "IsExtended"), "premise: the Field map is up");
            yield return null;
        }

        // What a pointer over the marker's centre hits, front to back.
        private static System.Collections.Generic.List<GameObject> RaycastHitsAt(Component marker)
        {
            var camera = marker.GetComponentInParent<Canvas>().rootCanvas.worldCamera;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, marker.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Select(hit => hit.gameObject).ToList();
        }

        [UnityTest]
        public IEnumerator WalkingOutOfTownWhileHeld_ShowsTheFieldMarker_WithNothingOfTheFieldMapInFrontOfIt()
        {
            yield return HeldAndWalkedOutToTheField();

            var marker = FieldMarker();
            Assert.That(marker.GetComponent<UnityEngine.UI.Image>().enabled, Is.True, "the marker is shown at the Recall point");

            // A side panel on a small screen may cover the point; what the Field map itself must not do is sit on top.
            var hits = RaycastHitsAt(marker);
            Assert.That(hits, Does.Contain(marker.gameObject), "a click can land on it");
            var inFront = hits.TakeWhile(hit => hit != marker.gameObject).Where(hit => hit.transform.IsChildOf(FieldFace().transform));
            Assert.That(inFront.Select(hit => hit.name), Is.Empty, "and nothing on the Field map covers it");
        }

        [UnityTest]
        public IEnumerator TheFieldMarker_WithTheRunHeld_TakesTheHeroToTown_AndTheRunStaysHeldWithThePortalOpen()
        {
            yield return HeldAndWalkedOutToTheField();
            var encounter = Simulation.Run.Encounter;

            ClickTheFieldMarker();
            yield return Until(() => Flag(TownFace(), "IsExtended"), "the hero walks into the portal and the Town map comes up");

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "the Run is still held");
            Assert.That(Simulation.Run.Encounter, Is.SameAs(encounter), "on the same fight");
            Assert.That(Session.Instance.Hero.TownPortal, Is.EqualTo(_thornwood), "the portal did not vanish: it is a one-way street");
            Assert.That(Named("TownPortalMarker").Single().GetComponent<UnityEngine.UI.Image>().enabled, Is.True, "and it waits in Town");
        }

        [UnityTest]
        public IEnumerator TheFieldMarkerThenTheTownPortal_GoToTownAndBack_AndOnlyTheTownSideClosesThePortal()
        {
            yield return HeldAndWalkedOutToTheField();
            ClickTheFieldMarker();
            yield return Until(() => Flag(TownFace(), "IsExtended"), "premise: the hero is in Town");

            ClickThePortal();
            yield return Until(() => Simulation.Run.Phase == RunPhase.Live, "the Town Portal resumes the Run");
            yield return null;

            Assert.That(Session.Instance.Hero.TownPortal, Is.Null, "using the portal from Town closes it");
            Assert.That(Flag(ToggleOf(_thornwood), "IsOn"), Is.True, "with the Location selected");
        }

        [UnityTest]
        public IEnumerator ClickingTheTownExitAndComingStraightBackWithTheRunHeld_StandsTheTownHeroAtTheTownExit_NotMidWalk()
        {
            yield return HeldAtThornwood();
            var figure = Named("TownHeroFigure").Single();

            GameObject.Find("ToFieldsButton").GetComponent<AbstractButton>()
                .OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            yield return null;
            Assume.That(Flag(figure, "IsWalking"), Is.True, "premise: he is still on his way to the Town Exit");

            GameObject.Find("ToTownButton").GetComponent<AbstractButton>()
                .OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            yield return Until(() => Flag(TownFace(), "IsExtended"), "premise: the Town map is back");

            var exit = (RectTransform)figure.GetType().GetField("_exit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(figure);
            var expected = (Vector2)figure.transform.parent.InverseTransformPoint(exit.TransformPoint(exit.rect.center));

            Assert.That(Flag(figure, "IsWalking"), Is.False, "he is not on the walk he left off");
            Assert.That(figure.transform.localPosition.x, Is.EqualTo(expected.x).Within(1f), "he stands at the Town Exit");
            Assert.That(figure.transform.localPosition.y, Is.EqualTo(expected.y).Within(1f), "he stands at the Town Exit");
        }

        [UnityTest]
        public IEnumerator TheTownHero_WalksIntoThePortalAndShrinksAndFades_BeforeTheMapChanges()
        {
            yield return HeldAtThornwood();
            var figure = Named("TownHeroFigure").Single();
            var group = figure.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(1f), "premise: he is whole");

            ClickThePortal();
            yield return Until(() => group.alpha < 1f, "he starts to fade");

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "while the map has not changed yet");
            Assert.That(figure.transform.localScale.y, Is.LessThan(1f), "and he shrinks with it");
            yield return Until(() => Simulation.Run.Phase == RunPhase.Live, "then the Run resumes");
        }

        [UnityTest]
        public IEnumerator TheFieldMarker_WhileThePortalIsStillOpening_IsNotClickable()
        {
            yield return FightingBeforeTheRecall();
            Simulation.Recall();
            yield return null;
            Assume.That(Simulation.Run.IsRecalling, Is.True, "premise: the portal is opening");

            ClickTheFieldMarker();

            Assert.That(Simulation.Run.IsRecalling, Is.True, "a click only resumes a held Run");
            Assert.That(RaycastHitsAt(FieldMarker()), Has.No.Member(FieldMarker().gameObject), "the opening swallows no clicks");
        }

        [UnityTest]
        public IEnumerator AfterARelocateFromHeld_TheFieldMarkerIsGone_ThoughThePortalStaysOpen()
        {
            Session.Instance.Hero.MarkCompleted(_thornwood.Id);
            var ashfall = GameBoot.Load().Locations.First(location => location.Id == "location.ashfall");
            yield return HeldAndWalkedOutToTheField();

            Simulation.Relocate(ashfall);
            yield return null;

            Assert.That(Session.Instance.Hero.TownPortal, Is.EqualTo(_thornwood), "the portal stays open");
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Live), "on a new Run");
            Assert.That(FieldMarker().GetComponent<UnityEngine.UI.Image>().enabled, Is.False, "with no marker on the Field map");
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
            yield return Until(() => Simulation.Run.Phase == RunPhase.Live, "the hero goes into the portal and a Run is Sent");
            yield return null;
            Assert.That(Session.Instance.Hero.TownPortal, Is.Null, "using the portal from Town closes it");
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
