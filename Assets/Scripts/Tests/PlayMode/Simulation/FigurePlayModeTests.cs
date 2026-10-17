using System.Collections;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
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

        [UnityTest]
        public IEnumerator TheHeroFigure_IsShownWhileARunLasts_AndHiddenAndFacingRightWhenItEnds()
        {
            var figure = HeroFigure();
            Assert.That(figure.GetType().Name, Is.EqualTo("Figure"), "one component serves hero and enemy");
            Assert.That(figure.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "no Run, no hero on the arena");

            Select(_locations[0]);
            yield return null;
            Assert.That(figure.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f), "bound when the Encounter binds");
            Assert.That((bool)Property(figure, "IsPlaced"), Is.True);

            _ = SimulationService.Instance.Return();
            yield return null;
            Assert.That(figure.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "hidden when it is released");
            Assert.That((int)Property(figure, "FacingSign"), Is.EqualTo(1), "and the next Run starts facing right");
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
