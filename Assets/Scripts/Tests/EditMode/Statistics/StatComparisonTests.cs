using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// The hovered item's per-affix comparison: the hero-total difference of equipping it in place of the
    /// worn gear it displaces (computed by the hero - stubbed here). A free slot, or a stat the displaced
    /// gear lacks, replaces nothing and so shows the full effect.
    /// </summary>
    [TestFixture]
    public sealed class StatComparisonTests
    {
        private static CharacterStatModifier Affix(StatName stat, float value, StatModifierType type = StatModifierType.FlatAdd) =>
            new(stat, new StatModifier(new Vector2Int(0, 1000), value, type));

        private static float Sum(IReadOnlyList<StatModifier> modifiers)
        {
            var sum = 0f;
            foreach (var m in modifiers)
                sum += m.Value;
            return sum;
        }

        /// <summary>A hero with no other modifiers: the swap is just what comes on minus what goes off.</summary>
        private static float Plain(StatName _, IReadOnlyList<StatModifier> incoming, IReadOnlyList<StatModifier> replaced) =>
            Sum(incoming) - Sum(replaced);

        [Test]
        public void ABetterAffix_IsGreenWithAPlusSign()
        {
            var comparison = StatComparison.Of(Affix(StatName.PhysicalDamage, 7f), new[] { Affix(StatName.PhysicalDamage, 6f) }, Plain);

            Assert.That(comparison.Verdict, Is.EqualTo(ComparisonVerdict.Better));
            Assert.That(comparison.Format(), Is.EqualTo("+ 1"));
        }

        [Test]
        public void AWorseAffix_IsRedWithAMinusSign()
        {
            var comparison = StatComparison.Of(Affix(StatName.PhysicalDamage, 6f), new[] { Affix(StatName.PhysicalDamage, 7f) }, Plain);

            Assert.That(comparison.Verdict, Is.EqualTo(ComparisonVerdict.Worse));
            Assert.That(comparison.Format(), Is.EqualTo("- 1"));
        }

        [Test]
        public void AFreeSlot_ShowsTheFullEffectOfTheAffix()
        {
            IReadOnlyList<StatModifier> replaced = null;

            var comparison = StatComparison.Of(Affix(StatName.PhysicalDamage, 7f), new CharacterStatModifier[0],
                (st, i, r) => { replaced = r; return Plain(st, i, r); });

            Assert.That(replaced, Is.Empty, "nothing is taken off");
            Assert.That(comparison.Delta, Is.EqualTo(7f));
            Assert.That(comparison.Verdict, Is.EqualTo(ComparisonVerdict.Better));
            Assert.That(comparison.Format(), Is.EqualTo("+ 7"));
        }

        [Test]
        public void AStatTheDisplacedGearLacks_ShowsTheFullEffectToo()
        {
            IReadOnlyList<StatModifier> replaced = null;

            var comparison = StatComparison.Of(Affix(StatName.AttackSpeed, 4f), new[] { Affix(StatName.PhysicalDamage, 7f) },
                (st, i, r) => { replaced = r; return Plain(st, i, r); });

            Assert.That(replaced, Is.Empty, "the displaced item's other stats are not this row's business");
            Assert.That(comparison.Delta, Is.EqualTo(4f));
        }

        [Test]
        public void AWornRowTheHoveredItemLacks_CarriesTheCostOfTheUnequipAlone()
        {
            IReadOnlyList<StatModifier> incoming = null, replaced = null;

            var comparison = StatComparison.OfLoss(Affix(StatName.Armor, 9f), new[] { Affix(StatName.PhysicalDamage, 7f) },
                (st, i, r) => { incoming = i; replaced = r; return Plain(st, i, r); });

            Assert.That(incoming, Is.Empty, "the hovered item brings nothing of this stat");
            Assert.That(Sum(replaced), Is.EqualTo(9f), "the worn row's own modifier comes off");
            Assert.That(comparison.Value.Delta, Is.EqualTo(-9f));
            Assert.That(comparison.Value.Verdict, Is.EqualTo(ComparisonVerdict.Worse));
            Assert.That(comparison.Value.Format(), Is.EqualTo("- 9"));
        }

        [Test]
        public void AWornRowTheHoveredItemAlsoHas_CarriesNothing_ItsRowIsOnTheHoveredSide()
        {
            var calls = 0;

            var comparison = StatComparison.OfLoss(Affix(StatName.PhysicalDamage, 6f), new[] { Affix(StatName.PhysicalDamage, 7f) },
                (_, _, _) => { calls++; return 1f; });

            Assert.That(comparison, Is.Null);
            Assert.That(calls, Is.Zero);
        }

        [Test]
        public void AnEqualSwap_IsNeutralAndShowsZero()
        {
            var comparison = StatComparison.Of(Affix(StatName.PhysicalDamage, 7f), new[] { Affix(StatName.PhysicalDamage, 7f) }, Plain);

            Assert.That(comparison.Verdict, Is.EqualTo(ComparisonVerdict.Neutral));
            Assert.That(comparison.Format(), Is.EqualTo("±0"));
        }

        [Test]
        public void ATwoHander_IsHandedEveryDisplacedModifierOfTheStatAtOnce()
        {
            var displaced = new[] { Affix(StatName.PhysicalDamage, 6f), Affix(StatName.PhysicalDamage, 5f), Affix(StatName.Armor, 9f) };
            IReadOnlyList<StatModifier> received = null;

            var comparison = StatComparison.Of(Affix(StatName.PhysicalDamage, 13f), displaced, (st, i, r) => { received = r; return Plain(st, i, r); });

            Assert.That(received, Has.Count.EqualTo(2), "both damage modifiers and not the armor");
            Assert.That(comparison.Delta, Is.EqualTo(2f), "13 against 6 + 5");
        }

        [Test]
        public void TheHerosOwnMultipliers_ShowThroughInTheDifference()
        {
            var comparison = StatComparison.Of(Affix(StatName.PhysicalDamage, 7f), new[] { Affix(StatName.PhysicalDamage, 6f) }, (_, _, _) => 2f);

            Assert.That(comparison.Format(), Is.EqualTo("+ 2"), "the hero's total moves by what the hero computes");
        }

        [Test]
        public void ADifferenceTooSmallToPrint_NeverTintsARow()
        {
            var comparison = StatComparison.Of(Affix(StatName.PhysicalDamage, 7f), new[] { Affix(StatName.PhysicalDamage, 7f) }, (_, _, _) => 0.0001f);

            Assert.That(comparison.Verdict, Is.EqualTo(ComparisonVerdict.Neutral));
            Assert.That(comparison.Format(), Is.EqualTo("±0"));
        }

        [Test]
        public void AnOverwriteModifier_PrintsItsValueWithoutTheSwappedSign()
        {
            var positive = new StatModifier(new Vector2Int(0, 100), 5f, StatModifierType.Overwrite);
            var negative = new StatModifier(new Vector2Int(-100, 100), -5f, StatModifierType.Overwrite);

            Assert.That(positive.ToString(), Is.EqualTo("=5"));
            Assert.That(negative.ToString(), Is.EqualTo("=- 5"));
        }
    }
}
