using Submodules.Utility.Services;
using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The state service over the Hero and the World (ADR-0015): the Run, its loot flow, the Send /
    /// Relocate / Recall transitions and the tick that drives them. It reads the current Hero and
    /// World off the <see cref="ISession"/> on every call and caches neither - the Run lives on the
    /// World and the Corpse and selected Location on the Hero - so a hero load changes what it
    /// operates on without it noticing. The tick is hosted by <see cref="GameLoop"/>.
    /// </summary>
    public interface ISimulationService : IService
    {
        /// <summary>The current World's Run FSM - <see cref="RunPhase.InTown"/> until a <see cref="Send"/>. Built on the first ask.</summary>
        RunState Run { get; }

        /// <summary>
        /// The live Run's loot flow - <c>null</c> in Town. Exposed so the Combat Panel's stats readout
        /// can show a full bag visibly stranding loot.
        /// </summary>
        LootFlow LootFlow { get; }

        /// <summary>
        /// Send the hero to <paramref name="location"/>: <see cref="RunPhase.InTown"/> to
        /// <see cref="RunPhase.InField"/>. Selects the Location on the Hero, opens the Run and, if the
        /// Hero's Corpse is at exactly this Location, lays it back out (to the bag where it fits, else
        /// the ground).
        /// </summary>
        void Send(LocationConfig location);

        /// <summary>
        /// Move the live Run to <paramref name="location"/> without a Recall. Throws unless a Run is
        /// live with its hero up. Like <see cref="Send"/>, lays the Hero's Corpse back out when the new
        /// Location is where it fell.
        /// </summary>
        void Relocate(LocationConfig location);

        /// <summary>End the Run with everything kept. Refused once the hero is down - that ends in a Death.</summary>
        RunResult Recall();

        /// <summary>
        /// One frame of the simulation, <paramref name="deltaSeconds"/> of real time: the Hero's
        /// regeneration (Town and Field alike), the Encounter while a Run is in the Field, and the
        /// Run's two ways home (a Death, or the Behaviour Profile's auto-Recall). Scaled by the
        /// Behaviour Profile's sim speed here, never through <c>Time.timeScale</c> (ADR-0008).
        /// </summary>
        void Tick(float deltaSeconds);
    }
}
