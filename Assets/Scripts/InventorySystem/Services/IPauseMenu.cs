using Submodules.Utility.Services;
using System;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// Whether the Pause Menu is open, and what that does to the game: the Run is held still through the
    /// simulation's menu hold, and every key but Cancel stops reaching the game. The menu's screen is a scene
    /// view that follows <see cref="OpenChanged"/>; the state lives here so the keys and the sim can read it
    /// without a scene.
    /// </summary>
    public interface IPauseMenu : IService
    {
        bool IsOpen { get; }

        /// <summary>Raised with the new state whenever <see cref="IsOpen"/> changes.</summary>
        event Action<bool> OpenChanged;

        /// <summary>Opens the menu and holds the Run. Does nothing when it is already open.</summary>
        void Open();

        /// <summary>Closes the menu and releases the hold, leaving the player's own Pause as it was found. Does nothing when it is closed.</summary>
        void Close();

        /// <summary>Opens a closed menu and closes an open one: what a Cancel press does at the Pause Menu's rank.</summary>
        void Toggle();
    }
}
