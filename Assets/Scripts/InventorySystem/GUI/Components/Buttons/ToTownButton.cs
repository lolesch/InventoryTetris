using Submodules.Utility.UI;
using ToolSmiths.InventorySystem.Runtime.Simulation;
using ToolSmiths.InventorySystem.Services;
using ToolSmiths.InventorySystem.Simulation;
using UnityEngine;

namespace ToolSmiths.InventorySystem.GUI.Components.Buttons
{
    /// <summary>
    /// To Town: fades the Town face back in unconditionally (<see cref="PanelButton.OnClick"/>) -
    /// covering both a real Return and backing out of a Go Venture preview, where no Run exists
    /// yet to Return. Only Returns when a Run is actually live (<see cref="RunMaps.FieldExitReturns"/>) -
    /// <see cref="RunState.Return"/> requires exactly that and throws otherwise, which the preview-cancel path
    /// would hit on every click without this guard. A held Run is only walked away from: it ends nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ToTownButton : PanelButton
    {
        [SerializeField, Tooltip("The Field map's Hero figure, which stands at this Exit when the map comes up and walks out to it on a Return.")]
        private HeroFigure heroFigure;
        [SerializeField, Tooltip("Where the figure stands at the Exit. Empty: this button's own centre.")]
        private RectTransform travelPoint;

        // Announces the Exit's point to the figure.
        protected override void OnEnable()
        {
            base.OnEnable();

            if (heroFigure != null)
                heroFigure.SetExit(this.TravelPointOr(travelPoint));
        }

        protected override void OnClick()
        {
            base.OnClick();

            var simulation = SimulationService.Instance;

            if (simulation.Run.Phase.FieldExitReturns())
                _ = simulation.Return();
        }
    }
}
