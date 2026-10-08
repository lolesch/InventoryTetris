using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// A hero built, changed, snapshotted to the text a save file holds, and loaded into a second,
    /// separately built game. What is compared is what a player can see: the gear, the bag, the
    /// Stash, the coins, the level, the current values, the sliders, the next Location and the Corpse.
    /// </summary>
    [TestFixture]
    public sealed class HeroPersistenceTests
    {
        private const float Tolerance = 0.001f;

        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private LocationConfig thornwood;
        private LocationConfig ashfen;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            thornwood = TestLocations.Create(config, created, "thornwood");
            ashfen = TestLocations.Create(config, created, "ashfen");
            TestLocations.Author(config, thornwood, ashfen);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private static ItemDefinition Equipment(TestGame game, EquipmentType type) =>
            game.Items.Catalog.OfCategory(ItemCategory.Equipment).First(definition => definition.EquipmentType == type);

        private static ItemDefinition Consumable(TestGame game, int index) =>
            game.Items.Catalog.OfCategory(ItemCategory.Consumable).Skip(index).First();

        private static CharacterStatModifier Bonus(StatName stat, float value) =>
            new(stat, new StatModifier(new Vector2Int(0, 1000), value));

        private static ItemInstance Worn(ItemDefinition definition, params CharacterStatModifier[] affixes) =>
            new(definition.Id, ItemRarity.Rare, 5, affixes);

        private static HeroDto ThroughText(HeroDto dto) => JsonUtility.FromJson<HeroDto>(JsonUtility.ToJson(dto));

        private static HeroDto Snapshot(TestGame game) =>
            ThroughText(HeroMapper.ToDto(game.Hero, "hero-1", "Aria", game.Simulation.Locations));

        // A hero that has something of everything a save holds.
        private TestGame PlayedGame()
        {
            var game = TestGame.Create(config);
            var hero = game.Hero;

            Assert.That(hero.PickUpItem(Worn(Equipment(game, EquipmentType.GreatSword), Bonus(StatName.Armor, 40f)), 1u), Is.True);
            Assert.That(hero.PickUpItem(Worn(Equipment(game, EquipmentType.Helm), Bonus(StatName.Health, 50f)), 1u), Is.True);

            var potions = new Package(hero.Inventory, new ItemInstance(Consumable(game, 0).Id, ItemRarity.Common, 1, null), 4u);
            Assert.That(hero.Inventory.TryAddToContainer(ref potions), Is.True);

            var stashed = new Package(hero.Stash, new ItemInstance(Consumable(game, 1).Id, ItemRarity.Common, 1, null), 2u);
            Assert.That(hero.Stash.TryAddToContainer(ref stashed), Is.True);

            hero.Wallet.Deposit(new Currency(1234u));

            // A level-up heals, so the damage comes after it: the hero ends below a maximum the gear raised.
            hero.GainExperience(1500f, hero.Level);
            hero.GetResource(StatName.Health).RemoveFromCurrent(7f);
            hero.GetResource(StatName.Resource).RemoveFromCurrent(11f);

            hero.Behaviour.SimSpeed = 3.5f;
            hero.Behaviour.Engagement = 4;
            hero.Behaviour.RetreatHealthFraction = 0.25f;
            hero.Behaviour.RecallBagFillFraction = 0.75f;
            hero.Behaviour.CastThreshold = 0.6f;
            hero.Behaviour.OriginWeight = 0.8f;
            hero.Behaviour.LootFilterMinimum = ItemRarity.Rare;

            hero.SelectedLocation = thornwood;
            hero.Corpse.Bury(game.Simulation.Locations.ProfileFor(ashfen),
                new[] { new ItemInstance(Equipment(game, EquipmentType.Boots).Id, ItemRarity.Magic, 3, null) });

            return game;
        }

        private static string[] Contents(AbstractDimensionalContainer container) =>
            container.StoredPackages
                .OrderBy(entry => entry.Key.x).ThenBy(entry => entry.Key.y)
                .Select(entry => $"{entry.Key.x},{entry.Key.y}:{entry.Value.Item.DefinitionId}:{entry.Value.Item.Rarity}:{entry.Value.Item.ItemLevel}:x{entry.Value.Amount}")
                .ToArray();

        [Test]
        public void ASnapshottedHero_LoadsIntoAFreshSession_WithEverythingAPlayerCanSee()
        {
            var before = PlayedGame();
            var saved = Snapshot(before);
            var after = TestGame.Create(config);
            var loaded = 0;
            after.Session.HeroLoaded += () => loaded++;

            var ok = after.Session.TryLoad(saved, after.Simulation.Locations, out var report);

            Assert.That(ok, Is.True);
            Assert.That(report.IsClean, Is.True);
            Assert.That(loaded, Is.EqualTo(1));

            var was = before.Hero;
            var now = after.Hero;

            Assert.That(Contents(now.Equipment), Is.EqualTo(Contents(was.Equipment)));
            Assert.That(Contents(now.Inventory), Is.EqualTo(Contents(was.Inventory)));
            Assert.That(Contents(now.Stash), Is.EqualTo(Contents(was.Stash)));
            Assert.That(now.Wallet.Balance.Total, Is.EqualTo(was.Wallet.Balance.Total));
            Assert.That(now.Wallet.Balance.Total, Is.EqualTo(1234u));

            Assert.That(now.Level, Is.EqualTo(was.Level));
            Assert.That(now.Level, Is.GreaterThan(1u));

            foreach (var resource in new[] { StatName.Health, StatName.Resource, StatName.Shield, StatName.Experience })
            {
                Assert.That(now.GetResource(resource).CurrentValue, Is.EqualTo(was.GetResource(resource).CurrentValue).Within(Tolerance), $"{resource} current");
                Assert.That(now.GetResource(resource).TotalValue, Is.EqualTo(was.GetResource(resource).TotalValue).Within(Tolerance), $"{resource} total");
            }

            Assert.That(now.GetStatValue(StatName.Armor), Is.EqualTo(was.GetStatValue(StatName.Armor)).Within(Tolerance));

            Assert.That(now.Behaviour.SimSpeed, Is.EqualTo(3.5f));
            Assert.That(now.Behaviour.Engagement, Is.EqualTo(4));
            Assert.That(now.Behaviour.RetreatHealthFraction, Is.EqualTo(0.25f));
            Assert.That(now.Behaviour.RecallBagFillFraction, Is.EqualTo(0.75f));
            Assert.That(now.Behaviour.CastThreshold, Is.EqualTo(0.6f));
            Assert.That(now.Behaviour.OriginWeight, Is.EqualTo(0.8f));
            Assert.That(now.Behaviour.LootFilterMinimum, Is.EqualTo(ItemRarity.Rare));

            Assert.That(now.SelectedLocation, Is.SameAs(thornwood));

            Assert.That(now.Corpse.Exists, Is.True);
            Assert.That(now.Corpse.Items.Select(item => item.DefinitionId), Is.EqualTo(was.Corpse.Items.Select(item => item.DefinitionId)));
        }

        [Test]
        public void ALoadedHero_IsANewHeroInANewWorld_NotTheOldOnesPatchedUp()
        {
            var before = TestGame.Create(config);
            var oldHero = before.Hero;
            var oldWorld = before.Session.World;

            Assert.That(before.Session.TryLoad(Snapshot(PlayedGame()), before.Simulation.Locations, out _), Is.True);

            Assert.That(before.Hero, Is.Not.SameAs(oldHero));
            Assert.That(before.Session.World, Is.Not.SameAs(oldWorld));
        }

        [Test]
        public void AHeroWithGearRaisedMaximums_KeepsItsCurrentHealthAfterALoad_NotClampedToTheBase()
        {
            var before = PlayedGame();
            var baseMaximum = TestGame.Create(config).Hero.GetResource(StatName.Health).TotalValue;
            var saved = Snapshot(before);
            var after = TestGame.Create(config);

            _ = after.Session.TryLoad(saved, after.Simulation.Locations, out _);

            var health = after.Hero.GetResource(StatName.Health);
            Assert.That(health.TotalValue, Is.GreaterThan(baseMaximum), "the helm raised the maximum");
            Assert.That(health.CurrentValue, Is.GreaterThan(baseMaximum), "the saved current value sits above the unequipped maximum");
            Assert.That(health.CurrentValue, Is.EqualTo(before.Hero.GetResource(StatName.Health).CurrentValue).Within(Tolerance));
        }

        [Test]
        public void TheLevelsModifiers_ComeFromTheSharedFunction_NotAReplayOfTheXpGain()
        {
            var before = PlayedGame();
            var level = before.Hero.Level;
            var saved = Snapshot(before);
            var after = TestGame.Create(config);

            _ = after.Session.TryLoad(saved, after.Simulation.Locations, out _);

            var experience = after.Hero.GetResource(StatName.Experience);
            var expected = Enumerable.Range(2, (int)level - 1).Select(l => LevelProgression.ExperienceThresholdModifier((uint)l)).ToArray();

            Assert.That(experience.StatModifiers.ToArray(), Is.EqualTo(expected));
            Assert.That(experience.CurrentValue, Is.EqualTo(before.Hero.GetResource(StatName.Experience).CurrentValue).Within(Tolerance),
                "the XP is the saved XP, not a bar emptied by a replayed level-up");
        }

        [Test]
        public void ALoadDuringARunInTheField_ReturnsFalseAndChangesNothing()
        {
            var game = TestGame.Create(config);
            game.Simulation.Send(thornwood);
            var hero = game.Hero;
            var world = game.Session.World;
            var loaded = 0;
            game.Session.HeroLoaded += () => loaded++;

            var ok = game.Session.TryLoad(Snapshot(PlayedGame()), game.Simulation.Locations, out var report);

            Assert.That(ok, Is.False);
            Assert.That(report, Is.Null);
            Assert.That(game.Hero, Is.SameAs(hero));
            Assert.That(game.Session.World, Is.SameAs(world));
            Assert.That(loaded, Is.Zero);
            Assert.That(game.Simulation.Run.Phase, Is.EqualTo(RunPhase.InField));
        }

        [Test]
        public void ARestoreThatThrows_LeavesTheCurrentPairInPlace()
        {
            var game = TestGame.Create(config);
            var hero = game.Hero;
            var saved = Snapshot(PlayedGame());

            Assert.Throws<InvalidOperationException>(() => game.Session.TryLoad(saved, new ThrowingLocations(), out _));

            Assert.That(game.Hero, Is.SameAs(hero));
        }

        // A restore that dies part way: the Location lookup is the last thing the restore calls.
        private sealed class ThrowingLocations : ILocationIndex
        {
            public LocationConfig Find(string id) => throw new InvalidOperationException("the lookup failed");
            public bool TryGetProfile(string locationId, out EncounterProfile profile) => throw new InvalidOperationException("the lookup failed");
            public bool TryGetId(EncounterProfile profile, out string locationId) => throw new InvalidOperationException("the lookup failed");
        }

        [Test]
        public void AnItemWhoseDefinitionIsGone_IsReportedByTheLoad_AndTheRestOfTheHeroLoads()
        {
            var saved = Snapshot(PlayedGame());
            var stash = saved.stash.packages.ToList();
            stash.Add(new PackageDto
            {
                x = 7,
                y = 5,
                instance = new ItemInstance("deleted-in-a-patch", ItemRarity.Common, 1, null).ToDto(),
                amount = 1u,
            });
            saved.stash.packages = stash.ToArray();
            var after = TestGame.Create(config);

            var ok = after.Session.TryLoad(saved, after.Simulation.Locations, out var report);

            Assert.That(ok, Is.True);
            Assert.That(report.Skipped.Single().Package.instance.definitionId, Is.EqualTo("deleted-in-a-patch"));
            Assert.That(report.Skipped.Single().Container, Is.EqualTo(SavedContainer.Stash));
            Assert.That(after.Hero.Stash.StoredPackages.Count, Is.EqualTo(stash.Count - 1));
            Assert.That(after.Hero.Level, Is.GreaterThan(1u));
        }

        [Test]
        public void ASelectedLocationThatIsNoLongerAuthored_LeavesNoneSelected()
        {
            var saved = Snapshot(PlayedGame());
            saved.selectedLocationId = "lost-in-a-patch";
            var after = TestGame.Create(config);

            var ok = after.Session.TryLoad(saved, after.Simulation.Locations, out _);

            Assert.That(ok, Is.True);
            Assert.That(after.Hero.SelectedLocation, Is.Null);
        }

        [Test]
        public void ASavedHeroSentFromTheLoadedGame_RecoversItsCorpse()
        {
            var saved = Snapshot(PlayedGame());
            var after = TestGame.Create(config);
            _ = after.Session.TryLoad(saved, after.Simulation.Locations, out _);

            after.Simulation.Send(ashfen);

            Assert.That(after.Hero.Corpse.Exists, Is.False, "the Corpse lay at ashfen");
        }

        [Test]
        public void TheSnapshot_CarriesTheIdAndTheName_ForTheSaveService()
        {
            var saved = Snapshot(TestGame.Create(config));

            Assert.That(saved.id, Is.EqualTo("hero-1"));
            Assert.That(saved.name, Is.EqualTo("Aria"));
        }

        [Test]
        public void AnItemWithAValueThisBuildCannotRead_IsSkippedAsUnreadable_AndTheHeroStillLoads()
        {
            var saved = Snapshot(PlayedGame());
            var after = TestGame.Create(config);
            var instance = new ItemInstance(
                after.Items.Catalog.OfCategory(ItemCategory.Consumable).First().Id, ItemRarity.Common, 1, null).ToDto();
            instance.rarity = "NoSuchRarityAnyMore";
            saved.stash.packages = saved.stash.packages.Append(new PackageDto { x = 7, y = 5, instance = instance, amount = 1u }).ToArray();
            saved.behaviour.lootFilterMinimum = "NoSuchRarityAnyMore";

            var ok = after.Session.TryLoad(saved, after.Simulation.Locations, out var report);

            Assert.That(ok, Is.True);
            Assert.That(report.Skipped.Single().Reason, Is.EqualTo(SkipReason.Unreadable));
            Assert.That(after.Hero.Level, Is.GreaterThan(1u), "the rest of the hero loaded");
        }

        [Test]
        public void ASaveFromBeforeTheOriginWeightSlider_LoadsItAtItsDefault_NotAtZero()
        {
            var saved = JsonUtility.FromJson<BehaviourDto>("{\"engagement\":4}");

            Assert.That(saved.originWeight, Is.EqualTo(HeroBehaviour.DefaultOriginWeight), "a missing field is not a weight of 0");
            Assert.That(JsonUtility.FromJson<BehaviourDto>("{\"originWeight\":0}").originWeight, Is.Zero, "negative control: a written 0 is read");
        }
    }
}
