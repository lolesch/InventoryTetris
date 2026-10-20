using Submodules.Utility.UI;

namespace ToolSmiths.InventorySystem.Runtime.Simulation
{
    /// <summary>
    /// The Field map's face. Purely UI-driven, like <see cref="ToolSmiths.InventorySystem.GUI.Components.Buttons.ToFieldsButton"/>
    /// and <see cref="ToolSmiths.InventorySystem.GUI.Components.Buttons.ToTownButton"/> that fade
    /// it - it carries no <see cref="RunPhase"/> of its own, because Go Venture previews this face
    /// while still <see cref="RunPhase.None"/> (no Run exists yet to be Live about; think
    /// opening the Town Portal without stepping through it). <see cref="RunPhasePanel"/> would
    /// keep this face hidden through exactly that preview, which is the one thing it must not do.
    /// </summary>
    public sealed class FieldFacePanel : SimplePanel
    {
        /// <summary>Raised as the map starts to come up: the hero's figure learns that the Town Exit brought it up.</summary>
        public event System.Action Appearing;

        protected override void BeforeAppear()
        {
            base.BeforeAppear();
            Appearing?.Invoke();
        }
    }
}
