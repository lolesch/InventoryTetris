using System;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// Everything the Encounter sim needs to run a series of Encounters at one Location: the
    /// source level both archetype curve-sets read, which archetype is Packed, a per-archetype
    /// Roster count, the Spawn Profile (Pack size, cadence, jitter, the packed-type odds), and
    /// the loot table a kill there rolls against (issue #24).
    ///
    /// This is the plain, engine-free value the <c>LocationConfig</c> ScriptableObject (issue
    /// #25) will map onto — no display name, none of the authoring concerns live here.
    /// Validation mirrors what #25 will enforce so a hand-built profile fails the same way.
    /// </summary>
    public sealed class EncounterProfile
    {
        public EncounterProfile(
            int sourceLevel,
            EnemyArchetype packed,
            IntRange rosterBrute,
            IntRange rosterSkirmisher,
            IntRange packBatch,
            float packedSpawnWeight,
            float spawnInterval,
            LootTable table,
            float spawnJitter = 0f,
            int initialSpawn = 2)
        {
            if (sourceLevel < 1)
                throw new ArgumentOutOfRangeException(nameof(sourceLevel), sourceLevel, "Source level must be at least 1.");
            if (rosterBrute.Min < 0 || rosterSkirmisher.Min < 0)
                throw new ArgumentOutOfRangeException(nameof(rosterBrute), "Roster minimums cannot be negative.");
            if (rosterBrute.Max < rosterBrute.Min || rosterSkirmisher.Max < rosterSkirmisher.Min)
                throw new ArgumentException("Roster max cannot be below its min.");
            if (rosterBrute.Max + rosterSkirmisher.Max < 1)
                throw new ArgumentException("An Encounter must field at least one enemy.");
            if (packBatch.Min < 1)
                throw new ArgumentOutOfRangeException(nameof(packBatch), packBatch.Min, "Pack batch must be at least 1.");
            if (packBatch.Max < packBatch.Min)
                throw new ArgumentException("Pack batch max cannot be below its min.");
            if (packedSpawnWeight < 0f || packedSpawnWeight > 1f)
                throw new ArgumentOutOfRangeException(nameof(packedSpawnWeight), packedSpawnWeight, "Packed spawn weight must be in [0, 1].");
            if (spawnInterval <= 0f)
                throw new ArgumentOutOfRangeException(nameof(spawnInterval), spawnInterval, "Spawn interval must be positive.");
            if (spawnJitter < 0f)
                throw new ArgumentOutOfRangeException(nameof(spawnJitter), spawnJitter, "Spawn jitter cannot be negative.");
            if (initialSpawn < 0)
                throw new ArgumentOutOfRangeException(nameof(initialSpawn), initialSpawn, "Initial spawn cannot be negative.");

            SourceLevel = sourceLevel;
            Packed = packed;
            RosterBrute = rosterBrute;
            RosterSkirmisher = rosterSkirmisher;
            PackBatch = packBatch;
            PackedSpawnWeight = packedSpawnWeight;
            SpawnInterval = spawnInterval;
            Table = table ?? throw new ArgumentNullException(nameof(table));
            SpawnJitter = spawnJitter;
            InitialSpawn = initialSpawn;
        }

        /// <summary>Feeds both archetype curve-sets. Fixed per Location, never scaled to the hero.</summary>
        public int SourceLevel { get; }

        /// <summary>The archetype that arrives in Packs; <see cref="EnemyArchetypes.Other"/> trickles in singly.</summary>
        public EnemyArchetype Packed { get; }

        public IntRange RosterBrute { get; }
        public IntRange RosterSkirmisher { get; }

        /// <summary>Pack size for the packed archetype. <c>[1, 1]</c> means "no real Pack".</summary>
        public IntRange PackBatch { get; }

        /// <summary>P(the next spawn draws the packed type) while both archetypes have roster left.</summary>
        public float PackedSpawnWeight { get; }

        public float SpawnInterval { get; }
        public float SpawnJitter { get; }

        /// <summary>Bodies present when an Encounter opens, drawn packed-type-first.</summary>
        public int InitialSpawn { get; }

        /// <summary>The loot table a kill at this Location rolls against (issue #24's <c>RollContext.Table</c>).</summary>
        public LootTable Table { get; }

        public IntRange RosterFor(EnemyArchetype archetype) =>
            archetype == EnemyArchetype.Brute ? RosterBrute : RosterSkirmisher;
    }
}
