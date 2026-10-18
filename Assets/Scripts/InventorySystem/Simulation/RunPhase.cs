namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>
    /// What the Run is doing (ADR-0008; <c>GLOSSARY.md</c> <i>Run</i>), not where the hero stands: that is the
    /// map that is up. There is no <c>Traveling</c> — <see cref="RunState.Send"/> and
    /// <see cref="RunState.Return"/> are instant — and no <c>GameOver</c>: Death ends the Run
    /// (<see cref="None"/>) under a penalty, it does not end the Session.
    /// </summary>
    public enum RunPhase
    {
        /// <summary>No Run. The map offers <see cref="RunState.Send"/>; the Stash and Store are open.</summary>
        None,

        /// <summary>The simulation ticks — an <see cref="EncounterSimulation"/> is live at a Location.</summary>
        Live,
    }
}
