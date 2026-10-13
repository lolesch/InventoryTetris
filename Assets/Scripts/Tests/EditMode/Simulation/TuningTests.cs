using NUnit.Framework;
using Submodules.Utility.Extensions;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Simulation
{
    /// <summary>
    /// The tuning types carry their own defaults and their own sanitising (epic #262, ticket #263): a tuning is
    /// built directly, with no <c>GameConfig</c> asset, and what a hand-edited asset could put in it is pulled
    /// back into range by the tuning itself.
    /// </summary>
    [TestFixture]
    public sealed class TuningTests
    {
        // ── defaults ─────────────────────────────────────────────────────────

        [Test]
        public void ADefaultEncounterTuning_IsTheQuietTestFriendlyOne()
        {
            var tuning = new EncounterTuning();

            Assert.That(tuning.Tick, Is.EqualTo(0.1f));
            Assert.That(tuning.Beat, Is.EqualTo(1f));
            Assert.That(tuning.CastCadence, Is.EqualTo(0.35f));
            Assert.That(tuning.MaxTicksPerAdvance, Is.EqualTo(8));
            Assert.That(tuning.DelayFirstSpawn, Is.False);
            Assert.That(tuning.DamageSpread, Is.Zero, "every figure is its base damage");
            Assert.That(tuning.HeroTargeting, Is.EqualTo(HeroTargeting.WeightedProximity));
            Assert.That(tuning.Arena.Radius, Is.Zero, "collapsed: every enemy spawns on the hero");
            Assert.That(tuning.Arena.SpawnMargin, Is.Zero);
        }

        [Test]
        public void TheStandardEncounterTuning_IsWhatTheGamePlaysOn()
        {
            var tuning = EncounterTuning.Standard();

            Assert.That(tuning.DelayFirstSpawn, Is.True, "the hero arrives to a quiet Location");
            Assert.That(tuning.DamageSpread, Is.EqualTo(0.2f));
            Assert.That(tuning.Arena.Radius, Is.EqualTo(10f));
            Assert.That(tuning.Arena.SpawnMargin, Is.EqualTo(2f));
            Assert.That(tuning.Arena.StopJitter, Is.EqualTo(0.2f));
            Assert.That(tuning.Arena.BearingJitter, Is.EqualTo(0.5f));
            Assert.That(tuning.Arena.HeroStrikeRange, Is.EqualTo(1.5f));
            Assert.That(tuning.Arena.MovementSpeedScale, Is.EqualTo(0.01f));
            Assert.That(tuning.Cast.Range, Is.EqualTo(7f));
            Assert.That(tuning.Cast.Size, Is.EqualTo(1f));
            Assert.That(tuning.Cast.Anchor, Is.EqualTo(AreaAnchor.Target));
            Assert.That(tuning.Cast.Shape.Kind, Is.EqualTo(AreaShapeKind.Disk));
            Assert.That(tuning.Cast.Shape.Radius, Is.EqualTo(2f));
        }

        // ── sanitising ───────────────────────────────────────────────────────

        [Test]
        public void ASanitisedEncounterTuning_PullsEveryValueBackIntoRange()
        {
            var tuning = new EncounterTuning
            {
                Tick = 0f,
                Beat = -1f,
                CastCadence = -2f,
                DamageSpread = 3f,
                MaxTicksPerAdvance = 0,
            };

            var sane = tuning.Sanitised();

            Assert.That(sane.Tick, Is.EqualTo(EncounterTuning.MinTick));
            Assert.That(sane.Beat, Is.Zero);
            Assert.That(sane.CastCadence, Is.EqualTo(EncounterTuning.MinCastCadence));
            Assert.That(sane.DamageSpread, Is.EqualTo(1f));
            Assert.That(sane.MaxTicksPerAdvance, Is.EqualTo(1));
        }

        [Test]
        public void ASanitisedArena_PullsEveryValueBackIntoRange()
        {
            var arena = new ArenaTuning
            {
                Radius = -5f,
                SpawnMargin = -1f,
                StopJitter = 1f,
                BearingJitter = 2f,
                HeroStrikeRange = -1f,
                MovementSpeedScale = -0.5f,
            };

            var sane = arena.Sanitised();

            Assert.That(sane.Radius, Is.Zero);
            Assert.That(sane.SpawnMargin, Is.Zero);
            Assert.That(sane.StopJitter, Is.EqualTo(ArenaTuning.MaxStopJitter), "an enemy always stands within range");
            Assert.That(sane.BearingJitter, Is.EqualTo(1f));
            Assert.That(sane.HeroStrikeRange, Is.Zero);
            Assert.That(sane.MovementSpeedScale, Is.Zero);
        }

        [Test]
        public void ASanitisedCast_PullsEveryValueBackIntoRange()
        {
            var cast = new CastDefinition { Range = -1f, Size = 0f, Targeting = (CastTargeting)99 };

            var sane = cast.Sanitised();

            Assert.That(sane.Range, Is.Zero);
            Assert.That(sane.Size, Is.EqualTo(CastDefinition.MinSize));
            Assert.That(sane.Targeting, Is.EqualTo(CastTargeting.DensestCluster));
        }

        [Test]
        public void ASanitisedTuning_KeepsWhatWasInRangeAndSanitisesWhatItNests()
        {
            var tuning = new EncounterTuning
            {
                Tick = 0.05f,
                DelayFirstSpawn = true,
                HeroTargeting = HeroTargeting.WeightedProximity,
                Arena = new ArenaTuning { Radius = 25f, StopJitter = 5f },
                Cast = new CastDefinition { Range = 9f, Size = 0f, Shape = AreaShape.Disk(3f), Anchor = AreaAnchor.Origin },
            };

            var sane = tuning.Sanitised();

            Assert.That(sane.Tick, Is.EqualTo(0.05f));
            Assert.That(sane.DelayFirstSpawn, Is.True);
            Assert.That(sane.Arena.Radius, Is.EqualTo(25f));
            Assert.That(sane.Arena.StopJitter, Is.EqualTo(ArenaTuning.MaxStopJitter));
            Assert.That(sane.Cast.Range, Is.EqualTo(9f));
            Assert.That(sane.Cast.Size, Is.EqualTo(CastDefinition.MinSize));
            Assert.That(sane.Cast.Shape.Radius, Is.EqualTo(3f));
            Assert.That(sane.Cast.Anchor, Is.EqualTo(AreaAnchor.Origin));
        }

        [Test]
        public void Sanitising_ReturnsACopy_SoTheAuthoredTuningIsNeverWrittenThrough()
        {
            var tuning = new EncounterTuning { Beat = -1f };

            var sane = tuning.Sanitised();

            Assert.That(sane, Is.Not.SameAs(tuning));
            Assert.That(sane.Arena, Is.Not.SameAs(tuning.Arena));
            Assert.That(sane.Cast, Is.Not.SameAs(tuning.Cast));
            Assert.That(tuning.Beat, Is.EqualTo(-1f));
        }

        [Test]
        public void ANullArenaOrCast_IsSanitisedToItsDefault()
        {
            var sane = new EncounterTuning { Arena = null, Cast = null }.Sanitised();

            Assert.That(sane.Arena.Radius, Is.Zero);
            Assert.That(sane.Cast.Range, Is.EqualTo(7f));
        }
    }
}
