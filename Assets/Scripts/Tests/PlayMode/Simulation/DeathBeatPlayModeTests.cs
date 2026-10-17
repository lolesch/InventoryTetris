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
using UnityEngine.UI;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// The Death beat on the real <c>Example</c> scene (issue #329, ADR-0021): the hero's figure lies where he fell,
    /// the enemy figures stand frozen, and only when the slow-down ends does the figure become the Corpse marker and
    /// the Death notice open. The views live in the predefined assembly, so they are read by type name.
    /// </summary>
    public sealed class DeathBeatPlayModeTests
    {
        private const int SettleTicks = 100;
        private const float BeatTimeoutSeconds = 10f;

        private LocationConfig _location;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _location = GameBoot.Load().Locations.First();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            if (SimulationService.Instance.Run.Phase == RunPhase.InField)
                _ = SimulationService.Instance.Return();

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);
        }

        private static Component[] Named(string typeName) =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(c => c.GetType().Name == typeName).Cast<Component>().ToArray();

        private static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(owner);

        // The Field map's hero figure: the Figure on the object that carries the HeroFigure component.
        private static Component HeroFigure() =>
            Named("HeroFigure").Single().GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "Figure");

        private static Component[] EnemyFigures() =>
            Named("Figure").Where(c => c != HeroFigure() && c.gameObject.activeInHierarchy && (bool)Property(c, "IsBound")).ToArray();

        private static bool IsLyingDown() => (bool)Property(HeroFigure(), "IsLyingDown");

        private static bool NoticeIsOpen() => Member<GameObject>(Named("DeathNoticePopup").Single(), "screen").activeSelf;

        private static Image[] ShownMarkers() =>
            Named("CorpseMarker").Select(m => m.GetComponent<Image>()).Where(i => i.enabled).ToArray();

        private static ISimulationService Simulation => SimulationService.Instance;

        // Brings the Field map up as the To Fields button does, and waits for its fade, which the figure follows.
        private static IEnumerator ShowTheFieldMap()
        {
            var face = Named("FieldFacePanel").Single();
            _ = face.GetType().GetMethod("ToggleState").Invoke(face, new object[] { true });
            for (var frame = 0; !(bool)Property(face, "IsExtended") && frame < 600; frame++)
                yield return null;

            var group = HeroFigure().GetComponent<CanvasGroup>();
            var until = Time.realtimeSinceStartup + BeatTimeoutSeconds;
            while (group.alpha < 1f && Time.realtimeSinceStartup < until)
                yield return null;

            Assert.That(group.alpha, Is.EqualTo(1f), "premise: the Field map is up");
        }

        // Sent to the first Location, fought until enemies stand, then brought down and ticked until the Death settles.
        private IEnumerator DieInTheFight()
        {
            yield return ShowTheFieldMap();

            // Picked on the map as a player does, which Sends the Run: the arena draws around the selected Location's icon.
            var toggle = Named("LocationToggle").First(c => Equals(Property(c, "Location"), _location));
            _ = toggle.GetType().GetMethod("SetToggle").Invoke(toggle, new object[] { true });

            // The figure walks in first; run the walk at speed.
            Time.timeScale = 20f;
            yield return null;
            var walking = Named("HeroFigure").Single();
            for (var frame = 0; (bool)Property(walking, "IsWalking") && frame < 3000; frame++)
                yield return null;
            Time.timeScale = 1f;
            Assert.That((bool)Property(walking, "IsWalking"), Is.False, "premise: the figure walked in");

            for (var tick = 0; tick < 12; tick++)
                Simulation.Tick(0.5f);
            yield return null;
            Assert.That(EnemyFigures(), Is.Not.Empty, "premise: enemies stand in the fight");

            Session.Instance.Hero.GetResource(StatName.Health).DepleteCurrent();
            for (var tick = 0; Simulation.Run.Phase == RunPhase.InField && tick < SettleTicks; tick++)
                Simulation.Tick(0.016f);

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown), "premise: the hero died");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFigureFalls_ThenBecomesTheMarkerWhereHeFell_ThenTheNoticeOpens_NotBefore()
        {
            yield return DieInTheFight();

            // The beat: the figure lies, the fight stands frozen, the marker and the notice wait.
            Assert.That(Simulation.DeathBeatPlaying, Is.True);
            Assert.That(IsLyingDown(), Is.True, "the figure turns to lying down");
            Assert.That(HeroFigure().GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0f), "and is still seen");
            Assert.That(ShownMarkers(), Is.Empty, "no marker yet");
            Assert.That(NoticeIsOpen(), Is.False, "no notice yet");

            var fell = (Vector2)HeroFigure().transform.position;
            var until = Time.realtimeSinceStartup + BeatTimeoutSeconds;
            var frames = 0;
            while (Simulation.DeathBeatPlaying && Time.realtimeSinceStartup < until)
            {
                Assert.That(NoticeIsOpen(), Is.False, "the notice does not open before the beat ends");
                Assert.That(ShownMarkers(), Is.Empty, "nor does the marker appear");
                Assert.That(IsLyingDown(), Is.True);
                frames++;
                yield return null;
            }

            Assert.That(Simulation.DeathBeatPlaying, Is.False, "premise: the beat ended");
            Assert.That(frames, Is.GreaterThan(0), "premise: the beat took some time");
            Assert.That(NoticeIsOpen(), Is.True, "the notice opens as the beat ends");

            var markers = ShownMarkers();
            Assert.That(markers.Length, Is.EqualTo(1), "the figure became the Corpse marker");
            // The marker offsets in its toggle's space, which the selected Location scales up a little: a few units, not a different place.
            Assert.That(Vector2.Distance(markers[0].transform.position, fell), Is.LessThan(8f), "where he fell");
            yield return null;
            Assert.That(HeroFigure().GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f), "the figure is gone");
            Assert.That(EnemyFigures(), Is.Empty, "and so is the fight");
        }

        [UnityTest]
        public IEnumerator ALocationPickedDuringTheBeat_SendsNobody_AndLeavesTheCorpseAlone()
        {
            yield return DieInTheFight();
            Assert.That(Simulation.DeathBeatPlaying, Is.True, "premise: inside the beat");
            var corpse = Session.Instance.Hero.Corpse;
            var fell = corpse.FallPosition;
            var toggle = Named("LocationToggle").First(c => !Equals(Property(c, "Location"), _location));

            _ = toggle.GetType().GetMethod("SetToggle").Invoke(toggle, new object[] { true });
            yield return null;

            Assert.That(Simulation.Run.Phase, Is.EqualTo(RunPhase.InTown), "a dead hero is not Sent");
            Assert.That(Session.Instance.Hero.SelectedLocation, Is.EqualTo(_location), "the Location the Death was judged against");
            Assert.That(corpse.FallPosition, Is.EqualTo(fell), "the first Corpse stands");
        }

        [UnityTest]
        public IEnumerator DuringTheBeat_TheEnemyFiguresStandFrozen()
        {
            yield return DieInTheFight();
            var enemies = EnemyFigures();
            var enemyAt = enemies.Select(e => e.transform.position).ToArray();

            for (var frame = 0; frame < 5 && Simulation.DeathBeatPlaying; frame++)
                yield return null;

            Assert.That(Simulation.DeathBeatPlaying, Is.True, "premise: still in the beat");
            Assert.That(EnemyFigures(), Is.EquivalentTo(enemies), "the same figures stand");
            Assert.That(enemies.Select(e => e.transform.position).ToArray(), Is.EqualTo(enemyAt), "where they stood");
        }

        [UnityTest]
        public IEnumerator ARespawn_StandsTheHeroFigureUpAtTheFieldExit()
        {
            yield return DieInTheFight();
            Simulation.Tick(2f);
            yield return null;

            Simulation.Respawn();
            yield return null;

            Assert.That(IsLyingDown(), Is.False, "the figure stands again");
            var exit = (RectTransform)Named("ToTownButton").First().transform;
            Assert.That(Vector2.Distance(HeroFigure().transform.position, exit.TransformPoint(exit.rect.center)), Is.LessThan(1f),
                "at the Field Exit, as for any Run");
        }
    }
}
