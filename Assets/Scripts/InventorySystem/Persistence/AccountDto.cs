using System;

namespace ToolSmiths.InventorySystem.Persistence
{
    /// <summary>
    /// The tier above the hero (GLOSSARY.md "Account"): for now only the hero the player last used,
    /// so a launch can continue it. The home ADR-0014 reserves for a shared Stash tab; nothing else
    /// is kept here yet.
    /// </summary>
    [Serializable]
    public sealed class AccountDto
    {
        /// <summary>The id of the last-selected hero, or empty when none is.</summary>
        public string lastSelectedHeroId = string.Empty;
    }
}
