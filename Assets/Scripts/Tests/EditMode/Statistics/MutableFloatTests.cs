using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Data.Statistics;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Statistics
{
    /// <summary>
    /// Locks in the modifier math of the stat engine: application order
    /// (FlatAdd -> PercentAdd -> PercentMult), Overwrite precedence, and removal.
    /// </summary>
    [TestFixture]
    public sealed class MutableFloatTests
    {
        private static readonly Vector2Int UnboundedRange = new(int.MinValue, int.MaxValue);

        private static StatModifier Mod(float value, StatModifierType type) =>
            new(UnboundedRange, value, type);

        [Test]
        public void BaseValue_WithNoModifiers_ReturnsBase()
        {
            var stat = new MutableFloat(100f);

            Assert.That((float)stat, Is.EqualTo(100f));
        }

        [Test]
        public void FlatAdd_AddsToBase()
        {
            var stat = new MutableFloat(100f);

            stat.AddModifier(Mod(10f, StatModifierType.FlatAdd));

            Assert.That((float)stat, Is.EqualTo(110f).Within(0.0001f));
        }

        [Test]
        public void Modifiers_ApplyInOrder_FlatThenPercentAddThenPercentMult()
        {
            var stat = new MutableFloat(100f);

            stat.AddModifier(Mod(10f, StatModifierType.FlatAdd));      // 110
            stat.AddModifier(Mod(50f, StatModifierType.PercentAdd));   // 110 * 1.5 = 165
            stat.AddModifier(Mod(100f, StatModifierType.PercentMult)); // 165 * 2 = 330

            Assert.That((float)stat, Is.EqualTo(330f).Within(0.0001f));
        }

        [Test]
        public void Overwrite_TakesPrecedenceOverEverythingElse()
        {
            var stat = new MutableFloat(100f);

            stat.AddModifier(Mod(10f, StatModifierType.FlatAdd));
            stat.AddModifier(Mod(50f, StatModifierType.PercentAdd));
            stat.AddModifier(Mod(42f, StatModifierType.Overwrite));

            Assert.That((float)stat, Is.EqualTo(42f));
        }

        [Test]
        public void TryRemoveModifier_RevertsItsContribution()
        {
            var stat = new MutableFloat(100f);
            var flat = Mod(25f, StatModifierType.FlatAdd);

            stat.AddModifier(flat);
            Assert.That((float)stat, Is.EqualTo(125f).Within(0.0001f));

            Assert.That(stat.TryRemoveModifier(flat), Is.True);
            Assert.That((float)stat, Is.EqualTo(100f).Within(0.0001f));
        }

        [Test]
        public void TryRemoveModifier_NotPresent_ReturnsFalse()
        {
            var stat = new MutableFloat(100f);

            Assert.That(stat.TryRemoveModifier(Mod(5f, StatModifierType.FlatAdd)), Is.False);
        }

        [Test]
        public void OnTotalChanged_FiresWhenTotalChanges()
        {
            var stat = new MutableFloat(100f);
            float? observed = null;
            stat.OnTotalChanged += v => observed = v;

            stat.AddModifier(Mod(10f, StatModifierType.FlatAdd));

            Assert.That(observed, Is.Not.Null);
            Assert.That(observed.Value, Is.EqualTo(110f).Within(0.0001f));
        }

        // ── Clone: an independent "what if" probe (tooltip breakdown groundwork) ──

        [Test]
        public void Clone_CarriesTheSameModifiersAndTotal()
        {
            var stat = new MutableFloat(100f);
            stat.AddModifier(Mod(10f, StatModifierType.FlatAdd));

            var clone = stat.Clone();

            Assert.That((float)clone, Is.EqualTo(110f).Within(0.0001f));
        }

        [Test]
        public void Clone_RemovingAModifierOnTheClone_LeavesTheOriginalUntouched()
        {
            var stat = new MutableFloat(100f);
            var flat = Mod(10f, StatModifierType.FlatAdd);
            stat.AddModifier(flat);

            var clone = stat.Clone();
            Assert.That(clone.TryRemoveModifier(flat), Is.True);

            Assert.That((float)clone, Is.EqualTo(100f).Within(0.0001f)); // clone: "before" this modifier
            Assert.That((float)stat, Is.EqualTo(110f).Within(0.0001f));  // original: unaffected
        }

        [Test]
        public void Clone_DoesNotCarryOverSubscribers()
        {
            var stat = new MutableFloat(100f);
            var fired = false;
            stat.OnTotalChanged += _ => fired = true;

            var clone = stat.Clone();
            clone.AddModifier(Mod(10f, StatModifierType.FlatAdd));

            Assert.That(fired, Is.False);
        }

        // ── TryRemoveModifier(modifier, warnIfMissing): quiet probe variant ──

        [Test]
        public void TryRemoveModifier_QuietOverload_StillRemovesAPresentModifier()
        {
            var stat = new MutableFloat(100f);
            var flat = Mod(10f, StatModifierType.FlatAdd);
            stat.AddModifier(flat);

            Assert.That(stat.TryRemoveModifier(flat, warnIfMissing: false), Is.True);
            Assert.That((float)stat, Is.EqualTo(100f).Within(0.0001f));
        }

        [Test]
        public void TryRemoveModifier_QuietOverload_NotPresent_ReturnsFalseWithoutLogging()
        {
            var stat = new MutableFloat(100f);

            Assert.That(stat.TryRemoveModifier(Mod(5f, StatModifierType.FlatAdd), warnIfMissing: false), Is.False);
        }
    }
}
