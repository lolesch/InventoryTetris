using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Persistence;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Finds an authored Location by its stable id, and hands out the one
    /// <see cref="EncounterProfile"/> the simulation runs it as. Read through
    /// <see cref="ISimulationService.Locations"/>.
    /// </summary>
    public interface ILocationRegistry : ILocationProfiles
    {
        /// <summary>The authored Location with this id, or <c>null</c> when there is none.</summary>
        LocationConfig Find(string id);

        /// <summary>The one memoized profile for <paramref name="location"/>, built on first ask.</summary>
        EncounterProfile ProfileFor(LocationConfig location);
    }
}
