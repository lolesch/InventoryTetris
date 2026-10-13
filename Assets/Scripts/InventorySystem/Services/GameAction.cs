using System;
using System.Collections.Generic;
using System.Linq;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// What the player can do with a key: the game's catalog for <c>IInputService&lt;GameAction&gt;</c>.
    /// Each member (bar <see cref="None"/>) is the action of the same name in
    /// <c>Resources/GameInput.inputactions</c>, which holds the default keys; the service throws at boot
    /// when one is missing. Members are named for the intent, never the key: the player rebinds them.
    ///
    /// <para>The values are serialized in scenes (a toggle's hotkey), so a member is appended with the
    /// next number and never renumbered.</para>
    /// </summary>
    public enum GameAction
    {
        /// <summary>No action: an unset hotkey. Never bound, never held.</summary>
        None = 0,

        /// <summary>Pauses and resumes the Run.</summary>
        TogglePause = 1,

        ToggleHeroPanel = 2,
        ToggleStash = 3,
        ToggleVendor = 4,
        ToggleHealer = 5,

        /// <summary>Held while clicking a slot: sends its item where it is usually wanted instead of picking it up.</summary>
        QuickMove = 6,

        /// <summary>Held: an equip, and the tooltip's comparison, targets the second slot of a dual-slot type.</summary>
        SecondSlot = 7,

        /// <summary>Held while picking up a stack: takes half.</summary>
        HalveStack = 8,

        /// <summary>Held: tooltips and stat displays show more than they do at a glance.</summary>
        ShowDetails = 9,

        /// <summary>
        /// The back-out key (Escape): each press peels one layer of the <see cref="ICancelChain"/>. Not
        /// rebindable, because Escape also cancels an interactive rebind (ADR-0019).
        /// </summary>
        Cancel = 10,

        /// <summary>Held on an open selling panel: shows the other tab of its two-panel switch. Apart from
        /// <see cref="ShowDetails"/> so that holding it over an item does not swap that item away.</summary>
        PeekTab = 11,
    }

    /// <summary>The actions a rebinding screen offers.</summary>
    public static class RebindableActions
    {
        /// <summary>Every action but the placeholder and <see cref="GameAction.Cancel"/>.</summary>
        public static readonly IReadOnlyList<GameAction> All = Enum.GetValues(typeof(GameAction))
            .Cast<GameAction>()
            .Where(action => action != GameAction.None && action != GameAction.Cancel)
            .ToArray();
    }
}
