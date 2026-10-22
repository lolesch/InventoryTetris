using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks WeightMigration.Remap: authored weights are re-homed by enum VALUE, not by
    /// array position — so a reorder keeps every weight, an insertion gets weight 0, and
    /// only a removed member's weight is dropped.
    /// </summary>
    [TestFixture]
    public sealed class WeightMigrationTests
    {
        [Test]
        public void Reorder_KeepsEveryWeight_MappedByValue()
        {
            var oldOrder = new[] { Tier.White, Tier.Blue, Tier.Yellow };
            var oldWeights = new[] { 10f, 20f, 30f };
            var newOrder = new[] { Tier.Yellow, Tier.White, Tier.Blue };

            var result = WeightMigration.Remap(oldOrder, oldWeights, newOrder);

            Assert.That(result, Is.EqualTo(new[] { 30f, 10f, 20f }));
        }

        [Test]
        public void InsertedMember_GetsZero_OthersUnchanged()
        {
            var oldOrder = new[] { Tier.White, Tier.Yellow };
            var oldWeights = new[] { 10f, 30f };
            var newOrder = new[] { Tier.White, Tier.Blue, Tier.Yellow };

            var result = WeightMigration.Remap(oldOrder, oldWeights, newOrder);

            Assert.That(result, Is.EqualTo(new[] { 10f, 0f, 30f }));
        }

        [Test]
        public void RemovedMember_LosesItsWeight_TheRestSurvive()
        {
            var oldOrder = new[] { Tier.White, Tier.Blue, Tier.Yellow };
            var oldWeights = new[] { 10f, 20f, 30f };
            var newOrder = new[] { Tier.White, Tier.Yellow };

            var result = WeightMigration.Remap(oldOrder, oldWeights, newOrder);

            Assert.That(result, Is.EqualTo(new[] { 10f, 30f }));
        }

        [Test]
        public void FreshTable_AllZero_WhenNothingWasAuthored()
        {
            var result = WeightMigration.Remap(
                new Tier[0], new float[0], new[] { Tier.White, Tier.Blue });

            Assert.That(result, Is.EqualTo(new[] { 0f, 0f }));
        }
    }
}
