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
    }
}
