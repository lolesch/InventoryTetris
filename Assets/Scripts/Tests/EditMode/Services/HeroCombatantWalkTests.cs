using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Inventories;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// What the sim reads of the hero to walk him (issue #209): his <c>MovementSpeed</c> stat, live, and the
    /// Strike Range of the weapon in his main hand - nothing while he is unarmed.
    /// </summary>
    [TestFixture]
    public sealed class HeroCombatantWalkTests
    {
        private readonly List<UnityEngine.Object> created = new();
        private TestGame game;
        private HeroCombatant combatant;

        [SetUp]
        public void SetUp()
        {
            var config = TestGameConfig.Create(created);
            game = TestGame.Create(config);
            combatant = new HeroCombatant(game.Hero);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in created)
                UnityEngine.Object.DestroyImmediate(asset);

            created.Clear();
        }

        private void Wield(EquipmentType type)
        {
            var definition = game.Items.Catalog.OfCategory(ItemCategory.Equipment).First(d => d.EquipmentType == type);
            // The equip itself, not a pick-up: a pick-up only fills an empty slot, and this swaps a worn weapon out.
            var package = new Package(game.Hero.Equipment, new ItemInstance(definition.Id, ItemRarity.Common, 1, null), 1u);
            Assert.That(game.Hero.Equipment.TryAddToContainer(ref package), Is.True);
        }

        [Test]
        public void MovementSpeed_IsTheHerosStat_AndTheDefaultHeroHasOne()
        {
            Assert.That(combatant.MovementSpeed, Is.EqualTo(game.Hero.GetStatValue(StatName.MovementSpeed)));
            Assert.That(combatant.MovementSpeed, Is.GreaterThan(0f), "a zero-speed hero would never close on a ranged enemy");
        }

        [Test]
        public void AnUnarmedHero_HasNoWeaponStrikeRange()
        {
            Assert.That(combatant.WeaponStrikeRange, Is.Null);
        }

        [TestCase(EquipmentType.Sword)]
        [TestCase(EquipmentType.Bow)]
        public void TheWieldedWeaponsType_SetsTheStrikeRange_ReadLiveFromAnUnchangedCombatant(EquipmentType weapon)
        {
            Assert.That(combatant.WeaponStrikeRange, Is.Null, "premise: unarmed, read before the equip");

            Wield(weapon);

            Assert.That(combatant.WeaponStrikeRange, Is.EqualTo(WeaponTypes.StrikeRange(weapon)));
        }

        [Test]
        public void AnEquippedArmourPiece_IsNotAWeapon()
        {
            Wield(EquipmentType.Helm);

            Assert.That(combatant.WeaponStrikeRange, Is.Null);
        }
    }
}
