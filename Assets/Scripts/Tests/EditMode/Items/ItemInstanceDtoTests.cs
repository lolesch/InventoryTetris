using System;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using static ToolSmiths.InventorySystem.Tests.EditMode.Items.Sample;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// The persistence seam Phase 1 has to get right up front: an <see cref="ItemInstance"/>
    /// flattens to a Unity-free POCO and rebuilds from it unchanged. The save system is not
    /// built yet - this round trip is the whole deliverable (foundational-rework spec,
    /// "Persistence constraints").
    /// </summary>
    [TestFixture]
    public sealed class ItemInstanceDtoTests
    {
        private static ItemInstance Rolled(ItemRarity rarity, int affixCount, int itemLevel)
        {
            var pool = new[]
            {
                Affix(StatName.Health, 10, 200, 120f, StatModifierType.FlatAdd),
                Affix(StatName.PhysicalDamage, 1, 50, 25f, StatModifierType.FlatAdd),
                Affix(StatName.MovementSpeed, -10, 10, 5f, StatModifierType.PercentAdd),
                Affix(StatName.Armor, 0, 500, 333f, StatModifierType.PercentMult),
            };

            return new ItemInstance($"chest.{rarity}".ToLowerInvariant(), rarity, itemLevel, pool.Take(affixCount).ToArray());
        }

        [Test]
        public void RoundTrip_IsEqual_AcrossRaritiesAffixCountsAndItemLevels(
            [Values(ItemRarity.Common, ItemRarity.Magic, ItemRarity.Rare, ItemRarity.Unique)] ItemRarity rarity,
            [Values(0, 1, 3, 4)] int affixCount,
            [Values(1, 42, 100)] int itemLevel)
        {
            var original = Rolled(rarity, affixCount, itemLevel);

            var restored = ItemInstance.FromDto(original.ToDto());

            Assert.That(restored, Is.EqualTo(original));
        }

        [Test]
        public void RoundTrip_KeepsTheDefinitionId()
        {
            var original = new ItemInstance("unique.the-gnasher", ItemRarity.Unique, 60, null);

            var restored = ItemInstance.FromDto(original.ToDto());

            Assert.That(restored.DefinitionId, Is.EqualTo("unique.the-gnasher"));
        }

        [Test]
        public void RoundTrip_PreservesEachAffixStatValueRangeAndType()
        {
            var affix = Affix(StatName.MagicResist, 5, 80, 47f, StatModifierType.PercentAdd);
            var original = new ItemInstance("id", ItemRarity.Rare, 3, new[] { affix });

            var restored = ItemInstance.FromDto(original.ToDto());

            var back = restored.Affixes.Single();
            Assert.That(back.Stat, Is.EqualTo(StatName.MagicResist));
            Assert.That(back.Modifier.Value, Is.EqualTo(47f));
            Assert.That(back.Modifier.Range.x, Is.EqualTo(5), "rangeMin");
            Assert.That(back.Modifier.Range.y, Is.EqualTo(80), "rangeMax");
            Assert.That(back.Modifier.Type, Is.EqualTo(StatModifierType.PercentAdd));
        }

        [Test]
        public void ToDto_WritesEnumsByName_NotNumber()
        {
            var dto = new ItemInstance("id", ItemRarity.Rare, 1,
                new[] { Affix(StatName.Health, 1, 10, 5f, StatModifierType.FlatAdd) }).ToDto();

            Assert.That(dto.rarity, Is.EqualTo("Rare"));
            Assert.That(dto.affixes[0].stat, Is.EqualTo("Health"));
            Assert.That(dto.affixes[0].type, Is.EqualTo("FlatAdd"));
        }

        [Test]
        public void FromDto_Null_Throws()
        {
            Assert.That(() => ItemInstance.FromDto(null), Throws.InstanceOf<ArgumentNullException>());
        }

        [Test]
        public void FromDto_UnknownRarityName_Throws_DoesNotSilentlyDefault()
        {
            var dto = new ItemInstanceDto { definitionId = "id", rarity = "Legendary", itemLevel = 1, affixes = Array.Empty<AffixDto>() };

            Assert.That(() => ItemInstance.FromDto(dto), Throws.ArgumentException);
        }

        [Test]
        public void FromDto_NumericRarityString_Throws()
        {
            var dto = new ItemInstanceDto { definitionId = "id", rarity = "999", itemLevel = 1, affixes = Array.Empty<AffixDto>() };

            Assert.That(() => ItemInstance.FromDto(dto), Throws.ArgumentException);
        }

        [Test]
        public void FromDto_UnknownStatName_Throws()
        {
            var dto = new ItemInstanceDto
            {
                definitionId = "id",
                rarity = "Common",
                itemLevel = 1,
                affixes = new[] { new AffixDto { stat = "Charisma", value = 1f, type = "FlatAdd", rangeMin = 0, rangeMax = 2 } },
            };

            Assert.That(() => ItemInstance.FromDto(dto), Throws.ArgumentException);
        }
    }
}
