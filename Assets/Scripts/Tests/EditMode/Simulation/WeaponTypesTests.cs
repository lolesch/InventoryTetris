using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The Strike Range is a base property of the weapon type (issue #209), not a stat gear can roll: a table
    /// from the wielded weapon's type to arena units, and nothing for what is not a weapon.
    /// </summary>
    [TestFixture]
    public sealed class WeaponTypesTests
    {
        [TestCase(EquipmentType.Sword)]
        [TestCase(EquipmentType.GreatSword)]
        [TestCase(EquipmentType.Bow)]
        [TestCase(EquipmentType.Crossbow)]
        public void EveryWeaponType_HasAStrikeRange(EquipmentType weapon)
        {
            Assert.That(WeaponTypes.StrikeRange(weapon), Is.GreaterThan(0f));
        }

        [Test]
        public void ARangedWeapon_ReachesFartherThanAMeleeOne()
        {
            Assert.That(WeaponTypes.StrikeRange(EquipmentType.Bow), Is.GreaterThan(WeaponTypes.StrikeRange(EquipmentType.GreatSword)));
            Assert.That(WeaponTypes.StrikeRange(EquipmentType.GreatSword), Is.GreaterThan(WeaponTypes.StrikeRange(EquipmentType.Sword)));
        }

        [TestCase(EquipmentType.NONE)]
        [TestCase(EquipmentType.Helm)]
        [TestCase(EquipmentType.Shield)]
        [TestCase(EquipmentType.Ring)]
        [TestCase(EquipmentType.ONEHANDEDWEAPONS)]
        public void WhatIsNotAWeapon_HasNoStrikeRange_SoTheHeroFallsBackToUnarmed(EquipmentType notAWeapon)
        {
            Assert.That(WeaponTypes.StrikeRange(notAWeapon), Is.Null);
        }
    }
}
