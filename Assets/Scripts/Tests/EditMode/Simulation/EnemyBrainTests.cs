using System;
using System.Collections.Generic;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// An enemy's Brain (ADR-0020) asks, the Encounter carries out: reach, the Strike cadence and the "ready but
    /// banks no burst" clamp stay the Encounter's. The tests build an Encounter with scripted Brains and observe it
    /// through <see cref="EncounterSimulation.Advance"/>. A Brute walks 2 units a second, Strikes from 1.5 and 0.55
    /// times a second, and spawns 12 units from the hero; the default Brain's whole behaviour is covered by the
    /// existing simulation tests, which run through it.
    /// </summary>
    [TestFixture]
    public sealed class EnemyBrainTests
    {
        private const float Tick = 0.1f;

        private sealed class ScriptedBrain : IEnemyBrain
        {
            private readonly Action<EnemyView, List<Intent>> _decide;

            public ScriptedBrain(Action<EnemyView, List<Intent>> decide) => _decide = decide;

            public void Decide(in EnemyView view, List<Intent> intents) => _decide(view, intents);
        }

        // Neither of the hero's attacks lands; he only stands there and takes the hits.
        private static FakeHero Passive() => new() { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f, Health = 1000f, MaxHealth = 1000f };

        private static EncounterTuning OnTheArena() => new() { Arena = ArenaTuning.Standard() };

        private static EncounterSimulation Sim(FakeHero hero, EncounterProfile profile, Func<Enemy, IEnemyBrain> brains) =>
            new(hero, profile, new RollStreams(new ConstantRollSource(0f), new ConstantRollSource(0.5f), new SeededRollSource(7)),
                Behaviours.Engaging(10), OnTheArena(), bag: null, enemyBrains: brains);

        private static void RunSeconds(EncounterSimulation sim, float seconds)
        {
            for (var i = 0; i < Math.Round(seconds / Tick); i++)
                sim.Advance(Tick);
        }

        [Test]
        public void ABrainThatIssuesNothing_LeavesItsEnemyStandingWhereItSpawned()
        {
            var hero = Passive();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), _ => new ScriptedBrain((view, intents) => { }));
            var spawn = sim.Enemies[0].Position;

            RunSeconds(sim, 10f);

            Assert.That(sim.Enemies[0].Position, Is.EqualTo(spawn));
            Assert.That(hero.PhysicalDamageTaken, Is.Zero);
        }

        [Test]
        public void AStrikeOutOfReach_IsRefusedAtNoCost_AndArrivalIsOneStrikeNotABurst()
        {
            var hero = Passive();
            var approach = false;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), _ => new ScriptedBrain((view, intents) =>
            {
                intents.Add(Intent.Strike(view.Hero));
                if (approach) intents.Add(Intent.Approach(view.Hero));
            }));
            var hits = 0;
            sim.HitLanded += hit => { if (hit.Target == hero) hits++; };

            RunSeconds(sim, 10f); // many cadences pass with the enemy 12 units away

            Assert.That(hits, Is.Zero, "out of reach, the Strike costs nothing and lands nothing");

            approach = true;
            while (hits == 0)
                sim.Advance(Tick);
            Assert.That(hits, Is.EqualTo(1), "the first tick in reach lands one Strike");

            sim.Advance(Tick);
            Assert.That(hits, Is.EqualTo(1), "the waiting swing banked nothing beyond it, so no burst follows");
        }

        [Test]
        public void AStrikeAskedEveryTick_LandsOnTheEnemysCadenceAlone_AsTheDefaultBrainDoes()
        {
            var askedEveryTick = Passive();
            var asking = Sim(askedEveryTick, Profiles.Solo(EnemyArchetype.Brute), _ => new ScriptedBrain((view, intents) =>
            {
                intents.Add(Intent.Approach(view.Hero));
                intents.Add(Intent.Strike(view.Hero));
            }));
            var byDefault = Passive();
            var defaulting = Sim(byDefault, Profiles.Solo(EnemyArchetype.Brute), null);

            RunSeconds(asking, 20f);
            RunSeconds(defaulting, 20f);

            Assert.That(byDefault.PhysicalDamageTaken, Is.GreaterThan(0f), "premise: the fight landed Strikes");
            Assert.That(askedEveryTick.PhysicalDamageTaken, Is.EqualTo(byDefault.PhysicalDamageTaken),
                "a refused Strike consumes no hit roll and no cadence, so the seeded damage is the same");
        }

        [Test]
        public void EveryBrainDecidesFromTheStartOfTheTick_WhateverItsPlaceInTheOrder()
        {
            var hero = Passive();
            var seenByFirst = new List<float>();
            var seenBySecond = new List<float>();
            var seenHeroPosition = new List<Coordinate>();
            var made = 0;
            var sim = Sim(hero, Profiles.Group(EnemyArchetype.Brute, 2), _ =>
            {
                var log = made++ == 0 ? seenByFirst : seenBySecond;
                return new ScriptedBrain((view, intents) =>
                {
                    log.Add(view.HeroHealthFraction);
                    if (log == seenByFirst) seenHeroPosition.Add(view.HeroPosition);
                    ChaseAndStrikeBrain.Instance.Decide(view, intents);
                });
            });

            RunSeconds(sim, 20f);

            Assert.That(seenByFirst[^1], Is.LessThan(seenByFirst[0]), "premise: the enemies hurt the hero over the fight");
            Assert.That(seenBySecond, Is.EqualTo(seenByFirst),
                "the first enemy's Strike lands after the second has decided, so both saw the same health every tick");
        }

        [Test]
        public void ABrainSeesTheHeroWhereHeStoodAtTheStartOfTheTick_NotWhereHeWalkedTo()
        {
            var hero = Passive();
            hero.MovementSpeed = 300f; // he sets off for his target on the first tick
            var seen = new List<Coordinate>();
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute), _ => new ScriptedBrain((view, intents) =>
                seen.Add(view.HeroPosition)));

            sim.Advance(Tick);
            sim.Advance(Tick);

            Assert.That(seen[0], Is.EqualTo(sim.Arena.Origin), "the first decision came before the hero moved");
            Assert.That(sim.HeroPosition, Is.Not.EqualTo(sim.Arena.Origin), "premise: he did walk that tick");
            Assert.That(seen[1], Is.Not.EqualTo(seen[0]), "the next tick's decision sees where he ended up");
        }
    }
}
