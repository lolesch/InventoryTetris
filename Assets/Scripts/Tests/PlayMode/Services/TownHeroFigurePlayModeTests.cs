using System.Collections;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Services;
using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Services
{
    /// <summary>
    /// The Town map's Hero figure on the real <c>Example</c> scene (issue #322, ADR-0021): it stands at the Waypoint,
    /// and a click or hotkey that opens a Town Stop walks it there. Clicks and keys arrive through the Input System's
    /// test fixture, as a player's do. The figure lives in the predefined assembly, out of a test assembly's reach,
    /// so it is read by type name. Run with the PlayMode filter.
    /// </summary>
    public sealed class TownHeroFigurePlayModeTests : InputTestFixture
    {
        private Keyboard keyboard;
        private Mouse mouse;

        private static InventoryContext Context => ServiceLocator.Get<IInventoryService>().ActiveContext;

        private IEnumerator OpenExample()
        {
            ServiceLocator.Reset();
            GameBoot.Arm(GameBoot.Load());

            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();

            yield return SceneManager.LoadSceneAsync("Example");
            yield return null;
        }

        [TearDown]
        public void CloseWhatTheTestOpened()
        {
            if (ServiceLocator.IsArmed)
                ServiceLocator.Get<IInventoryService>().CloseContext();
        }

        private static MonoBehaviour Figure() =>
            Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Single(c => c.GetType().Name == "TownHeroFigure");

        private static bool IsWalking(MonoBehaviour figure) => (bool)figure.GetType().GetProperty("IsWalking").GetValue(figure);

        private static RectTransform Named(string name) => (RectTransform)GameObject.Find(name).transform;

        private static Vector3 Centre(RectTransform rect) => rect.TransformPoint(rect.rect.center);

        // A Stop's centre drifts a few units once pressed (its pressed state rescales it about an off-centre pivot),
        // so "at the Stop" is within this, and the Stops are further apart than that.
        private const float AtTheStop = 10f;

        // World units between the figure and where a control sends him: its Travel Point's centre, else its own.
        private static float Apart(MonoBehaviour figure, RectTransform control) =>
            Vector3.Distance(figure.transform.position, Centre(TravelPoints.Of(control)));

        // The Overlay canvas draws in screen space, so a rect's world centre is where the pointer goes.
        private IEnumerator Click(RectTransform target)
        {
            Set(mouse.position, (Vector2)Centre(target));
            yield return null;
            currentTime += 1.0;
            Press(mouse.leftButton);
            yield return null;
            Release(mouse.leftButton);
            yield return null;
        }

        private static IEnumerator UntilArrived(MonoBehaviour figure)
        {
            var deadline = Time.time + 20f;
            while (IsWalking(figure) && Time.time < deadline)
                yield return null;

            Assert.That(IsWalking(figure), Is.False, "the walk ended");
        }

        // Raises the hero's walking pace by a flat amount, so a test's walk takes a moment and not a quarter minute.
        private static void Hasten(float flat) =>
            Session.Instance.Hero.GetStat(StatName.MovementSpeed).AddModifier(new StatModifier(new Vector2Int(0, 100000), flat));

        [UnityTest]
        public IEnumerator EnteringTheGame_StandsTheFigureAtTheWaypoint()
        {
            yield return OpenExample();
            var figure = Figure();

            Assert.That(Apart(figure, TravelPoints.Waypoint(figure)), Is.LessThan(1f));
            Assert.That(IsWalking(figure), Is.False);
        }

        [UnityTest]
        public IEnumerator AClickOnAStop_OpensItAtOnce_AndTheFigureWalksThere()
        {
            yield return OpenExample();
            var figure = Figure();
            var stash = Named("StashToggle");
            var before = Apart(figure, stash);

            yield return Click(stash);
            yield return null;

            Assert.That(Context, Is.EqualTo(InventoryContext.Stash), "opens at once, before the figure has arrived");
            Assert.That(IsWalking(figure), Is.True);

            yield return new WaitForSeconds(0.3f);
            Assert.That(Apart(figure, stash), Is.LessThan(before), "walking towards it");

            Hasten(5000f);
            yield return UntilArrived(figure);
            Assert.That(Apart(figure, stash), Is.LessThan(AtTheStop));
        }

        [UnityTest]
        public IEnumerator HealerThenStashMidWalk_BothOpenAtOnce_AndTheFigureTurnsToTheStash()
        {
            yield return OpenExample();
            var figure = Figure();
            var healer = Named("HealerToggle");
            var stash = Named("StashToggle");

            yield return Click(healer);
            yield return null;
            Assert.That(Context, Is.EqualTo(InventoryContext.Healer));

            yield return null;
            Assert.That(IsWalking(figure), Is.True, "premise: still on the way to the Healer");

            yield return Click(stash);
            yield return null;
            Assert.That(Context, Is.EqualTo(InventoryContext.Stash), "the second Stop opens at once too");
            Assert.That(IsWalking(figure), Is.True);

            Hasten(5000f);
            yield return UntilArrived(figure);
            Assert.That(Apart(figure, stash), Is.LessThan(AtTheStop), "ended at the Stash");
            Assert.That(Apart(figure, healer), Is.GreaterThan(AtTheStop), "never stopped at the Healer");
        }

        [UnityTest]
        public IEnumerator ATownStopHotkey_WalksTheFigureLikeAClick()
        {
            yield return OpenExample();
            var figure = Figure();

            currentTime += 1.0;
            PressAndRelease(keyboard.hKey);
            yield return null;

            Assert.That(Context, Is.EqualTo(InventoryContext.Healer));
            Assert.That(IsWalking(figure), Is.True);

            Hasten(5000f);
            yield return UntilArrived(figure);
            Assert.That(Apart(figure, Named("HealerToggle")), Is.LessThan(AtTheStop));
        }

        [UnityTest]
        public IEnumerator AHigherMovementSpeed_WalksFurtherInTheSameTime()
        {
            yield return OpenExample();
            var figure = Figure();

            var stash = Named("StashToggle");
            var travelTo = figure.GetType().GetMethod("TravelTo");

            _ = travelTo.Invoke(figure, new object[] { stash });
            var start = figure.transform.position;
            yield return new WaitForSeconds(0.4f);
            var slow = Vector3.Distance(start, figure.transform.position);
            Assert.That(IsWalking(figure), Is.True, "premise: the slow walk is still on");

            // Back to the Waypoint, then a walk of the same length of time with a hero twice as fast.
            _ = figure.GetType().GetMethod("PlaceAtWaypoint").Invoke(figure, null);
            Hasten(325f);
            _ = travelTo.Invoke(figure, new object[] { stash });
            start = figure.transform.position;
            yield return new WaitForSeconds(0.4f);
            var fast = Vector3.Distance(start, figure.transform.position);
            Assert.That(IsWalking(figure), Is.True, "premise: the fast walk is still on");

            Assert.That(fast, Is.GreaterThan(slow * 1.5f), $"slow {slow}, fast {fast}");
        }

        // The figure from the Waypoint (which sits on the Town Exit today) out to the Stash and back at rest there,
        // so a later walk to the Exit has a distance to cover.
        private IEnumerator StandAtTheStash(MonoBehaviour figure)
        {
            yield return Click(Named("StashToggle"));
            Hasten(5000f);
            yield return UntilArrived(figure);
        }

        private static CanvasGroup Face(string name) => GameObject.Find(name).GetComponent<CanvasGroup>();

        [UnityTest]
        public IEnumerator AClickOnTheTownExit_WalksTheFigureTowardsIt_WhileTheTownMapFades()
        {
            yield return OpenExample();
            var figure = Figure();
            var exit = Named("ToFieldsButton");
            yield return StandAtTheStash(figure);
            var before = Apart(figure, exit);

            yield return Click(exit);
            yield return new WaitForSeconds(0.1f);

            Assert.That(IsWalking(figure), Is.True);
            Assert.That(Apart(figure, exit), Is.LessThan(before), "walking towards the Exit");
            Assert.That(Face("InTown").blocksRaycasts, Is.False, "the fading Town map takes input away");
            Assert.That(Face("InFields").alpha, Is.LessThan(1f), "the Field map is on its way up, not there yet");
        }

        [UnityTest]
        public IEnumerator AClickDuringTheFade_DoesNotRetargetTheFigure()
        {
            yield return OpenExample();
            var figure = Figure();
            var exit = Named("ToFieldsButton");
            yield return StandAtTheStash(figure);

            yield return Click(exit);
            yield return Click(Named("HealerToggle"));
            yield return Click(Named("StashToggle"));

            Assert.That(Context, Is.Not.EqualTo(InventoryContext.Healer), "the Healer was out of reach");
            yield return UntilArrived(figure);
            Assert.That(Apart(figure, exit), Is.LessThan(AtTheStop), "ended at the Exit");
        }

        [UnityTest]
        public IEnumerator TheCrossfade_TakesLongEnoughToWatchTheHeroWalk_AndEndsOnTheFieldMap()
        {
            yield return OpenExample();
            var inTown = Face("InTown");
            var inFields = Face("InFields");
            var fade = GameObject.Find("InTown").GetComponent<SimplePanel>().FadeDuration;
            Assert.That(fade, Is.GreaterThanOrEqualTo(1f), "long enough to watch the hero walk");

            yield return Click(Named("ToFieldsButton"));
            yield return new WaitForSeconds(fade * 0.5f);

            Assert.That(inTown.alpha, Is.GreaterThan(0f).And.LessThan(1f), "mid-fade: the Town map is on its way out, not gone");

            yield return new WaitForSeconds(fade * 0.6f);
            Assert.That(inTown.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(inFields.alpha, Is.EqualTo(1f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator PlacingTheFigureAtAPoint_StandsItThereWithNoWalk()
        {
            yield return OpenExample();
            var figure = Figure();
            var exit = Named("ToFieldsButton");
            yield return StandAtTheStash(figure);
            Assert.That(Apart(figure, exit), Is.GreaterThan(AtTheStop), "premise: the Stash is away from the Exit");

            _ = figure.GetType().GetMethod("Place").Invoke(figure, new object[] { TravelPoints.Of(exit) });

            Assert.That(Apart(figure, exit), Is.LessThan(1f));
            Assert.That(IsWalking(figure), Is.False);
        }

        // The Waypoint sits on the Town Exit today; moved off it, "at the Waypoint" and "at the Town Exit" tell apart.
        private static void MoveTheWaypointOffTheExit() => TravelPoints.Waypoint(Figure()).position += new Vector3(0f, 300f, 0f);

        [UnityTest]
        public IEnumerator AfterAReturn_TheFigureStandsAtTheTownExit_NotTheWaypoint()
        {
            yield return OpenExample();
            MoveTheWaypointOffTheExit();
            var figure = Figure();

            SimulationService.Instance.Send(GameBoot.Load().Locations.First());
            _ = SimulationService.Instance.Return();

            Assert.That(Apart(figure, Named("ToFieldsButton")), Is.LessThan(1f), "he came in through the Exit");
            Assert.That(Apart(figure, TravelPoints.Waypoint(figure)), Is.GreaterThan(AtTheStop));
            Assert.That(IsWalking(figure), Is.False);
        }

        [UnityTest]
        public IEnumerator AfterARespawn_TheFigureStandsAtTheWaypoint()
        {
            yield return OpenExample();
            MoveTheWaypointOffTheExit();
            var figure = Figure();
            _ = figure.GetType().GetMethod("Place").Invoke(figure, new object[] { TravelPoints.Of(Named("ToFieldsButton")) });
            Assert.That(Apart(figure, TravelPoints.Waypoint(figure)), Is.GreaterThan(AtTheStop), "premise: he is away from the Waypoint");

            Session.Instance.Hero.GetResource(StatName.Health).DepleteCurrent();
            SimulationService.Instance.Respawn();

            Assert.That(Apart(figure, TravelPoints.Waypoint(figure)), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator AfterAHeroLoad_TheFigureStandsAtTheWaypoint()
        {
            yield return OpenExample();
            MoveTheWaypointOffTheExit();
            var figure = Figure();
            _ = figure.GetType().GetMethod("Place").Invoke(figure, new object[] { TravelPoints.Of(Named("ToFieldsButton")) });

            _ = Session.Instance.TryLoad(GameBoot.Load().DefaultHero);

            Assert.That(Apart(figure, TravelPoints.Waypoint(figure)), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator AStandingFigure_FollowsItsPointWhenThePointMoves()
        {
            yield return OpenExample();
            var figure = Figure();
            var waypoint = TravelPoints.Waypoint(figure);
            Assert.That(Apart(figure, waypoint), Is.LessThan(1f), "premise: he stands at the Waypoint");

            // A canvas that lays out after the figure enabled, or a point moved in the Editor, must not leave him behind.
            waypoint.anchoredPosition += new Vector2(150f, 90f);
            yield return null;

            Assert.That(Apart(figure, waypoint), Is.LessThan(1f));
        }

        [UnityTest]
        public IEnumerator AFigureStandingAtTheExit_FollowsAnExitAnnouncedAnew()
        {
            yield return OpenExample();
            var figure = Figure();
            var stash = Named("StashToggle");

            SimulationService.Instance.Send(GameBoot.Load().Locations.First());
            _ = SimulationService.Instance.Return();
            Assert.That(Apart(figure, Named("ToFieldsButton")), Is.LessThan(1f), "premise: he came in through the Exit");

            _ = figure.GetType().GetMethod("SetExit").Invoke(figure, new object[] { TravelPoints.Of(stash) });
            yield return null;

            Assert.That(Apart(figure, stash), Is.LessThan(1f), "the Exit moved and he with it");
        }

        [UnityTest]
        public IEnumerator AClickOnAStopThatDoesNotOpen_LeavesTheFigureWhereItStands()
        {
            yield return OpenExample();
            var figure = Figure();
            var waypoint = TravelPoints.Waypoint(figure);

            // The Vendor is not available yet: its toggle never takes the click.
            yield return Click(Named("VendorToggle"));
            yield return null;
            yield return null;
            Assert.That(Context, Is.Not.EqualTo(InventoryContext.Vendor), "premise: the Vendor did not open");
            Assert.That(IsWalking(figure), Is.False);
            Assert.That(Apart(figure, waypoint), Is.LessThan(1f));

            // A press on the open Stop's key closes it: nothing opened, so the figure stays where it arrived.
            var stash = Named("StashToggle");
            yield return Click(stash);
            yield return null;
            Hasten(5000f);
            yield return UntilArrived(figure);

            currentTime += 1.0;
            PressAndRelease(keyboard.sKey);
            yield return null;
            Assert.That(Context, Is.EqualTo(InventoryContext.None), "premise: the second press closed it");
            Assert.That(IsWalking(figure), Is.False);
            Assert.That(Apart(figure, stash), Is.LessThan(AtTheStop));
        }
    }
}
