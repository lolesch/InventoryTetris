using System;
using ToolSmiths.InventorySystem.Simulation;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The Pause Menu's Save &amp; Exit, scene-free so its button stays thin: whether leaving needs a
    /// confirmation, and the leave itself. A Run in the Field is ended first with the existing implicit
    /// Return (<see cref="ISimulationService.LeaveField"/>, everything kept, no penalty), then the hero is
    /// written, and only a write that succeeded goes on to <c>onSaved</c>. A failure leaves the player in the
    /// game; the Run is already in Town by then, so a Retry is the same call again.
    /// </summary>
    public static class SaveAndExit
    {
        /// <summary>What the confirmation tells a player who leaves a Run in the Field.</summary>
        public const string FieldQuestion = "Leaving ends your Run: the hero Returns and keeps everything it holds, with no penalty.";

        /// <summary>What the second confirmation tells a player who leaves after a failed write.</summary>
        public const string DiscardQuestion = "Exit without saving?\nThe hero's last save is kept, and this Run's loot is lost.";

        /// <summary>Whether a Leave ends a Run, so the player is asked first. In Town it goes straight through.</summary>
        public static bool NeedsConfirmation(ISimulationService simulation) => simulation.Run.Phase == RunPhase.InField;

        /// <summary>
        /// Ends a Run in the Field if there is one, writes the hero and, if the write succeeded, calls
        /// <paramref name="onSaved"/>. The Return is a save point itself, so a write that failed there is
        /// tried once more here, and this result is the one the player is told.
        /// </summary>
        /// <param name="onSaved">What happens once the hero is written: the menu's scene load, or Quit Game's quit.</param>
        public static SaveResult Leave(ISimulationService simulation, IHeroSaveService saves, Action onSaved)
        {
            _ = simulation.LeaveField();

            var result = saves.Save();

            if (result.Wrote)
                onSaved();

            return result;
        }
    }
}
