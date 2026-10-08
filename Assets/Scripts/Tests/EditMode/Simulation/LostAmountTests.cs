using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// A combatant reports the amount it actually lost from a hit (issue #210), after its own mitigation and no
    /// more than it had to lose - only the combatant knows both. This is the number the sim's typed hit events
    /// carry. Each type is checked against the resist that mitigates it and the one that does not.
    /// </summary>
    [TestFixture]
    public sealed class LostAmountTests
    {
        private static StatModifier Set(float value) => new(new Vector2Int(0, 1000), value, StatModifierType.Overwrite);

        private static Enemy Brute(float armor, float magicResist)
        {
            var enemy = new Enemy(EnemyArchetype.Brute, 5);
            enemy.Stat(StatName.Armor).AddModifier(Set(armor));
            enemy.Stat(StatName.MagicResist).AddModifier(Set(magicResist));
            return enemy;
        }

        [Test]
        public void APhysicalHit_ReportsWhatArmorLeftOfIt_AndNotTheMagicResist()
        {
            var enemy = Brute(armor: 50f, magicResist: 0f);

            var lost = enemy.ReceivePhysical(10f);

            Assert.That(lost, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(enemy.MaxHealth - enemy.Health, Is.EqualTo(lost).Within(0.0001f), "it is what left the health");
        }

        [Test]
        public void APhysicalHit_IgnoresMagicResist_NegativeControl()
        {
            var enemy = Brute(armor: 0f, magicResist: 50f);

            Assert.That(enemy.ReceivePhysical(10f), Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void AMagicalHit_ReportsWhatMagicResistLeftOfIt_AndNotTheArmor()
        {
            var enemy = Brute(armor: 0f, magicResist: 25f);

            var lost = enemy.ReceiveMagical(10f);

            Assert.That(lost, Is.EqualTo(7.5f).Within(0.0001f));
            Assert.That(enemy.MaxHealth - enemy.Health, Is.EqualTo(lost).Within(0.0001f));
        }

        [Test]
        public void AMagicalHit_IgnoresArmor_NegativeControl()
        {
            var enemy = Brute(armor: 50f, magicResist: 0f);

            Assert.That(enemy.ReceiveMagical(10f), Is.EqualTo(10f).Within(0.0001f));
        }

        [Test]
        public void AHitPastWhatIsLeft_ReportsOnlyWhatWasLeft_AndAHitOnTheDownReportsNothing()
        {
            var enemy = Brute(armor: 0f, magicResist: 0f);
            var health = enemy.Health;

            var overkill = enemy.ReceivePhysical(health * 10f);
            var afterwards = enemy.ReceiveMagical(5f);

            Assert.That(overkill, Is.EqualTo(health).Within(0.0001f), "no more than it had");
            Assert.That(afterwards, Is.Zero);
        }

        [Test]
        public void ANonPositiveHit_ReportsNothing()
        {
            var enemy = Brute(armor: 0f, magicResist: 0f);

            Assert.That(enemy.ReceivePhysical(0f), Is.Zero);
            Assert.That(enemy.ReceiveMagical(-3f), Is.Zero);
        }

        [Test]
        public void AResistPast100_ReportsNothingLost_NotANegativeHeal()
        {
            var enemy = Brute(armor: 250f, magicResist: 0f);

            Assert.That(enemy.ReceivePhysical(10f), Is.Zero);
        }
    }
}
