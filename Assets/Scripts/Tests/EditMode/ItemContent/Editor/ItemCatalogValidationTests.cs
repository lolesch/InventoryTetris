using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using UnityEditor;

namespace ToolSmiths.InventorySystem.Tests.EditMode.ItemContent
{
    /// <summary>
    /// Validates the real authored content produced by the <c>UniquesMigration</c> script
    /// (issue #7): the <see cref="ItemCatalogAsset"/> and every <see cref="ItemDefinitionAsset"/>
    /// it lists. Until the migration has been run this fixture reports <c>Ignore</c> rather
    /// than fail, so a checkout with only the code committed still runs green; once the
    /// assets exist these become hard assertions on the Phase 1 gate.
    /// </summary>
    [TestFixture]
    public sealed class ItemCatalogValidationTests
    {
        private const string CatalogPath =
            "Assets/Scripts/InventorySystem/Data/ItemDefinitions/Item Catalog.asset";

        private ItemCatalogAsset catalog;

        [SetUp]
        public void LoadCatalog()
        {
            catalog = AssetDatabase.LoadAssetAtPath<ItemCatalogAsset>(CatalogPath);
            if (catalog == null)
                Assert.Ignore($"no catalog at '{CatalogPath}' - run Tools > Inventory System > " +
                              "Migrate Uniques + Author Base Definitions");
        }

        [Test]
        public void EveryCategory_HasAtLeastOneDefinition()
        {
            Assert.That(catalog.OfCategory(ItemCategory.Equipment), Is.Not.Empty, "Equipment");
            Assert.That(catalog.OfCategory(ItemCategory.Consumable), Is.Not.Empty, "Consumable");
            Assert.That(catalog.OfCategory(ItemCategory.Currency), Is.Not.Empty, "Currency");
        }

        [Test]
        public void EveryDefinition_HasAUniqueNonEmptyId_ThatResolves()
        {
            var seen = new HashSet<string>();

            foreach (var definition in catalog.Definitions)
            {
                Assert.That(definition, Is.Not.Null, "a catalog slot is empty");
                Assert.That(string.IsNullOrWhiteSpace(definition.Id), Is.False, $"'{definition.name}' has no id");
                Assert.That(seen.Add(definition.Id), Is.True, $"duplicate id '{definition.Id}'");
                Assert.That(catalog.Definition(definition.Id), Is.SameAs(definition));
            }
        }

        [Test]
        public void EveryDefinition_ResolvesThroughItemView()
        {
            foreach (var definition in catalog.Definitions)
            {
                var instance = new ItemInstance(definition.Id, ItemRarity.Common, 1, null);

                var view = ItemView.Resolve(instance, catalog);

                Assert.That(view.Footprint, Is.EqualTo(definition.Footprint), definition.name);
                Assert.That(view.StackLimit, Is.EqualTo(definition.BaseStackLimit), definition.name);
            }
        }

        [Test]
        public void AllOneHundredFiftySixUniques_AreCarriedAsUniqueFlaggedDefinitions()
        {
            var uniques = catalog.Definitions.Count(d => d.IsUnique);

            Assert.That(uniques, Is.EqualTo(156),
                "expected the 156 migrated uniques (157 .asset files under Data/Items/Uniques minus Item Type Data)");
        }

        [Test]
        public void EveryEquipmentAndConsumableType_HasABaseDefinition()
        {
            var equipmentBases = catalog.Definitions
                .Where(d => !d.IsUnique && d.Category == ItemCategory.Equipment)
                .Select(d => d.EquipmentType)
                .ToHashSet();
            var consumableBases = catalog.Definitions
                .Where(d => !d.IsUnique && d.Category == ItemCategory.Consumable)
                .Select(d => d.ConsumableType)
                .ToHashSet();
            var currencyBases = catalog.Definitions
                .Where(d => d.Category == ItemCategory.Currency)
                .Select(d => d.CurrencyType)
                .ToHashSet();

            Assert.That(equipmentBases, Has.Count.EqualTo(17));
            Assert.That(consumableBases, Is.EquivalentTo(new[] { ConsumableType.Arrow, ConsumableType.Book, ConsumableType.Potion }));
            Assert.That(currencyBases, Is.EquivalentTo(new[] { CurrencyType.Copper, CurrencyType.Iron, CurrencyType.Silver, CurrencyType.Gold }));
        }
    }
}
