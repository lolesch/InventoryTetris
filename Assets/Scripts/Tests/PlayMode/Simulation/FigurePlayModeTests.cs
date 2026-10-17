using System.Collections;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
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
    /// The one figure that stands for the hero and for each enemy (epic #262, change C) on the real
    /// <c>Example</c> scene. The figure, the arena and its feedbacks live in the predefined assembly, out of a
    /// test assembly's reach, so they are read by type name. Run with the PlayMode filter.
    /// </summary>
    public sealed class FigurePlayModeTests
    {
        private LocationConfig[] _locations;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _locations = GameBoot.Load().Locations.ToArray();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            SimulationService.Instance.SetPaused(false);

            if (SimulationService.Instance.Run.Phase == RunPhase.InField)
                _ = SimulationService.Instance.Return();

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static Component Arena() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Single(c => c.GetType().Name == "EnemyArena");

        private static object Field(object target, string name)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                    return field.GetValue(target);
            }

            throw new System.MissingFieldException(target.GetType().Name, name);
        }

        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        // The Field map's own hero figure (issue #316): a HeroFigure component on the Figure, not a slot of the arena.
        private static Component HeroFigure() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Single(c => c.GetType().Name == "HeroFigure")
                .GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "Figure");

        private static Component Child(Component figure, string typeName) =>
            figure.GetComponentsInChildren<MonoBehaviour>(true).Single(c => c.GetType().Name == typeName);

        private static CharacterResource Health() => Session.Instance.Hero.GetResource(StatName.Health);

        // Picks the Location on the map as a player does: the arena draws around the selected Location's icon.
        private void Select(LocationConfig location)
        {
            var toggle = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(c => c.GetType().Name == "LocationToggle"
                            && Equals(c.GetType().GetProperty("Location").GetValue(c), location));
            toggle.GetType().GetMethod("SetToggle").Invoke(toggle, new object[] { true });
        }

        // The Field map's hero figure component (issue #323): the one that Travels, beside the Figure it draws with.
        private static Component Travel() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Single(c => c.GetType().Name == "HeroFigure");

        private static bool IsWalking() => (bool)Property(Travel(), "IsWalking");

        // Where each control sends the figure: its Travel Point, else its own rect.
        private static RectTransform FieldExit() =>
            TravelPoints.Of(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(c => c.GetType().Name == "ToTownButton").GetComponent<RectTransform>());

        private static RectTransform LocationIcon(LocationConfig location) =>
            TravelPoints.Of(Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(c => c.GetType().Name == "LocationToggle" && Equals(c.GetType().GetProperty("Location").GetValue(c), location))
                .GetComponent<RectTransform>());

        // Where the figure is drawn, in world space: compared with an anchor's world centre, not a restatement of the figure's own maths.
        private static Vector2 FigureAt() => HeroFigure().transform.position;

        private static Vector2 CentreOf(RectTransform anchor) => anchor.TransformPoint(anchor.rect.center);

        // Brings the Field map up as the To Fields button does, and waits for its fade, which the figure follows.
        private static IEnumerator ShowTheFieldMap()
        {
            var face = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include).Single(c => c.GetType().Name == "FieldFacePanel");
            face.GetType().GetMethod("ToggleState").Invoke(face, new object[] { true });
            for (var frame = 0; !(bool)Property(face, "IsExtended") && frame < 600; frame++)
                yield return null;

            // IsExtended turns true as the fade starts; the scene's fade is slow (#326), so wait for it to land.
            var group = HeroFigure().GetComponent<CanvasGroup>();
            var until = Time.realtimeSinceStartup + 10f;
            while (group.alpha < 1f && Time.realtimeSinceStartup < until)
                yield return null;

            yield return null;
        }

        // A walk is seconds long: run it at speed so the test does not wait on it.
        private static IEnumerator WaitForArrival()
        {
            Time.timeScale = 20f;
            yield return null; // the figure starts its walk on the frame after the Send
            for (var frame = 0; IsWalking() && frame < 3000; frame++)
                yield return null;

            Time.timeScale = 1f;
            Assert.That(IsWalking(), Is.False, "the walk ended");
        }

        [UnityTest]
        public IEnumerator TheHeroFigure_StandsAtTheFieldExitWhenTheMapComesUp_AndWalksBackToItAfterAReturn()
        {
            var figure = HeroFigure();
            Assert.That(figure.GetType().Name, Is.EqualTo("Figure"), "one component serves hero and enemy");

            yield return ShowTheFieldMap();
            Assert.That(figure.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "it fades with the map, which is up");
            Assert.That(Vector2.Distance(FigureAt(), CentreOf(FieldExit())), Is.LessThan(0.01f), "no Run: at the Field Exit");

            Select(_locations[0]);
            yield return WaitForArrival();
            Assert.That(Vector2.Distance(FigureAt(), CentreOf(FieldExit())), Is.GreaterThan(1f), "the Send took it away");

            _ = SimulationService.Instance.Return();
            yield return null;
            Assert.That(IsWalking(), Is.True, "a Return walks it out, it does not snap");

            yield return WaitForArrival();
            Assert.That(Vector2.Distance(FigureAt(), CentreOf(FieldExit())), Is.LessThan(0.01f), "and it ends at the Field Exit");
        }

        private static CanvasGroup Face(string name) => GameObject.Find(name).GetComponent<CanvasGroup>();

        // A press on a button as the pointer makes it: the Field Exit is the ToTownButton, the Town Exit the ToFieldsButton.
        private static void Press(string name) =>
            GameObject.Find(name).GetComponent<AbstractButton>()
                .OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });

        private static IEnumerator UntilAlpha(CanvasGroup group, float alpha)
        {
            var until = Time.realtimeSinceStartup + 10f;
            while (!Mathf.Approximately(group.alpha, alpha) && Time.realtimeSinceStartup < until)
                yield return null;

            Assert.That(group.alpha, Is.EqualTo(alpha).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator AClickOnTheFieldExit_DuringARun_EndsTheRunWithEverythingKept_AndWalksTheFigureOutFromTheFight()
        {
            Press("ToFieldsButton");
            yield return UntilAlpha(Face("InFields"), 1f);
            Select(_locations[0]);
            yield return WaitForArrival();
            var encounter = SimulationService.Instance.Run.Encounter;
            for (var tick = 0; encounter.Enemies.Count == 0 && tick < 100; tick++)
                SimulationService.Instance.Tick(0.5f);
            yield return null;
            var stood = FigureAt();
            var exit = CentreOf(FieldExit());
            Assert.That(Vector2.Distance(stood, exit), Is.GreaterThan(1f), "premise: the fight is away from the Exit");

            Press("ToTownButton");
            yield return null;

            var run = SimulationService.Instance.Run;
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown), "the Run ends at the click");
            Assert.That(run.LastResult?.Outcome, Is.EqualTo(RunOutcome.Returned));
            Assert.That(Session.Instance.Hero.Corpse.Exists, Is.False, "a Return keeps everything");
            Assert.That(IsWalking(), Is.True, "he walks out, not the Relocate walk to a Location");
            Assert.That(Vector2.Distance(FigureAt(), stood), Is.LessThan(Vector2.Distance(stood, exit) / 2f), "from where he stood in the fight");
            Assert.That(Face("InFields").alpha, Is.GreaterThan(0f), "the Field map is still fading out while he walks");

            yield return WaitForArrival();
            Assert.That(Vector2.Distance(FigureAt(), CentreOf(FieldExit())), Is.LessThan(0.01f), "he reached the Exit");
            yield return UntilAlpha(Face("InTown"), 1f);
        }

        [UnityTest]
        public IEnumerator AClickOnTheFieldExit_WithNoRun_GoesBackToTown_AndEndsNothing()
        {
            Press("ToFieldsButton");
            yield return UntilAlpha(Face("InFields"), 1f);
            var run = SimulationService.Instance.Run;
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown), "premise: no Run");

            Press("ToTownButton");
            yield return UntilAlpha(Face("InTown"), 1f);

            Assert.That(SimulationService.Instance.Run, Is.SameAs(run));
            Assert.That(run.Phase, Is.EqualTo(RunPhase.InTown));
            Assert.That(run.LastResult, Is.Null, "nothing ended");
            Assert.That(IsWalking(), Is.False);
        }

        [UnityTest]
        public IEnumerator TheHeroFigure_FadesWithTheFieldMap()
        {
            yield return null;
            Assert.That(HeroFigure().GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "the map is not up");

            yield return ShowTheFieldMap();
            Assert.That(HeroFigure().GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ASend_WalksTheFigureFromTheFieldExitToTheLocation_ThenItFollowsTheFight()
        {
            yield return ShowTheFieldMap();

            Select(_locations[0]);
            yield return null;
            Assert.That(IsWalking(), Is.True, "the Send starts a walk");
            Assert.That(Vector2.Distance(FigureAt(), CentreOf(FieldExit())), Is.LessThan(Vector2.Distance(CentreOf(FieldExit()), CentreOf(LocationIcon(_locations[0])))),
                "not there yet");
            Assert.That(SimulationService.Instance.Run.Phase, Is.EqualTo(RunPhase.InField), "the Run starts at the click, not on arrival");

            yield return WaitForArrival();
            Assert.That(Vector2.Distance(FigureAt(), CentreOf(LocationIcon(_locations[0]))), Is.LessThan(0.01f), "arrived at the Location");

            var encounter = SimulationService.Instance.Run.Encounter;
            yield return null;
            var arguments = new object[] { encounter.HeroPosition, null };
            _ = (bool)Arena().GetType().GetMethod("TryGetNumberOrigin").Invoke(Arena(), arguments);
            Assert.That(Vector2.Distance(((RectTransform)HeroFigure().transform).anchoredPosition, (Vector2)arguments[1]), Is.LessThan(0.01f),
                "then it draws the simulation's hero");
        }

        [UnityTest]
        public IEnumerator PausingDuringTheWalkIn_LeavesTheFigureWalking()
        {
            yield return ShowTheFieldMap();
            Select(_locations[0]);
            yield return null;

            SimulationService.Instance.SetPaused(true);
            Assert.That(SimulationService.Instance.IsPaused, Is.True);
            var before = FigureAt();
            yield return new WaitForSeconds(0.3f);

            Assert.That(IsWalking(), Is.True, "still walking");
            Assert.That(Vector2.Distance(FigureAt(), before), Is.GreaterThan(1f), "and moving");
        }

        [UnityTest]
        public IEnumerator ARefusedSend_LeavesTheFigureWhereItStands()
        {
            yield return ShowTheFieldMap();
            Select(_locations[0]);
            yield return WaitForArrival();
            yield return null;
            var before = FigureAt();

            Assert.Throws<System.InvalidOperationException>(() => SimulationService.Instance.Send(_locations[1]), "a Run is already in the Field");
            yield return null;

            Assert.That(IsWalking(), Is.False, "a refused Send starts no walk");
            Assert.That(Vector2.Distance(FigureAt(), before), Is.LessThan(5f), "the figure stays in the fight");
        }

        // The pooled figures that are up now: the live enemies' and the fading ones, not the hero's.
        private static Component[] EnemyFigures() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                .Where(c => c.GetType().Name == "Figure" && c != HeroFigure()).ToArray();

        private IEnumerator FightAtTheFirstLocation(LocationConfig location)
        {
            yield return ShowTheFieldMap();
            Select(location);
            yield return WaitForArrival();

            var encounter = SimulationService.Instance.Run.Encounter;
            for (var tick = 0; encounter.Enemies.Count == 0 && tick < 100; tick++)
                SimulationService.Instance.Tick(0.5f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ARelocate_WalksTheFigureFromWhereItStoodInTheFight_ToTheNewLocation_ThenItFollowsTheNewFight()
        {
            yield return FightAtTheFirstLocation(_locations[0]);
            var stood = FigureAt();
            var oldEncounter = SimulationService.Instance.Run.Encounter;

            Select(_locations[1]);
            yield return null;

            Assert.That(SimulationService.Instance.Run.Encounter, Is.Not.SameAs(oldEncounter), "the Relocate takes effect at the click");
            Assert.That(IsWalking(), Is.True, "the Relocate starts a walk");
            var distance = Vector2.Distance(stood, CentreOf(LocationIcon(_locations[1])));
            Assert.That(Vector2.Distance(FigureAt(), stood), Is.LessThan(distance / 2f), "it leaves from where it stood, not at the new Location");

            yield return WaitForArrival();
            Assert.That(Vector2.Distance(FigureAt(), CentreOf(LocationIcon(_locations[1]))), Is.LessThan(0.01f), "arrived at the new Location");

            var encounter = SimulationService.Instance.Run.Encounter;
            yield return null;
            var arguments = new object[] { encounter.HeroPosition, null };
            _ = (bool)Arena().GetType().GetMethod("TryGetNumberOrigin").Invoke(Arena(), arguments);
            Assert.That(Vector2.Distance(((RectTransform)HeroFigure().transform).anchoredPosition, (Vector2)arguments[1]), Is.LessThan(0.01f),
                "then it draws the new fight");
        }

        [UnityTest]
        public IEnumerator ARelocate_FadesTheLeftEncountersEnemiesOut_InsteadOfRemovingThemAtOnce()
        {
            yield return FightAtTheFirstLocation(_locations[0]);
            var left = EnemyFigures();
            Assert.That(left, Is.Not.Empty, "enemies were up");

            Select(_locations[1]);
            yield return null;

            Assert.That(left.All(figure => figure != null && figure.gameObject.activeInHierarchy), Is.True, "they do not vanish");
            Assert.That(left.Any(figure => figure.GetComponent<CanvasGroup>().alpha < 1f), Is.True, "they fade");
            Assert.That(left.All(figure => !(bool)Property(figure, "IsBound")), Is.True, "and no longer follow the fight left behind");

            Time.timeScale = 20f;
            for (var frame = 0; left.Any(figure => figure.gameObject.activeInHierarchy && !(bool)Property(figure, "IsBound")) && frame < 600; frame++)
                yield return null;
            Time.timeScale = 1f;

            Assert.That(left.Any(figure => figure.gameObject.activeInHierarchy && !(bool)Property(figure, "IsBound")), Is.False, "then they are gone");
        }

        [UnityTest]
        public IEnumerator ARefusedRelocate_LeavesTheFigureInTheFight()
        {
            yield return FightAtTheFirstLocation(_locations[0]);
            var before = FigureAt();

            Assert.Throws<System.ArgumentNullException>(() => SimulationService.Instance.Relocate(null), "no Location to go to");
            yield return null;

            Assert.That(IsWalking(), Is.False, "a refused Relocate starts no walk");
            Assert.That(Vector2.Distance(FigureAt(), before), Is.LessThan(5f), "the figure stays in the fight");
        }

        [Test]
        public void TheArenaView_BindsNoHeroFigure()
        {
            var fields = Arena().GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.That(fields.Where(f => f.FieldType.Name == "Figure" && f.Name != "prefab" && f.IsDefined(typeof(SerializeField), true)), Is.Empty,
                "the pooled prefab is the arena's only Figure slot; the hero's is the Field map's");
        }

        [UnityTest]
        public IEnumerator TheHeroFigure_StandsWhereTheSimulationProjectsTheHero()
        {
            Select(_locations[0]);
            var encounter = SimulationService.Instance.Run.Encounter;
            for (var tick = 0; encounter.Enemies.Count == 0 && tick < 100; tick++)
                SimulationService.Instance.Tick(0.5f);
            yield return WaitForArrival();
            yield return null;

            var arguments = new object[] { encounter.HeroPosition, null };
            var projected = (bool)Arena().GetType().GetMethod("TryGetNumberOrigin").Invoke(Arena(), arguments);
            var rect = (RectTransform)HeroFigure().transform;

            Assert.That(projected, Is.True);
            Assert.That(Vector2.Distance(rect.anchoredPosition, (Vector2)arguments[1]), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator TheHeroFigure_ShowsTheHeroHealth_AndFlashesAndShakesOnAHit_ButNotOnRegeneration()
        {
            Select(_locations[0]);
            yield return null;
            var figure = HeroFigure();
            var flash = Child(figure, "HitFlash");
            var shake = Child(figure, "HitShake");
            var bar = Child(figure, "HealthBarDisplay");
            var display = Field(bar, "healthDisplay");

            Assert.That(Field(display, "_resource"), Is.SameAs(Health()), "the bar follows the hero's health");
            Assert.That((bool)Property(flash, "IsBound"), Is.True);
            Assert.That((bool)Property(shake, "IsBound"), Is.True);

            _ = Health().RemoveFromCurrent(1f);
            Assert.That((float)Property(flash, "Intensity"), Is.GreaterThan(0f), "a hit flashes the hero");
            Assert.That((bool)Property(shake, "IsShaking"), Is.True, "and shakes it");

            for (var frame = 0; (float)Property(flash, "Intensity") > 0f && frame < 600; frame++)
                yield return null;
            Assert.That((float)Property(flash, "Intensity"), Is.EqualTo(0f), "the flash has faded");

            Health().RefillCurrent();
            Assert.That((float)Property(flash, "Intensity"), Is.EqualTo(0f), "regeneration does not flash");
        }

        [UnityTest]
        public IEnumerator TheHeroFigure_ShowsTheHeroShield_AndAnEnemyShowsNone()
        {
            Select(_locations[0]);
            var encounter = SimulationService.Instance.Run.Encounter;
            for (var tick = 0; encounter.Enemies.Count == 0 && tick < 100; tick++)
                SimulationService.Instance.Tick(0.5f);
            yield return null;

            var heroBar = Child(HeroFigure(), "HealthBarDisplay");
            Assert.That(Field(Field(heroBar, "shieldDisplay"), "_resource"), Is.SameAs(Session.Instance.Hero.GetResource(StatName.Shield)),
                "the hero's bar follows the hero's shield");
            Assert.That(((GameObject)Field(heroBar, "shieldRoot")).activeSelf, Is.True);

            var enemyFigure = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                .First(c => c.GetType().Name == "Figure" && c != HeroFigure());
            var enemyBar = Child(enemyFigure, "HealthBarDisplay");
            Assert.That(Field(Field(enemyBar, "shieldDisplay"), "_resource"), Is.Null, "an enemy has no shield to follow");
            Assert.That(((GameObject)Field(enemyBar, "shieldRoot")).activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator EachEnemy_StandsOnAPooledFigure_BoundToItsOwnHealth()
        {
            Select(_locations[0]);
            var encounter = SimulationService.Instance.Run.Encounter;

            // The scene's own loop may be paused or slow; the service's Tick is what a frame would call.
            for (var tick = 0; encounter.Enemies.Count == 0 && tick < 100; tick++)
                SimulationService.Instance.Tick(0.5f);
            yield return null;

            var figures = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude)
                .Where(c => c.GetType().Name == "Figure" && c != HeroFigure()).ToArray();

            Assert.That(encounter.Enemies.Count, Is.GreaterThan(0));
            Assert.That(figures.Length, Is.EqualTo(encounter.Enemies.Count), "one figure per living enemy");
            foreach (var figure in figures)
                Assert.That((bool)Property(figure, "IsPlaced"), Is.True);
        }
    }
}
