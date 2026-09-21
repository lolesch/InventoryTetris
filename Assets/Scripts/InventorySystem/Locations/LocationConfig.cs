using System;
using System.Text;
using ToolSmiths.InventorySystem.Data.Distributions;
using ToolSmiths.InventorySystem.Items;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

#if UNITY_EDITOR
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("InventorySystem.Locations.Tests")]
#endif

namespace ToolSmiths.InventorySystem.Locations
{
    /// <summary>
    /// The authored field destination (<c>CONTEXT.md</c> "Location") — a
    /// <see cref="ScriptableObject"/> a designer fills in the inspector: a stable id
    /// (never the asset GUID, never the asset name), a display name, a fixed source level, the
    /// loot table a Run there rolls against, which enemy <see cref="EnemyArchetype"/> it
    /// <em>Packs</em>, and its two <see cref="IntRange"/> rosters and Spawn Profile
    /// (ADR-0010 second amendment, issue #18 pass 2).
    ///
    /// This is the Unity side of the <c>Location</c> concept; the engine-free
    /// <see cref="EncounterProfile"/> (Simulation) is what the sim actually runs, and
    /// <see cref="ToProfile"/> is the mapping between the two. The <see cref="LootTable"/> the
    /// profile carries is this Location's own pair of distribution
    /// <see cref="ScriptableObject"/>s (wrapped by <see cref="LocationLootTable"/>) — authoring
    /// per-Location tables is what lets a harder Location roll a richer spread than an easy one.
    ///
    /// Validation mirrors what <see cref="EncounterProfile"/> already enforces (the constructor
    /// throws on bad ranges / spawn / level) and adds the authoring rules that are
    /// <c>LocationConfig</c>'s alone: a non-empty stable id and present distributions
    /// (<see cref="Validate"/>). Town is <em>not</em> a <c>LocationConfig</c> — it is the
    /// <see cref="RunState"/>'s <c>InTown</c> phase.
    /// </summary>
    [CreateAssetMenu(fileName = "New Location", menuName = "Inventory System/Location")]
    public sealed class LocationConfig : ScriptableObject
    {
        [Tooltip("Stable id a Run / Corpse references - a slug the author picks. Never the asset name, never the Unity asset GUID.")]
        [SerializeField] private string id;
        [SerializeField] private string displayName;

        [Header("Difficulty")]
        [Tooltip("Fixed per Location, never scaled to the hero: feeds RollContext.SourceLevel AND both archetype curve-sets.")]
        [SerializeField] private int sourceLevel = 1;

        [Header("Loot table - this Location's own")]
        [Tooltip("The category weights a kill here rolls against (ItemCategory enum order, summed to 1).")]
        [SerializeField] private ItemCategoryDistribution categoryDistribution;
        [Tooltip("The rarity weights a kill here rolls against (ItemRarity enum order, summed to 1). Magic find cascades over this.")]
        [SerializeField] private ItemRarityDistribution rarityDistribution;

        [Header("Encounter - who it fields and how")]
        [Tooltip("The archetype that arrives in Packs; its opposite trickles in one at a time. Both exist at every Location.")]
        [SerializeField] private EnemyArchetype packed = EnemyArchetype.Brute;
        [Tooltip("[min,max] Brutes each Encounter fields, rolled fresh per Encounter.")]
        [SerializeField] private Vector2Int rosterBrute = new(1, 1);
        [Tooltip("[min,max] Skirmishers each Encounter fields, rolled fresh per Encounter.")]
        [SerializeField] private Vector2Int rosterSkirmisher = new(1, 1);

        [Header("Spawn Profile")]
        [Tooltip("Pack size for the packed archetype. [1,1] means 'no real Pack', a pure trickle.")]
        [SerializeField] private Vector2Int packBatch = new(1, 1);
        [Tooltip("P(the next spawn draws the packed type) while both archetypes have roster left.")]
        [SerializeField, Range(0f, 1f)] private float packedSpawnWeight = 0.5f;
        [Tooltip("Seconds between spawn ticks.")]
        [SerializeField] private float spawnInterval = 2f;
        [Tooltip("Uniform ±jitter, in seconds, applied to each spawn interval.")]
        [SerializeField] private float spawnJitter;

        /// <summary>Stable identity a saved Run / Corpse references. Never the asset GUID, never the asset name.</summary>
        public string Id => id;

        public string DisplayName => displayName;

        /// <summary>Fixed per Location, never scaled to the hero.</summary>
        public int SourceLevel => sourceLevel;

        public EnemyArchetype Packed => packed;

        public Vector2Int RosterBrute => rosterBrute;
        public Vector2Int RosterSkirmisher => rosterSkirmisher;

