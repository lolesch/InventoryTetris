namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>How a Run ended — the two ways out of <see cref="RunPhase.InField"/>.</summary>
    public enum RunOutcome
    {
        /// <summary>The player pulled the hero out. Everything settled so far is kept, no penalty.</summary>
        Recalled,

        /// <summary>The hero was downed. The accumulation is kept, plus an XP loss and a currency fee.</summary>
        Died,
    }
}
