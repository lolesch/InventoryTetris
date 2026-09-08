using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The Corpse rules (issue #22; ADR-0009 <i>Death is corpse-recovery, not haul-forfeit</i>).
    /// Death sets the bag's contents aside as a single Location-tagged Corpse; a second Death
    /// before recovery replaces it outright. Re-entering the Corpse's own Location lays it back
    /// out as Drops and clears it; any other Location leaves it untouched.
    /// </summary>
    [TestFixture]
    public sealed class CorpseTests
    {
        private static ItemInstance Item(string id) => new(id, ItemRarity.Common, 1, null);

        private static EncounterProfile Thornwood() => Profiles.Solo(EnemyArchetype.Skirmisher);
        private static EncounterProfile Ashfen() => Profiles.Solo(EnemyArchetype.Brute);

        // ─── a fresh Corpse ─────────────────────────────────────────────────

        [Test]
        public void AFreshCorpse_DoesNotExist()
        {
            var corpse = new Corpse();

            Assert.That(corpse.Exists, Is.False);
            Assert.That(corpse.Location, Is.Null);
            Assert.That(corpse.Items, Is.Empty);
        }

        // ─── Bury ───────────────────────────────────────────────────────────

        [Test]
        public void Bury_SetsAsideTheBagContents_TaggedWithTheLocation()
        {
            var corpse = new Corpse();
            var location = Thornwood();
            var bagContents = new[] { Item("sword"), Item("shield") };

            corpse.Bury(location, bagContents);

            Assert.That(corpse.Exists, Is.True);
            Assert.That(corpse.Location, Is.SameAs(location));
            Assert.That(corpse.Items, Is.EqualTo(bagContents));
        }

        [Test]
        public void Bury_WithAnEmptyBag_StillCreatesACorpse()
        {
            var corpse = new Corpse();
            var location = Thornwood();

            corpse.Bury(location, Array.Empty<ItemInstance>());

            Assert.That(corpse.Exists, Is.True, "a Corpse exists on every Death, even an empty-handed one");
            Assert.That(corpse.Items, Is.Empty);
        }

        [Test]
        public void Bury_WithANullBag_IsTreatedAsEmpty()
        {
            var corpse = new Corpse();

            corpse.Bury(Thornwood(), null);

            Assert.That(corpse.Exists, Is.True);
            Assert.That(corpse.Items, Is.Empty);
        }

        [Test]
        public void Bury_WithNoLocation_Throws()
        {
            var corpse = new Corpse();

            Assert.That(() => corpse.Bury(null, Array.Empty<ItemInstance>()), Throws.ArgumentNullException);
        }

        [Test]
        public void ASecondBury_ReplacesTheCorpse_DiscardingWhatWasThereBefore()
        {
            var corpse = new Corpse();
            corpse.Bury(Thornwood(), new[] { Item("first-death-sword") });

            var secondLocation = Ashfen();
            corpse.Bury(secondLocation, new[] { Item("second-death-shield") });

            Assert.That(corpse.Location, Is.SameAs(secondLocation));
            Assert.That(corpse.Items, Has.Length.EqualTo(1));
            Assert.That(corpse.Items[0].DefinitionId, Is.EqualTo("second-death-shield"));
        }

        [Test]
        public void ASecondBury_AtTheSameLocation_StillReplacesTheCorpse()
        {
            var corpse = new Corpse();
            var location = Thornwood();
            corpse.Bury(location, new[] { Item("first-death-sword") });

            corpse.Bury(location, new[] { Item("second-death-shield") });

            Assert.That(corpse.Items, Has.Length.EqualTo(1));
            Assert.That(corpse.Items[0].DefinitionId, Is.EqualTo("second-death-shield"));
        }

        // ─── TryRecover ─────────────────────────────────────────────────────

        [Test]
        public void TryRecover_AtTheCorpsesOwnLocation_LaysTheContentsOutAsDrops()
        {
            var corpse = new Corpse();
            var location = Thornwood();
            var bagContents = new[] { Item("sword"), Item("shield") };
            corpse.Bury(location, bagContents);

            var recovered = corpse.TryRecover(location, out var drops);

            Assert.That(recovered, Is.True);
            Assert.That(drops, Is.EqualTo(bagContents));
        }

        [Test]
        public void TryRecover_AtTheCorpsesOwnLocation_ClearsTheCorpse()
        {
            var corpse = new Corpse();
            var location = Thornwood();
            corpse.Bury(location, new[] { Item("sword") });

            corpse.TryRecover(location, out _);

            Assert.That(corpse.Exists, Is.False, "laid-out contents are Drops now, not a Corpse anymore");
            Assert.That(corpse.Location, Is.Null);
            Assert.That(corpse.Items, Is.Empty);
        }

        [Test]
        public void TryRecover_AtAnyOtherLocation_ProducesNoDrops()
        {
            var corpse = new Corpse();
            var buried = Thornwood();
            var bagContents = new[] { Item("sword") };
            corpse.Bury(buried, bagContents);

            var recovered = corpse.TryRecover(Ashfen(), out var drops);

            Assert.That(recovered, Is.False);
            Assert.That(drops, Is.Empty);
        }

        [Test]
        public void TryRecover_AtAnyOtherLocation_LeavesTheCorpseUntouched()
        {
            var corpse = new Corpse();
            var buried = Thornwood();
            var bagContents = new[] { Item("sword") };
            corpse.Bury(buried, bagContents);

            corpse.TryRecover(Ashfen(), out _);

            Assert.That(corpse.Exists, Is.True);
            Assert.That(corpse.Location, Is.SameAs(buried));
            Assert.That(corpse.Items, Is.EqualTo(bagContents));
        }

        [Test]
        public void TryRecover_WithNoCorpse_ProducesNoDrops()
        {
            var corpse = new Corpse();

            var recovered = corpse.TryRecover(Thornwood(), out var drops);

            Assert.That(recovered, Is.False);
            Assert.That(drops, Is.Empty);
        }

        [Test]
        public void TryRecover_WithNoLocation_Throws()
        {
            var corpse = new Corpse();

            Assert.That(() => corpse.TryRecover(null, out _), Throws.ArgumentNullException);
        }
    }
}
