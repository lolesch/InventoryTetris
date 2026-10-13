using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The typed hit event and the damage spread (issue #211): every Strike and Cast that lands is announced with
    /// its dealer, target, damage type, raw amount and the amount actually lost, and each hit rolls a small spread
    /// on its own random stream. Driven through the simulation's advance entry point on the collapsed default
    /// arena, where every enemy is in reach from the first tick. The lost amount is checked against a known Armor
    /// and Magic Resist, each paired with the opposite resist as its negative control.
    /// </summary>
    [TestFixture]
    public sealed class HitEventTests
    {
        private const float Tick = 0.1f;

        private static StatModifier Set(float value) => new(new Vector2Int(0, 1000), value, StatModifierType.Overwrite);

        private static EncounterSimulation Sim(FakeHero hero, EncounterProfile profile) =>
            new(hero, profile, new ConstantRollSource(0f), Behaviours.Engaging(10), new EncounterTuning());

        private static List<HitEvent> Record(EncounterSimulation sim)
        {
            var hits = new List<HitEvent>();
            sim.HitLanded += hits.Add;
            return hits;
        }

        // Strikes on every tick; no Resource, so the Cast never fires.
        private static FakeHero Striker(float damage) =>
            new() { PhysicalDamage = damage, MagicalDamage = 0f, Resource = 0f, AttackSpeed = 10f };

        // Casts on the fourth tick (cadence 0.35 s), once, with the Resource for it; the Strike is idle.
        private static FakeHero Caster(float damage) =>
            new() { PhysicalDamage = 0f, MagicalDamage = damage, Resource = 100f, MaxResource = 100f, CastCost = 16f };

        // The hero sits out the fight, so only an enemy's Strike can raise a hit on him.
        private static FakeHero Passive() => new() { PhysicalDamage = 0f, MagicalDamage = 0f, Resource = 0f };

        // --- who, what and how much -------------------------------------------

        [Test]
        public void TheHerosStrike_RaisesAHit_CarryingTheAmountArmorLeftOfIt()
        {
            var hero = Striker(10f);
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute));
            var brute = sim.Enemies.Single();
            brute.Stat(StatName.Armor).AddModifier(Set(50f));
            var hits = Record(sim);

            sim.Advance(Tick);

            var hit = hits.Single(h => ReferenceEquals(h.Dealer, hero));
            Assert.That(hit.Target, Is.SameAs(brute));
            Assert.That(hit.DamageType, Is.EqualTo(DamageType.PhysicalDamage));
            Assert.That(hit.RawAmount, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(hit.LostAmount, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(brute.MaxHealth - brute.Health, Is.EqualTo(hit.LostAmount).Within(0.0001f));
        }

        [Test]
        public void AStrike_IgnoresMagicResist_NegativeControl()
        {
            var hero = Striker(10f);
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute));
            sim.Enemies.Single().Stat(StatName.Armor).AddModifier(Set(0f));
            sim.Enemies.Single().Stat(StatName.MagicResist).AddModifier(Set(50f));
            var hits = Record(sim);

            sim.Advance(Tick);

            Assert.That(hits.Single(h => ReferenceEquals(h.Dealer, hero)).LostAmount, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void TheHerosCast_RaisesOneHitPerEnemyItLands_CarryingTheAmountMagicResistLeftOfIt()
        {
            var hero = Caster(8f);
            var sim = Sim(hero, Profiles.Group(EnemyArchetype.Brute, 2));
            sim.Enemies[0].Stat(StatName.MagicResist).AddModifier(Set(25f));
            sim.Enemies[1].Stat(StatName.MagicResist).AddModifier(Set(50f));
            var hits = Record(sim);

            for (var i = 0; i < 4; i++) sim.Advance(Tick);

            var cast = hits.Where(h => ReferenceEquals(h.Dealer, hero) && h.DamageType == DamageType.MagicalDamage).ToList();
            Assert.That(cast.Select(h => h.Target), Is.EquivalentTo(sim.Enemies));
            foreach (var hit in cast)
                Assert.That(hit.RawAmount, Is.EqualTo(8f).Within(0.0001f));
            Assert.That(cast.Single(h => h.Target == sim.Enemies[0]).LostAmount, Is.EqualTo(6f).Within(0.0001f));
            Assert.That(cast.Single(h => h.Target == sim.Enemies[1]).LostAmount, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void ACast_IgnoresArmor_NegativeControl()
        {
            var hero = Caster(8f);
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute));
            sim.Enemies.Single().Stat(StatName.Armor).AddModifier(Set(50f));
            sim.Enemies.Single().Stat(StatName.MagicResist).AddModifier(Set(0f));
            var hits = Record(sim);

            for (var i = 0; i < 4; i++) sim.Advance(Tick);

            Assert.That(hits.Single(h => h.DamageType == DamageType.MagicalDamage).LostAmount, Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void ABrutesStrike_RaisesAPhysicalHitOnTheHero_CarryingTheAmountArmorLeftOfIt()
        {
            var hero = Passive();
            hero.ArmorPercent = 50f;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute));
            var brute = sim.Enemies.Single();
            var hits = Record(sim);

            for (var i = 0; i < 40; i++) sim.Advance(Tick);

            var onTheHero = hits.Where(h => ReferenceEquals(h.Target, hero)).ToList();
            var hit = onTheHero.First();
            Assert.That(hit.Dealer, Is.SameAs(brute));
            Assert.That(hit.DamageType, Is.EqualTo(DamageType.PhysicalDamage));
            Assert.That(hit.RawAmount, Is.EqualTo(brute.StrikeDamage).Within(0.0001f));
            Assert.That(hit.LostAmount, Is.EqualTo(brute.StrikeDamage * 0.5f).Within(0.0001f));
            Assert.That(hero.PhysicalDamageTaken, Is.EqualTo(onTheHero.Sum(h => h.LostAmount)).Within(0.001f), "every hit is announced");
        }

        [Test]
        public void ASkirmishersStrike_RaisesAMagicalHitOnTheHero_CarryingTheAmountMagicResistLeftOfIt()
        {
            var hero = Passive();
            hero.MagicResistPercent = 25f;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Skirmisher));
            var skirmisher = sim.Enemies.Single();
            var hits = Record(sim);

            for (var i = 0; i < 40; i++) sim.Advance(Tick);

            var onTheHero = hits.Where(h => ReferenceEquals(h.Target, hero)).ToList();
            var hit = onTheHero.First();
            Assert.That(hit.Dealer, Is.SameAs(skirmisher));
            Assert.That(hit.DamageType, Is.EqualTo(DamageType.MagicalDamage));
            Assert.That(hit.RawAmount, Is.EqualTo(skirmisher.StrikeDamage).Within(0.0001f));
            Assert.That(hit.LostAmount, Is.EqualTo(skirmisher.StrikeDamage * 0.75f).Within(0.0001f));
            Assert.That(hero.MagicalDamageTaken, Is.EqualTo(onTheHero.Sum(h => h.LostAmount)).Within(0.001f), "every hit is announced");
        }

        [Test]
        public void AStrikeThatTheHeroFullyMitigates_StillLands_WithNothingLost()
        {
            var hero = Passive();
            hero.ArmorPercent = 100f;
            var sim = Sim(hero, Profiles.Solo(EnemyArchetype.Brute));
            var hits = Record(sim);

            for (var i = 0; i < 40; i++) sim.Advance(Tick);

            var onTheHero = hits.Where(h => ReferenceEquals(h.Target, hero)).ToList();
            Assert.That(onTheHero, Is.Not.Empty);
            Assert.That(onTheHero.All(h => Mathf.Abs(h.LostAmount) < 0.0001f && h.RawAmount > 0f));
        }

        [Test]
        public void AHitOnACombatantAlreadyDown_IsNotAnnounced()
        {
            var hero = Passive();
            hero.Health = 0f;
            var sim = Sim(hero, Profiles.Group(EnemyArchetype.Skirmisher, 2));
            var hits = Record(sim);

            sim.Advance(Tick);

            Assert.That(hits.Where(h => ReferenceEquals(h.Target, hero)), Is.Empty);
        }

        // --- the scale and the place -------------------------------------------

        // The Standard arena puts enemies off the origin and walks the hero, so a Coordinate read off the event is
        // not vacuously the origin. Each hit is stored with where its target stood the moment it landed.
        private static EncounterSimulation OnTheArena(FakeHero hero, EncounterProfile profile, float spread = 0.2f) => new(
            hero, profile, new ConstantRollSource(0f), Behaviours.Engaging(10),
            new EncounterTuning { Arena = ArenaTuning.Standard(), DamageSpread = spread });

        private static List<(HitEvent Hit, Coordinate Stood)> RecordWithPlace(EncounterSimulation sim)
        {
            var hits = new List<(HitEvent, Coordinate)>();
            sim.HitLanded += hit => hits.Add((hit, hit.Target is Enemy enemy ? enemy.Position : sim.HeroPosition));
            return hits;
        }

        private static void RunUntil(EncounterSimulation sim, System.Func<bool> done, int maxTicks = 300)
        {
            for (var i = 0; i < maxTicks && !done(); i++) sim.Advance(Tick);
        }

        [Test]
        public void TheHerosStrike_CarriesHisBestPhysicalHit_AndWhereTheEnemyStood()
        {
            var hero = Striker(10f);
            hero.MovementSpeed = 300f; // he has to walk out to the skirmisher standing off
            var sim = OnTheArena(hero, Profiles.Solo(EnemyArchetype.Skirmisher));
            var hits = RecordWithPlace(sim);

            RunUntil(sim, () => hits.Any(h => h.Hit.DamageType == DamageType.PhysicalDamage));

            var (hit, stood) = hits.First(h => h.Hit.DamageType == DamageType.PhysicalDamage);
            Assert.That(hit.Reference, Is.EqualTo(12f).Within(0.0001f), "10 base at the top of a 20 % spread");
            Assert.That(hit.TargetPosition, Is.EqualTo(stood));
            Assert.That(hit.TargetPosition, Is.Not.EqualTo(sim.Arena.Origin), "the skirmisher stands off");
        }

        [Test]
        public void TheHerosCast_CarriesHisBestMagicalHit_AndWhereTheEnemyStood()
        {
            var sim = OnTheArena(Caster(8f), Profiles.Solo(EnemyArchetype.Brute));
            var hits = RecordWithPlace(sim);

            RunUntil(sim, () => hits.Any(h => h.Hit.DamageType == DamageType.MagicalDamage));

            var (hit, stood) = hits.First(h => h.Hit.DamageType == DamageType.MagicalDamage);
            Assert.That(hit.Reference, Is.EqualTo(9.6f).Within(0.0001f), "8 base at the top of a 20 % spread");
            Assert.That(hit.TargetPosition, Is.EqualTo(stood));
        }

        [Test]
        public void ABrutesStrike_CarriesTheLocationsStrongestPhysicalHit_AndWhereTheHeroStood()
        {
            var sim = OnTheArena(Passive(), Profiles.Solo(EnemyArchetype.Brute));
            var hits = RecordWithPlace(sim);

            RunUntil(sim, () => hits.Count > 0);

            var (hit, stood) = hits.First(h => ReferenceEquals(h.Hit.Target, sim.Hero));
            Assert.That(hit.Reference, Is.EqualTo(6.12f).Within(0.0001f), "brute damage 5.1 at source level 5, top of the spread");
            Assert.That(hit.TargetPosition, Is.EqualTo(stood));
            Assert.That(hit.TargetPosition, Is.EqualTo(sim.HeroPosition));
        }

        [Test]
        public void ASkirmishersStrike_CarriesTheLocationsStrongestMagicalHit_NotItsOwnPhysicalOne()
        {
            var sim = OnTheArena(Passive(), Profiles.Solo(EnemyArchetype.Skirmisher));
            var hits = RecordWithPlace(sim);

            RunUntil(sim, () => hits.Count > 0);

            var hit = hits.First(h => ReferenceEquals(h.Hit.Target, sim.Hero)).Hit;
            Assert.That(hit.DamageType, Is.EqualTo(DamageType.MagicalDamage));
            Assert.That(hit.Reference, Is.EqualTo(3.6f).Within(0.0001f), "skirmisher damage 3 at source level 5, top of the spread");
        }

        [Test]
        public void AHitOnTheWalkingHero_CarriesWhereHeStoodWhenItLanded()
        {
            var hero = new FakeHero { PhysicalDamage = 0.1f, MagicalDamage = 0f, Resource = 0f, AttackSpeed = 10f, MovementSpeed = 300f };
            var sim = OnTheArena(hero, Profiles.Solo(EnemyArchetype.Skirmisher));
            var hits = RecordWithPlace(sim);

            RunUntil(sim, () => hits.Any(h => ReferenceEquals(h.Hit.Target, sim.Hero)));

            var (hit, stood) = hits.First(h => ReferenceEquals(h.Hit.Target, sim.Hero));
            Assert.That(hit.TargetPosition, Is.EqualTo(stood));
            Assert.That(hit.TargetPosition, Is.Not.EqualTo(sim.Arena.Origin), "he had walked from the origin");
        }

        // --- the damage spread ------------------------------------------------

        /// <summary>A script of rolls, then neutral: a fight that rolls more than the test scripted does not throw.</summary>
        private sealed class ScriptedRolls : IRollSource
        {
            private readonly Queue<float> _script;
            public ScriptedRolls(params float[] script) => _script = new Queue<float>(script);
            public float Next() => _script.Count > 0 ? _script.Dequeue() : 0.5f;
        }

        private sealed class CountingRolls : IRollSource
        {
            private readonly IRollSource _inner;
            public CountingRolls(IRollSource inner) => _inner = inner;
            public int Count { get; private set; }

            public float Next()
            {
                Count++;
                return _inner.Next();
            }
        }

        private static EncounterSimulation SpreadSim(FakeHero hero, EncounterProfile profile, float spread,
            IRollSource hitRolls, IRollSource main = null) => new(hero, profile, main ?? new ConstantRollSource(0f),
            Behaviours.Engaging(10), new EncounterTuning { DamageSpread = spread }, bag: null, hitRolls: hitRolls);

        [TestCase(0f, 0.8f)]
        [TestCase(0.5f, 1.0f)]
        [TestCase(1f, 1.2f)]
        [TestCase(0.25f, 0.9f)]
        public void TheSpread_RollsEachStrikeWithinASymmetricFractionOfItsBaseDamage(float roll, float expectedFactor)
        {
            var hero = Striker(10f);
            var sim = SpreadSim(hero, Profiles.Solo(EnemyArchetype.Brute), 0.2f, new ScriptedRolls(roll));
            var hits = Record(sim);

            sim.Advance(Tick);

            Assert.That(hits.Single(h => ReferenceEquals(h.Dealer, hero)).RawAmount, Is.EqualTo(10f * expectedFactor).Within(0.0001f));
        }

        [Test]
        public void ASpreadHit_LosesTheSpreadAmountThroughMitigation()
        {
            var hero = Striker(10f);
            var sim = SpreadSim(hero, Profiles.Solo(EnemyArchetype.Brute), 0.2f, new ScriptedRolls(1f));
            sim.Enemies.Single().Stat(StatName.Armor).AddModifier(Set(50f));
            var hits = Record(sim);

            sim.Advance(Tick);

            Assert.That(hits.Single(h => ReferenceEquals(h.Dealer, hero)).LostAmount, Is.EqualTo(6f).Within(0.0001f), "12 raw, half stopped");
        }

        [Test]
        public void WithTheSpreadAtZero_EveryHitIsItsBaseDamage_WhateverTheStreamRolls_NegativeControl()
        {
            var hero = Striker(10f);
            var sim = SpreadSim(hero, Profiles.Solo(EnemyArchetype.Brute), 0f, new ConstantRollSource(1f));
            var hits = Record(sim);

            sim.Advance(Tick);

            Assert.That(hits.Single(h => ReferenceEquals(h.Dealer, hero)).RawAmount, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void WithoutAHitStream_TheSpreadRollsNeutral_AndEveryHitIsItsBaseDamage()
        {
            var hero = Striker(10f);
            var sim = SpreadSim(hero, Profiles.Solo(EnemyArchetype.Brute), 0.2f, hitRolls: null);
            var hits = Record(sim);

            sim.Advance(Tick);

            Assert.That(hits.Single(h => ReferenceEquals(h.Dealer, hero)).RawAmount, Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void TheDefaultTuning_HasNoSpread_SoEveryExistingFigureHolds()
        {
            Assert.That(new EncounterTuning().DamageSpread, Is.Zero);
        }

        [Test]
        public void TheCast_RollsTheSpreadOncePerEnemyItHits()
        {
            var hero = Caster(10f);
            var sim = SpreadSim(hero, Profiles.Group(EnemyArchetype.Brute, 2), 0.2f, new ScriptedRolls(0f, 1f));
            var hits = Record(sim);

            for (var i = 0; i < 4; i++) sim.Advance(Tick);

            var cast = hits.Where(h => ReferenceEquals(h.Dealer, hero) && h.DamageType == DamageType.MagicalDamage).ToList();
            Assert.That(cast, Has.Count.EqualTo(2));
            Assert.That(cast.Select(h => h.RawAmount).OrderBy(x => x), Is.EqualTo(new[] { 8f, 12f }).Within(0.0001f));
        }

        [Test]
        public void AnEnemyStrike_RollsTheSpreadToo()
        {
            var hero = Passive();
            hero.AttackSpeed = 0.01f; // the hero's own Strike stays out of the roll script
            var sim = SpreadSim(hero, Profiles.Solo(EnemyArchetype.Brute), 0.2f, new ScriptedRolls(0f));
            var brute = sim.Enemies.Single();
            var hits = Record(sim);

            for (var i = 0; i < 20; i++) sim.Advance(Tick);

            Assert.That(hits.First(h => ReferenceEquals(h.Dealer, brute)).RawAmount,
                Is.EqualTo(brute.StrikeDamage * 0.8f).Within(0.0001f));
        }

        [Test]
        public void TheSpreadStream_NeverShiftsTheEncountersOwnRolls()
        {
            int MainRollsWith(IRollSource hitRolls)
            {
                var main = new CountingRolls(new SeededRollSource(7));
                var sim = SpreadSim(Striker(10f), Profiles.Group(EnemyArchetype.Brute, 3), 0.2f, hitRolls, main);
                for (var i = 0; i < 100; i++) sim.Advance(Tick);
                return main.Count;
            }

            var low = MainRollsWith(new ConstantRollSource(0f));
            var high = MainRollsWith(new ConstantRollSource(1f));

            Assert.That(low, Is.GreaterThan(0), "the encounter rolls its roster and spawns from its own stream");
            Assert.That(high, Is.EqualTo(low));
        }

        private static List<(float Raw, float Lost)> Fight(int hitSeed)
        {
            var sim = SpreadSim(Striker(10f), Profiles.Group(EnemyArchetype.Brute, 3), 0.2f,
                new SeededRollSource(hitSeed), new SeededRollSource(7));
            var hits = Record(sim);
            for (var i = 0; i < 100; i++) sim.Advance(Tick);
            return hits.Select(h => (h.RawAmount, h.LostAmount)).ToList();
        }

        [Test]
        public void TheSameSeedRunTwice_GivesIdenticalHits_AndAnotherSeedDoesNot()
        {
            var first = Fight(hitSeed: 11);
            var second = Fight(hitSeed: 11);
            var other = Fight(hitSeed: 12);

            Assert.That(first, Is.Not.Empty);
            Assert.That(second, Is.EqualTo(first));
            Assert.That(other, Is.Not.EqualTo(first), "negative control: the spread is what varies them");
        }

        [Test]
        public void ASpreadOutsideZeroToOne_IsRejected()
        {
            var profile = Profiles.Solo(EnemyArchetype.Brute);

            Assert.Throws<System.ArgumentOutOfRangeException>(() => SpreadSim(Passive(), profile, -0.1f, null));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SpreadSim(Passive(), profile, 1.1f, null));
            Assert.DoesNotThrow(() => SpreadSim(Passive(), profile, 1f, null));
        }
    }
}
