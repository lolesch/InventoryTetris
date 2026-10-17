using NUnit.Framework;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>The Death notice's wording (issue #327, spec <i>Death is shown before it is left</i>): where the hero fell, what it cost and where the bag lies.</summary>
    [TestFixture]
    public sealed class DeathNoticeTests
    {
        [Test]
        public void TheText_NamesTheLocation_TheXpLost_AndTheFeeInItsCoins()
        {
            Assert.That(DeathNotice.TextFor("Thornwood", 120, 34L),
                Is.EqualTo("You fell in Thornwood. You lost 120 XP and a fee of 6 copper and 4 iron. Your bag lies where you fell; go back to pick it up."));
        }

        [Test]
        public void TheFee_ListsEachCoinLargestFirst_AndLeavesOutTheOnesWithNone()
        {
            // 1 gold (1200) + 3 silver (180) + 4 iron
            Assert.That(DeathNotice.FeeText(1384L), Is.EqualTo("1 gold, 3 silver and 4 iron"));
            Assert.That(DeathNotice.FeeText(1200L), Is.EqualTo("1 gold"));
        }

        [Test]
        public void ANothingFee_ReadsAsZeroIron()
        {
            Assert.That(DeathNotice.FeeText(0L), Is.EqualTo("0 iron"));
        }
    }
}
