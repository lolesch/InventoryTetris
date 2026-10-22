namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Whether the player's own input may spend a Portal Scroll on a Recall right now: the Recall is not refused
    /// (<see cref="ISimulationService.CanRecall"/>) and no menu holds the Run (<see cref="ISimulationService.IsMenuFrozen"/>),
    /// which is not the player's to spend a scroll under. The key (<see cref="RecallHotkey"/>) and a right-click on a
    /// scroll both ask this, so the two cannot drift. The HUD button is not behind a menu and asks the service alone.
    /// </summary>
    public static class RecallGate
    {
        public static bool Allows(ISimulationService simulation) => !simulation.IsMenuFrozen && simulation.CanRecall;
    }
}
