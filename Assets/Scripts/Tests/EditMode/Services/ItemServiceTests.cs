using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The item service built from a config and a fake <see cref="IRollSource"/> (issue #109): no
    /// scene, no character, no <c>UnityEngine.Random</c>. The roll bonuses are arguments, and every
    /// draw - item, rarity, coin denomination, pile size - comes off the injected source.
    /// </summary>
    [TestFixture]
    public sealed class ItemServiceTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;

        [SetUp]
        public void SetUp() => config = TestGameConfig.Create(created);

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private ItemService Service(IRollSource rolls) => new(config, rolls);

        // ── construction ─────────────────────────────────────────────────────

        [Test]
        public void Constructor_WithoutAConfig_Throws() =>
            Assert.That(() => new ItemService(null, new ConstantRolls(0f)), Throws.ArgumentNullException);

        [Test]
        public void Constructor_WithoutARollSource_Throws() =>
            Assert.That(() => new ItemService(config, null), Throws.ArgumentNullException);

        [Test]
        public void Constructor_ConfigWithoutACatalog_Throws_NamingTheField()
        {
            var empty = TestGameConfig.CreateEmpty(created);

            var e = Assert.Throws<InvalidOperationException>(() => new ItemService(empty, new ConstantRolls(0f)));

            Assert.That(e.Message, Does.Contain(nameof(GameConfig.Catalog)));
        }

        // ── coins ────────────────────────────────────────────────────────────

        [Test]
        public void RollPile_DrawsTheDenominationThenTheAmount_FromTheRollSource()
        {
            // Test config: copper and iron, one weight each; copper 4..12, iron 10..30.
            var service = Service(new QueuedRolls(0.25f, 0f, 0.75f, 0.99999f));

            Assert.That(service.RollPile(), Is.EqualTo((CurrencyType.Copper, 4u)));
            Assert.That(service.RollPile(), Is.EqualTo((CurrencyType.Iron, 30u)));
        }

        [Test]
        public void RollPile_OnTheFailBucket_IsNoCoins_AndDrawsNoAmount()
        {
            TestGameConfig.CurrencyFailWeight(config, 1000000f);
            var rolls = new QueuedRolls(0f);
            var service = Service(rolls);

            Assert.That(service.RollPile(), Is.EqualTo((CurrencyType.NONE, 0u)));
            Assert.That(rolls.Consumed, Is.EqualTo(1));
        }

        [Test]
        public void RollCurrency_IsACoinOfTheDrawnDenomination_WithTheDrawnPile()
        {
            var service = Service(new QueuedRolls(0.75f, 0f));

            var package = service.RollCurrency();

            Assert.That(package.Amount, Is.EqualTo(10u));
            Assert.That(service.Catalog.Definition(package.Item.DefinitionId).CurrencyType, Is.EqualTo(CurrencyType.Iron));
            Assert.That(package.Item.Rarity, Is.EqualTo(Currency.RarityOf(CurrencyType.Iron)));
        }

        [Test]
        public void RollCurrency_OnTheFailBucket_IsAnInvalidPackage()
        {
            TestGameConfig.CurrencyFailWeight(config, 1000000f);

            Assert.That(Service(new ConstantRolls(0f)).RollCurrency().IsValid, Is.False);
        }

        [TestCase(CurrencyType.Copper)]
        [TestCase(CurrencyType.Iron)]
        [TestCase(CurrencyType.Silver)]
        [TestCase(CurrencyType.Gold)]
        public void MintCurrency_IsOneCoinOfThatDenomination(CurrencyType type)
        {
            var service = Service(new ConstantRolls(0f));

            var coin = service.MintCurrency(type);

            Assert.That(service.Catalog.Definition(coin.DefinitionId).CurrencyType, Is.EqualTo(type));
            Assert.That(coin.Affixes, Is.Empty);
        }

        // ── loot ─────────────────────────────────────────────────────────────

        [Test]
        public void RollLoot_RollsTheAmountAsked()
        {
            var loot = Service(new ConstantRolls(0.5f)).RollLoot(2u);

            Assert.That(loot, Has.Count.EqualTo(2));
            Assert.That(loot.All(p => p.IsValid), Is.True);
        }

        [Test]
        public void RollLoot_AddsOneDropPerHundredPercentOfTheCallersItemQuantity()
        {
            var service = Service(new ConstantRolls(0.5f));

            Assert.That(service.RollLoot(1u, itemQuantity: 0f), Has.Count.EqualTo(1));
            Assert.That(service.RollLoot(1u, itemQuantity: 99f), Has.Count.EqualTo(1));
            Assert.That(service.RollLoot(1u, itemQuantity: 200f), Has.Count.EqualTo(3));
        }

        [Test]
        public void RollLoot_SameSourceSameSeed_RollsTheSameLoot_WhateverUnityRandomIs()
        {
            UnityEngine.Random.InitState(1);
            var first = Describe(Service(new SeededRolls(7)).RollLoot(25u, magicFind: 40f));

            UnityEngine.Random.InitState(999);
            var second = Describe(Service(new SeededRolls(7)).RollLoot(25u, magicFind: 40f));

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.Not.Empty);
        }

        [Test]
        public void RollLoot_NeedsNoCharacterAndNoScene()
        {
            // Nothing in this fixture arms a locator or builds a hero: the bonuses are arguments.
            Assert.That(Service(new SeededRolls(3)).RollLoot(5u, magicFind: 25f, itemQuantity: 100f), Is.Not.Empty);
        }

        [Test]
        public void RollEquipment_TheCallersMagicFindRaisesTheRarity()
        {
            var plain = Service(new ConstantRolls(0.6f)).RollEquipment(magicFind: 0f);
            var found = Service(new ConstantRolls(0.6f)).RollEquipment(magicFind: 100000f);

            Assert.That(found.Rarity, Is.GreaterThan(plain.Rarity));
        }

        [Test]
        public void RollEquipment_OfAType_IsOfThatType()
        {
            var service = Service(new SeededRolls(11));

            for (var i = 0; i < 10; i++)
                Assert.That(service.Catalog.Definition(service.RollEquipment(EquipmentType.Belt).DefinitionId).EquipmentType,
                    Is.EqualTo(EquipmentType.Belt));
        }

        [Test]
        public void RollConsumable_OfAType_IsOfThatType()
        {
            var service = Service(new SeededRolls(11));

            for (var i = 0; i < 10; i++)
                Assert.That(service.Catalog.Definition(service.RollConsumable(ConsumableType.Potion).DefinitionId).ConsumableType,
                    Is.EqualTo(ConsumableType.Potion));
        }

        // ── catalog and icons ────────────────────────────────────────────────

        [Test]
        public void View_ResolvesAnInstanceAgainstTheConfigsCatalog()
        {
            var service = Service(new ConstantRolls(0.5f));
            var item = service.RollEquipment();

            var view = service.View(item);

            Assert.That(view.Instance, Is.SameAs(item));
            Assert.That(view.Definition, Is.SameAs(config.Catalog.Definition(item.DefinitionId)));
        }

        [Test]
        public void GetIcon_IndexesTheConfigsCurrencyIcons_CopperIronSilverGold()
        {
            var service = Service(new ConstantRolls(0f));

            Assert.That(service.GetIcon(CurrencyType.Copper), Is.SameAs(config.CurrencyIcons[0]));
            Assert.That(service.GetIcon(CurrencyType.Iron), Is.SameAs(config.CurrencyIcons[1]));
            Assert.That(service.GetIcon(CurrencyType.Silver), Is.SameAs(config.CurrencyIcons[2]));
            Assert.That(service.GetIcon(CurrencyType.Gold), Is.SameAs(config.CurrencyIcons[3]));
            Assert.That(service.GetIcon(CurrencyType.NONE), Is.Null);
        }

        [Test]
        public void GetStatIcon_IsTheConfigsStatIcon()
        {
            var service = Service(new ConstantRolls(0f));

            Assert.That(service.GetStatIcon(StatName.Health), Is.SameAs(config.ItemTypeData.GetStatIcon(StatName.Health)));
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static List<string> Describe(IEnumerable<Package> loot) =>
            loot.Select(p => $"{p.Item.DefinitionId}|{p.Item.Rarity}|{p.Item.ItemLevel}|{p.Amount}|" +
                             string.Join(",", p.Item.Affixes.Select(a => $"{a.Stat}:{a.Modifier.Value}"))).ToList();

        private sealed class ConstantRolls : IRollSource
        {
            private readonly float value;
            public ConstantRolls(float value) => this.value = value;
            public float Next() => value;
        }

        private sealed class SeededRolls : IRollSource
        {
            private readonly System.Random rng;
            public SeededRolls(int seed) => rng = new System.Random(seed);
            public float Next() => (float)rng.NextDouble();
        }

        private sealed class QueuedRolls : IRollSource
        {
            private readonly float[] rolls;
            public QueuedRolls(params float[] rolls) => this.rolls = rolls;
            public int Consumed { get; private set; }

            public float Next() => Consumed < rolls.Length
                ? rolls[Consumed++]
                : throw new InvalidOperationException($"the roll script ran dry after {rolls.Length} rolls");
        }
    }
}
