using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Services;
using UnityEditor;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// A Location found by its stable id, and the Hero's Corpse saved as that id plus its items and
    /// restored into a second, separately built game. The Corpse matches a Location by profile
    /// reference, so the check that matters is that the restored Corpse is recovered by the normal
    /// Send of the new game's own simulation service.
    /// </summary>
    [TestFixture]
    public sealed class LocationPersistenceTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private GameConfig config;
        private LocationConfig thornwood;
        private LocationConfig ashfen;

        [SetUp]
        public void SetUp()
        {
            config = TestGameConfig.Create(created);
            thornwood = Location("thornwood");
            ashfen = Location("ashfen");
            TestLocations.Author(config, thornwood, ashfen);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private LocationConfig Location(string id) => TestLocations.Create(config, created, id);

        private TestGame NewGame() => TestGame.Create(config);

        private static ItemInstance Gear(TestGame game) =>
            new(game.Items.Catalog.OfCategory(ItemCategory.Equipment).First().Id, ItemRarity.Common, 1, null);

        private static CorpseDto ThroughText(CorpseDto dto) => JsonUtility.FromJson<CorpseDto>(JsonUtility.ToJson(dto));

        // ── the Location lookup ─────────────────────────────────────────────

        [Test]
        public void ALocationId_ResolvesToItsLocation_AndAnUnknownIdToNothing()
        {
            var registry = NewGame().Simulation.Locations;

            Assert.That(registry.Find("thornwood"), Is.SameAs(thornwood));
            Assert.That(registry.Find("ashfen"), Is.SameAs(ashfen));
            Assert.That(registry.Find("lost-in-a-patch"), Is.Null);
            Assert.That(registry.Find(""), Is.Null);
            Assert.That(registry.Find(null), Is.Null);
        }

        [Test]
        public void TheRegistry_HandsOutOneProfilePerLocation_EveryTime()
        {
            var registry = NewGame().Simulation.Locations;

            var first = registry.ProfileFor(thornwood);

            Assert.That(registry.ProfileFor(thornwood), Is.SameAs(first));
            Assert.That(registry.TryGetProfile("thornwood", out var byId), Is.True);
            Assert.That(byId, Is.SameAs(first));
            Assert.That(registry.ProfileFor(ashfen), Is.Not.SameAs(first));
        }

        [Test]
        public void AProfile_NamesTheLocationItWasBuiltFor_AndAStrangerNamesNone()
        {
            var registry = NewGame().Simulation.Locations;
            var other = NewGame().Simulation.Locations.ProfileFor(thornwood);

            Assert.That(registry.TryGetId(registry.ProfileFor(ashfen), out var id), Is.True);
            Assert.That(id, Is.EqualTo("ashfen"));
            Assert.That(registry.TryGetId(other, out _), Is.False, "a profile another registry built is not this one's");
        }

        [Test]
        public void TwoLocationsWithOneId_AreRefusedAtBoot()
        {
            TestLocations.Author(config, thornwood, Location("thornwood"));

            var exception = Assert.Throws<InvalidOperationException>(() => NewGame());

            Assert.That(exception.Message, Does.Contain("thornwood"));
        }

        // ── the Corpse ──────────────────────────────────────────────────────

        [Test]
        public void ACorpseSavedAndRestored_IsRecoveredAtItsLocation_ThroughTheNormalSend()
        {
            var before = NewGame();
            var sword = Gear(before);
            before.Session.Hero.Corpse.Bury(before.Simulation.Locations.ProfileFor(thornwood), new[] { sword });
            var saved = ThroughText(CorpseMapper.ToDto(before.Session.Hero.Corpse, before.Simulation.Locations));
            var after = NewGame();

            var report = CorpseMapper.Restore(saved, after.Session.Hero.Corpse, after.Simulation.Locations, after.Items.Catalog);

            Assert.That(report.IsClean, Is.True);
            Assert.That(after.Session.Hero.Corpse.Exists, Is.True);

            after.Simulation.Send(ashfen);
            Assert.That(after.Session.Hero.Corpse.Exists, Is.True, "another Location leaves it alone");
            _ = after.Simulation.Recall();

            after.Simulation.Send(thornwood);

            Assert.That(after.Session.Hero.Corpse.Exists, Is.False, "re-entering its Location recovers it");

            var carried = after.Session.Hero.Equipment.StoredPackages.Values
                .Concat(after.Session.Hero.Inventory.StoredPackages.Values)
                .Select(package => package.Item.DefinitionId);
            Assert.That(carried, Does.Contain(sword.DefinitionId), "recovered to the bag, or worn where a slot was free");
        }

        [Test]
        public void TheRestoredCorpse_UsesTheProfileTheServiceMemoizes()
        {
            var before = NewGame();
            before.Session.Hero.Corpse.Bury(before.Simulation.Locations.ProfileFor(ashfen), new[] { Gear(before) });
            var saved = ThroughText(CorpseMapper.ToDto(before.Session.Hero.Corpse, before.Simulation.Locations));
            var after = NewGame();

            _ = CorpseMapper.Restore(saved, after.Session.Hero.Corpse, after.Simulation.Locations, after.Items.Catalog);

            Assert.That(after.Session.Hero.Corpse.Location, Is.SameAs(after.Simulation.Locations.ProfileFor(ashfen)));
        }

        [Test]
        public void ACorpseAtAnUnknownLocation_IsReportedAndSkipped_NeverThrown()
        {
            var game = NewGame();
            var saved = new CorpseDto { locationId = "lost-in-a-patch", items = new[] { Gear(game).ToDto(), Gear(game).ToDto() } };

            var report = CorpseMapper.Restore(saved, game.Session.Hero.Corpse, game.Simulation.Locations, game.Items.Catalog);

            Assert.That(game.Session.Hero.Corpse.Exists, Is.False);
            Assert.That(report.Skipped, Has.Count.EqualTo(2));
            Assert.That(report.Skipped.Select(s => s.Reason), Is.All.EqualTo(SkipReason.UnknownLocation));
            Assert.That(report.Skipped.Select(s => s.Container), Is.All.EqualTo(SavedContainer.Corpse));
            Assert.That(report.Skipped[0].Package.instance.definitionId, Is.EqualTo(Gear(game).DefinitionId));
        }

        [Test]
        public void ACorpseItemWhoseDefinitionIsGone_IsSkipped_AndTheRestIsBuried()
        {
            var game = NewGame();
            var gone = new ItemInstance("deleted-in-a-patch", ItemRarity.Common, 1, null).ToDto();
            var saved = new CorpseDto { locationId = "thornwood", items = new[] { gone, Gear(game).ToDto() } };

            var report = CorpseMapper.Restore(saved, game.Session.Hero.Corpse, game.Simulation.Locations, game.Items.Catalog);

            Assert.That(game.Session.Hero.Corpse.Exists, Is.True);
            Assert.That(game.Session.Hero.Corpse.Items.Count, Is.EqualTo(1));
            Assert.That(report.Skipped.Single().Reason, Is.EqualTo(SkipReason.UnknownDefinition));
            Assert.That(report.Skipped.Single().Package.instance.definitionId, Is.EqualTo("deleted-in-a-patch"));
        }

        [Test]
        public void AHeroWithNoCorpse_SavesNone_AndRestoresNone()
        {
            var before = NewGame();
            var saved = ThroughText(CorpseMapper.ToDto(before.Session.Hero.Corpse, before.Simulation.Locations));
            var after = NewGame();

            var report = CorpseMapper.Restore(saved, after.Session.Hero.Corpse, after.Simulation.Locations, after.Items.Catalog);

            Assert.That(saved.locationId, Is.Empty);
            Assert.That(report.IsClean, Is.True);
            Assert.That(after.Session.Hero.Corpse.Exists, Is.False);
        }

        [Test]
        public void ACorpseWithNoItems_StillComesBackAsACorpse()
        {
            var before = NewGame();
            before.Session.Hero.Corpse.Bury(before.Simulation.Locations.ProfileFor(thornwood), Array.Empty<ItemInstance>());
            var saved = ThroughText(CorpseMapper.ToDto(before.Session.Hero.Corpse, before.Simulation.Locations));
            var after = NewGame();

            _ = CorpseMapper.Restore(saved, after.Session.Hero.Corpse, after.Simulation.Locations, after.Items.Catalog);

            Assert.That(after.Session.Hero.Corpse.Exists, Is.True);
            Assert.That(after.Session.Hero.Corpse.Items, Is.Empty);
        }
    }
}
