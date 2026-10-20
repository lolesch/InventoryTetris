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
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// A Recall's look on the real <c>Example</c> scene (issue #367, ADR-0022): a portal opens beside the Field figure,
    /// the figure goes into it, the Field map gives way to the Town map with the figure at the Waypoint and the
    /// Town Portal beside it, and the Town Stops work while the Run is held. The views live in the predefined
    /// assembly, so they are read by type name.
    /// </summary>
    public sealed class RecallPlayModeTests
    {
        private const float WaitSeconds = 12f;

        private LocationConfig _location;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _location = GameBoot.Load().Locations.First(location => location.Id == "location.thornwood");

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            InventoryService.Instance.CloseContext();

            // A hero cannot be replaced while a Run is in the Field, held or live.
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

        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(owner);

        private static bool Flag(object target, string name) => (bool)Property(target, name);

        private static Component TownFace() => Named("RunPhasePanel").Single();

        private static Component FieldFace() => Named("FieldFacePanel").Single();

        private static Image PortalImage(string typeName) => Named(typeName).Single().GetComponent<Image>();

        // The SidePanel authored with the context, by the scene's stored enum value (Healer = 4).
        private static Component SidePanelOf(int context) =>
            Named("SidePanel").Single(panel => Member<InventoryContext>(panel, "inventoryContext") == (InventoryContext)context);

        private static IEnumerator Until(System.Func<bool> condition, string because)
        {
            var until = Time.realtimeSinceStartup + WaitSeconds;
            while (!condition() && Time.realtimeSinceStartup < until)
                yield return null;

            Assert.That(condition(), Is.True, because);
        }

        private static void GiveAScroll()
        {
            var package = new Package(null, ItemService.Instance.RollConsumable(ConsumableType.PortalScroll, 0f), 1u);
            Assert.That(Session.Instance.Hero.Inventory.TryAddToContainer(ref package), Is.True, "premise: the scroll fits the bag");
        }

        // The Field map up, the hero Sent and walked in, a few ticks fought: a Recall is possible and has a fight to hold.
        private IEnumerator FightingInTheField()
        {
            _ = FieldFace().GetType().GetMethod("ToggleState").Invoke(FieldFace(), new object[] { true });
            yield return Until(() => Flag(FieldFace(), "IsExtended"), "premise: the Field map is up");

            var toggle = Named("LocationToggle").First(c => Equals(Property(c, "Location"), _location));
            _ = toggle.GetType().GetMethod("SetToggle").Invoke(toggle, new object[] { true });

            // The figure walks in first; run the walk at speed.
            Time.timeScale = 20f;
            yield return null;
            var figure = Named("HeroFigure").Single();
            for (var frame = 0; Flag(figure, "IsWalking") && frame < 3000; frame++)
                yield return null;
            Time.timeScale = 1f;
            Assert.That(Flag(figure, "IsWalking"), Is.False, "premise: the figure walked in");

            for (var tick = 0; tick < 12; tick++)
                Simulation.Tick(0.5f);
            yield return null;
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Live), "premise: the fight goes on");
        }

        private IEnumerator RecalledFromTheFight()
        {
            yield return FightingInTheField();
            GiveAScroll();
            Simulation.Recall();
            yield return null;
        }

        // The opening runs out on the sim clock, and the Run is held.
        private static IEnumerator OpenThePortal()
        {
            Simulation.Tick(1f);
            yield return null;
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "premise: the portal is open");
        }

        [UnityTest]
        public IEnumerator ARecall_OpensAPortalBesideTheFigureAtOnce_WhileTheFieldMapStaysUp()
        {
            yield return RecalledFromTheFight();

            Assert.That(PortalImage("FieldPortalMarker").enabled, Is.True, "a portal opens as the opening starts");
            Assert.That(Simulation.Run.IsRecalling, Is.True, "premise: the portal is still opening");
            Assert.That(Flag(FieldFace(), "IsExtended"), Is.True, "the hero keeps fighting on the Field map");
            Assert.That(Flag(TownFace(), "IsCollapsed"), Is.True, "no Town map yet");
        }

        [UnityTest]
        public IEnumerator OnceTheRunIsHeld_TheFigureGoesIntoThePortal_AndTheTownMapComesUp()
        {
            yield return RecalledFromTheFight();
            yield return OpenThePortal();

            yield return Until(() => Flag(Named("HeroFigure").Single(), "HasEnteredPortal"), "the figure walks into the portal");
            yield return Until(() => Flag(TownFace(), "IsExtended"), "the Town map comes up");

            yield return Until(() => Flag(FieldFace(), "IsCollapsed"), "the Field map crossfades away");
            Assert.That(PortalImage("TownPortalMarker").enabled, Is.True, "the Town Portal is up");
            Assert.That(Session.Instance.Hero.TownPortal, Is.EqualTo(_location));

            var figure = Named("TownHeroFigure").Single();
            var portal = Named("TownPortalMarker").Single();
            var beside = (Vector2)portal.GetType().GetProperty("Beside").GetValue(portal);
            var standsAt = (Vector2)figure.transform.parent.InverseTransformPoint(
                portal.transform.TransformPoint(((RectTransform)portal.transform).rect.center)) + beside;

            // He comes out on top of the portal and walks off it to its Beside point.
            yield return Until(() => figure.GetComponent<CanvasGroup>().alpha >= 1f && !Flag(figure, "IsWalking"), "he has stepped out and walked off");
            Assert.That(Vector2.Distance(figure.transform.localPosition, standsAt), Is.LessThan(1f),
                "the figure stands beside the Town Portal by its Beside offset");
        }

        [UnityTest]
        public IEnumerator AResume_TakesTheFigureOutOfThePortal_AndClosesItsMarkerOnTheFieldMap()
        {
            yield return RecalledFromTheFight();
            yield return OpenThePortal();
            yield return Until(() => Flag(Named("HeroFigure").Single(), "HasEnteredPortal"), "premise: the figure went in");

            Simulation.Resume();
            yield return null;

            Assert.That(Flag(Named("HeroFigure").Single(), "HasEnteredPortal"), Is.False, "he is out of the portal");
            Assert.That(PortalImage("FieldPortalMarker").enabled, Is.False, "and the portal on the Field map is closed");
            Assert.That(PortalImage("TownPortalMarker").enabled, Is.False, "and so is the Town Portal");
        }

        [UnityTest]
        public IEnumerator WhileTheRunIsHeld_TheHealerOpensAndRefillsTheHero_AndTheStashOpens()
        {
            yield return RecalledFromTheFight();
            yield return OpenThePortal();
            yield return Until(() => Flag(TownFace(), "IsExtended"), "premise: the Town map is up");

            var health = Session.Instance.Hero.GetResource(StatName.Health);
            health.RestoreCurrent(health.TotalValue * 0.25f);

            InventoryService.Instance.SetContext(InventoryContext.Healer);
            yield return Until(() => Flag(SidePanelOf(4), "IsExtended"), "the Healer's panel opens");
            Assert.That(health.CurrentValue, Is.EqualTo(health.TotalValue), "the Healer refills the hero");

            InventoryService.Instance.SetContext(InventoryContext.Stash);
            Assert.That(InventoryService.Instance.ActiveContext, Is.EqualTo(InventoryContext.Stash), "the Stash opens");
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "and the Run is still held");
        }

        // A press as the pointer makes it: the Town Exit is the ToFieldsButton, the Field Exit the ToTownButton.
        private static void Press(string name) =>
            GameObject.Find(name).GetComponent<AbstractButton>()
                .OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });

        private static Vector2 CentreOf(RectTransform anchor) => anchor.TransformPoint(anchor.rect.center);

        private static RectTransform FieldExit() =>
            TravelPoints.Of((RectTransform)GameObject.Find("ToTownButton").transform);

        // Held, the figure in the portal and the Town map up: where a player stands before walking out.
        private IEnumerator HeldInTown()
        {
            yield return RecalledFromTheFight();
            yield return OpenThePortal();
            yield return Until(() => Flag(Named("HeroFigure").Single(), "HasEnteredPortal"), "premise: the figure went in");
            yield return Until(() => Flag(TownFace(), "IsExtended"), "premise: the Town map is up");
        }

        [UnityTest]
        public IEnumerator WhileHeld_TheTownExit_BringsUpTheFieldMapWithTheFigureAtTheFieldExit_AndTheRunStaysHeldWithItsPortal()
        {
            yield return HeldInTown();

            Press("ToFieldsButton");
            yield return Until(() => Flag(FieldFace(), "IsExtended"), "the Field map comes up");

            var figure = Named("HeroFigure").Single();
            Assert.That(Flag(figure, "HasEnteredPortal"), Is.False, "he has stepped out of the portal");
            Assert.That(figure.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.01f), "and is drawn");
            Assert.That(Vector2.Distance(figure.transform.position, CentreOf(FieldExit())), Is.LessThan(1f), "at the Field Exit");
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held), "the Run is still held");
            Assert.That(Session.Instance.Hero.TownPortal, Is.EqualTo(_location), "and the portal still open");
            Assert.That(PortalImage("FieldPortalMarker").enabled, Is.True, "with its marker on the Field map");
            Assert.That(Flag(TownFace(), "IsCollapsed"), Is.True, "the Town map has given way");
        }

        [UnityTest]
        public IEnumerator WhileHeld_TheFieldExit_BringsTheTownMapBack_AndEndsNothing()
        {
            yield return HeldInTown();
            Press("ToFieldsButton");
            yield return Until(() => Flag(FieldFace(), "IsExtended"), "premise: the Field map is up");

            var run = Simulation.Run;
            var settled = 0;
            void Count(RunResult _) => settled++;
            Simulation.RunSettled += Count;

            Press("ToTownButton");
            yield return Until(() => Flag(TownFace(), "IsExtended"), "the Town map comes back");
            Simulation.RunSettled -= Count;

            Assert.That(settled, Is.Zero, "no Return ends the Run");
            Assert.That(Simulation.Run, Is.SameAs(run));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.Held), "the Run is still held");
            Assert.That(run.LastResult, Is.Null, "nothing ended");
            Assert.That(Session.Instance.Hero.TownPortal, Is.EqualTo(_location), "and the portal still open");
        }

        [UnityTest]
        public IEnumerator WhileHeld_WalkingOutAndBackAndOutAgain_LeavesTheFigureAtTheFieldExit()
        {
            yield return HeldInTown();
            Press("ToFieldsButton");
            yield return Until(() => Flag(FieldFace(), "IsExtended"), "premise: the Field map is up");
            Press("ToTownButton");
            yield return Until(() => Flag(TownFace(), "IsExtended"), "premise: the Town map is back");

            Press("ToFieldsButton");
            yield return Until(() => Flag(FieldFace(), "IsExtended"), "the Field map comes up again");

            var figure = Named("HeroFigure").Single();
            Assert.That(figure.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.01f), "he is drawn");
            Assert.That(Vector2.Distance(figure.transform.position, CentreOf(FieldExit())), Is.LessThan(1f), "at the Field Exit");
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Held));
        }

        [UnityTest]
        public IEnumerator WhileThePortalOpens_TheFieldExit_IsRefusedQuietly_AndTheTownFaceStaysDown()
        {
            yield return RecalledFromTheFight();
            Assert.That(Simulation.Run.IsRecalling, Is.True, "premise: the portal is opening");

            Assert.That(() => Press("ToTownButton"), Throws.Nothing);
            yield return null;

            Assert.That(Simulation.Run.IsRecalling, Is.True, "the opening runs on");
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Live), "no Return");
            Assert.That(Simulation.Run.LastResult, Is.Null);
            Assert.That(Flag(FieldFace(), "IsExtended"), Is.True, "the Field map stays up");
            Assert.That(Flag(TownFace(), "IsCollapsed"), Is.True, "and the Town face does not fade in half-way");
        }

        [UnityTest]
        public IEnumerator ALiveRun_TheFieldExit_StillReturns()
        {
            yield return FightingInTheField();

            Press("ToTownButton");
            yield return null;
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.Live), "the Run waits while he walks to the Exit");

            Time.timeScale = 20f;
            for (var frame = 0; Simulation.Run.Phase == RunPhase.Live && frame < 3000; frame++)
                yield return null;

            Time.timeScale = 1f;
            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.None), "the Run ends once he stands at the Exit");
            Assert.That(Simulation.Run.LastResult?.Outcome, Is.EqualTo(RunOutcome.Returned));
        }

        [UnityTest]
        public IEnumerator ADeathDuringTheOpening_PlaysTheOrdinaryDeathBeat_AndBringsUpNoTownMap()
        {
            yield return RecalledFromTheFight();

            Session.Instance.Hero.GetResource(StatName.Health).DepleteCurrent();
            for (var tick = 0; Simulation.Run.Phase == RunPhase.Live && tick < 100; tick++)
                Simulation.Tick(0.016f);
            yield return null;

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.None), "premise: the hero died in the opening");
            Assert.That(Simulation.DeathBeatPlaying, Is.True, "the ordinary death beat");
            Assert.That(PortalImage("FieldPortalMarker").enabled, Is.False, "the opening is cancelled");

            for (var frame = 0; frame < 30 && Simulation.DeathBeatPlaying; frame++)
            {
                Assert.That(Flag(TownFace(), "IsCollapsed"), Is.True, "no Town map comes up early");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheDebugToolsRecallButton_RecallsALiveRun()
        {
            yield return FightingInTheField();
            GiveAScroll();
            var type = System.Type.GetType("ToolSmiths.InventorySystem.GUI.Components.DebugTools, Assembly-CSharp");
            var host = new GameObject("DebugToolsTestHost");
            var tools = host.AddComponent(type);

            _ = type.GetMethod("RecallPlayer").Invoke(tools, null);
            Object.Destroy(host);

            Assert.That(Simulation.Run.IsRecalling, Is.True, "the Run is recalling");
        }

        [UnityTest]
        public IEnumerator TheTownPortal_ShowsExactlyWhileTheHeroHasOne_EvenInTownWithNoRun()
        {
            var marker = PortalImage("TownPortalMarker");
            yield return null;
            Assert.That(marker.enabled, Is.False, "no portal open");

            Session.Instance.Hero.TownPortal = _location;
            yield return null;
            Assert.That(marker.enabled, Is.True, "an open portal shows with no Run at all");

            Session.Instance.Hero.TownPortal = null;
            yield return null;
            Assert.That(marker.enabled, Is.False, "closed again");
        }
    }
}