        public Vector2Int PackBatch => packBatch;
        public float PackedSpawnWeight => packedSpawnWeight;
        public float SpawnInterval => spawnInterval;
        public float SpawnJitter => spawnJitter;

        /// <summary>This Location's own category weights - what every kill's category roll reads (via <see cref="ToProfile"/>).</summary>
        public AbstractProbabilityDistribution CategoryDistribution => categoryDistribution;

        /// <summary>This Location's own rarity weights - what every kill's rarity roll reads.</summary>
        public AbstractProbabilityDistribution RarityDistribution => rarityDistribution;

        private void OnValidate()
        {
            if (rosterBrute.x < 0) rosterBrute.x = 0;
            if (rosterSkirmisher.x < 0) rosterSkirmisher.x = 0;
            if (rosterBrute.y < rosterBrute.x) rosterBrute.y = rosterBrute.x;
            if (rosterSkirmisher.y < rosterSkirmisher.x) rosterSkirmisher.y = rosterSkirmisher.x;
            if (packBatch.x < 1) packBatch.x = 1;
            if (packBatch.y < packBatch.x) packBatch.y = packBatch.x;
            if (spawnInterval <= 0f) spawnInterval = 0.1f;
            if (packedSpawnWeight < 0f) packedSpawnWeight = 0f;
            if (packedSpawnWeight > 1f) packedSpawnWeight = 1f;
        }

        /// <summary>
        /// The authoring rules that are <c>LocationConfig</c>'s alone - a non-empty id and both
        /// distributions present. Range / spawn / level validation is delegated to the
        /// <see cref="EncounterProfile"/> constructor via <see cref="ToProfile"/> (which throws),
        /// so an ill-authored range surfaces there the same way a hand-built profile fails.
        /// Returns an empty array when valid.
        /// </summary>
        public string[] Validate()
        {
            if (string.IsNullOrWhiteSpace(id))
                return new[] { "id is empty - author a stable slug (never the asset name or Unity asset GUID)." };
            if (categoryDistribution == null || rarityDistribution == null)
                return new[] { "the loot table is incomplete - author both the category and rarity distributions." };
            return Array.Empty<string>();
        }

        /// <summary>
        /// The engine-free <see cref="EncounterProfile"/> this Location runs in the sim. Maps the
        /// authored <see cref="Vector2Int"/> ranges onto <see cref="IntRange"/>s and wraps the two
        /// authored distributions into the profile's <see cref="LootTable"/>.
        ///
        /// Throws <see cref="InvalidOperationException"/> when the id is empty or a distribution
        /// is missing (<see cref="Validate"/>), and <see cref="ArgumentException"/> (via the
        /// <see cref="EncounterProfile"/> constructor) when the ranges / spawn / level are
        /// ill-formed - the same rules a hand-built profile is held to.
        /// </summary>
        public EncounterProfile ToProfile()
        {
            var problems = Validate();
            if (problems.Length > 0)
                throw new InvalidOperationException(
                    $"Location '{name}' is invalid: {string.Join("; ", problems)}");

            return new EncounterProfile(
                sourceLevel,
                packed,
                new IntRange(rosterBrute.x, rosterBrute.y),
                new IntRange(rosterSkirmisher.x, rosterSkirmisher.y),
                new IntRange(packBatch.x, packBatch.y),
                packedSpawnWeight,
                spawnInterval,
                new LocationLootTable(categoryDistribution, rarityDistribution),
                spawnJitter);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only authoring seam, mirroring <see cref="ItemDefinitionAsset.Author"/> - a
        /// test or a one-shot migration script writes this asset's content once; after that the
        /// fields are edited by hand in the inspector. Not part of the runtime contract.
        /// </summary>
        internal void Author(
            string id, string displayName, int sourceLevel,
            ItemCategoryDistribution categoryDistribution, ItemRarityDistribution rarityDistribution,
            EnemyArchetype packed, Vector2Int rosterBrute, Vector2Int rosterSkirmisher,
            Vector2Int packBatch, float packedSpawnWeight, float spawnInterval, float spawnJitter)
        {
            this.id = id;
            this.displayName = displayName;
            this.sourceLevel = sourceLevel;
            this.categoryDistribution = categoryDistribution;
            this.rarityDistribution = rarityDistribution;
            this.packed = packed;
            this.rosterBrute = rosterBrute;
            this.rosterSkirmisher = rosterSkirmisher;
            this.packBatch = packBatch;
            this.packedSpawnWeight = Mathf.Clamp01(packedSpawnWeight);
            this.spawnInterval = spawnInterval;
            this.spawnJitter = spawnJitter;
            OnValidate();
        }
#endif
    }
}