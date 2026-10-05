using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// The seam between a saved Location id and the <see cref="EncounterProfile"/> the sim runs.
    /// A Corpse matches a Location by profile <em>reference</em>, so a restore must reach the one
    /// profile the simulation service memoizes; that service sits above this assembly, so the
    /// restore asks through this interface instead.
    /// </summary>
    public interface ILocationProfiles
    {
        /// <summary>The profile for the authored Location with this id; <c>false</c> when there is none.</summary>
        bool TryGetProfile(string locationId, out EncounterProfile profile);

        /// <summary>The stable id of the Location <paramref name="profile"/> was built for; <c>false</c> for a profile this index never handed out.</summary>
        bool TryGetId(EncounterProfile profile, out string locationId);
    }
}
