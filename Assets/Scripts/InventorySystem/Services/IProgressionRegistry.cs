using ToolSmiths.InventorySystem.Locations;
using ToolSmiths.InventorySystem.Runtime.Character;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>The Unlock graph the game config authors, and the state of any node of it for a hero (ADR-0023).</summary>
    public interface IProgressionRegistry
    {
        /// <summary>
        /// The state of <paramref name="unlockable"/> for <paramref name="hero"/>, derived from the hero's
        /// Completed set: Complete if in it; else Active if nothing names it or any naming link is
        /// satisfied; else Locked if any naming source is at least Active; else Hidden.
        /// </summary>
        UnlockState StateOf(Unlockable unlockable, Hero hero);

        /// <summary>
        /// Adds <paramref name="id"/> to the hero's Completed set, then Completes each Quest that was Active
        /// before this call and has a just-Completed id as objective, repeating until nothing changes.
        /// Returns whether the Completed set grew. A Complete already recorded can still finish an Active Quest.
        /// </summary>
        bool Complete(string id, Hero hero);
    }
}
