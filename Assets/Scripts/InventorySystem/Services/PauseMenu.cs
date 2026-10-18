using System;

namespace ToolSmiths.InventorySystem.Services
{
    /// <summary>
    /// The <see cref="IPauseMenu"/> of one boot. It holds the simulation while open through
    /// <see cref="ISimulationService.SetMenuFreeze"/>, never the player's Pause and never <c>Time.timeScale</c>.
    /// </summary>
    public sealed class PauseMenu : IPauseMenu
    {
        private readonly ISimulationService simulation;

        public PauseMenu(ISimulationService simulation) =>
            this.simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));

        public bool IsOpen { get; private set; }

        public event Action<bool> OpenChanged;

        public void Open() => Set(true);

        public void Close() => Set(false);

        public void Toggle() => Set(!IsOpen);

        private void Set(bool open)
        {
            if (IsOpen == open)
                return;

            IsOpen = open;
            simulation.SetMenuFreeze(open);
            OpenChanged?.Invoke(open);
        }
    }
}
