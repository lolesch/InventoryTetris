using NUnit.Framework;
using Submodules.Utility.Services;
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The Death notice on the real <c>Example</c> scene (issue #327, ADR-0021): a Death opens it with the Run's
    /// result, its one button Respawn is selected, and the button, Enter and Esc each revive the hero at the
    /// Waypoint. Keys and clicks arrive through the Input System's test fixture, as a player's do. The popup and
    /// the figure live in the predefined assembly, so they are read by type name. Run with the PlayMode filter.
    /// </summary>
    public sealed class DeathNoticePlayModeTests : InputTestFixture
    {
        private const int DeathTimeoutTicks = 100;

        private Keyboard keyboard;
        private RunResult result;

        private static ISimulationService Simulation => ServiceLocator.Get<ISimulationService>();

        private static Hero Hero => Session.Instance.Hero;

        // The hero dies in the first Location, as a Run does: sent there, brought down, ticked until the Death settles.
        private IEnumerator DieInTheField()
        {
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());
            keyboard = InputSystem.AddDevice<Keyboard>();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;

            Simulation.RunSettled += died => result = died;
            Simulation.Send(GameBoot.Load().Locations.First(location => location.Id == "location.thornwood"));
            Hero.GetResource(StatName.Health).DepleteCurrent();

            for (var tick = 0; Simulation.Run.Phase == RunPhase.Live && tick < DeathTimeoutTicks; tick++)
                Simulation.Tick(0.5f);

            // The notice waits for the Death beat; its own timing is DeathBeatPlayModeTests'.
            Simulation.Tick(2f);
            yield return null;
            Assert.That(result.Outcome, Is.EqualTo(RunOutcome.Died), "premise: the hero died");
        }

        private static MonoBehaviour Notice() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Single(c => c.GetType().Name == "DeathNoticePopup");

        private static MonoBehaviour Figure() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Single(c => c.GetType().Name == "TownHeroFigure");

        private static T Member<T>(Component owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner);

        private static bool IsOpen(MonoBehaviour notice) => Member<GameObject>(notice, "screen").activeSelf;

        private static float Health() => Hero.GetResource(StatName.Health).CurrentValue;

        // Walks the Town figure off the Waypoint, so the Respawn has somewhere to bring it back from.
        private static IEnumerator WalkTheFigureAway()
        {
            var figure = Figure();
            var stash = (RectTransform)GameObject.Find("StashToggle").transform;
            var waypoint = TravelPoints.Waypoint(figure);

            Hero.GetStat(StatName.MovementSpeed).AddModifier(new StatModifier(new Vector2Int(0, 100000), 325f));
            _ = figure.GetType().GetMethod("TravelTo", new[] { typeof(RectTransform) }).Invoke(figure, new object[] { stash });
            yield return new WaitForSeconds(0.4f);

            Assert.That(Vector3.Distance(figure.transform.position, waypoint.TransformPoint(waypoint.rect.center)), Is.GreaterThan(5f),
                "premise: the figure left the Waypoint");
        }

        private void AssertRevivedAtTheWaypoint(MonoBehaviour notice)
        {
            Assert.That(IsOpen(notice), Is.False, "the notice closes");
            Assert.That(Hero.GetResource(StatName.Health).IsFull, Is.True, "full Health");
            Assert.That(Hero.IsDead, Is.False);

            var figure = Figure();
            var waypoint = TravelPoints.Waypoint(figure);
            Assert.That(Vector3.Distance(figure.transform.position, waypoint.TransformPoint(waypoint.rect.center)), Is.LessThan(1f),
                "at the Waypoint");
            Assert.That((bool)figure.GetType().GetProperty("IsWalking").GetValue(figure), Is.False, "and standing");
        }

        [TearDown]
        public void LeaveTheHeroAlive()
        {
            if (ServiceLocator.IsArmed && Hero.IsDead)
                Simulation.Respawn();
        }

        [UnityTest]
        public IEnumerator ADeath_OpensTheNotice_WithTheLocation_TheXpLost_AndTheFee()
        {
            yield return DieInTheField();
            var notice = Notice();
            var location = Hero.SelectedLocation;

            Assert.That(IsOpen(notice), Is.True);
            var text = Member<TextMeshProUGUI>(notice, "message").text;
            Assert.That(text, Is.EqualTo(DeathNotice.TextFor(location.DisplayName, result.XpLost, result.CurrencyFee)));
            Assert.That(text, Does.Contain(location.DisplayName));
            Assert.That(Member<Component>(notice, "respawn").gameObject, Is.SameAs(EventSystem.current.currentSelectedGameObject),
                "Respawn is selected, so Enter presses it");
            Assert.That(Health(), Is.EqualTo(0f), "the globe stays empty behind the notice");
        }

        [UnityTest]
        public IEnumerator TheRespawnButton_RevivesTheHeroAtTheWaypoint()
        {
            yield return DieInTheField();
            var notice = Notice();
            yield return WalkTheFigureAway();

            var click = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            _ = ExecuteEvents.Execute(Member<Component>(notice, "respawn").gameObject, click, ExecuteEvents.pointerClickHandler);

            AssertRevivedAtTheWaypoint(notice);
        }

        [UnityTest]
        public IEnumerator Enter_Respawns()
        {
            yield return DieInTheField();
            var notice = Notice();
            yield return WalkTheFigureAway();

            currentTime += 1.0;
            PressAndRelease(keyboard.enterKey);
            yield return null;

            AssertRevivedAtTheWaypoint(notice);
        }

        [UnityTest]
        public IEnumerator Esc_Respawns_AndDoesNotOpenThePauseMenu()
        {
            yield return DieInTheField();
            var notice = Notice();
            yield return WalkTheFigureAway();

            currentTime += 1.0;
            PressAndRelease(keyboard.escapeKey);
            yield return null;

            AssertRevivedAtTheWaypoint(notice);
            Assert.That(ServiceLocator.Get<IPauseMenu>().IsOpen, Is.False, "the press was the notice's");
        }

        [UnityTest]
        public IEnumerator NothingButRespawn_LeavesTheNotice()
        {
            yield return DieInTheField();
            var notice = Notice();

            for (var tick = 0; tick < 10; tick++)
                Simulation.Tick(1f);
            yield return null;

            Assert.That(IsOpen(notice), Is.True, "time passing does not close it");
            Assert.That(Health(), Is.EqualTo(0f), "and does not revive");
        }
    }
}
