using System;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// A hero as it is saved. Not saved, because they are derived or belong elsewhere: stat
    /// modifiers (rebuilt from the gear and the level), the dev flags, regeneration timers, the cast
    /// latch, the base stats (a loaded hero is built from the one default template), and everything
    /// the World holds. The Wallet is the Inventory's coin cells, so saving the Inventory saves it.
    /// The Stash is its own section so it can move out of the hero later (ADR-0014).
    /// </summary>
    [Serializable]
    public sealed class HeroDto
    {
        /// <summary>The hero's generated id: the file name, never changed.</summary>
        public string id = string.Empty;

        /// <summary>The display name; a rename never moves the file.</summary>
        public string name = string.Empty;

        /// <summary>
        /// The id of the <c>HeroData</c> the hero is built from (its icon, its class name). Empty in a save
        /// written before there was one, and for a template that is no longer authored: both read as the
        /// default template, so the field needs no schema migration.
        /// </summary>
        public string templateId = string.Empty;

        /// <summary>
        /// When the hero was created, in UTC ticks. 0 in a save written before there was one, so those read
        /// as the oldest and need no schema migration. It gives the hero list a fixed order that a save,
        /// which moves the saved-at stamp, does not change.
        /// </summary>
        public long createdAtTicks;

        public uint level = 1u;

        public float health;
        public float resource;
        public float shield;
        public float experience;

        public BehaviourDto behaviour = new();

        /// <summary>The selected Location's stable id, or empty before the hero was first sent.</summary>
        public string selectedLocationId = string.Empty;

        public CorpseDto corpse = new();

        public ContainerDto equipment = new();
        public ContainerDto inventory = new();
        public ContainerDto stash = new();
    }

    /// <summary>The six Behaviour Profile values. The loot filter is an enum, so it is stored by name.</summary>
    [Serializable]
    public sealed class BehaviourDto
    {
        public float simSpeed = 1f;
        public int engagement = 1;
        public float retreatHealthFraction;
        public float recallBagFillFraction = 1f;
        public float castThreshold;
        public string lootFilterMinimum = string.Empty;
    }
}
