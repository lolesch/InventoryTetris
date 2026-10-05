using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The authored Locations by stable id (<see cref="ILocationIndex"/>), and the one
    /// <see cref="EncounterProfile"/> the simulation runs each of them as. Read through
    /// <see cref="ISimulationService.Locations"/>.
    /// </summary>
    public interface ILocationRegistry : ILocationIndex
    {
        /// <summary>The one memoized profile for <paramref name="location"/>, built on first ask.</summary>
        EncounterProfile ProfileFor(LocationConfig location);
    }
}
