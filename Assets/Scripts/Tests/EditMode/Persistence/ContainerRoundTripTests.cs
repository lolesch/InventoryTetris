using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Tests.EditMode.Items;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Persistence
{
    /// <summary>
    /// A hero's Inventory, Stash and Equipment saved and restored into a freshly built set, through
    /// the Dto text a save file would hold. What is compared is what a player sees: the cells, the
    /// amounts, the gear's stat totals and the Wallet balance.
    /// </summary>
    [TestFixture]
    public sealed class ContainerRoundTripTests
    {
        private static readonly Vector2Int BagSize = new(6, 4);
        private static readonly Vector2Int StashSize = new(8, 6);
        private static readonly Vector2Int GearSize = new(14, 1);

        private InMemoryItemCatalog catalog;

        [SetUp]
        public void SetUp() =>
            catalog = new InMemoryItemCatalog(
                new FakeItemDefinition { Id = "helm", Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Helm },
                new FakeItemDefinition { Id = "sword", Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Sword },
                new FakeItemDefinition { Id = "greatsword", Category = ItemCategory.Equipment, EquipmentType = EquipmentType.GreatSword },
                new FakeItemDefinition { Id = "potion", Category = ItemCategory.Consumable, BaseStackLimit = 10u },
                new FakeItemDefinition { Id = "shield-plate", Category = ItemCategory.Equipment, EquipmentType = EquipmentType.Shield, Footprint = ItemSize.TwoByTwo },
                new FakeItemDefinition { Id = "coin.copper", Category = ItemCategory.Currency, CurrencyType = CurrencyType.Copper, BaseStackLimit = 100u },
                new FakeItemDefinition { Id = "coin.iron", Category = ItemCategory.Currency, CurrencyType = CurrencyType.Iron, BaseStackLimit = 100u });

        private sealed class Gear
        {
            public Hero Hero;
            public CharacterEquipment Equipment;
            public CharacterInventory Inventory;
            public CharacterInventory Stash;
            public Wallet Wallet;
        }

        private Gear NewGear(Vector2Int? bag = null)
        {
            var hero = new Hero(
                new[] { new CharacterStat(StatName.Armor, 0f), new CharacterStat(StatName.PhysicalDamage, 5f) },
                new[]
                {
                    new CharacterResource(StatName.Health, 100f),
                    new CharacterResource(StatName.Resource, 100f),
                    new CharacterResource(StatName.Shield, 0f),
                    new CharacterResource(StatName.Experience, 280f),
                });

            var inventory = new CharacterInventory(bag ?? BagSize, catalog);

            return new Gear
            {
                Hero = hero,
                Equipment = new CharacterEquipment(GearSize, catalog, hero),
                Inventory = inventory,
                Stash = new CharacterInventory(StashSize, catalog),
                Wallet = new Wallet(inventory, new CatalogMinter(catalog)),
            };
        }

        private sealed class CatalogMinter : ICurrencyMinter
        {
            private readonly IItemCatalog catalog;

            public CatalogMinter(IItemCatalog catalog) => this.catalog = catalog;

            public ItemInstance MintCurrency(CurrencyType type) =>
                catalog.OfCategory(ItemCategory.Currency)
                    .Where(definition => definition.CurrencyType == type)
                    .Select(definition => ItemInstance.Coin(definition.Id, type))
                    .FirstOrDefault();
        }

        private static ItemInstance Armored(string id, float armor) =>
            new(id, ItemRarity.Rare, 10, new[]
            {
                new CharacterStatModifier(StatName.Armor, new StatModifier(new Vector2Int(0, 100), armor)),
            });

        private static ItemInstance Plain(string id) => new(id, ItemRarity.Common, 1, null);

        private static void Put(AbstractDimensionalContainer container, Vector2Int cell, ItemInstance item, uint amount = 1u)
        {
            var package = new Package(container, item, amount);
            Assert.That(container.TryAddAtPosition(cell, ref package), Is.True, $"setup: {item.DefinitionId} at {cell}");
        }

        // The save file holds text, so the round trip goes through it.
        private static ContainerDto ThroughText(AbstractDimensionalContainer container) =>
            JsonUtility.FromJson<ContainerDto>(JsonUtility.ToJson(ContainerMapper.ToDto(container)));

        private static RestoreReport Restore(Gear saved, Gear into)
        {
            var restore = new ContainerRestore(into.Inventory);
            restore.Place(SavedContainer.Equipment, ThroughText(saved.Equipment), into.Equipment);
            restore.Place(SavedContainer.Inventory, ThroughText(saved.Inventory), into.Inventory);
            restore.Place(SavedContainer.Stash, ThroughText(saved.Stash), into.Stash);
            return restore.Settle();
        }

        private static PackageDto Entry(int x, int y, string definitionId, uint amount = 1u) => new()
        {
            x = x,
            y = y,
            instance = Plain(definitionId).ToDto(),
            amount = amount,
        };

        private static RestoreReport RestoreInventory(Gear into, params PackageDto[] entries)
        {
            var restore = new ContainerRestore(into.Inventory);
            restore.Place(SavedContainer.Inventory, new ContainerDto { packages = entries }, into.Inventory);
            return restore.Settle();
        }

        private static Vector2Int[] Cells(AbstractDimensionalContainer container) =>
            container.StoredPackages.Keys.OrderBy(cell => cell.x).ThenBy(cell => cell.y).ToArray();

        [Test]
        public void ABagAStashAndWornGear_SurviveARoundTrip_WithTheSameCellsAndAmounts()
        {
            var saved = NewGear();
            Put(saved.Equipment, new Vector2Int(7, 0), Armored("helm", 12f));
            Put(saved.Inventory, new Vector2Int(2, 1), Plain("potion"), 7u);
            Put(saved.Inventory, new Vector2Int(5, 3), Plain("sword"));
            Put(saved.Stash, new Vector2Int(3, 0), Plain("shield-plate"));
            Put(saved.Stash, new Vector2Int(6, 5), Plain("potion"), 10u);
            var restored = NewGear();

            var report = Restore(saved, restored);

            Assert.That(report.IsClean, Is.True);
            Assert.That(Cells(restored.Equipment), Is.EqualTo(Cells(saved.Equipment)));
            Assert.That(Cells(restored.Inventory), Is.EqualTo(Cells(saved.Inventory)));
            Assert.That(Cells(restored.Stash), Is.EqualTo(Cells(saved.Stash)));
            Assert.That(restored.Inventory.StoredPackages[new Vector2Int(2, 1)].Amount, Is.EqualTo(7u));
            Assert.That(restored.Inventory.StoredPackages[new Vector2Int(2, 1)].Item.DefinitionId, Is.EqualTo("potion"));
            Assert.That(restored.Stash.StoredPackages[new Vector2Int(6, 5)].Amount, Is.EqualTo(10u));
        }

        [Test]
        public void ARestoredItem_KeepsItsRollAndRarity()
        {
            var saved = NewGear();
            Put(saved.Inventory, new Vector2Int(0, 0), Armored("sword", 33f));
            var restored = NewGear();

            _ = Restore(saved, restored);

            var item = restored.Inventory.StoredPackages[Vector2Int.zero].Item;
            Assert.That(item.Rarity, Is.EqualTo(ItemRarity.Rare));
            Assert.That(item.ItemLevel, Is.EqualTo(10));
            Assert.That(item.Affixes.Single().Modifier.Value, Is.EqualTo(33f));
        }

        [Test]
        public void ARestoredTwoHander_OccupiesBothSlots_AndTheHeroStatsEqualThePreSaveTotals()
        {
            var saved = NewGear();
            Put(saved.Equipment, new Vector2Int(12, 0), Armored("greatsword", 40f));
            Put(saved.Equipment, new Vector2Int(7, 0), Armored("helm", 12f));
            var restored = NewGear();

            _ = Restore(saved, restored);

            Assert.That(restored.Equipment.StoredPackages.ContainsKey(new Vector2Int(12, 0)), Is.True);
            Assert.That(restored.Equipment.IsEmptySpace(new Vector2Int(13, 0), Vector2Int.one, out _), Is.False,
                "the two-hander fills the off-hand slot too");
            Assert.That(restored.Hero.GetStat(StatName.Armor).TotalValue, Is.EqualTo(52f));
            Assert.That(restored.Hero.GetStat(StatName.Armor).TotalValue, Is.EqualTo(saved.Hero.GetStat(StatName.Armor).TotalValue));
        }

        [Test]
        public void StackedCoins_RestoreAsOnePile_AndTheWalletBalanceIsUnchanged()
        {
            var saved = NewGear();
            Put(saved.Inventory, new Vector2Int(0, 0), ItemInstance.Coin("coin.copper", CurrencyType.Copper), 85u);
            Put(saved.Inventory, new Vector2Int(1, 0), ItemInstance.Coin("coin.iron", CurrencyType.Iron), 12u);
            var restored = NewGear();

            _ = Restore(saved, restored);

            Assert.That(restored.Inventory.StoredPackages[Vector2Int.zero].Amount, Is.EqualTo(85u), "one pile, not 85 coins");
            Assert.That(restored.Wallet.Balance.Total, Is.EqualTo(saved.Wallet.Balance.Total));
            Assert.That(restored.Wallet.Balance.Total, Is.Positive);
        }

        [Test]
        public void ACoinSavedBeforeTheLadder_ComesBackOnTheCurrentRung()
        {
            var restored = NewGear();
            var oldCoin = ItemInstance.Coin("coin.copper", CurrencyType.Copper).ToDto();
            oldCoin.rarity = nameof(ItemRarity.Common);

            _ = RestoreInventory(restored, new PackageDto { x = 0, y = 0, instance = oldCoin, amount = 30u });

            Assert.That(restored.Inventory.StoredPackages[Vector2Int.zero].Item.Rarity,
                Is.EqualTo(Currency.RarityOf(CurrencyType.Copper)));
        }

        [Test]
        public void AnUnknownDefinitionId_IsSkippedAndReported_AndTheRestOfTheContainerLoads()
        {
            var restored = NewGear();

            var report = RestoreInventory(restored,
                Entry(0, 0, "potion", 3u),
                Entry(1, 0, "deleted-in-a-patch"),
                Entry(2, 0, "sword"));

            Assert.That(Cells(restored.Inventory), Is.EqualTo(new[] { new Vector2Int(0, 0), new Vector2Int(2, 0) }));
            var skipped = report.Skipped.Single();
            Assert.That(skipped.Reason, Is.EqualTo(SkipReason.UnknownDefinition));
            Assert.That(skipped.Container, Is.EqualTo(SavedContainer.Inventory));
            Assert.That(skipped.Package.instance.definitionId, Is.EqualTo("deleted-in-a-patch"));
        }

        [Test]
        public void APackageThatNoLongerFitsItsCell_GoesToTheFirstFreeInventoryCell()
        {
            var restored = NewGear(bag: new Vector2Int(2, 2));

            var report = RestoreInventory(restored,
                Entry(0, 0, "sword"),
                Entry(5, 5, "helm"));

            Assert.That(report.IsClean, Is.True);
            Assert.That(restored.Inventory.StoredPackages[new Vector2Int(0, 1)].Item.DefinitionId, Is.EqualTo("helm"));
        }

        [Test]
        public void ADisplacedPackage_NeverTakesACellASavedPackageClaims()
        {
            var restored = NewGear(bag: new Vector2Int(2, 2));

            // The off-grid helm comes first in the file; (0,0) must still be the sword's.
            var report = RestoreInventory(restored,
                Entry(9, 9, "helm"),
                Entry(0, 0, "sword"));

            Assert.That(report.IsClean, Is.True);
            Assert.That(restored.Inventory.StoredPackages[new Vector2Int(0, 0)].Item.DefinitionId, Is.EqualTo("sword"));
            Assert.That(restored.Inventory.StoredPackages[new Vector2Int(0, 1)].Item.DefinitionId, Is.EqualTo("helm"));
        }

        [Test]
        public void APackageWithNoFreeInventoryCell_IsReportedWithWhatWasLost()
        {
            var restored = NewGear(bag: new Vector2Int(1, 1));

            var report = RestoreInventory(restored,
                Entry(0, 0, "sword"),
                Entry(4, 4, "potion", 6u));

            var skipped = report.Skipped.Single();
            Assert.That(skipped.Reason, Is.EqualTo(SkipReason.DoesNotFit));
            Assert.That(skipped.Package.instance.definitionId, Is.EqualTo("potion"));
            Assert.That(skipped.Package.amount, Is.EqualTo(6u));
            Assert.That(restored.Inventory.StoredPackages.Count, Is.EqualTo(1));
        }

        [Test]
        public void AStashPackageThatNoLongerFits_GoesToTheInventory()
        {
            var restored = NewGear();
            var restore = new ContainerRestore(restored.Inventory);

            restore.Place(SavedContainer.Stash, new ContainerDto { packages = new[] { Entry(30, 30, "helm") } }, restored.Stash);
            var report = restore.Settle();

            Assert.That(report.IsClean, Is.True);
            Assert.That(restored.Stash.StoredPackages, Is.Empty);
            Assert.That(restored.Inventory.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo("helm"));
        }

        [Test]
        public void GearSavedInASlotItsTypeNoLongerUses_GoesToTheInventory_AndAppliesNoStats()
        {
            var restored = NewGear();
            var restore = new ContainerRestore(restored.Inventory);
            var chestSlot = new PackageDto { x = 4, y = 0, instance = Armored("helm", 12f).ToDto(), amount = 1u };

            restore.Place(SavedContainer.Equipment, new ContainerDto { packages = new[] { chestSlot } }, restored.Equipment);
            var report = restore.Settle();

            Assert.That(report.IsClean, Is.True);
            Assert.That(restored.Equipment.StoredPackages, Is.Empty);
            Assert.That(restored.Inventory.StoredPackages.Values.Single().Item.DefinitionId, Is.EqualTo("helm"));
            Assert.That(restored.Hero.GetStat(StatName.Armor).TotalValue, Is.Zero);
        }

        [Test]
        public void ASectionTheSaveDoesNotHave_RestoresNothing()
        {
            var restored = NewGear();
            var restore = new ContainerRestore(restored.Inventory);

            restore.Place(SavedContainer.Stash, null, restored.Stash);
            restore.Place(SavedContainer.Stash, new ContainerDto { packages = null }, restored.Stash);

            Assert.That(restore.Settle().IsClean, Is.True);
            Assert.That(restored.Stash.StoredPackages, Is.Empty);
        }

        [Test]
        public void ARestoreIntoAFreshHero_SavesBackToTheSameText()
        {
            var saved = NewGear();
            Put(saved.Equipment, new Vector2Int(12, 0), Armored("greatsword", 40f));
            Put(saved.Inventory, new Vector2Int(2, 1), Plain("potion"), 7u);
            Put(saved.Stash, new Vector2Int(3, 0), Plain("shield-plate"));
            var restored = NewGear();
            _ = Restore(saved, restored);

            Assert.That(JsonUtility.ToJson(ContainerMapper.ToDto(restored.Equipment)), Is.EqualTo(JsonUtility.ToJson(ContainerMapper.ToDto(saved.Equipment))));
            Assert.That(JsonUtility.ToJson(ContainerMapper.ToDto(restored.Inventory)), Is.EqualTo(JsonUtility.ToJson(ContainerMapper.ToDto(saved.Inventory))));
            Assert.That(JsonUtility.ToJson(ContainerMapper.ToDto(restored.Stash)), Is.EqualTo(JsonUtility.ToJson(ContainerMapper.ToDto(saved.Stash))));
        }
    }
}
