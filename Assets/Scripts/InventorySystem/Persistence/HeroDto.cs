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
