using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ToolSmiths.InventorySystem.Tests.PlayMode.Simulation
{
    /// <summary>
    /// Damage numbers rise from the hit's own place (epic #262, change B/C) on the real <c>Example</c> scene: the
    /// arena projects the <see cref="Coordinate"/> the hit carries, for hero and enemy alike, and the killing
    /// blow's number needs no dying view. The arena and the numbers live in the predefined assembly, out of a test
    /// assembly's reach, so they are read by type name. Run with the PlayMode filter.
    /// </summary>
    public sealed class DamageNumbersPlayModeTests
    {
        private const int MaxTicks = 600;

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

        private static object Numbers() => Field(Arena(), "numbers");

        // Where each rising number started: the origin it was shown at, startOffset included.
        private static List<Vector2> RisingFrom() =>
            ((System.Collections.IEnumerable)Field(Numbers(), "_live")).Cast<object>().Select(n => (Vector2)Field(n, "_origin")).ToList();

        // The arena's projection of a place, worked from what it serialises: the centre it was last drawn around,
        // the canvas units per arena unit and the tilt of the depth axis.
        private static Vector2 Expected(EncounterSimulation encounter, Coordinate place)
        {
            var arena = Arena();
            var offset = place - encounter.Arena.Origin;
            var scale = (float)Field(arena, "arenaScale");
            var tilt = (float)Field(arena, "tilt");
            return (Vector2)Field(arena, "_center")
                   + new Vector2(offset.x * scale, offset.z * scale * tilt)
                   + (Vector2)Field(Numbers(), "startOffset");
        }

        private static bool RoseFrom(IEnumerable<Vector2> origins, Vector2 expected) =>
            origins.Any(o => Vector2.Distance(o, expected) < 0.01f);

        private void Select(LocationConfig location)
        {
            var toggle = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .First(c => c.GetType().Name == "LocationToggle"
                            && Equals(c.GetType().GetProperty("Location").GetValue(c), location));
            toggle.GetType().GetMethod("SetToggle").Invoke(toggle, new object[] { true });
        }

        // Ticks the Run one step per frame until a hit matching `wanted` lands; the hits of the frame it landed in are returned,
        // after the frame that flushed their numbers.
        private IEnumerator UntilAHit(System.Func<EncounterSimulation, HitEvent, bool> wanted, List<HitEvent> frameHits)
        {
            Select(_locations[0]);
            yield return null;

            var encounter = SimulationService.Instance.Run.Encounter;
            var landed = new List<HitEvent>();
            encounter.HitLanded += landed.Add;

            for (var tick = 0; tick < MaxTicks; tick++)
            {
                landed.Clear();
                SimulationService.Instance.Tick(0.1f);
                yield return null;

                if (landed.Any(h => h.LostAmount > 0f && wanted(encounter, h)))
                {
                    frameHits.AddRange(landed);
                    yield break;
                }

                if (SimulationService.Instance.Run.Encounter != encounter)
                    break;
            }

            Assert.Fail("no matching hit landed");
        }

        [UnityTest]
        public IEnumerator AHitOnTheHero_RisesFromWhereTheHeroStood()
        {
            var hits = new List<HitEvent>();
            yield return UntilAHit((e, h) => ReferenceEquals(h.Target, e.Hero), hits);

            var encounter = SimulationService.Instance.Run.Encounter;
            var hit = hits.Last(h => h.LostAmount > 0f && ReferenceEquals(h.Target, encounter.Hero));
            Assert.That(RoseFrom(RisingFrom(), Expected(encounter, hit.TargetPosition)), Is.True,
                "the number starts at the arena's projection of the hit's own Coordinate");
        }

        [UnityTest]
        public IEnumerator AKillingBlow_RisesFromWhereTheEnemyFell()
        {
            var hits = new List<HitEvent>();
            yield return UntilAHit((e, h) => h.Target is Enemy enemy && enemy.Health <= 0f, hits);

            var encounter = SimulationService.Instance.Run.Encounter;
            var blow = hits.Last(h => h.LostAmount > 0f && h.Target is Enemy enemy && enemy.Health <= 0f);
            Assert.That(encounter.Enemies.Any(e => ReferenceEquals(e, blow.Target)), Is.False, "the enemy is gone from the sim");
            Assert.That(RoseFrom(RisingFrom(), Expected(encounter, blow.TargetPosition)), Is.True,
                "its number still rises where it fell");
        }

        [UnityTest]
        public IEnumerator NoNumberRemains_AfterTheArenaUnbinds()
        {
            var hits = new List<HitEvent>();
            yield return UntilAHit((_, _) => true, hits);
            Assert.That(RisingFrom(), Is.Not.Empty, "a number is rising");

            _ = SimulationService.Instance.Return();
            yield return null;

            Assert.That(RisingFrom(), Is.Empty);
            Assert.That((bool)Numbers().GetType().GetProperty("HasPending").GetValue(Numbers()), Is.False);
        }

        [UnityTest]
        public IEnumerator TheOriginsInterface_ProjectsACoordinate_NotACombatant()
        {
            var origins = Arena().GetType().GetInterfaces().Single(i => i.Name == "IDamageNumberOrigins");
            var parameters = origins.GetMethods().Single().GetParameters();

            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(Coordinate)));
            yield break;
        }
    }
}
