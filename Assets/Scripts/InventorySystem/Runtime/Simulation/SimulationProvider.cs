using Submodules.Utility.Provider;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// Retired by #113: the Run, its loot flow, the Corpse and the tick are
    /// <see cref="ToolSmiths.InventorySystem.Services.ISimulationService"/>'s, over the Hero and the
    /// World, and nothing calls this any more. It stays only as the empty component the scene still
    /// holds, so no scene reference goes missing before the contract ticket (#119) deletes both.
    /// </summary>
    public sealed class SimulationProvider : AbstractProvider<SimulationProvider>
    {
    }
}
