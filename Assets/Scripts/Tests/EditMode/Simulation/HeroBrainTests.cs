using System;
using System.Collections.Generic;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The hero's Brain (ADR-0020) asks, the Encounter carries out: reach, both cadences with the "ready but banks no
    /// burst" clamp, Resource cost and the order of the tick's resolution stay the Encounter's. The tests script the
    /// hero's Brain and observe the Encounter through <see cref="EncounterSimulation.Advance"/>; the default
    /// auto-hero Brain's whole behaviour is covered by the existing simulation tests, which run through it.
    /// </summary>
    [TestFixture]
    public sealed class HeroBrainTests
    {
        private const float Tick = 0.1f;

        private sealed class ScriptedBrain : IHeroBrain
        {
            private readonly Action<HeroView, List<Intent>> _decide;

            public ScriptedBrain(Action<HeroView, List<Intent>> decide) => _decide = decide;

            public void Decide(in HeroView view, List<Intent> intents) => _decide(view, intents);
        }

        private sealed class IdleEnemyBrain : IEnemyBrain
        {
            public void Decide(in EnemyView view, List<Intent> intents) { }
        }

        private static IEnemyBrain Idle(Enemy _) => new IdleEnemyBrain();

        // No Strike of his lands unless a test gives him damage; the Cast is dry by default.
        private static FakeHero Hero() => new() { PhysicalDamage = 5f, MagicalDamage = 5f, Resource = 0f, AttackSpeed = 1f };

        private static EncounterTuning OnTheArena() => new() { Arena = ArenaTuning.Standard(), Beat = 1000f };

        private static EncounterSimulation Sim(
            FakeHero hero, EncounterProfile profile, IHeroBrain brain, Func<Enemy, IEnemyBrain> enemyBrains = null,
            EncounterTuning tuning = null, HeroBehaviour behaviour = null) =>
            new(hero, profile, new RollStreams(new ConstantRollSource(0f), new ConstantRollSource(0.5f), new SeededRollSource(7)),
                behaviour ?? Behaviours.Engaging(10), tuning, bag: null, enemyBrains: enemyBrains, heroBrain: brain);

        private static void Run(EncounterSimulation sim, int ticks)
        {
            for (var i = 0; i < ticks; i++)
                sim.Advance(Tick);
        }

        // ─── Recall ──────────────────────────────────────────────────────────

        [Test]
        public void ARecallIntent_EndsTheFight_AndRaisesRecallRequestedOnce()
        {
            var sim = Sim(Hero(), Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) => intents.Add(Intent.Recall())), Idle);
            var requested = 0;
            sim.RecallRequested += () => requested++;

            Run(sim, 5);

            Assert.That(requested, Is.EqualTo(1));
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
        }

        [Test]
        public void ATickThatKillsTheHeroAndAsksForARecall_IsADeath()
        {
            int TicksUntilHeroDies()
            {
                var fragile = Hero();
                fragile.Health = fragile.MaxHealth = 1f;
                var probe = Sim(fragile, Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) => { }));
                var ticks = 0;
                while (!fragile.IsDown && ticks < 500)
                {
                    probe.Advance(Tick);
                    ticks++;
                }
                return ticks;
            }

            var deathTick = TicksUntilHeroDies();
            Assert.That(deathTick, Is.InRange(2, 499), "premise: the Brute does kill a one-health hero, after a few ticks");

            var hero = Hero();
            hero.Health = hero.MaxHealth = 1f;
            var calls = 0;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) =>
            {
                if (++calls == deathTick) intents.Add(Intent.Recall());
            }));
            var downed = 0;
            var requested = 0;
            sim.HeroDowned += () => downed++;
            sim.RecallRequested += () => requested++;

            Run(sim, deathTick + 5);

            Assert.That(calls, Is.EqualTo(deathTick), "premise: the Recall was asked on the very tick the hero fell");
            Assert.That(downed, Is.EqualTo(1));
            Assert.That(requested, Is.Zero, "the Death is not dodgeable by a Recall racing it");
        }

        [Test]
        public void ARecallIntent_IsCarriedOutAfterTheTicksStrikesLand()
        {
            var hero = Hero();
            hero.AttackSpeed = 10f;
            hero.PhysicalDamage = 1_000_000f;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Skirmisher), new ScriptedBrain((view, intents) =>
            {
                intents.Add(Intent.Approach(view.Enemies[0]));
                intents.Add(Intent.Strike(view.Enemies[0]));
                intents.Add(Intent.Recall());
            }), Idle);

            Run(sim, 1);

            Assert.That(sim.EnemiesDefeated, Is.EqualTo(1), "the Strike of the tick that asked for the Recall still landed");
            Assert.That(sim.Phase, Is.EqualTo(SimulationPhase.Ended));
        }

        // ─── Strike ──────────────────────────────────────────────────────────

        [Test]
        public void AStrikeOutOfReach_IsRefusedAtNoCost_AndArrivalIsOneStrikeNotABurst()
        {
            var hero = Hero();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) =>
                intents.Add(Intent.Strike(view.Enemies[0]))), Idle, OnTheArena());
            var enemy = sim.Enemies[0];
            var hits = 0;
            sim.HitLanded += hit => { if (hit.Target == enemy) hits++; };

            Run(sim, 100); // many cadences pass with the Brute 12 units away
            Assert.That(hits, Is.Zero, "out of reach, the Strike costs nothing and lands nothing");

            enemy.Position = sim.HeroPosition;
            Run(sim, 1);
            Assert.That(hits, Is.EqualTo(1), "the first tick in reach lands one Strike");

            Run(sim, 1);
            Assert.That(hits, Is.EqualTo(1), "the waiting swing banked nothing beyond it, so no burst follows");
        }

        [Test]
        public void AStrikeAskedEveryTick_LandsOnTheHerosCadenceAlone()
        {
            var hero = Hero();
            hero.AttackSpeed = 2f; // one interval is 0.5 s: every fifth tick
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) =>
                intents.Add(Intent.Strike(view.Enemies[0]))), Idle);
            var hits = 0;
            sim.HitLanded += _ => hits++;

            Run(sim, 20);

            Assert.That(hits, Is.EqualTo(4), "2 s at two Strikes a second; asking early does not hasten the swing");
        }

        [Test]
        public void ABrainIsToldWhenTheStrikeIsReady()
        {
            var hero = Hero();
            hero.AttackSpeed = 2f;
            var ready = new List<bool>();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) =>
            {
                ready.Add(view.StrikeReady);
                if (view.StrikeReady) intents.Add(Intent.Strike(view.Enemies[0]));
            }), Idle);

            Run(sim, 10);

            Assert.That(ready, Is.EqualTo(new[] { false, false, false, false, true, false, false, false, false, true }));
        }

        // ─── Cast ────────────────────────────────────────────────────────────

        private static IHeroBrain CastsEveryTick(Action<HeroView> watch = null) => new ScriptedBrain((view, intents) =>
        {
            watch?.Invoke(view);
            intents.Add(Intent.Cast());
        });

        [Test]
        public void ACastWithoutResource_IsRefusedAtNoCost()
        {
            var hero = Hero();
            hero.CastCost = 16f;
            hero.Resource = 10f; // not enough for one
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), CastsEveryTick(), Idle);
            var casts = 0;
            sim.HeroCast += () => casts++;

            Run(sim, 20);

            Assert.That(casts, Is.Zero);
            Assert.That(hero.Resource, Is.EqualTo(10f), "a refused Cast spends nothing");
            Assert.That(hero.MagicalDamageTaken, Is.Zero);
        }

        [Test]
        public void ACastOffTheBeat_IsRefusedAtNoCost_AndTheCastsKeepTheCadenceClamp()
        {
            var open = 0;
            var hero = Hero();
            hero.MagicalDamage = hero.PhysicalDamage = 0f; // the Brute outlives the whole run
            hero.CastCost = 1f;
            hero.MaxResource = hero.Resource = 1000f;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), CastsEveryTick(view => { if (view.CastBeatOpen) open++; }), Idle);
            var casts = 0;
            sim.HeroCast += () => casts++;

            Run(sim, 40);

            Assert.That(casts, Is.GreaterThan(0), "premise: he does cast");
            Assert.That(casts, Is.LessThan(40), "premise: asking every tick is not casting every tick");
            Assert.That(casts, Is.EqualTo(open), "a Cast lands on each open beat and only then");
            Assert.That(hero.Resource, Is.EqualTo(1000f - casts), "only the Casts that fired cost anything");

            var defaulting = Hero();
            defaulting.MagicalDamage = defaulting.PhysicalDamage = 0f;
            defaulting.CastCost = 1f;
            defaulting.MaxResource = defaulting.Resource = 1000f;
            var byDefault = Sim(defaulting, Profiles.Solo(EnemyArchetype.Brute), null, Idle);
            var defaultCasts = 0;
            byDefault.HeroCast += () => defaultCasts++;
            Run(byDefault, 40);
            Assert.That(casts, Is.EqualTo(defaultCasts), "the cadence is the Encounter's: the auto-hero Brain gets the same Casts");
        }

        [Test]
        public void ACastAtNobody_IsRefusedAtNoCost()
        {
            var hero = Hero();
            hero.CastCost = 1f;
            hero.MaxResource = hero.Resource = 1000f;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), CastsEveryTick(), Idle, OnTheArena());
            var casts = 0;
            sim.HeroCast += () => casts++;

            Run(sim, 40); // the Brute is 12 units away, far outside the Cast's range

            Assert.That(casts, Is.Zero);
            Assert.That(hero.Resource, Is.EqualTo(1000f));
        }

        // ─── walking ─────────────────────────────────────────────────────────

        [Test]
        public void AWalkIntent_StepsTheHeroTowardThePoint_AndStopsOnIt()
        {
            var hero = Hero();
            hero.MovementSpeed = 5000f; // 50 units a second: more than the arena needs in one tick
            var point = new Coordinate(3f, 4f);
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) =>
                intents.Add(Intent.Walk(point))), Idle, OnTheArena());

            Run(sim, 1);
            Assert.That(sim.HeroPosition, Is.EqualTo(point), "he walks all the way and never past it");

            Run(sim, 3);
            Assert.That(sim.HeroPosition, Is.EqualTo(point));
        }

        [Test]
        public void ABrainThatIssuesNothing_LeavesTheHeroWhereHeStands()
        {
            var hero = Hero();
            hero.MovementSpeed = 500f;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) => { }), Idle, OnTheArena());

            Run(sim, 10);

            Assert.That(sim.HeroPosition, Is.EqualTo(sim.Arena.Origin));
            Assert.That(sim.Enemies[0].Health, Is.EqualTo(sim.Enemies[0].MaxHealth));
        }

        // ─── what he sees, and the ring ──────────────────────────────────────

        [Test]
        public void TheBrainSeesTheEnemiesWhereTheyStoodAtTheStartOfTheTick_NotWhereTheyWalkedTo()
        {
            var seen = new List<Coordinate>();
            var sim = Sim(Hero(), Profiles.Solo(EnemyArchetype.Brute), new ScriptedBrain((view, intents) =>
                seen.Add(view.Enemies[0].Position)), null, OnTheArena());
            var spawn = sim.Enemies[0].Position;

            Run(sim, 2);

            Assert.That(seen[0], Is.EqualTo(spawn), "the first decision came before the Brute took a step");
            Assert.That(sim.Enemies[0].Position, Is.Not.EqualTo(spawn), "premise: it did walk that tick");
            Assert.That(seen[1], Is.Not.EqualTo(seen[0]), "the next decision sees where it ended up");
        }

        [Test]
        public void TheHeroTarget_IsTheTargetOfTheLatestStrikeOrApproachIntent()
        {
            var tick = 0;
            Func<List<Enemy>, Action<List<Intent>>>[] script =
            {
                e => intents => intents.Add(Intent.Approach(e[1])),
                e => intents => { intents.Add(Intent.Approach(e[1])); intents.Add(Intent.Strike(e[2])); },
                e => intents => { intents.Add(Intent.Strike(e[0])); intents.Add(Intent.Walk(new Coordinate(1f, 1f))); },
                e => intents => intents.Add(Intent.Walk(new Coordinate(1f, 1f))),
            };
            var sim = Sim(Hero(), Profiles.Group(EnemyArchetype.Brute, 3), new ScriptedBrain((view, intents) =>
            {
                var enemies = new List<Enemy>(view.Enemies);
                script[tick++](enemies)(intents);
            }), Idle, OnTheArena());
            var (a, b, c) = (sim.Enemies[0], sim.Enemies[1], sim.Enemies[2]);

            Assert.That(sim.HeroTarget, Is.Null, "no tick has decided yet");

            Run(sim, 1);
            Assert.That(sim.HeroTarget, Is.SameAs(b), "an approach names the target");

            Run(sim, 1);
            Assert.That(sim.HeroTarget, Is.SameAs(c), "the latest of the tick's Intents wins");

            Run(sim, 1);
            Assert.That(sim.HeroTarget, Is.SameAs(a), "a Walk beside a Strike changes nothing");

            Run(sim, 1);
            Assert.That(sim.HeroTarget, Is.Null, "a tick with no Strike or approach has no target");
        }
    }
}
