using NUnit.Framework;
using ToolSmiths.InventorySystem.Services;

namespace ToolSmiths.InventorySystem.Tests.Services
{
    /// <summary>The rules a new hero's name must pass before anything is created (issue #254).</summary>
    [TestFixture]
    public sealed class HeroNameRulesTests
    {
        [Test]
        public void ANameIsTrimmed_AndAcceptedWithAnEmptyReason()
        {
            var check = HeroNameRules.Check("  Aria  ");

            Assert.That(check.IsValid, Is.True);
            Assert.That(check.Name, Is.EqualTo("Aria"));
            Assert.That(check.Reason, Is.Empty);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void AnEmptyName_IsRefused_WithTheReason(string name)
        {
            var check = HeroNameRules.Check(name);

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Reason, Is.EqualTo("Enter a name."));
        }

        [Test]
        public void TwentyCharactersAreAccepted_TwentyOneAreNot()
        {
            Assert.That(HeroNameRules.Check(new string('a', 20)).IsValid, Is.True);

            var check = HeroNameRules.Check(new string('a', 21));

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Reason, Is.EqualTo("A name is at most 20 characters; this one has 21."));
        }

        [Test]
        public void TheLengthIsCountedAfterTrimming()
        {
            Assert.That(HeroNameRules.Check("  " + new string('a', 20) + "  ").IsValid, Is.True);
        }

        [Test]
        public void ACharacterAFileNameCannotHold_IsRefused_AndNamed()
        {
            var check = HeroNameRules.Check("A/b:c");

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Reason, Is.EqualTo("A name cannot contain / :"));
        }

        [Test]
        public void EachOffendingCharacterIsNamedOnce_InTheOrderItAppears()
        {
            var check = HeroNameRules.Check("a?b*c?");

            Assert.That(check.Reason, Is.EqualTo("A name cannot contain ? *"));
        }

        [Test]
        public void AControlCharacter_IsRefused()
        {
            var check = HeroNameRules.Check("a\tb");

            Assert.That(check.IsValid, Is.False);
            Assert.That(check.Reason, Is.EqualTo("A name cannot contain a control character"));
        }

        [TestCase("Aria the Bold")]
        [TestCase("Zoë-Ann")]
        [TestCase("Hero 2")]
        [TestCase("x.y")]
        public void OrdinaryNames_AreAccepted(string name)
        {
            Assert.That(HeroNameRules.Check(name).IsValid, Is.True);
        }
    }
}
