namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>Where the <see cref="EncounterSimulation"/> is in its loop.</summary>
    public enum SimulationPhase
    {
        /// <summary>An Encounter is live — enemies spawning, cadences exchanging.</summary>
        Fighting,

        /// <summary>The Encounter cleared; the one-second beat before the next builds.</summary>
        Beat,

        /// <summary>The hero is down, or the Run abandoned the fight. Nothing advances further.</summary>
        Ended,
    }
}
