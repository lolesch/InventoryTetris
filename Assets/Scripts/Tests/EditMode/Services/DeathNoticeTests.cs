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
                Is.EqualTo("You fell in Thornwood. You lost 120 XP and a fee of 6 copper and 4 iron. Your gear, bag and coins lie where you fell; go back to pick them up."));
        }

        [Test]
        public void WithoutALocation_TheNoticeStillReads_AsTheField()
        {
            Assert.That(DeathNotice.TextFor(null, 5, 0L), Does.StartWith("You fell in the field. You lost 5 XP"));
        }

        [Test]
        public void TheFee_ListsEachCoinLargestFirst_AndLeavesOutTheOnesWithNone()
        {
            // 1 gold (1200) + 3 silver (180) + 4 iron
            Assert.That(DeathNotice.FeeText(1384L), Is.EqualTo("1 gold, 3 silver and 4 iron"));
            Assert.That(DeathNotice.FeeText(1200L), Is.EqualTo("1 gold"));
        }

        [Test]
        public void AFeeAboveWhatAUintHolds_ReadsInFull_NotWrapped()
        {
            // 4294967296 = 2^32 = 3579139 gold (4294966800) + 496 left: 8 silver, 3 copper, 1 iron
            Assert.That(DeathNotice.FeeText(uint.MaxValue + 1L), Is.EqualTo("3579139 gold, 8 silver, 3 copper and 1 iron"));
        }

        [Test]
        public void ANothingFee_ReadsAsZeroIron()
        {
            Assert.That(DeathNotice.FeeText(0L), Is.EqualTo("0 iron"));
        }
    }
}
