using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Runtime.Character;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Character
{
    /// <summary>
    /// <see cref="Hero"/> is the hero's whole model - stats, resources, level and XP, regeneration,
    /// damage, modifier comparison, the item-stat add/remove - built here with no
    /// <c>GameObject</c> (issue #110). Expected values are worked from the fixture's base numbers,
    /// not read back off the hero.
    /// </summary>
    [TestFixture]
    public sealed class HeroTests
    {
        private const float Tolerance = 0.001f;
        private const string DefaultHeroPath = "Assets/Scripts/InventorySystem/Characters/DefaultHero.asset";

        // Armor 20 mitigates 20% of a physical hit; MagicResist 10 mitigates 10% of a magical one.
        private static Hero NewHero(float shield = 0f, float attackSpeed = 0f, bool spendResource = true) =>
            new(new[]
                {
                    new CharacterStat(StatName.AttackSpeed, attackSpeed),
                    new CharacterStat(StatName.PhysicalDamage, 50f),
                    new CharacterStat(StatName.MagicalDamage, 40f),
                    new CharacterStat(StatName.HealthRegeneration, 5f),
                    new CharacterStat(StatName.Armor, 20f),
                    new CharacterStat(StatName.MagicResist, 10f),
                    new CharacterStat(StatName.ResourceRegeneration, 8f),
                },
                new[]
                {
                    new CharacterResource(StatName.Health, 100f),
                    new CharacterResource(StatName.Resource, 100f),
                    new CharacterResource(StatName.Shield, shield),
                    new CharacterResource(StatName.Experience, 280f),
                })
            { SpendResource = spendResource };

        private static StatModifier Flat(float value) => new(new Vector2Int(0, 1000), value);
        private static StatModifier Percent(float value) => new(new Vector2Int(0, 1000), value, StatModifierType.PercentAdd);
        private static CharacterStatModifier On(StatName stat, StatModifier modifier) => new(stat, modifier);

        private static void Drain(Hero hero, StatName resource, float toCurrent) =>
            hero.GetResource(resource).RemoveFromCurrent(hero.GetResource(resource).CurrentValue - toCurrent);

        [Test]
        public void Construction_NeedsNoGameObject_AndStartsFullExceptExperience()
        {
            var hero = NewHero(shield: 30f);

            Assert.That(hero.Level, Is.EqualTo(1u));
            Assert.That(hero.IsDead, Is.False);
            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(100f));
            Assert.That(hero.GetResource(StatName.Resource).CurrentValue, Is.EqualTo(100f));
            Assert.That(hero.GetResource(StatName.Shield).CurrentValue, Is.EqualTo(30f));
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.Zero);
        }

        [TestCase(StatName.Health)]
        [TestCase(StatName.Resource)]
        [TestCase(StatName.Shield)]
        [TestCase(StatName.Experience)]
        public void Construction_WithoutARequiredResource_FailsNamingIt(StatName missing)
        {
            var resources = new[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience }
                .Where(name => name != missing)
                .Select(name => new CharacterResource(name, 100f));

            var exception = Assert.Throws<InvalidOperationException>(() => _ = new Hero(System.Array.Empty<CharacterStat>(), resources));

            Assert.That(exception.Message, Does.Contain(missing.ToString()));
        }

        [Test]
        public void Hero_IsTheStatReceiverTheContainerCoreTakes()
        {
            IStatReceiver receiver = NewHero();

            Assert.That(receiver, Is.InstanceOf<Hero>());
        }

        [Test]
        public void GetStat_FindsAStatOrAResource_AndNullForOneTheHeroLacks()
        {
            var hero = NewHero();

            Assert.That(hero.GetStat(StatName.Armor).BaseValue, Is.EqualTo(20f));
            Assert.That(hero.GetStat(StatName.Health), Is.SameAs(hero.GetResource(StatName.Health)));
            Assert.That(hero.GetStat(StatName.IncreasedItemRarity), Is.Null);
            Assert.That(hero.GetResource(StatName.Armor), Is.Null, "Armor is a stat, not a resource");
        }

        // --- the template -------------------------------------------------------------------------

        [Test]
        public void DefaultHeroData_ReproducesTheValuesTheScenePlayerSerialized()
        {
            var data = AssetDatabase.LoadAssetAtPath<HeroData>(DefaultHeroPath);
            Assert.That(data, Is.Not.Null, DefaultHeroPath + " is missing");

            var hero = new Hero(data);

            var stats = new Dictionary<StatName, float>
            {
                [StatName.AttackSpeed] = 0.658f,
                [StatName.PhysicalDamage] = 59f,
                [StatName.MagicalDamage] = 0f,
                [StatName.ArmorPenetration] = 0f,
                [StatName.MagicPenetration] = 0f,
                [StatName.HealthRegeneration] = 3.5f,
                [StatName.Armor] = 26f,
                [StatName.MagicResist] = 30f,
                [StatName.MovementSpeed] = 325f,
                [StatName.ResourceRegeneration] = 7f,
                [StatName.IncreasedItemRarity] = 0f,
                [StatName.IncreasedItemQuantity] = 0f,
            };

            foreach (var (stat, baseValue) in stats)
            {
                Assert.That(hero.GetStat(stat), Is.Not.Null, $"{stat} is missing");
                Assert.That(hero.GetStat(stat).BaseValue, Is.EqualTo(baseValue).Within(0.0001f), stat.ToString());
            }

            Assert.That(hero.Stats, Has.Count.EqualTo(stats.Count), "no stat beyond the ones the player serialized");

            var resources = new Dictionary<StatName, float>
            {
                [StatName.Health] = 640f,
                [StatName.Resource] = 280f,
                [StatName.Shield] = 0f,
                [StatName.Experience] = 280f,
            };

            foreach (var (resource, baseValue) in resources)
            {
                Assert.That(hero.GetResource(resource), Is.Not.Null, $"{resource} is missing");
                Assert.That(hero.GetResource(resource).BaseValue, Is.EqualTo(baseValue), resource.ToString());
            }

            Assert.That(hero.Resources, Has.Count.EqualTo(resources.Count));
            Assert.That(hero.Level, Is.EqualTo(1u));
            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(640f), "built full");
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.Zero, "built with no XP");
        }

        [Test]
        public void TwoHeroesBuiltFromOneTemplate_ShareNoState()
        {
            var data = AssetDatabase.LoadAssetAtPath<HeroData>(DefaultHeroPath);
            var first = new Hero(data);
            var second = new Hero(data);

            first.AddItemStats(new[] { On(StatName.Armor, Flat(10f)) });

            Assert.That(first.GetStatValue(StatName.Armor), Is.EqualTo(36f));
            Assert.That(second.GetStatValue(StatName.Armor), Is.EqualTo(26f));
            Assert.That(data.Stats[0].BaseValue, Is.EqualTo(0.658f).Within(0.0001f), "the template is untouched");
        }

        // --- stat totals and item stats -----------------------------------------------------------

        [Test]
        public void StatTotal_AppliesFlatAdditionsBeforePercentAdditions()
        {
            var hero = NewHero();

            hero.AddItemStats(new[] { On(StatName.Armor, Flat(10f)), On(StatName.Armor, Percent(50f)) });

            Assert.That(hero.GetStatValue(StatName.Armor), Is.EqualTo(45f), "(20 + 10) * 1.5");
        }

        [Test]
        public void RemoveItemStats_LiftsTheModifiersOffAgain()
        {
            var hero = NewHero();
            var gear = new[] { On(StatName.Armor, Flat(10f)), On(StatName.PhysicalDamage, Percent(10f)) };
            hero.AddItemStats(gear);

            hero.RemoveItemStats(gear);

            Assert.That(hero.GetStatValue(StatName.Armor), Is.EqualTo(20f));
            Assert.That(hero.GetStatValue(StatName.PhysicalDamage), Is.EqualTo(50f));
        }

        [Test]
        public void AddItemStats_RaisesAResourceTotal_AndRemovingItClampsTheCurrentBack()
        {
            var hero = NewHero();
            var health = hero.GetResource(StatName.Health);
            var gear = new[] { On(StatName.Health, Flat(50f)) };

            hero.AddItemStats(gear);
            Assert.That(health.TotalValue, Is.EqualTo(150f));
            Assert.That(health.CurrentValue, Is.EqualTo(100f), "more room, not more health");

            health.RefillCurrent();
            hero.RemoveItemStats(gear);

            Assert.That(health.TotalValue, Is.EqualTo(100f));
            Assert.That(health.CurrentValue, Is.EqualTo(100f), "current follows the total back down");
        }

        [Test]
        public void AddItemStats_ForAStatTheHeroLacks_IsIgnored()
        {
            var hero = NewHero();

            Assert.DoesNotThrow(() => hero.AddItemStats(new[] { On(StatName.IncreasedItemRarity, Flat(5f)) }));
        }

        [Test]
        public void AddItemStats_AppliesEveryAffixOfTheItem_ToItsOwnStat()
        {
            var hero = NewHero();

            hero.AddItemStats(new[]
            {
                On(StatName.PhysicalDamage, Flat(5f)),
                On(StatName.MagicResist, Flat(7f)),
                On(StatName.Resource, Flat(20f)),
            });

            Assert.That(hero.GetStatValue(StatName.PhysicalDamage), Is.EqualTo(55f));
            Assert.That(hero.GetStatValue(StatName.MagicResist), Is.EqualTo(17f));
            Assert.That(hero.GetResource(StatName.Resource).TotalValue, Is.EqualTo(120f));
            Assert.That(hero.GetStatValue(StatName.Armor), Is.EqualTo(20f), "untouched");
        }

        // --- StatsChanged: what the stat panel binds to ---------------------------------------------

        private static int CountStatsChanged(Hero hero, Action act)
        {
            var raised = 0;
            hero.StatsChanged += () => raised++;

            act();

            return raised;
        }

        [Test]
        public void StatsChanged_IsRaised_WhenGearIsEquipped()
        {
            var hero = NewHero();

            var raised = CountStatsChanged(hero, () => hero.AddItemStats(new[] { On(StatName.Armor, Flat(10f)) }));

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void StatsChanged_IsRaised_WhenGearIsUnequipped()
        {
            var hero = NewHero();
            var gear = new[] { On(StatName.Armor, Flat(10f)) };
            hero.AddItemStats(gear);

            var raised = CountStatsChanged(hero, () => hero.RemoveItemStats(gear));

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void StatsChanged_IsRaised_ForAResourceTotalToo()
        {
            var hero = NewHero();

            var raised = CountStatsChanged(hero, () => hero.AddItemStats(new[] { On(StatName.Health, Flat(50f)) }));

            Assert.That(raised, Is.EqualTo(1));
        }

        [Test]
        public void StatsChanged_IsRaised_WhenTheHeroLevelsUp()
        {
            var hero = NewHero();

            var experience = hero.GetResource(StatName.Experience);
            var thresholdBefore = experience.TotalValue;

            var raised = CountStatsChanged(hero, () => hero.GainExperience(280f, monsterLevel: 1u));

            Assert.That(experience.TotalValue, Is.GreaterThan(thresholdBefore), "the next threshold is a new Experience total");
            Assert.That(raised, Is.EqualTo(1), "one level, one new total - the heal moves only current values");
        }

        [Test]
        public void StatsChanged_IsNotRaised_WhenOnlyACurrentValueMoves()
        {
            var hero = NewHero();

            var raised = CountStatsChanged(hero, () =>
            {
                hero.ReceiveDamage(DamageType.PhysicalDamage, 30f);
                hero.Regenerate(1f);
                hero.GainExperience(50f, monsterLevel: 1u);
            });

            Assert.That(raised, Is.Zero, "the panel shows totals, not the globes' current values");
        }

        [Test]
        public void StatsChanged_IsNotRaised_ByComparingTwoModifiers()
        {
            var hero = NewHero();
            var worn = Flat(30f);
            hero.AddItemStats(new[] { On(StatName.Armor, worn) });

            var raised = CountStatsChanged(hero, () => _ = hero.CompareStatModifiers(StatName.Armor, Flat(40f), worn));

            Assert.That(raised, Is.Zero, "a hover compares on copies");
        }

        [Test]
        public void StatsChanged_IsNotRaised_ForAModifierThatLeavesTheTotalAlone()
        {
            var hero = NewHero();

            var raised = CountStatsChanged(hero, () => hero.AddItemStats(new[] { On(StatName.Armor, Flat(0f)) }));

            Assert.That(raised, Is.Zero);
        }

        // --- regeneration -------------------------------------------------------------------------

        [Test]
        public void Regenerate_RefillsHealthAndResource_ByTheirRegenStatsOverTheElapsedTime()
        {
            var hero = NewHero();
            Drain(hero, StatName.Health, 50f);
            Drain(hero, StatName.Resource, 20f);

            hero.Regenerate(2f);

            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(60f), "50 + 5/s * 2s");
            Assert.That(hero.GetResource(StatName.Resource).CurrentValue, Is.EqualTo(36f), "20 + 8/s * 2s");
        }

        [Test]
        public void Regenerate_DoesNotReviveADeadHero()
        {
            var hero = NewHero();
            hero.GetResource(StatName.Health).DepleteCurrent();

            hero.Regenerate(10f);

            Assert.That(hero.IsDead, Is.True);
            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.Zero);
        }

        [Test]
        public void Regenerate_RecoversAnEmptyResourceAtOnce()
        {
            var hero = NewHero();
            hero.GetResource(StatName.Resource).DepleteCurrent();

            hero.Regenerate(1f);

            Assert.That(hero.GetResource(StatName.Resource).CurrentValue, Is.EqualTo(8f));
        }

        [Test]
        public void Regenerate_HoldsAnEmptyShieldForTwoSeconds_ThenRechargesItByTheHealthRegen()
        {
            var hero = NewHero(shield: 30f);
            hero.GetResource(StatName.Shield).DepleteCurrent();

            hero.Regenerate(1f);
            Assert.That(hero.GetResource(StatName.Shield).CurrentValue, Is.Zero, "still inside the wait");

            hero.Regenerate(1f);
            Assert.That(hero.GetResource(StatName.Shield).CurrentValue, Is.EqualTo(5f), "wait over: 5/s * 1s");
        }

        [Test]
        public void Regenerate_KeepsItsEmptyTimers_BetweenCalls()
        {
            var hero = NewHero(shield: 30f);
            hero.GetResource(StatName.Shield).DepleteCurrent();

            hero.Regenerate(0.5f);
            hero.Regenerate(0.5f);
            hero.Regenerate(0.5f);
            Assert.That(hero.GetResource(StatName.Shield).CurrentValue, Is.Zero, "1.5s empty");

            hero.Regenerate(0.5f);
            Assert.That(hero.GetResource(StatName.Shield).CurrentValue, Is.EqualTo(2.5f), "2s empty: the step that ends the wait regenerates");
        }

        // --- level and XP -------------------------------------------------------------------------

        [Test]
        public void GainExperience_FromAMonsterBelowTheHero_DoesNotWrapTheLevelDifference()
        {
            var hero = NewHero();
            hero.GainExperience(280f, monsterLevel: 1u);
            Assert.That(hero.Level, Is.EqualTo(2u), "precondition: one level-up");

            hero.GainExperience(10f, monsterLevel: 1u);

            Assert.That(hero.Level, Is.EqualTo(2u), "10 XP from a lower-level monster is a few XP, not a wrapped uint");
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.InRange(0f, 10f));
        }

        [Test]
        public void ASparseTemplate_RegeneratesAndCalculatesDamage_WithoutThrowing()
        {
            var hero = new Hero(Array.Empty<CharacterStat>(), new[]
            {
                new CharacterResource(StatName.Health, 100f),
                new CharacterResource(StatName.Resource, 100f),
                new CharacterResource(StatName.Shield, 0f),
                new CharacterResource(StatName.Experience, 280f),
            });

            Assert.DoesNotThrow(() => hero.Regenerate(1f));
            Assert.That(hero.CalculateDamageOutput(DamageType.PhysicalDamage), Is.Zero);
            Assert.That(hero.CalculateReceivingDamage(DamageType.PhysicalDamage, 10f), Is.EqualTo(10f));
        }

        [Test]
        public void GainExperience_BelowTheThreshold_KeepsTheLevel()
        {
            var hero = NewHero();

            hero.GainExperience(100f, monsterLevel: 1u);

            Assert.That(hero.Level, Is.EqualTo(1u));
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.EqualTo(100f));
        }

        [Test]
        public void GainExperience_PastTheThreshold_LevelsUpAndRaisesTheNextThreshold()
        {
            var hero = NewHero();
            var experience = hero.GetResource(StatName.Experience);

            hero.GainExperience(1000f, monsterLevel: 1u);

            // 280 fills level 1 -> 2 and the bar grows by 2*100+80; the next 560 fills 2 -> 3 and it
            // grows by 3*100+80; the last 160 is progress toward level 4.
            Assert.That(hero.Level, Is.EqualTo(3u));
            Assert.That(experience.TotalValue, Is.EqualTo(940f), "280 + 280 + 380");
            Assert.That(experience.CurrentValue, Is.EqualTo(160f));
        }

        [Test]
        public void LevelUp_HealsTheHeroItself()
        {
            var hero = NewHero();
            Drain(hero, StatName.Health, 10f);
            Drain(hero, StatName.Resource, 5f);

            hero.GainExperience(280f, monsterLevel: 1u);

            Assert.That(hero.Level, Is.EqualTo(2u));
            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(100f));
            Assert.That(hero.GetResource(StatName.Resource).CurrentValue, Is.EqualTo(100f));
        }

        [Test]
        public void GainExperience_BelowTheThreshold_DoesNotHeal()
        {
            var hero = NewHero();
            Drain(hero, StatName.Health, 10f);

            hero.GainExperience(100f, monsterLevel: 1u);

            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(10f));
        }

        [Test]
        public void GainExperience_WhenDead_GainsNothing()
        {
            var hero = NewHero();
            hero.GetResource(StatName.Health).DepleteCurrent();

            hero.GainExperience(1000f, monsterLevel: 1u);

            Assert.That(hero.Level, Is.EqualTo(1u));
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.Zero);
        }

        [Test]
        public void Heal_RefillsHealthAndResource_ButNotShieldOrExperience()
        {
            var hero = NewHero(shield: 30f);
            Drain(hero, StatName.Health, 1f);
            Drain(hero, StatName.Resource, 1f);
            Drain(hero, StatName.Shield, 1f);
            hero.GainExperience(50f, monsterLevel: 1u);

            hero.Heal();

            Assert.That(hero.GetResource(StatName.Health).IsFull, Is.True);
            Assert.That(hero.GetResource(StatName.Resource).IsFull, Is.True);
            Assert.That(hero.GetResource(StatName.Shield).CurrentValue, Is.EqualTo(1f));
            Assert.That(hero.GetResource(StatName.Experience).CurrentValue, Is.EqualTo(50f));
        }

        // --- damage -------------------------------------------------------------------------------

        [Test]
        public void ReceiveDamage_Physical_IsMitigatedByArmor()
        {
            var hero = NewHero();

            hero.ReceiveDamage(DamageType.PhysicalDamage, 100f);

            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(20f).Within(Tolerance), "100 * (1 - 20%) = 80");
        }

        [Test]
        public void ReceiveDamage_Magical_IsMitigatedByMagicResist()
        {
            var hero = NewHero();

            hero.ReceiveDamage(DamageType.MagicalDamage, 50f);

            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(55f).Within(Tolerance), "50 * (1 - 10%) = 45");
        }

        [Test]
        public void ReceiveDamage_ARestPast100_NeverHealsTheHero()
        {
            var hero = NewHero();
            hero.AddItemStats(new[] { On(StatName.Armor, Flat(200f)) });
            Drain(hero, StatName.Health, 50f);

            hero.ReceiveDamage(DamageType.PhysicalDamage, 100f);

            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(50f).Within(Tolerance), "clamped to full mitigation");
        }

        [Test]
        public void ReceiveDamage_TheShieldAbsorbsBeforeTheHealth()
        {
            var hero = NewHero(shield: 30f);

            hero.ReceiveDamage(DamageType.PhysicalDamage, 50f);

            Assert.That(hero.GetResource(StatName.Shield).CurrentValue, Is.EqualTo(0f).Within(Tolerance), "50 * 80% = 40: the shield takes 30");
            Assert.That(hero.GetResource(StatName.Health).CurrentValue, Is.EqualTo(90f).Within(Tolerance), "and the health the other 10");
        }

        [Test]
        public void ReceiveDamage_ReportsWhatTheShieldAbsorbedAndTheHealthLost()
        {
            var hero = NewHero(shield: 30f);
            var reports = new List<(DamageType Type, float Absorbed, float Lost)>();
            hero.DamageReceived += (type, absorbed, lost) => reports.Add((type, absorbed, lost));

            hero.ReceiveDamage(DamageType.PhysicalDamage, 50f);

            Assert.That(reports, Has.Count.EqualTo(1));
            Assert.That(reports[0].Type, Is.EqualTo(DamageType.PhysicalDamage));
            Assert.That(reports[0].Absorbed, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(reports[0].Lost, Is.EqualTo(10f).Within(Tolerance));
        }

        [Test]
        public void ReceiveDamage_WhenInvincible_TakesNothing()
        {
            var hero = NewHero();
            hero.IsInvincible = true;

            hero.ReceiveDamage(DamageType.PhysicalDamage, 100f);

            Assert.That(hero.GetResource(StatName.Health).IsFull, Is.True);
        }

        [Test]
        public void ReceiveDamage_WhenAlreadyDead_IsIgnored()
        {
            var hero = NewHero();
            hero.GetResource(StatName.Health).DepleteCurrent();
            var reported = false;
            hero.DamageReceived += (_, _, _) => reported = true;

            hero.ReceiveDamage(DamageType.PhysicalDamage, 100f);

            Assert.That(reported, Is.False);
        }

        [Test]
        public void ReceiveDamage_EmptiesTheHealth_AndTheHeroIsDead()
        {
            var hero = NewHero();

            hero.ReceiveDamage(DamageType.PhysicalDamage, 1000f);

            Assert.That(hero.IsDead, Is.True);
        }

        [Test]
        public void DealDamageTo_SpendsResource_AndHitsForTheDamageOutput()
        {
            var attacker = NewHero(attackSpeed: 100f);
            var target = NewHero();

            attacker.DealDamageTo(target, DamageType.PhysicalDamage);

            Assert.That(attacker.GetResource(StatName.Resource).CurrentValue, Is.EqualTo(97f).Within(Tolerance), "3% of the 100 Resource total");
            Assert.That(target.GetResource(StatName.Health).CurrentValue, Is.EqualTo(20f).Within(Tolerance), "50 * (1 + 100*0.01) = 100, 20% armored away");
        }

        [Test]
        public void DealDamageTo_ReportsTheOutputItDealt()
        {
            var attacker = NewHero();
            float? dealt = null;
            attacker.DamageDealt += (_, damage) => dealt = damage;

            attacker.DealDamageTo(NewHero(), DamageType.MagicalDamage);

            Assert.That(dealt, Is.EqualTo(40f));
        }

        [Test]
        public void DealDamageTo_WithoutTheResourceToPay_DealsNothing()
        {
            var attacker = NewHero();
            Drain(attacker, StatName.Resource, 2f);
            var target = NewHero();

            attacker.DealDamageTo(target, DamageType.PhysicalDamage);

            Assert.That(target.GetResource(StatName.Health).IsFull, Is.True);
            Assert.That(attacker.GetResource(StatName.Resource).CurrentValue, Is.EqualTo(2f));
        }

        [Test]
        public void DealDamageTo_WithSpendResourceOff_IsFree()
        {
            var attacker = NewHero(spendResource: false);
            Drain(attacker, StatName.Resource, 0f);
            var target = NewHero();

            attacker.DealDamageTo(target, DamageType.PhysicalDamage);

            Assert.That(target.GetResource(StatName.Health).CurrentValue, Is.EqualTo(60f).Within(Tolerance), "50 - 20%");
        }

        [Test]
        public void DealDamageTo_WhenDead_DoesNothing()
        {
            var attacker = NewHero();
            attacker.GetResource(StatName.Health).DepleteCurrent();
            var target = NewHero();

            attacker.DealDamageTo(target, DamageType.PhysicalDamage);

            Assert.That(target.GetResource(StatName.Health).IsFull, Is.True);
        }

        // --- modifier comparison ------------------------------------------------------------------

        [Test]
        public void CompareStatModifiers_ASwapForAWorseItem_IsNegative()
        {
            var hero = NewHero();
            var worn = Flat(30f);
            hero.AddItemStats(new[] { On(StatName.Armor, worn) });

            var difference = hero.CompareStatModifiers(StatName.Armor, current: Flat(10f), other: worn);

            Assert.That(difference, Is.EqualTo(-20f), "(20 + 10) - (20 + 30)");
        }

        [Test]
        public void CompareStatModifiers_ASwapForABetterItem_IsPositive()
        {
            var hero = NewHero();
            var worn = Flat(30f);
            hero.AddItemStats(new[] { On(StatName.Armor, worn) });

            var difference = hero.CompareStatModifiers(StatName.Armor, current: Flat(40f), other: worn);

            Assert.That(difference, Is.EqualTo(10f));
        }

        [Test]
        public void CompareStatModifiers_ChangesNothingOnTheHero()
        {
            var hero = NewHero();
            var worn = Flat(30f);
            hero.AddItemStats(new[] { On(StatName.Armor, worn) });

            _ = hero.CompareStatModifiers(On(StatName.Armor, Flat(40f)), worn);

            Assert.That(hero.GetStatValue(StatName.Armor), Is.EqualTo(50f));
            Assert.That(hero.GetStat(StatName.Armor).StatModifiers, Has.Count.EqualTo(1));
        }
    }
}
