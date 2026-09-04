using System;
using System.Collections.Generic;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.ItemContent
{
    /// <summary>
    /// <see cref="ItemDefinitionAsset"/> and <see cref="ItemCatalogAsset"/> are the two
    /// Unity-facing adapters over the item contract (issue #7). These fixtures sit in a
    /// plain <c>Editor/</c> folder - compiled into <c>Assembly-CSharp-Editor</c> - because
    /// exercising them needs <see cref="ScriptableObject.CreateInstance{T}"/>, which the
    /// pure <c>InventorySystem.Items.Tests</c> assembly deliberately keeps out (spec: the
    /// roll-path tests use fakes, "no ScriptableObject").
    /// </summary>
    [TestFixture]
    public sealed class ItemDefinitionAssetTests
    {
        private static ItemDefinitionAsset Definition(
            string id = "x",
            ItemCategory category = ItemCategory.Equipment,
            ItemDefinitionAsset.AuthoredAffixSlot[] pool = null,
            CharacterStatModifier[] uniqueAffixes = null,
            bool isUnique = false,
            int requirementLevel = 0)
        {
            var asset = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            asset.Author(
                id, category, ItemSize.OneByOne, 1u,
                pool, Array.Empty<CharacterStatModifier>(), requirementLevel,
                isUnique, uniqueAffixes,
                EquipmentType.NONE, ConsumableType.NONE, CurrencyType.NONE, null);
            return asset;
        }

        [Test]
        public void AffixPool_ConvertsEachAuthoredSlotToItsRollPathForm()
        {
            var authored = new ItemDefinitionAsset.AuthoredAffixSlot(
                StatName.Armor, new Vector2Int(10, 15), StatModifierType.FlatAdd, 2.5f);

            var pool = Definition(pool: new[] { authored }).AffixPool;

            Assert.That(pool, Has.Count.EqualTo(1));
            Assert.That(pool[0].Stat, Is.EqualTo(StatName.Armor));
            Assert.That(pool[0].RangeMin, Is.EqualTo(10));
            Assert.That(pool[0].RangeMax, Is.EqualTo(15));
            Assert.That(pool[0].ModifierType, Is.EqualTo(StatModifierType.FlatAdd));
            Assert.That(pool[0].Weight, Is.EqualTo(2.5f));
        }

        [Test]
        public void NullAuthoredArrays_ResolveToEmpty_NeverNull()
        {
            var definition = Definition(pool: null, uniqueAffixes: null);

            Assert.That(definition.AffixPool, Is.Empty);
            Assert.That(definition.ImplicitStats, Is.Empty);
            Assert.That(definition.UniqueAffixes, Is.Empty);
        }

        [Test]
        public void Requirement_CarriesTheAuthoredLevel()
        {
            Assert.That(Definition(requirementLevel: 12).Requirement.Level, Is.EqualTo(12));
            Assert.That(Definition(requirementLevel: 0).Requirement.IsNone, Is.True);
        }

        [Test]
        public void UniqueAffixes_ArePassedThroughUnchanged()
        {
            var fixedAffix = new CharacterStatModifier(
                StatName.IncreasedItemRarity,
                new StatModifier(new Vector2Int(0, 75), 40f, StatModifierType.PercentAdd));

            var definition = Definition(isUnique: true, uniqueAffixes: new[] { fixedAffix });

            Assert.That(definition.IsUnique, Is.True);
            Assert.That(definition.UniqueAffixes, Has.Count.EqualTo(1));
            Assert.That(definition.UniqueAffixes[0].Stat, Is.EqualTo(StatName.IncreasedItemRarity));
            Assert.That(definition.UniqueAffixes[0].Modifier.Value, Is.EqualTo(40f));
        }

        [Test]
        public void ItemView_ResolvesAnInstanceAgainstAnAssetBackedCatalog()
        {
            var chest = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            chest.Author(
                "unique.chest-1", ItemCategory.Equipment, ItemSize.TwoByThree, 1u,
                null, Array.Empty<CharacterStatModifier>(), 0,
                true, Array.Empty<CharacterStatModifier>(),
                EquipmentType.Chest, ConsumableType.NONE, CurrencyType.NONE, null);

            var catalog = ScriptableObject.CreateInstance<ItemCatalogAsset>();
            catalog.SetDefinitions(new[] { chest });

            var view = ItemView.Resolve(
                new ItemInstance("unique.chest-1", ItemRarity.Unique, 5, null), catalog);

            Assert.That(view.Footprint, Is.EqualTo(ItemSize.TwoByThree));
            Assert.That(view.Dimensions, Is.EqualTo(new Vector2Int(2, 3)));
            Assert.That(view.DisplayName, Is.EqualTo("Unique Chest"));
        }
    }

    [TestFixture]
    public sealed class ItemCatalogAssetTests
    {
        private static ItemDefinitionAsset Definition(string id, ItemCategory category)
        {
            var asset = ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            asset.Author(
                id, category, ItemSize.OneByOne, 1u,
                null, Array.Empty<CharacterStatModifier>(), 0,
                false, Array.Empty<CharacterStatModifier>(),
                EquipmentType.NONE, ConsumableType.NONE, CurrencyType.NONE, null);
            return asset;
        }

        private static ItemCatalogAsset Catalog(params ItemDefinitionAsset[] definitions)
        {
            var catalog = ScriptableObject.CreateInstance<ItemCatalogAsset>();
            catalog.SetDefinitions(definitions);
            return catalog;
        }

        [Test]
        public void Definition_ReturnsTheAssetWithThatId()
        {
            var ring = Definition("base.equipment.ring", ItemCategory.Equipment);

            var catalog = Catalog(Definition("base.equipment.amulet", ItemCategory.Equipment), ring);

            Assert.That(catalog.Definition("base.equipment.ring"), Is.SameAs(ring));
        }

        [Test]
        public void Definition_UnknownId_ThrowsKeyNotFound_NotSilentNull()
        {
            var catalog = Catalog(Definition("base.currency.gold", ItemCategory.Currency));

            Assert.That(() => catalog.Definition("nope"), Throws.InstanceOf<KeyNotFoundException>());
        }

        [Test]
        public void OfCategory_ReturnsEveryDefinitionInThatCategory_AndNoOther()
        {
            var catalog = Catalog(
                Definition("base.equipment.chest", ItemCategory.Equipment),
                Definition("base.consumable.potion", ItemCategory.Consumable),
                Definition("base.currency.iron", ItemCategory.Currency),
                Definition("base.currency.gold", ItemCategory.Currency));

            var currency = new List<ItemDefinition>(catalog.OfCategory(ItemCategory.Currency));

            Assert.That(currency, Has.Count.EqualTo(2));
            Assert.That(currency.TrueForAll(d => d.Category == ItemCategory.Currency));
        }
    }
}
