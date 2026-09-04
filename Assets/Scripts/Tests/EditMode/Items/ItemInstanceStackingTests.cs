using NUnit.Framework;
using ToolSmiths.InventorySystem.Data.Enums;
using ToolSmiths.InventorySystem.Items;
using static ToolSmiths.InventorySystem.Tests.EditMode.Items.Sample;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// Stacking identity (<c>CONTEXT.md</c> "Package", foundational-rework spec): two
    /// instances merge into one stack iff same definition, a stack limit above one, and
    /// neither carries rolled state. Currency and a plain consumable stack; equipment and a
    /// rolled consumable never do. <c>AbstractItem.Equals</c> ignoring affixes is not carried
    /// forward - this check is explicit.
    /// </summary>
    [TestFixture]
    public sealed class ItemInstanceStackingTests
    {
        private static ItemInstance Coin(string id = "currency.iron") => new(id, ItemRarity.Common, 0, null);

        [Test]
        public void HasInstanceState_IsFalse_WhenThereAreNoAffixes()
        {
            Assert.That(Coin().HasInstanceState, Is.False);
        }

        [Test]
        public void HasInstanceState_IsTrue_WithAnyAffix()
        {
            var rolled = new ItemInstance("consumable.potion", ItemRarity.Magic, 3, new[] { Affix(StatName.Health) });

            Assert.That(rolled.HasInstanceState, Is.True);
        }

        [Test]
        public void StacksWith_SameDefinitionNoState_AndStackLimitAboveOne_IsTrue()
        {
            Assert.That(Coin().StacksWith(Coin(), stackLimit: 120u), Is.True);
        }

        [Test]
        public void StacksWith_DifferentDefinition_IsFalse()
        {
            Assert.That(Coin("currency.iron").StacksWith(Coin("currency.copper"), 120u), Is.False);
        }

        [Test]
        public void StacksWith_StackLimitOfOne_IsFalse()
        {
            // Equipment - stack limit 1 - never reaches a true here.
            Assert.That(Coin().StacksWith(Coin(), stackLimit: 1u), Is.False);
        }

        [Test]
        public void StacksWith_EitherSideHasRolledAffixes_IsFalse()
        {
            var plain = new ItemInstance("consumable.potion", ItemRarity.Common, 1, null);
            var rolled = new ItemInstance("consumable.potion", ItemRarity.Magic, 1, new[] { Affix(StatName.Health) });

            Assert.That(plain.StacksWith(rolled, 10u), Is.False, "a rolled consumable does not stack onto a plain one");
            Assert.That(rolled.StacksWith(plain, 10u), Is.False, "and not the other way round");
        }

        [Test]
        public void StacksWith_Null_IsFalse()
        {
            Assert.That(Coin().StacksWith(null, 120u), Is.False);
        }
    }
}
