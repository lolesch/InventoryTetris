using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>Which loot-filter toggle a definition answers to.</summary>
    [TestFixture]
    public sealed class LootKindsTests
    {
        [TestCase(EquipmentType.Amulet, LootKind.Amulet)]
        [TestCase(EquipmentType.Helm, LootKind.Helmet)]
        [TestCase(EquipmentType.Shield, LootKind.Shield)]
        [TestCase(EquipmentType.Sword, LootKind.Weapon1H)]
        [TestCase(EquipmentType.Bow, LootKind.Weapon1H)]
        [TestCase(EquipmentType.Crossbow, LootKind.Weapon2H)]
        [TestCase(EquipmentType.GreatSword, LootKind.Weapon2H)]
        [TestCase(EquipmentType.NONE, LootKind.None)]
        public void AnEquipmentType_MapsToItsToggle(EquipmentType type, LootKind expected) =>
            Assert.That(LootKinds.Of(new FakeItemDefinition { EquipmentType = type }), Is.EqualTo(expected));

        [TestCase(ConsumableType.Potion, LootKind.Potion)]
        [TestCase(ConsumableType.Book, LootKind.Book)]
        [TestCase(ConsumableType.PortalScroll, LootKind.PortalScroll)]
        public void AConsumableType_MapsToItsToggle(ConsumableType type, LootKind expected) =>
            Assert.That(LootKinds.Of(new FakeItemDefinition { Category = ItemCategory.Consumable, ConsumableType = type }), Is.EqualTo(expected));

        [Test]
        public void ACoin_MapsToTheCurrencyToggle() =>
            Assert.That(LootKinds.Of(new FakeItemDefinition { Category = ItemCategory.Currency, CurrencyType = CurrencyType.Gold }),
                Is.EqualTo(LootKind.Currency));
    }
}
