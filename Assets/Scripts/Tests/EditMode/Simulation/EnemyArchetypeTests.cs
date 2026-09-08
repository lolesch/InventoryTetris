using NUnit.Framework;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The two shared archetype curve-sets (ADR-0010 second amendment). Pins the shape
    /// <c>stat = Base + PerLevel · S^Exp</c> and the load-bearing property from the issue-#18
    /// <c>/prototype</c>: at a Location's source level a Brute is a bulky, armored, low-XP body
    /// and a Skirmisher is a fragile, fast, no-armor, high-XP one.
    /// </summary>
    [TestFixture]
    public sealed class EnemyArchetypeTests
    {
        [Test]
        public void StatCurve_IsBasePlusPerLevelTimesSourceLevelToTheExp()
        {
            var curve = new StatCurve(10f, 2f, 1f); // linear: 10 + 2·S

            Assert.That(curve.At(1), Is.EqualTo(12f).Within(0.0001f));
            Assert.That(curve.At(5), Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void Brute_AtSourceLevel5_MatchesThePrototypeTuning()
        {
            var brute = new Enemy(EnemyArchetype.Brute, sourceLevel: 5);

            Assert.That(brute.MaxHealth, Is.EqualTo(172f).Within(2f), "Brute HP @ S5 ≈ 172 (FINDINGS.md)");
            Assert.That(brute.AttackSpeed, Is.EqualTo(0.55f), "slow");
            Assert.That(brute.ArmorPercent, Is.GreaterThan(0f), "armored");
        }

        [Test]
        public void Skirmisher_AtSourceLevel5_MatchesThePrototypeTuning()
        {
            var skirmisher = new Enemy(EnemyArchetype.Skirmisher, sourceLevel: 5);

            Assert.That(skirmisher.MaxHealth, Is.EqualTo(60f).Within(2f), "Skirmisher HP @ S5 ≈ 60 (FINDINGS.md)");
            Assert.That(skirmisher.AttackSpeed, Is.EqualTo(1.6f), "fast");
            Assert.That(skirmisher.ArmorPercent, Is.EqualTo(0f), "no armor");
        }

        [Test]
        public void Brute_IsBulkierAndWorthLessXpThanASkirmisher_AtEverySourceLevel()
        {
            for (var s = 1; s <= 8; s++)
            {
                var brute = new Enemy(EnemyArchetype.Brute, s);
                var skirmisher = new Enemy(EnemyArchetype.Skirmisher, s);

                Assert.That(brute.MaxHealth, Is.GreaterThan(skirmisher.MaxHealth), $"HP @ S{s}");
                Assert.That(brute.Xp, Is.LessThan(skirmisher.Xp), $"XP @ S{s}");
                Assert.That(brute.AttackSpeed, Is.LessThan(skirmisher.AttackSpeed), $"cadence @ S{s}");
            }
        }

        [Test]
        public void Other_FlipsThePackedArchetype()
        {
            Assert.That(EnemyArchetypes.Other(EnemyArchetype.Brute), Is.EqualTo(EnemyArchetype.Skirmisher));
            Assert.That(EnemyArchetypes.Other(EnemyArchetype.Skirmisher), Is.EqualTo(EnemyArchetype.Brute));
        }

        [Test]
        public void Enemy_MitigatesTheStrikeByItsArmor_ButNotTheCast()
        {
            var armor = new Enemy(EnemyArchetype.Brute, sourceLevel: 5).ArmorPercent;

            var struck = new Enemy(EnemyArchetype.Brute, sourceLevel: 5);
            struck.ReceivePhysical(20f);
            Assert.That(struck.MaxHealth - struck.Health, Is.EqualTo(20f * (1f - armor * 0.01f)).Within(0.001f));

            var cast = new Enemy(EnemyArchetype.Brute, sourceLevel: 5);
            cast.ReceiveMagical(20f);
            Assert.That(cast.MaxHealth - cast.Health, Is.EqualTo(20f).Within(0.001f), "no magic resist");
        }
    }
}
