using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// <see cref="ItemInstance"/> is the rolled item, immutable after construction. These
    /// pin the construction guards, the defensive copy of the affix list, and the value
    /// equality the DTO round-trip leans on.
    /// </summary>
    [TestFixture]
    public sealed class ItemInstanceTests
    {
        private static CharacterStatModifier Affix(
            StatName stat = StatName.Health, int min = 1, int max = 100, float value = 50f,
            StatModifierType type = StatModifierType.FlatAdd) =>
            new(stat, new StatModifier(new Vector2Int(min, max), value, type));

        [Test]
        public void Constructor_CopiesTheAffixList_SoAMutationAfterwardsDoesNotLeakIn()
        {
            var affixes = new List<CharacterStatModifier> { Affix(StatName.Health) };

            var instance = new ItemInstance("chest.rare", ItemRarity.Rare, 10, affixes);
            affixes.Add(Affix(StatName.Armor));
            affixes[0] = Affix(StatName.MovementSpeed);

            Assert.That(instance.Affixes, Has.Count.EqualTo(1));
            Assert.That(instance.Affixes[0].Stat, Is.EqualTo(StatName.Health));
        }

        [Test]
        public void Affixes_IsNotTheListThatWasPassedIn()
        {
            var affixes = new List<CharacterStatModifier> { Affix() };

            var instance = new ItemInstance("id", ItemRarity.Common, 1, affixes);

            Assert.That(ReferenceEquals(instance.Affixes, affixes), Is.False);
        }

        [Test]
        public void Constructor_NullAffixes_IsAnEmptyAffixList()
        {
            var instance = new ItemInstance("id", ItemRarity.Common, 1, null);

            Assert.That(instance.Affixes, Is.Empty);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void Constructor_BlankDefinitionId_Throws(string id)
        {
            Assert.That(() => new ItemInstance(id, ItemRarity.Common, 1, null), Throws.ArgumentException);
        }

        [Test]
        public void Constructor_NegativeItemLevel_Throws()
        {
            Assert.That(() => new ItemInstance("id", ItemRarity.Common, -1, null),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Equals_SameDefinitionRarityLevelAndAffixes_IsTrue()
        {
            var a = new ItemInstance("id", ItemRarity.Magic, 7, new[] { Affix(StatName.Health, value: 40f) });
            var b = new ItemInstance("id", ItemRarity.Magic, 7, new[] { Affix(StatName.Health, value: 40f) });

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equals_DifferentAffixValue_IsFalse()
        {
            var a = new ItemInstance("id", ItemRarity.Magic, 7, new[] { Affix(StatName.Health, value: 40f) });
            var b = new ItemInstance("id", ItemRarity.Magic, 7, new[] { Affix(StatName.Health, value: 41f) });

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void Equals_DifferentRarity_IsFalse()
        {
            var a = new ItemInstance("id", ItemRarity.Magic, 7, null);
            var b = new ItemInstance("id", ItemRarity.Rare, 7, null);

            Assert.That(a, Is.Not.EqualTo(b));
        }

        [Test]
        public void Equals_AffixOrderDiffers_IsFalse()
        {
            var a = new ItemInstance("id", ItemRarity.Rare, 1, new[] { Affix(StatName.Health), Affix(StatName.Armor) });
            var b = new ItemInstance("id", ItemRarity.Rare, 1, new[] { Affix(StatName.Armor), Affix(StatName.Health) });

            Assert.That(a, Is.Not.EqualTo(b));
        }
    }
}
