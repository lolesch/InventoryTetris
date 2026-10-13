using System;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The roll streams one Encounter draws from, as a single value.
    ///
    /// <b>They stay separate so that a seeded outcome never reorders:</b> a roll added to the movement or the hit
    /// stream can then never shift a loot, roster or spawn roll off the main stream. A new stream is a member
    /// here, not another parameter in every module that hands the streams on.
    /// </summary>
    public sealed class RollStreams
    {
        /// <param name="main">Arrival jitter, Roster size, spawn type, Pack size, jitter, spawn desync, and the loot.</param>
        /// <param name="movement">
        /// Spawn bearing and stop jitter. Optional: without one every movement roll reads 0.5 (no bearing jitter,
        /// a stop pulled in by half the jitter).
        /// </param>
        /// <param name="hit">
        /// One roll per hit, for the <see cref="EncounterTuning.DamageSpread"/>. Optional: without one every hit
        /// rolls 0.5, the middle of the spread, so a hit is its base damage.
        /// </param>
        public RollStreams(IRollSource main, IRollSource movement = null, IRollSource hit = null)
        {
            Main = main ?? throw new ArgumentNullException(nameof(main));
            Movement = movement ?? new MiddleRolls();
            Hit = hit ?? new MiddleRolls();
        }

        public IRollSource Main { get; }

        public IRollSource Movement { get; }

        public IRollSource Hit { get; }

        private sealed class MiddleRolls : IRollSource
        {
            public float Next() => 0.5f;
        }
    }
}
