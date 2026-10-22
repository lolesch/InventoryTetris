namespace ToolSmiths.InventorySystem.Simulation
{
    /// <summary>Which map is up for a Run's phase (ADR-0022): the Run's state is never what opens a panel, but it does pick the map.</summary>
    public static class RunMaps
    {
        /// <summary>
        /// Whether the Town map is up with the Run in <paramref name="phase"/>: with no Run, and with a held one,
        /// whose hero heals and sorts the bag in Town. Only a live Run is on the Field map.
        /// </summary>
        public static bool TownMapIsUp(this RunPhase phase) => phase != RunPhase.Live;

        /// <summary>
        /// Whether a click on the Field Exit ends the Run in <paramref name="phase"/>: a live Run Returns, while a held
        /// one is only walked away from and a Run-less preview has nothing to end.
        /// </summary>
        public static bool FieldExitReturns(this RunPhase phase) => phase == RunPhase.Live;
    }
}
