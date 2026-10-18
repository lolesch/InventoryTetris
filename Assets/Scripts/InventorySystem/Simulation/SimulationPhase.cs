namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>Where the <see cref="EncounterSimulation"/> is in its loop.</summary>
    public enum SimulationPhase
    {
        /// <summary>The hero has reached the Location but the first enemies have not arrived yet — a rolled quiet wait.</summary>
        Arriving,

        /// <summary>An Encounter is live — enemies spawning, cadences exchanging.</summary>
        Fighting,

        /// <summary>The Encounter cleared; the one-second beat before the next builds.</summary>
        Beat,

        /// <summary>The last Encounter of the Population cleared. Nothing spawns, the hero stands where he is and time stands still until the Run leaves.</summary>
        Complete,

        /// <summary>The hero is down, or the Run abandoned the fight. Nothing advances further.</summary>
        Ended,
    }
}
