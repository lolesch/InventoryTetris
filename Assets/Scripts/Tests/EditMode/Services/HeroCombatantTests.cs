using NUnit.Framework;
using ToolSmiths.InventorySystem.Data;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Runtime.Character;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>
    /// The hero's side of the Encounter sim reports what each hit cost him (issue #210): the amount he
    /// actually lost after his own Armor or Magic Resist, which is what the sim's typed hit events carry.
    /// </summary>
    [TestFixture]
    public sealed class HeroCombatantTests
    {
        // Armor 20 mitigates 20% of a physical hit; MagicResist 10 mitigates 10% of a magical one.
        private static HeroCombatant NewCombatant() => new(new Hero(
            new[]
            {
                new CharacterStat(StatName.Armor, 20f),
                new CharacterStat(StatName.MagicResist, 10f),
            },
            new[]
            {
                new CharacterResource(StatName.Health, 100f),
                new CharacterResource(StatName.Resource, 100f),
                new CharacterResource(StatName.Shield, 0f),
                new CharacterResource(StatName.Experience, 280f),
            }));

        [Test]
        public void ReceivePhysical_ReturnsTheAmountLostAfterArmor_NotTheMagicResist()
        {
            var hero = NewCombatant();

            var lost = hero.ReceivePhysical(50f);

            Assert.That(lost, Is.EqualTo(40f).Within(0.001f), "50 * (1 - 20%)");
            Assert.That(hero.Health, Is.EqualTo(60f).Within(0.001f), "and that is what left his health");
        }

        [Test]
        public void ReceiveMagical_ReturnsTheAmountLostAfterMagicResist_NotTheArmor()
        {
            var hero = NewCombatant();

            var lost = hero.ReceiveMagical(50f);

            Assert.That(lost, Is.EqualTo(45f).Within(0.001f), "50 * (1 - 10%)");
        }

        [Test]
        public void ANonPositiveHit_LosesNothing()
        {
            var hero = NewCombatant();

            Assert.That(hero.ReceivePhysical(0f), Is.Zero);
            Assert.That(hero.ReceiveMagical(-5f), Is.Zero);
            Assert.That(hero.Health, Is.EqualTo(100f));
        }
    }
}
