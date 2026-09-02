using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;
using static ToolSmiths.InventorySystem.Tests.EditMode.Items.Sample;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// <see cref="ItemView"/> is the one place a display's runtime reads - footprint, stack
    /// limit, name, colour - are resolved from an instance plus its catalog. It also owns
    /// the rarity-colour and footprint switches that used to be scattered <c>static</c> on
    /// <c>AbstractItem</c>.
    /// </summary>
    [TestFixture]
    public sealed class ItemViewTests
    {
        private static ItemInstance Instance(string definitionId, ItemRarity rarity = ItemRarity.Common) =>
            new(definitionId, rarity, 1, null);

        [Test]
        public void Resolve_ReadsFootprintStackLimitAndColour_FromTheDefinitionAndInstance()
        {
            var definition = new FakeItemDefinition
            {
                Id = "chest",
                Footprint = ItemSize.TwoByThree,
                BaseStackLimit = 1u,
            };
            var view = ItemView.Resolve(Instance("chest", ItemRarity.Rare), new InMemoryItemCatalog(definition));

            Assert.That(view.Footprint, Is.EqualTo(ItemSize.TwoByThree));
            Assert.That(view.Dimensions, Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(view.StackLimit, Is.EqualTo(1u));
            Assert.That(view.RarityColor, Is.EqualTo(Color.yellow)); // Rare
        }

        [Test]
        public void StackLimit_TracksTheDefinition_NotAFixedOne()
        {
            var definition = new FakeItemDefinition { Id = "arrows", Category = ItemCategory.Consumable, BaseStackLimit = 99u };

            var view = ItemView.Resolve(Instance("arrows"), new InMemoryItemCatalog(definition));

            Assert.That(view.StackLimit, Is.EqualTo(99u));
        }

        [TestCase(ItemSize.OneByOne, 1, 1)]
        [TestCase(ItemSize.OneByFour, 1, 4)]
        [TestCase(ItemSize.TwoByThree, 2, 3)]
        [TestCase(ItemSize.NONE, 0, 0)]
        public void DimensionsOf_MapsEachFootprintToItsCellCount(ItemSize size, int width, int height)
        {
            Assert.That(ItemView.DimensionsOf(size), Is.EqualTo(new Vector2Int(width, height)));
        }

        [TestCase(ItemRarity.Common, 1f, 1f, 1f)]
        [TestCase(ItemRarity.Unique, 1f, 0.35f, 0f)]
        public void RarityColorOf_KeepsTheColoursAbstractItemUsed(ItemRarity rarity, float r, float g, float b)
        {
            var colour = ItemView.RarityColorOf(rarity);

            Assert.That(colour.r, Is.EqualTo(r));
            Assert.That(colour.g, Is.EqualTo(g));
            Assert.That(colour.b, Is.EqualTo(b));
        }

        [Test]
        public void DisplayName_ForEquipment_IsRarityThenEquipmentType()
        {
            var definition = new FakeItemDefinition
            {
                Id = "helm",
                Category = ItemCategory.Equipment,
                EquipmentType = EquipmentType.Helm,
            };

            var view = ItemView.Resolve(Instance("helm", ItemRarity.Magic), new InMemoryItemCatalog(definition));

            Assert.That(view.DisplayName, Is.EqualTo("Magic Helm"));
        }

        [Test]
        public void Resolve_DefinitionIdNotInCatalog_ThrowsInsteadOfResolvingBlank()
        {
            var emptyCatalog = new InMemoryItemCatalog();

            Assert.That(() => ItemView.Resolve(Instance("ghost"), emptyCatalog),
                Throws.InstanceOf<KeyNotFoundException>());
        }

        [Test]
        public void Resolve_NullArguments_Throw()
        {
            Assert.That(() => ItemView.Resolve(null, new InMemoryItemCatalog()), Throws.ArgumentNullException);
            Assert.That(() => ItemView.Resolve(Instance("x"), null), Throws.ArgumentNullException);
        }

        // ── the ambient Catalog / Of(...) - the terse form the runtime call sites use ──

        [TearDown]
        public void ClearAmbientCatalog() => ItemView.Catalog = null;

        [Test]
        public void Of_ResolvesAgainstTheAmbientCatalog()
        {
            ItemView.Catalog = new InMemoryItemCatalog(new FakeItemDefinition { Id = "belt", Footprint = ItemSize.TwoByOne });

            Assert.That(ItemView.Of(Instance("belt")).Footprint, Is.EqualTo(ItemSize.TwoByOne));
        }

        [Test]
        public void Of_WithNoCatalogSet_ThrowsInsteadOfNullReferencing()
        {
            ItemView.Catalog = null;

            Assert.That(() => ItemView.Of(Instance("belt")), Throws.InvalidOperationException);
        }

        // ── SellValue - the AbstractItem/CurrencyItem value switches, moved verbatim ──

        [Test]
        public void SellValue_ForEquipment_SumsAbsValueTimesGoldRatioOverTheAffixes()
        {
            var definition = new FakeItemDefinition { Id = "chest", Category = ItemCategory.Equipment };
            var instance = new ItemInstance("chest", ItemRarity.Rare, 1, new[]
            {
                Affix(StatName.Health, 0, 100, 10f),  // 10 * 2.67
                Affix(StatName.Armor, 0, 100, 5f),    //  5 * 20
            });

            var view = ItemView.Resolve(instance, new InMemoryItemCatalog(definition));

            Assert.That(view.SellValue, Is.EqualTo((10f * 2.67f) + (5f * 20f)).Within(0.001f));
        }

        [TestCase(CurrencyType.Iron, 1f)]
        [TestCase(CurrencyType.Copper, 5f)]    // Currency.ironToCopper
        [TestCase(CurrencyType.Gold, 1200f)]   // Currency.ironToGold
        public void SellValue_ForCurrency_IsTheDenominationValue(CurrencyType currency, float expected)
        {
            var definition = new FakeItemDefinition { Id = "coin", Category = ItemCategory.Currency, CurrencyType = currency };
            var view = ItemView.Resolve(new ItemInstance("coin", ItemRarity.Common, 0, null), new InMemoryItemCatalog(definition));

            Assert.That(view.SellValue, Is.EqualTo(expected));
        }

        [Test]
        public void Icon_ForAFakeDefinitionWithNoAdapter_IsNull()
        {
            var view = ItemView.Resolve(Instance("x"), new InMemoryItemCatalog(new FakeItemDefinition { Id = "x" }));

            Assert.That(view.Icon, Is.Null);
        }
    }
}
