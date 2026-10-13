using System;
using Submodules.Utility.Extensions;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>How the hero picks the enemy his Cast is aimed at.</summary>
    public enum CastTargeting
    {
        /// <summary>
        /// Of the enemies within Cast Range, the one whose shape would catch the most enemies; a tie goes to the
        /// one nearest the hero, then the earliest spawned.
        /// </summary>
        DensestCluster,
    }

    /// <summary>
    /// What a Cast is (spatial-combat spec): how far it reaches, how it picks its target, and the area it hits
    /// there. A tuning value of <see cref="EncounterTuning"/> now, a skill's data once skills exist. The Cast's
    /// cost and cadence stay where they were - the hero's cast cost and <see cref="EncounterTuning.CastCadence"/>.
    /// </summary>
    [Serializable]
    public sealed class CastDefinition
    {
        /// <summary>The smallest Cast size: the area multiplier stays positive.</summary>
        public const float MinSize = 0.01f;

        /// <summary>
        /// Cast Range: only enemies within this arena distance of the hero are candidates to aim at. It is not
        /// the shape's reach - an enemy in range may be hit by a shape that spills past it. Not negative.
        /// </summary>
        public float Range = 7f;

        /// <summary>The pattern that picks the enemy the shape is placed on.</summary>
        public CastTargeting Targeting = CastTargeting.DensestCluster;

        /// <summary>The area one Cast hits, before <see cref="Size"/> scales it.</summary>
        public AreaShape Shape = AreaShape.Disk(2f);

        /// <summary>
        /// The area multiplier applied to <see cref="Shape"/> (<see cref="AreaShape.Scaled"/>): 1 is the shape as
        /// given, 2 is twice the area. At least <see cref="MinSize"/>.
        /// </summary>
        public float Size = 1f;

        /// <summary>Where the shape starts: on the aimed-at enemy, or on the hero pointing at it.</summary>
        public AreaAnchor Anchor = AreaAnchor.Target;

        /// <summary>
        /// The Cast the game plays on: range 7, a disk of radius 2 on the target. Untested starting points, to be
        /// tuned in play.
        /// </summary>
        public static CastDefinition Standard() => new();

        /// <summary>A copy with every value pulled into its range - the one place the ranges are stated.</summary>
        public CastDefinition Sanitised() => new()
        {
            Range = Math.Max(0f, Range),
            Targeting = Enum.IsDefined(typeof(CastTargeting), Targeting) ? Targeting : CastTargeting.DensestCluster,
            Shape = Shape,
            Size = Math.Max(MinSize, Size),
            Anchor = Anchor,
        };

        /// <summary>Throws when a value is outside the range <see cref="Sanitised"/> pulls it back into.</summary>
        internal void Validate()
        {
            var sane = Sanitised();

            TuningRange.Require(nameof(Range), Range, sane.Range);
            TuningRange.Require(nameof(Size), Size, sane.Size);
            if (Targeting != sane.Targeting)
                throw new ArgumentOutOfRangeException(nameof(Targeting), Targeting, "Unknown Cast targeting.");
        }
    }
}
