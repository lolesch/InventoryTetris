namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// The two states a Run is ever in (ADR-0008; <c>CONTEXT.md</c> <i>Run</i>). There is no
    /// <c>Traveling</c> — <see cref="RunState.Send"/> and <see cref="RunState.Recall"/> are
    /// instant — and no <c>GameOver</c>: Death returns the hero to <see cref="InTown"/> under a
    /// penalty, it does not end the Session.
    /// </summary>
    public enum RunPhase
    {
        /// <summary>The safe hub. The map offers <see cref="RunState.Send"/>; the Stash and Store are open.</summary>
        InTown,

        /// <summary>A Run is underway at a Location — an <see cref="EncounterSimulation"/> is live.</summary>
        InField,
    }
}
